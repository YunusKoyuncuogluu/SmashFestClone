using System;
using System.Collections.Generic;
using SmashFest.Data;
using UnityEditor;
using UnityEngine;

namespace SmashFest.LevelEditor
{
    [Serializable]
    internal class LevelEditorMatrixView
    {
        private const float MatrixRowLabelWidth = 28f;
        private const float MatrixCellGap = 2f;
        private const float MatrixHorizontalOverhead = 38f;
        private const float MatrixVerticalOverhead = 52f;
        private const float MinMatrixCellSize = 6f;

        [NonSerialized] private LevelBlueprintEditorWindow window;
        [NonSerialized] private LevelEditorState state;
        [NonSerialized] private LevelEditorActions actions;
        [NonSerialized] private LevelEditorPaletteView palette;
        private float matrixCellWidth = 24f;
        private float matrixCellHeight = 24f;
        private int dragFootprintBlockId;
        private Vector3Int existingHoverSize;
        private readonly Vector3Int[] overwriteSizes = new Vector3Int[LevelEditorState.MaxOverwriteHighlights];
        private HighlightFrame existingFrame;
        private HighlightFrame previewFrame;
        private HighlightFrame moveOriginFrame;
        private readonly HighlightFrame[] overwriteFrames = new HighlightFrame[LevelEditorState.MaxOverwriteHighlights];
        private Rect[] matrixCellRects = Array.Empty<Rect>();
        private int matrixCellStride;

        public void Bind(LevelBlueprintEditorWindow window, LevelEditorState state, LevelEditorActions actions, LevelEditorPaletteView palette)
        {
            this.window = window;
            this.state = state;
            this.actions = actions;
            this.palette = palette;
        }

        public void DrawMatrix(float matrixHeight)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(matrixHeight));
            EditorGUILayout.LabelField("Matrix  |  Y = 0 is the ground row", EditorStyles.boldLabel);

            if (state.ActiveLevel == null)
            {
                if (state.isMovingExistingBlock)
                    actions.CancelMove();

                if (state.hasHoveredCell || state.hasExistingHover || state.overwriteCount > 0)
                {
                    state.hasHoveredCell = false;
                    state.hasExistingHover = false;
                    state.overwriteCount = 0;
                    state.hoverChanged = true;
                }

                EditorGUILayout.HelpBox("No level is selected.", MessageType.None);
                EditorGUILayout.EndVertical();
                return;
            }

            Vector3Int dimensions = state.ActiveLevel.GridDimensions;
            UpdateMatrixCellMetrics(dimensions, matrixHeight);

            bool trackHover = ShouldTrackHover(Event.current);
            bool hadHover = state.hasHoveredCell;
            Vector3Int previousHoveredCell = state.hoveredCell;
            if (trackHover)
                state.hasHoveredCell = false;

            if (!trackHover)
                UpdateHoverPresentation();

            EditorGUILayout.BeginVertical();
            EnsureCellRectBuffer(dimensions.x, dimensions.y);
            DrawColumnHeaders(dimensions.x);
            for (int y = dimensions.y - 1; y >= 0; y--)
            {
                EditorGUILayout.BeginHorizontal();
                Rect rowLabelRect = GUILayoutUtility.GetRect(
                    MatrixRowLabelWidth,
                    matrixCellHeight,
                    GUILayout.Width(MatrixRowLabelWidth),
                    GUILayout.Height(matrixCellHeight));
                if (Event.current.type == EventType.Repaint)
                {
                    state.cellLabelStyle.normal.textColor = new Color(0.75f, 0.92f, 0.96f, 1f);
                    GUI.Label(rowLabelRect, y.ToString(), state.cellLabelStyle);
                }

                for (int x = 0; x < dimensions.x; x++)
                {
                    DrawCell(new Vector3Int(x, y, state.selectedZLayer));
                    if (x < dimensions.x - 1)
                        GUILayout.Space(MatrixCellGap);
                }

                EditorGUILayout.EndHorizontal();
            }

            if (Event.current != null && Event.current.type == EventType.Repaint)
            {
                DrawMergedBlockLabels();
                DrawUnifiedFrames();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndVertical();

            if (!trackHover)
                return;

            Vector3Int currentCell = state.hoveredCell;
            bool hoverCellChanged = hadHover != state.hasHoveredCell || (state.hasHoveredCell && currentCell != previousHoveredCell);
            if (hoverCellChanged)
                state.hoverChanged = true;

            UpdateHoverPresentation();
        }

