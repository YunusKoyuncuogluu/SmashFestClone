using System;
using System.Collections.Generic;
using SmashFest.Data;
using UnityEditor;
using UnityEngine;

namespace SmashFest.LevelEditor
{
    [Serializable]
    internal class LevelEditorToolbar
    {
        [NonSerialized] private LevelBlueprintEditorWindow window;
        [NonSerialized] private LevelEditorState state;
        [NonSerialized] private LevelEditorActions actions;
        [NonSerialized] private LevelEditorPaletteView palette;
        private int selectedPresetIndex;
        private readonly string[] presetLabels =
        {
            "Select Preset...",
            "Straight Apartment",
            "45 Degree Angled Tower",
            "Glass Weak-Point",
            "Shelf & Jars",
            "V-Angled Twin Tables"
        };
        private bool showDatabaseSettings;
        private LevelArchitecturePresetBuilder presetBuilder;

        public void Bind(LevelBlueprintEditorWindow window, LevelEditorState state, LevelEditorActions actions, LevelEditorPaletteView palette)
        {
            this.window = window;
            this.state = state;
            this.actions = actions;
            this.palette = palette;
        }

        public void DrawCompactHeader()
        {
            actions.EnsureRepository();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("-10", GUILayout.MinWidth(30f)))
                actions.NavigateToLevel(state.editingLevelIndex - 10);

            if (GUILayout.Button("Prev", GUILayout.MinWidth(30f)))
                actions.NavigateToLevel(state.editingLevelIndex - 1);

            EditorGUILayout.LabelField("Level", GUILayout.Width(34f));
            state.jumpLevelField = EditorGUILayout.IntField(state.jumpLevelField, GUILayout.MinWidth(30f), GUILayout.MaxWidth(64f));
            EditorGUILayout.LabelField("/ " + JsonLevelRepository.MaxLevelCount.ToString(), GUILayout.Width(44f));
            if (GUILayout.Button("Go", GUILayout.MinWidth(26f)))
                actions.NavigateToLevel(state.jumpLevelField);

            if (GUILayout.Button("Next", GUILayout.MinWidth(30f)))
                actions.NavigateToLevel(state.editingLevelIndex + 1);

            if (GUILayout.Button("+10", GUILayout.MinWidth(30f)))
                actions.NavigateToLevel(state.editingLevelIndex + 10);

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(state.ActiveLevel == null);
            EditorGUILayout.LabelField("Max Ammo", GUILayout.Width(64f));
            EditorGUI.BeginChangeCheck();
            int maxAmmo = state.ActiveLevel != null ? state.ActiveLevel.MaxAmmo : 0;
            maxAmmo = EditorGUILayout.IntField(maxAmmo, GUILayout.Width(40f));
            if (EditorGUI.EndChangeCheck() && state.ActiveLevel != null)
            {
                int clampedAmmo = maxAmmo < 0 ? 0 : maxAmmo;
                actions.CommitLevelChange("Edit Max Ammo", () => state.ActiveLevel.MaxAmmo = clampedAmmo);
            }

            if (GUILayout.Button("Frame Scene", GUILayout.MinWidth(70f)))
                actions.FrameLevel();

            if (GUILayout.Button("Save JSON", GUILayout.MinWidth(70f)))
            {
                actions.PersistJson(true);
                state.statusMessage = "Saved LevelsDatabase.json.";
            }

            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
            DrawWorldScaleRow();
            EditorGUILayout.EndVertical();
        }

