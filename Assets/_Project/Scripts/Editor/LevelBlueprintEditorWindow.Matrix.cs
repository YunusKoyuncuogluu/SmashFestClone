using System;
using System.Collections.Generic;
using SmashFest.Data;
using UnityEditor;
using UnityEngine;

namespace SmashFest.LevelEditor
{
    public partial class LevelBlueprintEditorWindow
    {
        private void DrawMatrix(float matrixHeight)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(matrixHeight));
            EditorGUILayout.LabelField("Matrix  |  Y = 0 is the ground row", EditorStyles.boldLabel);

            if (activeLevel == null)
            {
                if (isMovingExistingBlock)
                    CancelMove();

                if (hasHoveredCell || hasExistingHover || overwriteCount > 0)
                {
                    hasHoveredCell = false;
                    hasExistingHover = false;
                    overwriteCount = 0;
                    hoverChanged = true;
                }

                EditorGUILayout.HelpBox("No level is selected.", MessageType.None);
                EditorGUILayout.EndVertical();
                return;
            }

            Vector3Int dimensions = activeLevel.GridDimensions;
            UpdateMatrixCellMetrics(dimensions, matrixHeight);

            bool trackHover = ShouldTrackHover(Event.current);
            bool hadHover = hasHoveredCell;
            Vector3Int previousHoveredCell = hoveredCell;
            if (trackHover)
                hasHoveredCell = false;

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
                    cellLabelStyle.normal.textColor = new Color(0.75f, 0.92f, 0.96f, 1f);
                    GUI.Label(rowLabelRect, y.ToString(), cellLabelStyle);
                }

                for (int x = 0; x < dimensions.x; x++)
                {
                    DrawCell(new Vector3Int(x, y, selectedZLayer));
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

            Vector3Int currentCell = hoveredCell;
            bool hoverCellChanged = hadHover != hasHoveredCell || (hasHoveredCell && currentCell != previousHoveredCell);
            if (hoverCellChanged)
                hoverChanged = true;

            UpdateHoverPresentation();
        }

        private void UpdateMatrixCellMetrics(Vector3Int dimensions, float matrixHeight)
        {
            int rows = Mathf.Max(1, dimensions.y);
            int columns = Mathf.Max(1, dimensions.x);
            float matrixWidth = position.width - WindowPadding;
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
                    cellLabelStyle.normal.textColor = new Color(0.75f, 0.92f, 0.96f, 1f);
                    GUI.Label(headerRect, x.ToString(), cellLabelStyle);
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
                hoveredCell = cell;
                hasHoveredCell = true;
            }

            TrackCellHighlight(cell, cellRect);
            StoreCellRect(cell, cellRect);

            BlockPlacementData placement = default;
            bool occupied = activeLevel != null && activeLevel.TryGetBlockAt(cell, activeCatalog, out placement, out _);
            BlockDefinition definition = default;
            bool hasDefinition = occupied && activeCatalog != null && activeCatalog.TryGetBlockDefinition(placement.blockId, out definition);

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
            if (occupied && hasDefinition && activeLevel != null)
                effectiveSize = activeLevel.GetEffectiveSize(placement, activeCatalog);

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
            bool moveOriginCell = IsMoveOriginCell(cell);
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
                Color highlight = ColorForStatus(footprintStatus);
                highlight.a = footprintStatus == PlacementValidationStatus.InvalidOutOfBounds ? 0.62f : 0.48f;
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

            if (inPreview && footprintStatus == PlacementValidationStatus.InvalidOutOfBounds && cell == footprintAnchor)
            {
                DrawCenteredLabel(cellRect, "OUT OF BOUNDS", Color.black);
                return;
            }

            if (!occupied || multiCell)
                return;

            string label = hasDefinition
                ? FormatMatrixBlockLabel(definition.blockName, effectiveSize, true)
                : ("#" + placement.blockId.ToString() + "\n" + FormatFootprint(effectiveSize));
            DrawCenteredLabel(cellRect, label, fill);
        }

        private void DrawMergedBlockLabels()
        {
            if (activeLevel == null)
                return;

            int count = activeLevel.PlacementCount;
            for (int i = 0; i < count; i++)
            {
                BlockPlacementData placement;
                if (!activeLevel.TryGetPlacementByIndex(i, out placement))
                    continue;

                BlockDefinition definition = default;
                bool hasDefinition = activeCatalog != null && activeCatalog.TryGetBlockDefinition(placement.blockId, out definition);
                Vector3Int size = Vector3Int.one;
                if (hasDefinition)
                    size = blockFootprint.GetEffectiveSize(definition.sizeInCells, placement.rotationEuler);

                if (size.x <= 1 && size.y <= 1)
                    continue;

                int minZ = placement.gridPosition.z;
                int maxZ = minZ + size.z - 1;
                if (selectedZLayer < minZ || selectedZLayer > maxZ)
                    continue;

                Rect mergedRect;
                if (!TryGetMergedRect(placement.gridPosition, size, out mergedRect))
                    continue;

                DrawBorder(mergedRect, new Color(0.08f, 0.09f, 0.1f, 0.95f), 2f);
                string label = hasDefinition
                    ? FormatMatrixBlockLabel(definition.blockName, size, false)
                    : ("#" + placement.blockId.ToString() + " " + FormatFootprint(size));
                Color fill = hasDefinition ? definition.editorColor : new Color(0.45f, 0.45f, 0.48f, 1f);
                DrawCenteredLabel(mergedRect, label, fill);
            }
        }

        private void DrawCenteredLabel(Rect rect, string text, Color background)
        {
            matrixLabelStyle.normal.textColor = GetReadableTextColor(background);
            GUI.Label(rect, text, matrixLabelStyle);
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
                if (isMovingExistingBlock)
                    CancelMove();

                if (!hasBrush && !dragFootprintActive)
                {
                    if (TryBeginMove(cell))
                    {
                        current.Use();
                        return;
                    }

                    hasSelectedPlacement = false;
                }

                isPainting = true;
                isErasing = false;
                hasLastEditedCell = false;
                PaintCell(cell);
                current.Use();
                return;
            }

            if (isMovingExistingBlock)
            {
                if (current.type != EventType.Layout && current.type != EventType.Repaint)
                    current.Use();

                return;
            }

            if (current.type == EventType.MouseDown && current.button == 1)
            {
                isErasing = true;
                isPainting = false;
                hasLastEditedCell = false;
                EraseCell(cell);
                current.Use();
                return;
            }

            if (current.type == EventType.MouseDrag && isPainting)
            {
                PaintCell(cell);
                current.Use();
                return;
            }

            if (current.type == EventType.MouseDrag && isErasing)
            {
                EraseCell(cell);
                current.Use();
            }
        }

        private void HandleCellDrop(Vector3Int cell, Event current)
        {
            int blockId;
            if (!dragPayload.TryGetBlockId(out blockId))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                current.Use();
                return;
            }

            BlockDefinition definition;
            if (activeCatalog == null || !activeCatalog.TryGetBlockDefinition(blockId, out definition))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                current.Use();
                return;
            }

            dragFootprintActive = true;
            dragFootprintBlockId = blockId;
            hasBrush = true;
            brushBlockId = blockId;
            Vector3Int customSize = ResolveBrushCustomSize(definition);
            Vector3Int sizeInCells = customSize.x > 0 ? customSize : definition.sizeInCells;
            Vector3Int effectiveSize = blockFootprint.GetEffectiveSize(sizeInCells, brushRotationEuler);
            PlacementValidationStatus status = PlacementValidationStatus.InvalidOutOfBounds;
            if (activeLevel != null)
                status = activeLevel.ValidatePlacement(cell, effectiveSize, activeCatalog);

            if (status == PlacementValidationStatus.InvalidOutOfBounds)
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
            else
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            if (current.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                hasBrush = true;
                brushBlockId = blockId;
                hasLastEditedCell = false;
                if (status != PlacementValidationStatus.InvalidOutOfBounds)
                {
                    PaintCell(cell);
                    statusMessage = "Placed " + GetBlockLabel(definition) + ".";
                }
                else
                    statusMessage = "OUT OF BOUNDS. The footprint does not fit.";

                EndLevelEdit();
                isPainting = false;
                dragFootprintActive = false;
            }

            current.Use();
        }

        private void UpdateHoverPresentation()
        {
            RefreshFootprintPreview(hasHoveredCell, hoveredCell);
            RefreshEntityHighlights();
            PrepareHighlightFrames();
        }

        private void RefreshEntityHighlights()
        {
            hasExistingHover = false;
            overwriteCount = 0;
            if (!hasHoveredCell || activeLevel == null)
                return;

            BlockPlacementData placement;
            bool isAnchorCell;
            if (activeLevel.TryGetBlockAt(hoveredCell, activeCatalog, out placement, out isAnchorCell))
            {
                bool movingThisBlock = isMovingExistingBlock && placement.gridPosition == moveOriginPlacement.gridPosition;
                if (!movingThisBlock)
                {
                    existingHoverPlacement = placement;
                    existingHoverSize = activeLevel.GetEffectiveSize(placement, activeCatalog);
                    hasExistingHover = isAnchorCell || existingHoverSize.x > 0;
                }
            }

            if (!hasFootprintPreview || footprintStatus != PlacementValidationStatus.ValidOverwrite)
                return;

            int count = activeLevel.PlacementCount;
            for (int i = 0; i < count; i++)
            {
                if (overwriteCount >= MaxOverwriteHighlights)
                    break;

                BlockPlacementData candidate;
                if (!activeLevel.TryGetPlacementByIndex(i, out candidate))
                    continue;

                if (isMovingExistingBlock && candidate.gridPosition == moveOriginPlacement.gridPosition)
                    continue;

                Vector3Int candidateSize = activeLevel.GetEffectiveSize(candidate, activeCatalog);
                if (!blockFootprint.Overlaps(footprintAnchor, footprintSize, candidate.gridPosition, candidateSize))
                    continue;

                overwritePlacements[overwriteCount] = candidate;
                overwriteSizes[overwriteCount] = candidateSize;
                overwriteCount++;
            }
        }

        private void PrepareHighlightFrames()
        {
            existingFrame.active = hasExistingHover && !ContainsOverwriteAnchor(existingHoverPlacement.gridPosition);
            existingFrame.anchor = existingHoverPlacement.gridPosition;
            existingFrame.size = existingHoverSize;
            existingFrame.color = new Color(1f, 0.94f, 0.45f, 1f);
            existingFrame.hasTopLeft = false;
            existingFrame.hasBottomRight = false;

            previewFrame.active = hasFootprintPreview;
            previewFrame.anchor = footprintAnchor;
            previewFrame.size = footprintSize;
            previewFrame.color = ColorForStatus(footprintStatus);
            previewFrame.hasTopLeft = false;
            previewFrame.hasBottomRight = false;

            moveOriginFrame.active = false;
            moveOriginFrame.hasTopLeft = false;
            moveOriginFrame.hasBottomRight = false;
            moveOriginFrame.dashed = false;
            if (isMovingExistingBlock)
            {
                moveOriginFrame.active = true;
                moveOriginFrame.anchor = moveOriginPlacement.gridPosition;
                moveOriginFrame.size = activeLevel != null
                    ? activeLevel.GetEffectiveSize(moveOriginPlacement, activeCatalog)
                    : Vector3Int.one;
                moveOriginFrame.color = new Color(0.82f, 0.88f, 0.92f, 0.95f);
                moveOriginFrame.dashed = true;
            }

            for (int i = 0; i < MaxOverwriteHighlights; i++)
            {
                overwriteFrames[i].active = false;
                overwriteFrames[i].hasTopLeft = false;
                overwriteFrames[i].hasBottomRight = false;
            }

            for (int i = 0; i < overwriteCount; i++)
            {
                overwriteFrames[i].active = true;
                overwriteFrames[i].anchor = overwritePlacements[i].gridPosition;
                overwriteFrames[i].size = overwriteSizes[i];
                overwriteFrames[i].color = new Color(1f, 0.32f, 0.08f, 1f);
            }
        }

        private void TrackCellHighlight(Vector3Int cell, Rect cellRect)
        {
            TrackHighlightRect(ref existingFrame, cell, cellRect);
            TrackHighlightRect(ref previewFrame, cell, cellRect);
            TrackHighlightRect(ref moveOriginFrame, cell, cellRect);
            for (int i = 0; i < overwriteCount; i++)
                TrackHighlightRect(ref overwriteFrames[i], cell, cellRect);
        }

        private void TrackHighlightRect(ref HighlightFrame frame, Vector3Int cell, Rect cellRect)
        {
            if (!frame.active || !blockFootprint.ContainsCell(frame.anchor, frame.size, cell))
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
            for (int i = 0; i < overwriteCount; i++)
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
            if (!hasExistingHover)
                return false;

            return blockFootprint.ContainsCell(existingHoverPlacement.gridPosition, existingHoverSize, cell);
        }

        private bool IsOverwriteCell(Vector3Int cell)
        {
            for (int i = 0; i < overwriteCount; i++)
            {
                if (blockFootprint.ContainsCell(overwritePlacements[i].gridPosition, overwriteSizes[i], cell))
                    return true;
            }

            return false;
        }

        private bool ContainsOverwriteAnchor(Vector3Int anchor)
        {
            for (int i = 0; i < overwriteCount; i++)
            {
                if (overwritePlacements[i].gridPosition == anchor)
                    return true;
            }

            return false;
        }

        private void RefreshFootprintPreview(bool previewVisible, Vector3Int previewAnchor)
        {
            hasFootprintPreview = false;
            hasMovePreview = false;
            if (isMovingExistingBlock)
            {
                RefreshMovePreview(previewVisible, previewAnchor);
                return;
            }

            if (!previewVisible || activeLevel == null || activeCatalog == null)
                return;

            int blockId;
            if (!TryResolveFootprintBlock(out blockId))
                return;

            BlockDefinition definition;
            if (!activeCatalog.TryGetBlockDefinition(blockId, out definition))
                return;

            footprintAnchor = previewAnchor;
            Vector3Int customSize = ResolveBrushCustomSize(definition);
            Vector3Int sizeInCells = customSize.x > 0 ? customSize : definition.sizeInCells;
            footprintSize = blockFootprint.GetEffectiveSize(sizeInCells, brushRotationEuler);
            footprintStatus = activeLevel.ValidatePlacement(footprintAnchor, footprintSize, activeCatalog);
            hasFootprintPreview = true;
        }

        private bool TryGetPreviewMembership(Vector3Int cell)
        {
            if (!hasFootprintPreview)
                return false;

            return blockFootprint.ContainsCell(footprintAnchor, footprintSize, cell);
        }

        private bool TryResolveFootprintBlock(out int blockId)
        {
            if (dragFootprintActive)
            {
                blockId = dragFootprintBlockId;
                return true;
            }

            if (hasBrush)
            {
                blockId = brushBlockId;
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

        private string FormatFootprint(Vector3Int size)
        {
            Vector3Int clamped = blockFootprint.ClampSize(size);
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

        private void DrawBorder(Rect rect, Color color, float thickness)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private Color GetReadableTextColor(Color background)
        {
            float luminance = (background.r * 0.299f) + (background.g * 0.587f) + (background.b * 0.114f);
            if (luminance > 0.62f)
                return new Color(0.08f, 0.08f, 0.08f, 1f);

            return Color.white;
        }
    }
}