        private void UpdateMatrixCellMetrics(Vector3Int dimensions, float matrixHeight)
        {
            int rows = Mathf.Max(1, dimensions.y);
            int columns = Mathf.Max(1, dimensions.x);
            float matrixWidth = window.position.width - LevelEditorState.WindowPadding;
            float widthBudget = matrixWidth - MatrixHorizontalOverhead - (MatrixCellGap * (columns - 1));
            float heightBudget = matrixHeight - MatrixVerticalOverhead;
            matrixCellWidth = Mathf.Max(MinMatrixCellSize, Mathf.Floor(widthBudget / columns));
            matrixCellHeight = Mathf.Max(MinMatrixCellSize, Mathf.Floor(heightBudget / rows));
        }

        private void DrawColumnHeaders(int width)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(28f);
            for (int x = 0; x < width; x++)
            {
                Rect headerRect = GUILayoutUtility.GetRect(
                    matrixCellWidth,
                    16f,
                    GUILayout.Width(matrixCellWidth),
                    GUILayout.Height(16f));
                if (Event.current.type == EventType.Repaint)
                {
                    state.cellLabelStyle.normal.textColor = new Color(0.75f, 0.92f, 0.96f, 1f);
                    GUI.Label(headerRect, x.ToString(), state.cellLabelStyle);
                }

                if (x < width - 1)
                    GUILayout.Space(2f);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawCell(Vector3Int cell)
        {
            Rect cellRect = GUILayoutUtility.GetRect(
                matrixCellWidth,
                matrixCellHeight,
                GUILayout.Width(matrixCellWidth),
                GUILayout.Height(matrixCellHeight));
            Event current = Event.current;
            bool inside = current != null && cellRect.Contains(current.mousePosition);
            if (inside && ShouldTrackHover(current))
            {
                state.hoveredCell = cell;
                state.hasHoveredCell = true;
            }

            TrackCellHighlight(cell, cellRect);
            StoreCellRect(cell, cellRect);

            BlockPlacementData placement = default;
            bool occupied = state.ActiveLevel != null && state.ActiveLevel.TryGetBlockAt(cell, state.activeCatalog, out placement, out _);
            BlockDefinition definition = default;
            bool hasDefinition = occupied && state.activeCatalog != null && state.activeCatalog.TryGetBlockDefinition(placement.blockId, out definition);

            if (current != null && current.type == EventType.Repaint)
                PaintCellVisual(cellRect, cell, occupied, hasDefinition, placement, definition, inside);

            if (inside)
                HandleCellInput(cell, current);
        }

        private void PaintCellVisual(
            Rect cellRect,
            Vector3Int cell,
            bool occupied,
            bool hasDefinition,
            BlockPlacementData placement,
            BlockDefinition definition,
            bool inside)
        {
            Vector3Int effectiveSize = Vector3Int.one;
            if (occupied && hasDefinition && state.ActiveLevel != null)
                effectiveSize = state.ActiveLevel.GetEffectiveSize(placement, state.activeCatalog);

            bool multiCell = occupied && (effectiveSize.x > 1 || effectiveSize.y > 1);
            Color fill;
            if (occupied && hasDefinition)
            {
                fill = definition.editorColor;
                fill.a = 1f;
            }
            else if (occupied)
                fill = new Color(0.45f, 0.45f, 0.48f, 1f);
            else if (cell.y == 0)
                fill = new Color(0.13f, 0.2f, 0.18f, 1f);
            else
                fill = new Color(0.16f, 0.17f, 0.2f, 1f);

            EditorGUI.DrawRect(cellRect, fill);
            if (multiCell)
            {
                int localX = cell.x - placement.gridPosition.x;
                if (localX < effectiveSize.x - 1)
                    EditorGUI.DrawRect(new Rect(cellRect.xMax, cellRect.y, 2f, cellRect.height), fill);
            }

            bool inPreview = TryGetPreviewMembership(cell);
            bool moveOriginCell = actions.IsMoveOriginCell(cell);
            if (moveOriginCell && !inPreview)
                EditorGUI.DrawRect(cellRect, new Color(0.04f, 0.05f, 0.07f, 0.55f));
            bool existingCell = IsExistingHoverCell(cell);
            bool overwriteCell = IsOverwriteCell(cell);
            if (overwriteCell && !inPreview)
                EditorGUI.DrawRect(cellRect, new Color(1f, 0.34f, 0.08f, 0.42f));
            else if (existingCell && !inPreview)
                EditorGUI.DrawRect(cellRect, new Color(1f, 0.92f, 0.4f, 0.24f));

            if (inPreview)
            {
                Color highlight = ColorForStatus(state.footprintStatus);
                highlight.a = state.footprintStatus == PlacementValidationStatus.InvalidOutOfBounds ? 0.62f : 0.48f;
                EditorGUI.DrawRect(cellRect, highlight);
            }

            Color border = new Color(0.28f, 0.34f, 0.38f, 1f);
            bool showCursorCell = inside && !inPreview && !existingCell && !overwriteCell;
            if (showCursorCell)
                border = new Color(1f, 0.95f, 0.55f, 1f);

            if (multiCell)
            {
                DrawMultiCellSeams(cellRect, cell, placement.gridPosition, effectiveSize);
                if (showCursorCell)
                    DrawBorder(cellRect, border, 2f);
            }
            else
                DrawBorder(cellRect, border, showCursorCell ? 2f : 1f);

            if (inPreview && state.footprintStatus == PlacementValidationStatus.InvalidOutOfBounds && cell == state.footprintAnchor)
            {
                DrawCenteredLabel(cellRect, "OUT OF BOUNDS", Color.black);
                return;
            }

            if (!occupied || multiCell)
                return;

            string label = hasDefinition
                ? actions.FormatMatrixBlockLabel(definition.blockName, effectiveSize, true)
                : ("#" + placement.blockId.ToString() + "\n" + FormatFootprint(effectiveSize));
            DrawCenteredLabel(cellRect, label, fill);
        }

        private void DrawMergedBlockLabels()
        {
            if (state.ActiveLevel == null)
                return;

            int count = state.ActiveLevel.PlacementCount;
            for (int i = 0; i < count; i++)
            {
                BlockPlacementData placement;
                if (!state.ActiveLevel.TryGetPlacementByIndex(i, out placement))
                    continue;

                BlockDefinition definition = default;
                bool hasDefinition = state.activeCatalog != null && state.activeCatalog.TryGetBlockDefinition(placement.blockId, out definition);
                Vector3Int size = Vector3Int.one;
                if (hasDefinition)
                    size = state.blockFootprint.GetEffectiveSize(definition.sizeInCells, placement.rotationEuler);

                if (size.x <= 1 && size.y <= 1)
                    continue;

                int minZ = placement.gridPosition.z;
                int maxZ = minZ + size.z - 1;
                if (state.selectedZLayer < minZ || state.selectedZLayer > maxZ)
                    continue;

                Rect mergedRect;
                if (!TryGetMergedRect(placement.gridPosition, size, out mergedRect))
                    continue;

                DrawBorder(mergedRect, new Color(0.08f, 0.09f, 0.1f, 0.95f), 2f);
                string label = hasDefinition
                    ? actions.FormatMatrixBlockLabel(definition.blockName, size, false)
                    : ("#" + placement.blockId.ToString() + " " + FormatFootprint(size));
                Color fill = hasDefinition ? definition.editorColor : new Color(0.45f, 0.45f, 0.48f, 1f);
                DrawCenteredLabel(mergedRect, label, fill);
            }
        }

        private void DrawCenteredLabel(Rect rect, string text, Color background)
        {
            state.matrixLabelStyle.normal.textColor = GetReadableTextColor(background);
            GUI.Label(rect, text, state.matrixLabelStyle);
        }

        private void DrawMultiCellSeams(Rect cellRect, Vector3Int cell, Vector3Int anchor, Vector3Int size)
        {
            Color seam = new Color(1f, 1f, 1f, 0.16f);
            int localX = cell.x - anchor.x;
            int localY = cell.y - anchor.y;
            if (localX > 0)
                EditorGUI.DrawRect(new Rect(cellRect.x, cellRect.y, 1f, cellRect.height), seam);

            if (localX < size.x - 1)
                EditorGUI.DrawRect(new Rect(cellRect.xMax - 1f, cellRect.y, 1f, cellRect.height), seam);

            if (localY > 0)
                EditorGUI.DrawRect(new Rect(cellRect.x, cellRect.yMax - 1f, cellRect.width, 1f), seam);

            if (localY < size.y - 1)
                EditorGUI.DrawRect(new Rect(cellRect.x, cellRect.y, cellRect.width, 1f), seam);
        }

        private bool TryGetMergedRect(Vector3Int anchor, Vector3Int size, out Rect mergedRect)
        {
            mergedRect = default;
            Rect firstCell;
            Rect lastCell;
            if (!TryGetStoredCellRect(anchor.x, anchor.y, out firstCell))
                return false;

            if (!TryGetStoredCellRect(anchor.x + size.x - 1, anchor.y + size.y - 1, out lastCell))
                return false;

            float xMin = firstCell.x < lastCell.x ? firstCell.x : lastCell.x;
            float yMin = firstCell.y < lastCell.y ? firstCell.y : lastCell.y;
            float xMax = firstCell.xMax > lastCell.xMax ? firstCell.xMax : lastCell.xMax;
            float yMax = firstCell.yMax > lastCell.yMax ? firstCell.yMax : lastCell.yMax;
            mergedRect = new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
            return mergedRect.width > 0f && mergedRect.height > 0f;
        }

        private bool TryGetStoredCellRect(int x, int y, out Rect cellRect)
        {
            cellRect = default;
            if (x < 0 || y < 0 || matrixCellStride <= 0)
                return false;

            int index = x + (y * matrixCellStride);
            if (index < 0 || index >= matrixCellRects.Length)
                return false;

            cellRect = matrixCellRects[index];
            return cellRect.width > 0f && cellRect.height > 0f;
        }

        private void EnsureCellRectBuffer(int width, int height)
        {
            int needed = width * height;
            if (needed < 1)
                needed = 1;

            if (matrixCellStride == width && matrixCellRects.Length >= needed)
                return;

            matrixCellRects = new Rect[needed];
            matrixCellStride = width;
        }

        private void StoreCellRect(Vector3Int cell, Rect cellRect)
        {
            if (cell.x < 0 || cell.y < 0 || matrixCellStride <= 0)
                return;

            int index = cell.x + (cell.y * matrixCellStride);
            if (index < 0 || index >= matrixCellRects.Length)
                return;

            matrixCellRects[index] = cellRect;
        }

        private void HandleCellInput(Vector3Int cell, Event current)
        {
            if (current.type == EventType.DragUpdated || current.type == EventType.DragPerform)
            {
                HandleCellDrop(cell, current);
                return;
            }

            if (current.type == EventType.MouseDown && current.button == 0)
            {
                if (state.isMovingExistingBlock)
                    actions.CancelMove();

                if (!state.hasBrush && !state.dragFootprintActive)
                {
                    if (actions.TryBeginMove(cell))
                    {
                        current.Use();
                        return;
                    }

                    state.hasSelectedPlacement = false;
                }

                state.isPainting = true;
                state.isErasing = false;
                state.hasLastEditedCell = false;
                actions.PaintCell(cell);
                current.Use();
                return;
            }

            if (state.isMovingExistingBlock)
            {
                if (current.type != EventType.Layout && current.type != EventType.Repaint)
                    current.Use();

                return;
            }

            if (current.type == EventType.MouseDown && current.button == 1)
            {
                state.isErasing = true;
                state.isPainting = false;
                state.hasLastEditedCell = false;
                actions.EraseCell(cell);
                current.Use();
                return;
            }

            if (current.type == EventType.MouseDrag && state.isPainting)
            {
                actions.PaintCell(cell);
                current.Use();
                return;
            }

            if (current.type == EventType.MouseDrag && state.isErasing)
            {
                actions.EraseCell(cell);
                current.Use();
            }
        }

        private void HandleCellDrop(Vector3Int cell, Event current)
        {
            int blockId;
            if (!state.dragPayload.TryGetBlockId(out blockId))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                current.Use();
                return;
            }

            BlockDefinition definition;
            if (state.activeCatalog == null || !state.activeCatalog.TryGetBlockDefinition(blockId, out definition))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                current.Use();
                return;
            }