        private void DrawWorldScaleRow()
        {
            EditorGUI.BeginDisabledGroup(state.activeCatalog == null);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("World Scale", GUILayout.Width(76f));
            float currentScale = state.activeCatalog != null ? state.activeCatalog.globalBlockScale : 1f;
            EditorGUI.BeginChangeCheck();
            float nextScale = EditorGUILayout.Slider(currentScale, BlockCatalogSO.MinGlobalBlockScale, BlockCatalogSO.MaxGlobalBlockScale);
            if (EditorGUI.EndChangeCheck() && state.activeCatalog != null)
            {
                Undo.RecordObject(state.activeCatalog, "Change World Scale");
                state.activeCatalog.SetScale(nextScale);
                EditorUtility.SetDirty(state.activeCatalog);
                SceneView.RepaintAll();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUI.EndDisabledGroup();
        }

        public void DrawDatabaseSettingsFoldout()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            showDatabaseSettings = EditorGUILayout.Foldout(showDatabaseSettings, "Database & Grid Settings", true);
            if (!showDatabaseSettings)
            {
                EditorGUILayout.EndVertical();
                return;
            }

            if (state.activeCatalog == null)
            {
                EditorGUILayout.HelpBox("Block Catalog missing. Create default project assets.", MessageType.Info);
                if (GUILayout.Button("Create Default Level Assets"))
                {
                    LevelBlueprintSampleAssets.Create();
                    GUIUtility.ExitGUI();
                }
            }

            EditorGUI.BeginChangeCheck();
            BlockCatalogSO nextCatalog = (BlockCatalogSO)EditorGUILayout.ObjectField(
                "Block Catalog",
                state.activeCatalog,
                typeof(BlockCatalogSO),
                false);
            if (EditorGUI.EndChangeCheck())
                window.SetActiveCatalog(nextCatalog);

            if (state.ActiveLevel != null)
            {
                Vector3Int dimensions = state.ActiveLevel.GridDimensions;
                EditorGUI.BeginChangeCheck();
                int gridX = EditorGUILayout.DelayedIntField("Grid X", dimensions.x);
                int gridY = EditorGUILayout.DelayedIntField("Grid Y", dimensions.y);
                int gridZ = EditorGUILayout.DelayedIntField("Grid Z", dimensions.z);
                if (EditorGUI.EndChangeCheck())
                {
                    Vector3Int clampedDimensions = new Vector3Int(
                        Mathf.Max(1, gridX),
                        Mathf.Max(1, gridY),
                        Mathf.Max(1, gridZ));
                    actions.CommitLevelChange("Edit Grid Dimensions", () =>
                    {
                        state.ActiveLevel.GridDimensions = clampedDimensions;
                        state.layerTools.RemoveOutOfBounds(state.ActiveLevel, state.activeCatalog);
                    });
                }

                EditorGUI.BeginChangeCheck();
                Vector3 cellSize = EditorGUILayout.Vector3Field("Cell Size", state.ActiveLevel.CellSize);
                if (EditorGUI.EndChangeCheck())
                    actions.CommitLevelChange("Edit Cell Size", () => state.ActiveLevel.CellSize = cellSize);

                EditorGUI.BeginChangeCheck();
                Vector3 originOffset = EditorGUILayout.Vector3Field("Origin Offset", state.ActiveLevel.OriginOffset);
                if (EditorGUI.EndChangeCheck())
                    actions.CommitLevelChange("Edit Origin Offset", () => state.ActiveLevel.OriginOffset = originOffset);
            }

            EditorGUI.BeginDisabledGroup(state.activeCatalog == null);
            if (GUILayout.Button("Generate All 1000 Levels To JSON", GUILayout.Height(24f)))
                GenerateAllLevelsToJson();

            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();
        }

        private void ApplyPreset(int presetIndex)
        {
            if (state.ActiveLevel == null || state.activeCatalog == null)
            {
                state.statusMessage = "Select a level and a catalog first.";
                return;
            }

            if (Application.isPlaying)
            {
                state.statusMessage = "Exit Play Mode before applying a preset.";
                return;
            }

            actions.CancelMove();
            if (presetBuilder == null)
                presetBuilder = new LevelArchitecturePresetBuilder();

            string message = string.Empty;
            actions.CommitLevelChange("Apply Architecture Preset", () =>
            {
                message = presetBuilder.Apply(state.ActiveLevel, state.activeCatalog, presetIndex, state.selectedZLayer);
            });
            if (presetBuilder.LastAnchorLayer != state.selectedZLayer)
                state.selectedZLayer = presetBuilder.LastAnchorLayer;

            actions.PersistJson(true);
            state.hasSelectedPlacement = false;
            state.statusMessage = message;
        }

