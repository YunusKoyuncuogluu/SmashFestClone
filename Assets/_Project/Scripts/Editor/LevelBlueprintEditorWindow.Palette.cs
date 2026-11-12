using System;
using System.Collections.Generic;
using SmashFast.Data;
using UnityEditor;
using UnityEngine;

namespace SmashFast.LevelEditor
{
    public partial class LevelBlueprintEditorWindow
    {
        private void DrawBottomPalette()
        {
            float availWidth = position.width - WindowPadding - 8f;
            bool narrow = position.width < NarrowPaletteWidth;
            float filterHeight = narrow ? 48f : 26f;
            float gridHeight = PaletteHeight - filterHeight - SizeStripHeight - PaletteVerticalSlack;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(PaletteHeight));
            DrawPaletteFilterRow(availWidth, narrow);

            if (activeCatalog == null)
                EditorGUILayout.HelpBox("Assign a Block Catalog to choose blocks.", MessageType.None);
            else if (activeCatalog.FamilyCount == 0 && activeCatalog.StandaloneCount == 0)
                EditorGUILayout.HelpBox("The catalog has no families or standalone blocks yet.", MessageType.Warning);
            else
            {
                DrawSizeStrip(availWidth);
                DrawPaletteGrid(availWidth, gridHeight);
            }

            EditorGUILayout.EndVertical();

            if (AssetPreview.IsLoadingAssetPreviews())
                Repaint();
        }

        private void DrawPaletteFilterRow(float availWidth, bool narrow)
        {
            if (narrow)
            {
                paletteCategoryIndex = GUILayout.Toolbar(
                    paletteCategoryIndex,
                    paletteCategoryLabels,
                    GUILayout.Height(22f));
                EditorGUILayout.BeginHorizontal(GUILayout.Height(22f));
                EditorGUILayout.LabelField("Search", GUILayout.Width(44f));
                paletteSearch = EditorGUILayout.TextField(paletteSearch);
                EditorGUILayout.EndHorizontal();
                return;
            }

            EditorGUILayout.BeginHorizontal(GUILayout.Height(26f));
            paletteCategoryIndex = GUILayout.Toolbar(
                paletteCategoryIndex,
                paletteCategoryLabels,
                GUILayout.Height(22f),
                GUILayout.Width(availWidth * 0.62f));
            EditorGUILayout.LabelField("Search", GUILayout.Width(44f));
            paletteSearch = EditorGUILayout.TextField(paletteSearch);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSizeStrip(float availWidth)
        {
            Rect stripRect = GUILayoutUtility.GetRect(
                0f,
                SizeStripHeight,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(SizeStripHeight));
            stripRect.width = availWidth;
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(stripRect, new Color(0.1f, 0.11f, 0.13f, 1f));
                DrawBorder(stripRect, new Color(0.34f, 0.38f, 0.42f, 1f), 1f);
            }

            if (selectedCardKey < 0)
            {
                GUI.Label(stripRect, "Click a block card to pick a size. Drag any card to place its 1x block.", EditorStyles.wordWrappedMiniLabel);
                return;
            }

            if (selectedCardKey >= StandaloneCardKeyOffset)
            {
                BlockDefinition standalone;
                if (!activeCatalog.TryGetStandalone(selectedCardKey - StandaloneCardKeyOffset, out standalone))
                    return;

                if (IsStandaloneTable(standalone))
                {
                    DrawTableStrip(
                        stripRect,
                        standalone,
                        new Color(0.72f, 0.48f, 0.28f, 1f),
                        new Color(0.82f, 0.58f, 0.35f, 1f));
                    return;
                }

                GUI.Label(stripRect, GetBlockLabel(standalone) + ": " + FormatFootprint(standalone.sizeInCells) + " only. Drag the card onto the matrix.", EditorStyles.wordWrappedMiniLabel);
                return;
            }

            BlockFamilyDefinition family;
            if (!activeCatalog.TryGetFamily(selectedCardKey, out family))
                return;

            if (family.foundationTable)
            {
                BlockDefinition tableDefinition;
                if (TryResolveFamilyDefault(selectedCardKey, family, out tableDefinition))
                    DrawTableStrip(stripRect, tableDefinition, family.xColor, family.zColor);

                return;
            }

            if (family.GetHighestAssignedLength() <= 1)
            {
                GUI.Label(stripRect, family.familyName + ": 1x1 only. Drag the card onto the matrix.", EditorStyles.wordWrappedMiniLabel);
                return;
            }

            DrawFamilyStrip(stripRect, selectedCardKey, family);
        }