            state.dragFootprintActive = true;
            dragFootprintBlockId = blockId;
            state.hasBrush = true;
            state.brushBlockId = blockId;
            Vector3Int customSize = palette.ResolveBrushCustomSize(definition);
            Vector3Int sizeInCells = customSize.x > 0 ? customSize : definition.sizeInCells;
            Vector3Int effectiveSize = state.blockFootprint.GetEffectiveSize(sizeInCells, state.brushRotationEuler);
            PlacementValidationStatus status = PlacementValidationStatus.InvalidOutOfBounds;
            if (state.ActiveLevel != null)
                status = state.ActiveLevel.ValidatePlacement(cell, effectiveSize, state.activeCatalog);

            if (status == PlacementValidationStatus.InvalidOutOfBounds)
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
            else
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            if (current.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                state.hasBrush = true;
                state.brushBlockId = blockId;
                state.hasLastEditedCell = false;
                if (status != PlacementValidationStatus.InvalidOutOfBounds)
                {
                    actions.PaintCell(cell);
                    state.statusMessage = "Placed " + palette.GetBlockLabel(definition) + ".";
                }
                else
                    state.statusMessage = "OUT OF BOUNDS. The footprint does not fit.";

                actions.EndLevelEdit();
                state.isPainting = false;
                state.dragFootprintActive = false;
            }