        private void RegenerateCurrentLevel()
        {
            if (state.ActiveLevel == null || state.activeCatalog == null)
            {
                state.statusMessage = "Select a catalog first.";
                return;
            }

            if (Application.isPlaying)
            {
                state.statusMessage = "Exit Play Mode before regenerating.";
                return;
            }

            actions.CancelMove();
            if (presetBuilder == null)
                presetBuilder = new LevelArchitecturePresetBuilder();

            int index = state.editingLevelIndex < 1 ? 1 : state.editingLevelIndex;
            string message = string.Empty;
            actions.CommitLevelChange("Regenerate Current Level", () =>
            {
                state.ActiveLevel.LevelIndex = index;
                message = presetBuilder.GenerateForLevelIndex(state.ActiveLevel, state.activeCatalog, index);
            });
            state.selectedZLayer = presetBuilder.LastAnchorLayer;
            actions.PersistJson(true);
            state.hasSelectedPlacement = false;
            state.statusMessage = message;
        }

        private void GenerateAllLevelsToJson()
        {
            if (state.activeCatalog == null)
            {
                state.statusMessage = "Select a catalog first.";
                return;
            }

            if (Application.isPlaying)
            {
                state.statusMessage = "Exit Play Mode before generating levels.";
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "Generate 1000 Levels",
                "This will overwrite every level in LevelsDatabase.json (1.."
                    + JsonLevelRepository.MaxLevelCount.ToString()
                    + "). Continue?",
                "Generate",
                "Cancel");
            if (!confirmed)
            {
                state.statusMessage = "Generate 1000 cancelled.";
                return;
            }

            actions.CancelMove();
            actions.EnsureRepository();
            if (presetBuilder == null)
                presetBuilder = new LevelArchitecturePresetBuilder();

            LevelData template = state.ActiveLevel;
            LevelData temp = new LevelData();
            if (template != null)
                CopyGrid(template, temp);
            else
            {
                temp.GridDimensions = new Vector3Int(16, 14, 6);
                temp.CellSize = Vector3.one;
                temp.OriginOffset = Vector3.zero;
                temp.FallEliminationY = -1.5f;
            }

            List<LevelRecord> records = new List<LevelRecord>(JsonLevelRepository.MaxLevelCount);
            int emptyCount = 0;
            try
            {
                for (int index = 1; index <= JsonLevelRepository.MaxLevelCount; index++)
                {
                    if ((index % 20) == 0 || index == JsonLevelRepository.MaxLevelCount)
                    {
                        float progress = index / (float)JsonLevelRepository.MaxLevelCount;
                        EditorUtility.DisplayProgressBar(
                            "Generate 1000 Levels",
                            "Building level " + index.ToString() + " / " + JsonLevelRepository.MaxLevelCount.ToString(),
                            progress);
                    }

                    string message = presetBuilder.GenerateForLevelIndex(temp, state.activeCatalog, index);
                    if (temp.PlacementCount <= 0)
                        emptyCount++;

                    records.Add(state.levelRepository.CreateRecordFromLevel(temp));
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            state.levelRepository.ReplaceAll(records);
            state.ActiveLevel = null;
            actions.PersistJson(true);
            state.jsonDirty = false;
            actions.NavigateToLevel(state.editingLevelIndex);
            state.selectedZLayer = 0;
            state.hasSelectedPlacement = false;
            state.statusMessage = "Generated " + records.Count.ToString()
                + " levels into LevelsDatabase.json. Empty=" + emptyCount.ToString() + ".";
        }

        private void CopyGrid(LevelData source, LevelData target)
        {
            target.GridDimensions = source.GridDimensions;
            target.CellSize = source.CellSize;
            target.OriginOffset = source.OriginOffset;
            target.FallEliminationY = source.FallEliminationY;
            target.MaxAmmo = source.MaxAmmo;
            target.ParAmmo = source.ParAmmo;
        }

        public void DrawActionBar()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(state.ActiveLevel == null || state.activeCatalog == null);
            if (GUILayout.Button("Regenerate", GUILayout.MinWidth(70f)))
                RegenerateCurrentLevel();

            selectedPresetIndex = EditorGUILayout.Popup(selectedPresetIndex, presetLabels, GUILayout.MinWidth(90f));
            EditorGUI.BeginDisabledGroup(selectedPresetIndex <= 0);
            if (GUILayout.Button("Apply", GUILayout.Width(50f)))
                ApplyPreset(selectedPresetIndex - 1);

            EditorGUI.EndDisabledGroup();
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            string brushLabel = "None";
            if (state.hasBrush && state.activeCatalog != null && state.activeCatalog.TryGetBlockDefinition(state.brushBlockId, out BlockDefinition brushDefinition))
                brushLabel = actions.GetShortBlockName(brushDefinition.blockName) + " #" + brushDefinition.blockId.ToString();

            EditorGUILayout.LabelField("Brush", GUILayout.Width(36f));
            EditorGUILayout.LabelField(brushLabel, EditorStyles.boldLabel, GUILayout.MinWidth(40f));
            if (GUILayout.Button("Clear Brush", GUILayout.MinWidth(76f)))
            {
                state.hasBrush = false;
                state.brushBlockId = 0;
                state.statusMessage = "Brush cleared.";
                state.hoverChanged = true;
            }

            EditorGUILayout.EndHorizontal();

            if (state.hasBrush || state.hasSelectedPlacement)
                DrawCompactOrientationRow();

            if (!state.hasBrush && state.hasSelectedPlacement && state.ActiveLevel != null
                && new LevelLayout(state.ActiveLevel, state.activeCatalog).IsFoundationTable(state.selectedPlacement))
            {
                DrawSelectedTableScaleRow();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawCompactOrientationRow()
        {
            EditorGUILayout.BeginHorizontal();
            string title = state.hasBrush ? "Angle" : "Block";
            if (!state.hasBrush && state.hasSelectedPlacement && state.ActiveLevel != null && new LevelLayout(state.ActiveLevel, state.activeCatalog).IsFoundationTable(state.selectedPlacement))
                title = "Table";

            EditorGUILayout.LabelField(title, GUILayout.Width(38f));
            if (GUILayout.Button("-45", GUILayout.MinWidth(24f)))
                ApplyAngle(-45f);

            if (GUILayout.Button("-30", GUILayout.MinWidth(24f)))
                ApplyAngle(-30f);

            if (GUILayout.Button("0", GUILayout.MinWidth(20f)))
                ApplyAngle(0f);

            if (GUILayout.Button("+30", GUILayout.MinWidth(24f)))
                ApplyAngle(30f);

            if (GUILayout.Button("+45", GUILayout.MinWidth(24f)))
                ApplyAngle(45f);

            if (GUILayout.Button("+90", GUILayout.MinWidth(24f)))
                ApplyAngle(90f);

            EditorGUILayout.LabelField("Axis", GUILayout.Width(28f));
            if (GUILayout.Button("X", GUILayout.MinWidth(20f)))
                ApplyLengthAxis(0);

            if (GUILayout.Button("Y", GUILayout.MinWidth(20f)))
                ApplyLengthAxis(1);

            if (GUILayout.Button("Z", GUILayout.MinWidth(20f)))
                ApplyLengthAxis(2);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSelectedTableScaleRow()
        {
            Vector3Int size = state.ActiveLevel.GetSizeInCells(state.selectedPlacement, state.activeCatalog);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("TableTop", GUILayout.Width(52f));
            EditorGUILayout.LabelField("Width", GUILayout.Width(38f));
            if (GUILayout.Button("-", GUILayout.Width(22f)))
                ApplySelectedTableSize(size.x - 1, size.z);

            EditorGUILayout.LabelField(size.x.ToString(), GUILayout.Width(16f));
            if (GUILayout.Button("+", GUILayout.Width(22f)))
                ApplySelectedTableSize(size.x + 1, size.z);

            EditorGUILayout.LabelField("Depth", GUILayout.Width(38f));
            if (GUILayout.Button("-", GUILayout.Width(22f)))
                ApplySelectedTableSize(size.x, size.z - 1);

            EditorGUILayout.LabelField(size.z.ToString(), GUILayout.Width(16f));
            if (GUILayout.Button("+", GUILayout.Width(22f)))
                ApplySelectedTableSize(size.x, size.z + 1);

            EditorGUILayout.EndHorizontal();
        }

        private void ApplySelectedTableSize(int width, int depth)
        {
            if (state.ActiveLevel == null || !state.hasSelectedPlacement)
                return;

            int clampedWidth = Mathf.Clamp(width, 1, 8);
            int clampedDepth = Mathf.Clamp(depth, 1, 6);
            Vector3Int newSize = new Vector3Int(clampedWidth, 1, clampedDepth);
            Vector3Int currentSize = state.ActiveLevel.GetEffectiveSize(state.selectedPlacement, state.activeCatalog);
            if (newSize == state.ActiveLevel.GetSizeInCells(state.selectedPlacement, state.activeCatalog))
                return;

            Vector3Int rotated = state.blockFootprint.GetEffectiveSize(newSize, state.selectedPlacement.rotationEuler);
            PlacementValidationStatus status = state.ActiveLevel.ValidatePlacementIgnoring(
                state.selectedPlacement.gridPosition,
                rotated,
                state.activeCatalog,
                state.selectedPlacement.gridPosition);
            if (status == PlacementValidationStatus.InvalidOutOfBounds)
            {
                state.statusMessage = "TableTop " + clampedWidth.ToString() + "x" + clampedDepth.ToString() + " does not fit.";
                window.Repaint();
                return;
            }

            actions.BeginLevelEdit("Resize Table");
            BlockPlacementData updated = state.selectedPlacement;
            updated.customSize = newSize;
            state.ActiveLevel.TryRemoveAt(state.selectedPlacement.gridPosition);
            if (!state.ActiveLevel.PlaceBlock(updated, rotated, state.activeCatalog))
            {
                state.ActiveLevel.PlaceBlock(state.selectedPlacement, currentSize, state.activeCatalog);
                actions.EndLevelEdit();
                state.statusMessage = "TableTop resize failed.";
                return;
            }

            state.selectedPlacement = updated;
            state.hasSelectedPlacement = true;
            state.statusMessage = "TableTop resized to " + clampedWidth.ToString() + "x1x" + clampedDepth.ToString() + ".";
            actions.EndLevelEdit();
            state.hoverChanged = true;
            window.Repaint();
            SceneView.RepaintAll();
        }

        private void ApplyAngle(float yaw)
        {
            Vector3 euler = state.hasBrush ? state.brushRotationEuler : state.selectedPlacement.rotationEuler;
            euler.y = yaw;
            ApplyOrientation(euler, "Yaw set to " + yaw.ToString("0") + ".");
        }

        private void ApplyLengthAxis(int axis)
        {
            BlockDefinition definition;
            if (!TryGetActiveDefinition(out definition))
                return;

            Vector3Int sizeInCells = ResolveActiveSizeInCells(definition);
            Vector3 euler = state.blockFootprint.ResolveLengthAxisEuler(sizeInCells, axis);
            string axisName = "X";
            if (axis == 1)
                axisName = "Y";
            else if (axis == 2)
                axisName = "Z";

            ApplyOrientation(euler, "Axis set to " + axisName + ".");
        }

        private Vector3Int ResolveActiveSizeInCells(BlockDefinition definition)
        {
            if (!state.hasBrush && state.hasSelectedPlacement && state.ActiveLevel != null)
                return state.ActiveLevel.GetSizeInCells(state.selectedPlacement, state.activeCatalog);

            Vector3Int customSize = palette.ResolveBrushCustomSize(definition);
            if (customSize.x > 0)
                return customSize;

            return definition.sizeInCells;
        }

        public void CycleLengthAxis()
        {
            BlockDefinition definition;
            if (!TryGetActiveDefinition(out definition))
                return;

            Vector3 current = state.hasBrush ? state.brushRotationEuler : state.selectedPlacement.rotationEuler;
            Vector3Int effective = state.blockFootprint.GetEffectiveSize(ResolveActiveSizeInCells(definition), current);
            int currentAxis = 0;
            if (effective.y >= effective.x && effective.y >= effective.z)
                currentAxis = 1;
            else if (effective.z >= effective.x && effective.z >= effective.y)
                currentAxis = 2;

            int nextAxis = currentAxis + 1;
            if (nextAxis > 2)
                nextAxis = 0;

            ApplyLengthAxis(nextAxis);
        }

        private void ApplyOrientation(Vector3 euler, string message)
        {
            if (state.hasBrush)
            {
                state.brushRotationEuler = euler;
                state.statusMessage = message;
                state.hoverChanged = true;
                window.Repaint();
                SceneView.RepaintAll();
                return;
            }

            if (!state.hasSelectedPlacement || state.ActiveLevel == null || state.activeCatalog == null)
                return;

            BlockDefinition definition;
            if (!state.activeCatalog.TryGetBlockDefinition(state.selectedPlacement.blockId, out definition))
                return;

            Vector3Int sizeInCells = state.ActiveLevel.GetSizeInCells(state.selectedPlacement, state.activeCatalog);
            Vector3Int size = state.blockFootprint.GetEffectiveSize(sizeInCells, euler);
            PlacementValidationStatus status = state.ActiveLevel.ValidatePlacementIgnoring(
                state.selectedPlacement.gridPosition,
                size,
                state.activeCatalog,
                state.selectedPlacement.gridPosition);
            if (status == PlacementValidationStatus.InvalidOutOfBounds)
            {
                state.statusMessage = "That orientation does not fit inside the grid.";
                return;
            }

            BlockPlacementData updated = state.selectedPlacement;
            updated.rotationEuler = euler;
            actions.CommitLevelChange("Set Block Orientation", () => state.ActiveLevel.TrySetPlacement(updated));
            state.selectedPlacement = updated;
            if (state.isMovingExistingBlock)
            {
                state.moveRotationEuler = euler;
                state.moveOriginPlacement.rotationEuler = euler;
            }

            AssetDatabase.SaveAssets();
            state.statusMessage = message;
            state.hoverChanged = true;
            window.Repaint();
            SceneView.RepaintAll();
        }

        private bool TryGetActiveDefinition(out BlockDefinition definition)
        {
            definition = default;
            if (state.activeCatalog == null)
                return false;

            int blockId = state.brushBlockId;
            if (!state.hasBrush)
            {
                if (!state.hasSelectedPlacement)
                    return false;

                blockId = state.selectedPlacement.blockId;
            }

            return state.activeCatalog.TryGetBlockDefinition(blockId, out definition);
        }

        public void DrawLayerBar()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (state.ActiveLevel == null)
            {
                EditorGUILayout.LabelField("No level loaded.");
                EditorGUILayout.EndVertical();
                return;
            }

            int layerCount = state.ActiveLevel.GridDimensions.z;
            actions.EnsureLayerLabels(layerCount);
            int maxLayer = layerCount - 1;
            int toolbarLayer = GUILayout.Toolbar(state.selectedZLayer, state.layerLabels, GUILayout.MinHeight(22f));
            if (toolbarLayer != state.selectedZLayer)
            {
                state.selectedZLayer = toolbarLayer;
                actions.SyncPreview();
                SceneView.RepaintAll();
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(state.selectedZLayer <= 0);
            bool copyPrevious = GUILayout.Button("Copy Prev", GUILayout.MinWidth(60f));
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(state.selectedZLayer >= maxLayer);
            bool copyNext = GUILayout.Button("Copy Next", GUILayout.MinWidth(60f));
            EditorGUI.EndDisabledGroup();
            bool clearLayer = GUILayout.Button("Clear Layer", GUILayout.MinWidth(60f));
            EditorGUILayout.EndHorizontal();

            if (copyPrevious)
                actions.CopyPreviousLayer();

            if (copyNext)
                actions.CopyLayerToNext();

            if (clearLayer)
                actions.ClearActiveLayer();

            EditorGUILayout.EndVertical();
        }
    }
}