        private void DrawFamilyStrip(Rect stripRect, int familyIndex, BlockFamilyDefinition family)
        {
            float innerWidth = stripRect.width - (StripPadding * 2f);
            if (family.onlyVerticalAxis)
            {
                float verticalChipWidth = Mathf.Clamp((innerWidth - StripLabelWidth - (4f * StripChipGap)) / 5f, 16f, 64f);
                DrawStripAxis(stripRect.x + StripPadding, stripRect.y + StripPadding, verticalChipWidth, familyIndex, family, 1);
                return;
            }

            float half = (innerWidth - StripColumnGap) * 0.5f;
            float chipWidth = Mathf.Max(16f, (half - StripLabelWidth - (4f * StripChipGap)) / 5f);
            float rowOneY = stripRect.y + StripPadding;
            float rowTwoY = rowOneY + FamilyChipHeight + StripRowGap;
            float leftX = stripRect.x + StripPadding;
            float rightX = leftX + half + StripColumnGap;
            DrawStripAxis(leftX, rowOneY, chipWidth, familyIndex, family, 0);
            DrawStripAxis(rightX, rowOneY, chipWidth, familyIndex, family, 1);
            DrawStripAxis(leftX, rowTwoY, chipWidth, familyIndex, family, 2);
        }

        private void DrawStripAxis(float originX, float originY, float chipWidth, int familyIndex, BlockFamilyDefinition family, int axis)
        {
            Color rowColor = family.xColor;
            string axisLabel = "x";
            if (axis == 1)
            {
                rowColor = family.yColor;
                axisLabel = "y";
            }
            else if (axis == 2)
            {
                rowColor = family.zColor;
                axisLabel = "z";
            }

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(new Rect(originX, originY + 4f, 4f, FamilyChipHeight - 8f), rowColor);
                paletteIdStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(originX + 6f, originY, StripLabelWidth - 6f, FamilyChipHeight), axisLabel.ToUpperInvariant() + ":", paletteIdStyle);
            }

            for (int length = 1; length <= BlockFamilyDefinition.MaxLength; length++)
            {
                Rect chipRect = new Rect(
                    originX + StripLabelWidth + ((length - 1) * (chipWidth + StripChipGap)),
                    originY,
                    chipWidth,
                    FamilyChipHeight);
                BlockDefinition definition = default;
                bool available = length <= family.GetHighestAssignedLength()
                    && activeCatalog.TryGetFamilyVariant(familyIndex, length, axis, out definition);

                DrawFamilyChip(chipRect, length.ToString() + axisLabel, rowColor, available, definition);
            }
        }

        private void DrawTableStrip(Rect stripRect, BlockDefinition definition, Color widthColor, Color depthColor)
        {
            float innerWidth = stripRect.width - (StripPadding * 2f);
            float chipWidth = Mathf.Max(16f, (innerWidth - TableStripLabelWidth - (7f * StripChipGap)) / 8f);
            float rowOneY = stripRect.y + StripPadding;
            float rowTwoY = rowOneY + FamilyChipHeight + StripRowGap;
            float originX = stripRect.x + StripPadding;
            if (Event.current.type == EventType.Repaint)
            {
                paletteIdStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(originX, rowOneY, TableStripLabelWidth, FamilyChipHeight), "Width (X):", paletteIdStyle);
                GUI.Label(new Rect(originX, rowTwoY, TableStripLabelWidth, FamilyChipHeight), "Depth (Z):", paletteIdStyle);
            }

            for (int width = 1; width <= 8; width++)
            {
                Rect chipRect = new Rect(
                    originX + TableStripLabelWidth + ((width - 1) * (chipWidth + StripChipGap)),
                    rowOneY,
                    chipWidth,
                    FamilyChipHeight);
                bool selected = hasBrush
                    && brushBlockId == definition.blockId
                    && brushTableWidth == width;
                DrawTableWidthChip(chipRect, width.ToString() + "x", widthColor, definition, width, selected);
            }

            for (int depth = 1; depth <= 4; depth++)
            {
                Rect chipRect = new Rect(
                    originX + TableStripLabelWidth + ((depth - 1) * (chipWidth + StripChipGap)),
                    rowTwoY,
                    chipWidth,
                    FamilyChipHeight);
                bool selected = hasBrush
                    && brushBlockId == definition.blockId
                    && brushTableDepth == depth;
                DrawTableDepthChip(chipRect, depth.ToString() + "z", depthColor, definition, depth, selected);
            }
        }

        private void DrawPaletteGrid(float availWidth, float gridHeight)
        {
            BuildVisibleCardKeys();
            float contentWidth = availWidth - GridScrollbarWidth;
            paletteScroll = EditorGUILayout.BeginScrollView(
                paletteScroll,
                false,
                true,
                GUIStyle.none,
                GUI.skin.verticalScrollbar,
                GUI.skin.scrollView,
                GUILayout.Height(gridHeight));

            int cardCount = visibleCardKeys.Count;
            if (cardCount == 0)
            {
                EditorGUILayout.LabelField("No blocks match the filter.", EditorStyles.miniLabel);
                EditorGUILayout.EndScrollView();
                return;
            }

            int columns = Mathf.Clamp((int)(contentWidth / PaletteCardMinWidth), 2, 5);
            float cardWidth = (contentWidth - ((columns - 1) * PaletteCardGap)) / columns;
            int rows = (cardCount + columns - 1) / columns;
            float contentHeight = rows * (PaletteCardHeight + PaletteCardGap);
            Rect contentRect = GUILayoutUtility.GetRect(
                contentWidth,
                contentHeight,
                GUILayout.Width(contentWidth),
                GUILayout.Height(contentHeight));

            for (int i = 0; i < cardCount; i++)
            {
                int column = i % columns;
                int row = i / columns;
                Rect cardRect = new Rect(
                    contentRect.x + (column * (cardWidth + PaletteCardGap)),
                    contentRect.y + (row * (PaletteCardHeight + PaletteCardGap)),
                    cardWidth,
                    PaletteCardHeight);
                DrawPaletteCard(cardRect, visibleCardKeys[i], gridHeight);
            }

            EditorGUILayout.EndScrollView();
        }

        private void BuildVisibleCardKeys()
        {
            visibleCardKeys.Clear();
            int familyCount = activeCatalog.FamilyCount;
            for (int i = 0; i < familyCount; i++)
            {
                BlockFamilyDefinition family;
                if (!activeCatalog.TryGetFamily(i, out family))
                    continue;

                if (PassesPaletteFilter(family.familyName, family.foundationTable, family.fragileProp, family.materialType))
                    visibleCardKeys.Add(i);
            }

            int standaloneCount = activeCatalog.StandaloneCount;
            for (int i = 0; i < standaloneCount; i++)
            {
                BlockDefinition definition;
                if (!activeCatalog.TryGetStandalone(i, out definition))
                    continue;

                bool isJar = definition.shapeRole == BlockShapeRole.FragileProp
                    || definition.shapeRole == BlockShapeRole.TopOrnament;
                if (PassesPaletteFilter(definition.blockName, IsStandaloneTable(definition), isJar, definition.materialType))
                    visibleCardKeys.Add(StandaloneCardKeyOffset + i);
            }
        }

        private bool IsStandaloneTable(BlockDefinition definition)
        {
            return definition.shapeRole == BlockShapeRole.FoundationTable
                || definition.materialType == BlockMaterialType.TableFoundation;
        }

        private bool PassesPaletteFilter(string name, bool isTable, bool isJarOrProp, BlockMaterialType materialType)
        {
            if (!string.IsNullOrEmpty(paletteSearch))
            {
                if (string.IsNullOrEmpty(name) || name.IndexOf(paletteSearch, StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
            }

            if (paletteCategoryIndex <= 0)
                return true;

            string safeName = string.IsNullOrEmpty(name) ? string.Empty : name;
            if (paletteCategoryIndex == 1)
                return isTable || safeName.IndexOf("Table", StringComparison.OrdinalIgnoreCase) >= 0;

            if (paletteCategoryIndex == 2)
            {
                return safeName.IndexOf("Log", StringComparison.OrdinalIgnoreCase) >= 0
                    || safeName.IndexOf("Wood", StringComparison.OrdinalIgnoreCase) >= 0
                    || safeName.IndexOf("Snow", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            if (paletteCategoryIndex == 3)
                return safeName.IndexOf("Can", StringComparison.OrdinalIgnoreCase) >= 0;

            if (paletteCategoryIndex == 4)
            {
                return safeName.IndexOf("Ice", StringComparison.OrdinalIgnoreCase) >= 0
                    || safeName.IndexOf("Glass", StringComparison.OrdinalIgnoreCase) >= 0
                    || materialType == BlockMaterialType.Glass;
            }

            if (paletteCategoryIndex == 5)
            {
                return safeName.IndexOf("Box", StringComparison.OrdinalIgnoreCase) >= 0
                    || safeName.IndexOf("Stone", StringComparison.OrdinalIgnoreCase) >= 0
                    || materialType == BlockMaterialType.Stone;
            }

            return isJarOrProp
                || safeName.IndexOf("Jar", StringComparison.OrdinalIgnoreCase) >= 0
                || safeName.IndexOf("Cone", StringComparison.OrdinalIgnoreCase) >= 0
                || safeName.IndexOf("Ornament", StringComparison.OrdinalIgnoreCase) >= 0
                || materialType == BlockMaterialType.CeramicJar;
        }

        private void DrawPaletteCard(Rect rect, int cardKey, float gridHeight)
        {
            BlockDefinition dragDefinition = default;
            bool hasDragDefinition;
            string title;
            Texture preview;
            Color stripeColor;
            bool isTable;
            if (cardKey >= StandaloneCardKeyOffset)
            {
                BlockDefinition standalone;
                if (!activeCatalog.TryGetStandalone(cardKey - StandaloneCardKeyOffset, out standalone))
                    return;

                dragDefinition = standalone;
                hasDragDefinition = true;
                title = GetCardDisplayName(GetBlockLabel(standalone));
                preview = GetPreviewTexture(standalone);
                stripeColor = standalone.editorColor;
                isTable = IsStandaloneTable(standalone);
            }
            else
            {
                BlockFamilyDefinition family;
                if (!activeCatalog.TryGetFamily(cardKey, out family))
                    return;

                hasDragDefinition = TryResolveFamilyDefault(cardKey, family, out dragDefinition);
                title = GetCardDisplayName(string.IsNullOrEmpty(family.familyName) ? "Family" : family.familyName);
                preview = GetFamilyPreviewTexture(family);
                stripeColor = family.xColor;
                isTable = family.foundationTable;
            }

            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            Event current = Event.current;
            EventType eventType = current.GetTypeForControl(controlId);

            if (eventType == EventType.Repaint)
            {
                bool selected = cardKey == selectedCardKey;
                EditorGUI.DrawRect(rect, selected ? new Color(0.2f, 0.26f, 0.31f, 1f) : new Color(0.17f, 0.18f, 0.2f, 1f));
                DrawBorder(
                    rect,
                    selected ? new Color(0.4f, 0.95f, 1f, 1f) : new Color(0.34f, 0.38f, 0.42f, 1f),
                    selected ? 2f : 1f);
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), stripeColor);
                Rect previewRect = new Rect(
                    rect.x + 8f,
                    rect.y + ((rect.height - PalettePreviewSize) * 0.5f),
                    PalettePreviewSize,
                    PalettePreviewSize);
                if (preview != null)
                    GUI.DrawTexture(previewRect, preview, ScaleMode.ScaleToFit);

                paletteTitleStyle.normal.textColor = Color.white;
                GUI.Label(
                    new Rect(previewRect.xMax + 4f, rect.y, rect.xMax - previewRect.xMax - 6f, rect.height),
                    title,
                    paletteTitleStyle);
                return;
            }

            bool insideView = current.mousePosition.y >= paletteScroll.y
                && current.mousePosition.y <= paletteScroll.y + gridHeight;
            if (eventType == EventType.MouseDown
                && current.button == 0
                && insideView
                && rect.Contains(current.mousePosition))
            {
                GUIUtility.hotControl = controlId;
                paletteCardPressPosition = current.mousePosition;
                current.Use();
                return;
            }

            if (GUIUtility.hotControl != controlId)
                return;

            if (eventType == EventType.MouseDrag)
            {
                if ((current.mousePosition - paletteCardPressPosition).sqrMagnitude >= CardDragThresholdSqr)
                {
                    GUIUtility.hotControl = 0;
                    if (hasDragDefinition)
                        BeginCardDrag(dragDefinition, isTable);
                }

                current.Use();
                return;
            }

            if (eventType != EventType.MouseUp)
                return;

            GUIUtility.hotControl = 0;
            if (rect.Contains(current.mousePosition))
            {
                selectedCardKey = cardKey;
                if (hasDragDefinition)
                    SelectCardBrush(dragDefinition);
            }

            current.Use();
            Repaint();
        }

        private string GetCardDisplayName(string blockName)
        {
            if (string.IsNullOrEmpty(blockName))
                return "Block";

            string trimmed = blockName;
            if (trimmed.EndsWith("Cylinder", StringComparison.Ordinal) && trimmed.Length > "Cylinder".Length)
                trimmed = trimmed.Substring(0, trimmed.Length - "Cylinder".Length);
            else if (trimmed.EndsWith("Block", StringComparison.Ordinal) && trimmed.Length > "Block".Length)
                trimmed = trimmed.Substring(0, trimmed.Length - "Block".Length);

            return trimmed;
        }

        private bool TryResolveFamilyDefault(int familyIndex, BlockFamilyDefinition family, out BlockDefinition definition)
        {
            if (family.foundationTable && activeCatalog.TryGetFoundationTable(out definition))
                return true;

            return activeCatalog.TryGetFamilyVariant(familyIndex, 1, family.onlyVerticalAxis ? 1 : 0, out definition);
        }

        private void BeginCardDrag(BlockDefinition definition, bool isTable)
        {
            CancelMove();
            hasBrush = true;
            brushBlockId = definition.blockId;
            string label = GetBlockLabel(definition);
            if (isTable)
            {
                brushTableWidth = DefaultTableWidth;
                brushTableDepth = DefaultTableDepth;
                label = "Table " + DefaultTableWidth.ToString() + "x" + DefaultTableDepth.ToString();
            }

            statusMessage = "Brush: " + label + ". Drag it onto the matrix.";
            dragPayload.Begin(definition.blockId, label);
            Repaint();
        }

        private void SelectCardBrush(BlockDefinition definition)
        {
            CancelMove();
            hasBrush = true;
            brushBlockId = definition.blockId;
            statusMessage = "Brush: " + GetBlockLabel(definition) + ". Drag it onto the matrix or click cells to paint.";
        }

        private void DrawTableWidthChip(
            Rect chipRect,
            string label,
            Color rowColor,
            BlockDefinition definition,
            int width,
            bool selected)
        {
            Event current = Event.current;
            if (current != null && current.type == EventType.Repaint)
            {
                Color fill = selected ? Color.Lerp(rowColor, Color.white, 0.35f) : rowColor;
                EditorGUI.DrawRect(chipRect, fill);
                DrawBorder(chipRect, selected ? Color.white : new Color(0.1f, 0.1f, 0.1f, 0.6f), selected ? 2f : 1f);
                paletteIdStyle.normal.textColor = GetReadableTextColor(fill);
                GUI.Label(chipRect, label, paletteIdStyle);
            }

            if (current == null || current.type != EventType.MouseDown || current.button != 0)
                return;

            if (!chipRect.Contains(current.mousePosition))
                return;

            CancelMove();
            hasBrush = true;
            brushBlockId = definition.blockId;
            brushTableWidth = width;
            statusMessage = "Brush: Table "
                + brushTableWidth.ToString()
                + "x1x"
                + brushTableDepth.ToString()
                + ". Drag onto the matrix.";
            dragPayload.Begin(definition.blockId, "Table " + brushTableWidth.ToString() + "x" + brushTableDepth.ToString());
            current.Use();
            Repaint();
        }

        private void DrawTableDepthChip(
            Rect chipRect,
            string label,
            Color rowColor,
            BlockDefinition definition,
            int depth,
            bool selected)
        {
            Event current = Event.current;
            if (current != null && current.type == EventType.Repaint)
            {
                Color fill = selected ? Color.Lerp(rowColor, Color.white, 0.35f) : rowColor;
                EditorGUI.DrawRect(chipRect, fill);
                DrawBorder(chipRect, selected ? Color.white : new Color(0.1f, 0.1f, 0.1f, 0.6f), selected ? 2f : 1f);
                paletteIdStyle.normal.textColor = GetReadableTextColor(fill);
                GUI.Label(chipRect, label, paletteIdStyle);
            }

            if (current == null || current.type != EventType.MouseDown || current.button != 0)
                return;

            if (!chipRect.Contains(current.mousePosition))
                return;

            CancelMove();
            hasBrush = true;
            brushBlockId = definition.blockId;
            brushTableDepth = depth;
            statusMessage = "Brush: Table "
                + brushTableWidth.ToString()
                + "x1x"
                + brushTableDepth.ToString()
                + ". Drag onto the matrix.";
            dragPayload.Begin(definition.blockId, "Table " + brushTableWidth.ToString() + "x" + brushTableDepth.ToString());
            current.Use();
            Repaint();
        }

        private Vector3Int ResolveBrushCustomSize(BlockDefinition definition)
        {
            if (definition.shapeRole == BlockShapeRole.FoundationTable
                || definition.materialType == BlockMaterialType.TableFoundation)
            {
                return new Vector3Int(
                    Mathf.Clamp(brushTableWidth, 1, 8),
                    1,
                    Mathf.Clamp(brushTableDepth, 1, 6));
            }

            return Vector3Int.zero;
        }

        private Texture GetFamilyPreviewTexture(BlockFamilyDefinition family)
        {
            if (family == null)
                return null;

            for (int length = 1; length <= BlockFamilyDefinition.MaxLength; length++)
            {
                GameObject prefab = family.GetLengthPrefab(length);
                if (prefab == null)
                    continue;

                Texture preview = AssetPreview.GetAssetPreview(prefab);
                if (preview == null)
                    preview = AssetPreview.GetMiniThumbnail(prefab);

                if (preview != null)
                    return preview;
            }

            return null;
        }

        private void DrawFamilyChip(Rect chipRect, string label, Color rowColor, bool available, BlockDefinition definition)
        {
            Event current = Event.current;
            bool selected = available && hasBrush && brushBlockId == definition.blockId;
            if (current != null && current.type == EventType.Repaint)
            {
                Color fill = available ? rowColor : new Color(0.25f, 0.25f, 0.25f, 1f);
                if (selected)
                    fill = Color.Lerp(fill, Color.white, 0.35f);

                EditorGUI.DrawRect(chipRect, fill);
                DrawBorder(chipRect, selected ? Color.white : new Color(0.1f, 0.1f, 0.1f, 0.6f), selected ? 2f : 1f);
                paletteIdStyle.normal.textColor = GetReadableTextColor(fill);
                GUI.Label(chipRect, label, paletteIdStyle);
            }

            if (!available || current == null || current.type != EventType.MouseDown || current.button != 0)
                return;

            if (!chipRect.Contains(current.mousePosition))
                return;

            CancelMove();
            hasBrush = true;
            brushBlockId = definition.blockId;
            statusMessage = "Brush: " + GetBlockLabel(definition) + ". Drag the chip onto the matrix.";
            dragPayload.Begin(definition.blockId, GetBlockLabel(definition));
            current.Use();
            Repaint();
        }

        private void DrawStatusBar()
        {
            string hoverText = hasHoveredCell
                ? ("Cell " + hoveredCell.x + "," + hoveredCell.y + "," + hoveredCell.z)
                : "No cell";
            EditorGUILayout.LabelField(statusMessage + "  |  " + hoverText + "  |  R rotate  T axis", EditorStyles.miniLabel);
        }

        private string GetBlockLabel(BlockDefinition definition)
        {
            if (string.IsNullOrEmpty(definition.blockName))
                return "Block " + definition.blockId.ToString();

            return definition.blockName;
        }

        private Texture GetPreviewTexture(BlockDefinition definition)
        {
            Texture preview = null;
            if (definition.prefab != null)
            {
                preview = AssetPreview.GetAssetPreview(definition.prefab);
                if (preview == null)
                    preview = AssetPreview.GetMiniThumbnail(definition.prefab);
            }

            if (preview == null && definition.previewMesh != null)
            {
                preview = AssetPreview.GetAssetPreview(definition.previewMesh);
                if (preview == null)
                    preview = AssetPreview.GetMiniThumbnail(definition.previewMesh);
            }

            return preview;
        }
    }
}