            current.Use();
        }

        private void UpdateHoverPresentation()
        {
            RefreshFootprintPreview(state.hasHoveredCell, state.hoveredCell);
            RefreshEntityHighlights();
            PrepareHighlightFrames();
        }

        private void RefreshEntityHighlights()
        {
            state.hasExistingHover = false;
            state.overwriteCount = 0;
            if (!state.hasHoveredCell || state.ActiveLevel == null)
                return;

            BlockPlacementData placement;
            bool isAnchorCell;
            if (state.ActiveLevel.TryGetBlockAt(state.hoveredCell, state.activeCatalog, out placement, out isAnchorCell))
            {
                bool movingThisBlock = state.isMovingExistingBlock && placement.gridPosition == state.moveOriginPlacement.gridPosition;
                if (!movingThisBlock)
                {
                    state.existingHoverPlacement = placement;
                    existingHoverSize = state.ActiveLevel.GetEffectiveSize(placement, state.activeCatalog);
                    state.hasExistingHover = isAnchorCell || existingHoverSize.x > 0;
                }
            }

            if (!state.hasFootprintPreview || state.footprintStatus != PlacementValidationStatus.ValidOverwrite)
                return;

            int count = state.ActiveLevel.PlacementCount;
            for (int i = 0; i < count; i++)
            {
                if (state.overwriteCount >= LevelEditorState.MaxOverwriteHighlights)
                    break;

                BlockPlacementData candidate;
                if (!state.ActiveLevel.TryGetPlacementByIndex(i, out candidate))
                    continue;

                if (state.isMovingExistingBlock && candidate.gridPosition == state.moveOriginPlacement.gridPosition)
                    continue;

                Vector3Int candidateSize = state.ActiveLevel.GetEffectiveSize(candidate, state.activeCatalog);
                if (!state.blockFootprint.Overlaps(state.footprintAnchor, state.footprintSize, candidate.gridPosition, candidateSize))
                    continue;

                state.overwritePlacements[state.overwriteCount] = candidate;
                overwriteSizes[state.overwriteCount] = candidateSize;
                state.overwriteCount++;
            }
        }

        private void PrepareHighlightFrames()
        {
            existingFrame.active = state.hasExistingHover && !ContainsOverwriteAnchor(state.existingHoverPlacement.gridPosition);
            existingFrame.anchor = state.existingHoverPlacement.gridPosition;
            existingFrame.size = existingHoverSize;
            existingFrame.color = new Color(1f, 0.94f, 0.45f, 1f);
            existingFrame.hasTopLeft = false;
            existingFrame.hasBottomRight = false;

            previewFrame.active = state.hasFootprintPreview;
            previewFrame.anchor = state.footprintAnchor;
            previewFrame.size = state.footprintSize;
            previewFrame.color = ColorForStatus(state.footprintStatus);
            previewFrame.hasTopLeft = false;
            previewFrame.hasBottomRight = false;

            moveOriginFrame.active = false;
            moveOriginFrame.hasTopLeft = false;
            moveOriginFrame.hasBottomRight = false;
            moveOriginFrame.dashed = false;
            if (state.isMovingExistingBlock)
            {
                moveOriginFrame.active = true;
                moveOriginFrame.anchor = state.moveOriginPlacement.gridPosition;
                moveOriginFrame.size = state.ActiveLevel != null
                    ? state.ActiveLevel.GetEffectiveSize(state.moveOriginPlacement, state.activeCatalog)
                    : Vector3Int.one;
                moveOriginFrame.color = new Color(0.82f, 0.88f, 0.92f, 0.95f);
                moveOriginFrame.dashed = true;
            }

            for (int i = 0; i < LevelEditorState.MaxOverwriteHighlights; i++)
            {
                overwriteFrames[i].active = false;
                overwriteFrames[i].hasTopLeft = false;
                overwriteFrames[i].hasBottomRight = false;
            }

            for (int i = 0; i < state.overwriteCount; i++)
            {
                overwriteFrames[i].active = true;
                overwriteFrames[i].anchor = state.overwritePlacements[i].gridPosition;
                overwriteFrames[i].size = overwriteSizes[i];
                overwriteFrames[i].color = new Color(1f, 0.32f, 0.08f, 1f);
            }
        }

        private void TrackCellHighlight(Vector3Int cell, Rect cellRect)
        {
            TrackHighlightRect(ref existingFrame, cell, cellRect);
            TrackHighlightRect(ref previewFrame, cell, cellRect);
            TrackHighlightRect(ref moveOriginFrame, cell, cellRect);
            for (int i = 0; i < state.overwriteCount; i++)
                TrackHighlightRect(ref overwriteFrames[i], cell, cellRect);
        }

        private void TrackHighlightRect(ref HighlightFrame frame, Vector3Int cell, Rect cellRect)
        {
            if (!frame.active || !state.blockFootprint.ContainsCell(frame.anchor, frame.size, cell))
                return;

            if (!frame.hasTopLeft)
            {
                frame.topLeft = cellRect;
                frame.bottomRight = cellRect;
                frame.hasTopLeft = true;
                frame.hasBottomRight = true;
                return;
            }

            float xMin = frame.topLeft.x < cellRect.x ? frame.topLeft.x : cellRect.x;
            float yMin = frame.topLeft.y < cellRect.y ? frame.topLeft.y : cellRect.y;
            float xMax = frame.bottomRight.xMax > cellRect.xMax ? frame.bottomRight.xMax : cellRect.xMax;
            float yMax = frame.bottomRight.yMax > cellRect.yMax ? frame.bottomRight.yMax : cellRect.yMax;
            frame.topLeft = new Rect(xMin, yMin, 0f, 0f);
            frame.bottomRight = new Rect(xMax, yMax, 0f, 0f);
        }

        private void DrawUnifiedFrames()
        {
            DrawUnifiedFrame(moveOriginFrame);
            DrawUnifiedFrame(previewFrame);
            for (int i = 0; i < state.overwriteCount; i++)
                DrawUnifiedFrame(overwriteFrames[i]);

            DrawUnifiedFrame(existingFrame);
        }

        private void DrawUnifiedFrame(HighlightFrame frame)
        {
            if (!frame.active || !frame.hasTopLeft || !frame.hasBottomRight)
                return;

            float x = frame.topLeft.x;
            float y = frame.topLeft.y;
            float width = frame.bottomRight.xMax - x;
            float height = frame.bottomRight.yMax - y;
            if (width <= 0f || height <= 0f)
                return;

            Rect unifiedRect = new Rect(x, y, width, height);
            if (frame.dashed)
            {
                DrawDashedBorder(unifiedRect, frame.color, 3f);
                return;
            }

            DrawBorder(unifiedRect, frame.color, 4f);
        }

        private void DrawDashedBorder(Rect rect, Color color, float thickness)
        {
            DrawDashedHorizontal(rect.x, rect.y, rect.width, thickness, color);
            DrawDashedHorizontal(rect.x, rect.yMax - thickness, rect.width, thickness, color);
            DrawDashedVertical(rect.x, rect.y, rect.height, thickness, color);
            DrawDashedVertical(rect.xMax - thickness, rect.y, rect.height, thickness, color);
        }

        private void DrawDashedHorizontal(float x, float y, float width, float thickness, Color color)
        {
            const float dash = 6f;
            const float gap = 4f;
            float cursor = 0f;
            while (cursor < width)
            {
                float length = dash;
                if (cursor + length > width)
                    length = width - cursor;

                EditorGUI.DrawRect(new Rect(x + cursor, y, length, thickness), color);
                cursor += dash + gap;
            }
        }

        private void DrawDashedVertical(float x, float y, float height, float thickness, Color color)
        {
            const float dash = 6f;
            const float gap = 4f;
            float cursor = 0f;
            while (cursor < height)
            {
                float length = dash;
                if (cursor + length > height)
                    length = height - cursor;

                EditorGUI.DrawRect(new Rect(x, y + cursor, thickness, length), color);
                cursor += dash + gap;
            }
        }

        private bool IsExistingHoverCell(Vector3Int cell)
        {
            if (!state.hasExistingHover)
                return false;

            return state.blockFootprint.ContainsCell(state.existingHoverPlacement.gridPosition, existingHoverSize, cell);
        }

        private bool IsOverwriteCell(Vector3Int cell)
        {
            for (int i = 0; i < state.overwriteCount; i++)
            {
                if (state.blockFootprint.ContainsCell(state.overwritePlacements[i].gridPosition, overwriteSizes[i], cell))
                    return true;
            }

            return false;
        }

        private bool ContainsOverwriteAnchor(Vector3Int anchor)
        {
            for (int i = 0; i < state.overwriteCount; i++)
            {
                if (state.overwritePlacements[i].gridPosition == anchor)
                    return true;
            }

            return false;
        }

        private void RefreshFootprintPreview(bool previewVisible, Vector3Int previewAnchor)
        {
            state.hasFootprintPreview = false;
            state.hasMovePreview = false;
            if (state.isMovingExistingBlock)
            {
                actions.RefreshMovePreview(previewVisible, previewAnchor);
                return;
            }

            if (!previewVisible || state.ActiveLevel == null || state.activeCatalog == null)
                return;

            int blockId;
            if (!TryResolveFootprintBlock(out blockId))
                return;

            BlockDefinition definition;
            if (!state.activeCatalog.TryGetBlockDefinition(blockId, out definition))
                return;

            state.footprintAnchor = previewAnchor;
            Vector3Int customSize = palette.ResolveBrushCustomSize(definition);
            Vector3Int sizeInCells = customSize.x > 0 ? customSize : definition.sizeInCells;
            state.footprintSize = state.blockFootprint.GetEffectiveSize(sizeInCells, state.brushRotationEuler);
            state.footprintStatus = state.ActiveLevel.ValidatePlacement(state.footprintAnchor, state.footprintSize, state.activeCatalog);
            state.hasFootprintPreview = true;
        }

        private bool TryGetPreviewMembership(Vector3Int cell)
        {
            if (!state.hasFootprintPreview)
                return false;

            return state.blockFootprint.ContainsCell(state.footprintAnchor, state.footprintSize, cell);
        }

        public bool TryResolveFootprintBlock(out int blockId)
        {
            if (state.dragFootprintActive)
            {
                blockId = dragFootprintBlockId;
                return true;
            }

            if (state.hasBrush)
            {
                blockId = state.brushBlockId;
                return true;
            }

            blockId = 0;
            return false;
        }

        private Color ColorForStatus(PlacementValidationStatus status)
        {
            if (status == PlacementValidationStatus.ValidEmpty)
                return new Color(0.2f, 0.9f, 0.35f, 1f);

            if (status == PlacementValidationStatus.ValidOverwrite)
                return new Color(1f, 0.68f, 0.12f, 1f);

            return new Color(1f, 0.18f, 0.14f, 1f);
        }

        public string FormatFootprint(Vector3Int size)
        {
            Vector3Int clamped = state.blockFootprint.ClampSize(size);
            if (clamped.z == 1)
                return "[" + clamped.x.ToString() + "x" + clamped.y.ToString() + "]";

            return "[" + clamped.x.ToString() + "x" + clamped.y.ToString() + "x" + clamped.z.ToString() + "]";
        }

        private bool ShouldTrackHover(Event current)
        {
            if (current == null)
                return false;

            return current.type == EventType.MouseMove
                || current.type == EventType.MouseDrag
                || current.type == EventType.DragUpdated;
        }

        public void DrawBorder(Rect rect, Color color, float thickness)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        public Color GetReadableTextColor(Color background)
        {
            float luminance = (background.r * 0.299f) + (background.g * 0.587f) + (background.b * 0.114f);
            if (luminance > 0.62f)
                return new Color(0.08f, 0.08f, 0.08f, 1f);

            return Color.white;
        }
    }
}
