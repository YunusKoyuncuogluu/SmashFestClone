using System;
using System.Collections.Generic;
using SmashFast.Data;
using UnityEditor;
using UnityEngine;

namespace SmashFast.LevelEditor
{
    public partial class LevelBlueprintEditorWindow
    {
        private void DrawCompactHeader()
        {
            EnsureRepository();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("-10", GUILayout.MinWidth(30f)))
                NavigateToLevel(editingLevelIndex - 10);

            if (GUILayout.Button("Prev", GUILayout.MinWidth(30f)))
                NavigateToLevel(editingLevelIndex - 1);

            EditorGUILayout.LabelField("Level", GUILayout.Width(34f));
            jumpLevelField = EditorGUILayout.IntField(jumpLevelField, GUILayout.MinWidth(30f), GUILayout.MaxWidth(64f));
            EditorGUILayout.LabelField("/ " + JsonLevelRepository.MaxLevelCount.ToString(), GUILayout.Width(44f));
            if (GUILayout.Button("Go", GUILayout.MinWidth(26f)))
                NavigateToLevel(jumpLevelField);

            if (GUILayout.Button("Next", GUILayout.MinWidth(30f)))
                NavigateToLevel(editingLevelIndex + 1);

            if (GUILayout.Button("+10", GUILayout.MinWidth(30f)))
                NavigateToLevel(editingLevelIndex + 10);

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(activeLevel == null);
            EditorGUILayout.LabelField("Max Ammo", GUILayout.Width(64f));
            EditorGUI.BeginChangeCheck();
            int maxAmmo = activeLevel != null ? activeLevel.MaxAmmo : 0;
            maxAmmo = EditorGUILayout.IntField(maxAmmo, GUILayout.Width(40f));
            if (EditorGUI.EndChangeCheck() && activeLevel != null)
            {
                int clampedAmmo = maxAmmo < 0 ? 0 : maxAmmo;
                CommitLevelChange("Edit Max Ammo", () => activeLevel.MaxAmmo = clampedAmmo);
            }

            if (GUILayout.Button("Frame Scene", GUILayout.MinWidth(70f)))
                FrameLevel();

            if (GUILayout.Button("Save JSON", GUILayout.MinWidth(70f)))
            {
                PersistJson(true);
                statusMessage = "Saved LevelsDatabase.json.";
            }

            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
            DrawWorldScaleRow();
            EditorGUILayout.EndVertical();
        }

        private void DrawWorldScaleRow()
        {
            EditorGUI.BeginDisabledGroup(activeCatalog == null);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("World Scale", GUILayout.Width(76f));
            float currentScale = activeCatalog != null ? activeCatalog.globalBlockScale : 1f;
            EditorGUI.BeginChangeCheck();
            float nextScale = EditorGUILayout.Slider(currentScale, BlockCatalogSO.MinGlobalBlockScale, BlockCatalogSO.MaxGlobalBlockScale);
            if (EditorGUI.EndChangeCheck() && activeCatalog != null)
            {
                Undo.RecordObject(activeCatalog, "Change World Scale");
                activeCatalog.SetScale(nextScale);
                EditorUtility.SetDirty(activeCatalog);
                SceneView.RepaintAll();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUI.EndDisabledGroup();
        }

        private void DrawDatabaseSettingsFoldout()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            showDatabaseSettings = EditorGUILayout.Foldout(showDatabaseSettings, "Database & Grid Settings", true);
            if (!showDatabaseSettings)
            {
                EditorGUILayout.EndVertical();
                return;
            }

            if (activeCatalog == null)
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
                activeCatalog,
                typeof(BlockCatalogSO),
                false);
            if (EditorGUI.EndChangeCheck())
                SetActiveCatalog(nextCatalog);

            if (activeLevel != null)
            {
                Vector3Int dimensions = activeLevel.GridDimensions;
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
                    CommitLevelChange("Edit Grid Dimensions", () =>
                    {
                        activeLevel.GridDimensions = clampedDimensions;
                        activeLevel.RemoveOutOfBounds(activeCatalog);
                    });
                }

                EditorGUI.BeginChangeCheck();
                Vector3 cellSize = EditorGUILayout.Vector3Field("Cell Size", activeLevel.CellSize);
                if (EditorGUI.EndChangeCheck())
                    CommitLevelChange("Edit Cell Size", () => activeLevel.CellSize = cellSize);

                EditorGUI.BeginChangeCheck();
                Vector3 originOffset = EditorGUILayout.Vector3Field("Origin Offset", activeLevel.OriginOffset);
                if (EditorGUI.EndChangeCheck())
                    CommitLevelChange("Edit Origin Offset", () => activeLevel.OriginOffset = originOffset);
            }

            EditorGUI.BeginDisabledGroup(activeCatalog == null);
            if (GUILayout.Button("Generate All 1000 Levels To JSON", GUILayout.Height(24f)))
                GenerateAllLevelsToJson();

            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();
        }

        private void ApplyPreset(int presetIndex)
        {
            if (activeLevel == null || activeCatalog == null)
            {
                statusMessage = "Select a level and a catalog first.";
                return;
            }

            if (Application.isPlaying)
            {
                statusMessage = "Exit Play Mode before applying a preset.";
                return;
            }

            CancelMove();
            if (presetBuilder == null)
                presetBuilder = new LevelArchitecturePresetBuilder();

            string message = string.Empty;
            CommitLevelChange("Apply Architecture Preset", () =>
            {
                message = presetBuilder.Apply(activeLevel, activeCatalog, presetIndex, selectedZLayer);
            });
            if (presetBuilder.LastAnchorLayer != selectedZLayer)
                selectedZLayer = presetBuilder.LastAnchorLayer;

            PersistJson(true);
            hasSelectedPlacement = false;
            statusMessage = message;
        }

        private void RegenerateCurrentLevel()
        {
            if (activeLevel == null || activeCatalog == null)
            {
                statusMessage = "Select a catalog first.";
                return;
            }

            if (Application.isPlaying)
            {
                statusMessage = "Exit Play Mode before regenerating.";
                return;
            }

            CancelMove();
            if (presetBuilder == null)
                presetBuilder = new LevelArchitecturePresetBuilder();

            int index = editingLevelIndex < 1 ? 1 : editingLevelIndex;
            string message = string.Empty;
            CommitLevelChange("Regenerate Current Level", () =>
            {
                activeLevel.LevelIndex = index;
                message = presetBuilder.GenerateForLevelIndex(activeLevel, activeCatalog, index);
            });
            selectedZLayer = presetBuilder.LastAnchorLayer;
            PersistJson(true);
            hasSelectedPlacement = false;
            statusMessage = message;
        }

        private void GenerateAllLevelsToJson()
        {
            if (activeCatalog == null)
            {
                statusMessage = "Select a catalog first.";
                return;
            }

            if (Application.isPlaying)
            {
                statusMessage = "Exit Play Mode before generating levels.";
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
                statusMessage = "Generate 1000 cancelled.";
                return;
            }

            CancelMove();
            EnsureRepository();
            if (presetBuilder == null)
                presetBuilder = new LevelArchitecturePresetBuilder();

            LevelData template = activeLevel;
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

                    string message = presetBuilder.GenerateForLevelIndex(temp, activeCatalog, index);
                    if (temp.PlacementCount <= 0)
                        emptyCount++;

                    records.Add(levelRepository.CreateRecordFromLevel(temp));
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            levelRepository.ReplaceAll(records);
            activeLevel = null;
            PersistJson(true);
            jsonDirty = false;
            NavigateToLevel(editingLevelIndex);
            selectedZLayer = 0;
            hasSelectedPlacement = false;
            statusMessage = "Generated " + records.Count.ToString()
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

        private void DrawActionBar()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(activeLevel == null || activeCatalog == null);
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
            if (hasBrush && activeCatalog != null && activeCatalog.TryGetBlockDefinition(brushBlockId, out BlockDefinition brushDefinition))
                brushLabel = GetShortBlockName(brushDefinition.blockName) + " #" + brushDefinition.blockId.ToString();

            EditorGUILayout.LabelField("Brush", GUILayout.Width(36f));
            EditorGUILayout.LabelField(brushLabel, EditorStyles.boldLabel, GUILayout.MinWidth(40f));
            if (GUILayout.Button("Clear Brush", GUILayout.MinWidth(76f)))
            {
                hasBrush = false;
                brushBlockId = 0;
                statusMessage = "Brush cleared.";
                hoverChanged = true;
            }

            EditorGUILayout.EndHorizontal();

            if (hasBrush || hasSelectedPlacement)
                DrawCompactOrientationRow();

            if (!hasBrush && hasSelectedPlacement && activeLevel != null
                && activeLevel.IsFoundationTable(selectedPlacement, activeCatalog))
            {
                DrawSelectedTableScaleRow();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawCompactOrientationRow()
        {
            EditorGUILayout.BeginHorizontal();
            string title = hasBrush ? "Angle" : "Block";
            if (!hasBrush && hasSelectedPlacement && activeLevel != null && activeLevel.IsFoundationTable(selectedPlacement, activeCatalog))
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
            Vector3Int size = activeLevel.GetSizeInCells(selectedPlacement, activeCatalog);
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
            if (activeLevel == null || !hasSelectedPlacement)
                return;

            int clampedWidth = Mathf.Clamp(width, 1, 8);
            int clampedDepth = Mathf.Clamp(depth, 1, 6);
            Vector3Int newSize = new Vector3Int(clampedWidth, 1, clampedDepth);
            Vector3Int currentSize = activeLevel.GetEffectiveSize(selectedPlacement, activeCatalog);
            if (newSize == activeLevel.GetSizeInCells(selectedPlacement, activeCatalog))
                return;

            Vector3Int rotated = blockFootprint.GetEffectiveSize(newSize, selectedPlacement.rotationEuler);
            PlacementValidationStatus status = activeLevel.ValidatePlacementIgnoring(
                selectedPlacement.gridPosition,
                rotated,
                activeCatalog,
                selectedPlacement.gridPosition);
            if (status == PlacementValidationStatus.InvalidOutOfBounds)
            {
                statusMessage = "TableTop " + clampedWidth.ToString() + "x" + clampedDepth.ToString() + " does not fit.";
                Repaint();
                return;
            }

            BeginLevelEdit("Resize Table");
            BlockPlacementData updated = selectedPlacement;
            updated.customSize = newSize;
            activeLevel.TryRemoveAt(selectedPlacement.gridPosition);
            if (!activeLevel.PlaceBlock(updated, rotated, activeCatalog))
            {
                activeLevel.PlaceBlock(selectedPlacement, currentSize, activeCatalog);
                EndLevelEdit();
                statusMessage = "TableTop resize failed.";
                return;
            }

            selectedPlacement = updated;
            hasSelectedPlacement = true;
            statusMessage = "TableTop resized to " + clampedWidth.ToString() + "x1x" + clampedDepth.ToString() + ".";
            EndLevelEdit();
            hoverChanged = true;
            Repaint();
            SceneView.RepaintAll();
        }

        private void ApplyAngle(float yaw)
        {
            Vector3 euler = hasBrush ? brushRotationEuler : selectedPlacement.rotationEuler;
            euler.y = yaw;
            ApplyOrientation(euler, "Yaw set to " + yaw.ToString("0") + ".");
        }

        private void ApplyLengthAxis(int axis)
        {
            BlockDefinition definition;
            if (!TryGetActiveDefinition(out definition))
                return;

            Vector3Int sizeInCells = ResolveActiveSizeInCells(definition);
            Vector3 euler = blockFootprint.ResolveLengthAxisEuler(sizeInCells, axis);
            string axisName = "X";
            if (axis == 1)
                axisName = "Y";
            else if (axis == 2)
                axisName = "Z";

            ApplyOrientation(euler, "Axis set to " + axisName + ".");
        }

        private Vector3Int ResolveActiveSizeInCells(BlockDefinition definition)
        {
            if (!hasBrush && hasSelectedPlacement && activeLevel != null)
                return activeLevel.GetSizeInCells(selectedPlacement, activeCatalog);

            Vector3Int customSize = ResolveBrushCustomSize(definition);
            if (customSize.x > 0)
                return customSize;

            return definition.sizeInCells;
        }

        private void CycleLengthAxis()
        {
            BlockDefinition definition;
            if (!TryGetActiveDefinition(out definition))
                return;

            Vector3 current = hasBrush ? brushRotationEuler : selectedPlacement.rotationEuler;
            Vector3Int effective = blockFootprint.GetEffectiveSize(ResolveActiveSizeInCells(definition), current);
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
            if (hasBrush)
            {
                brushRotationEuler = euler;
                statusMessage = message;
                hoverChanged = true;
                Repaint();
                SceneView.RepaintAll();
                return;
            }

            if (!hasSelectedPlacement || activeLevel == null || activeCatalog == null)
                return;

            BlockDefinition definition;
            if (!activeCatalog.TryGetBlockDefinition(selectedPlacement.blockId, out definition))
                return;

            Vector3Int sizeInCells = activeLevel.GetSizeInCells(selectedPlacement, activeCatalog);
            Vector3Int size = blockFootprint.GetEffectiveSize(sizeInCells, euler);
            PlacementValidationStatus status = activeLevel.ValidatePlacementIgnoring(
                selectedPlacement.gridPosition,
                size,
                activeCatalog,
                selectedPlacement.gridPosition);
            if (status == PlacementValidationStatus.InvalidOutOfBounds)
            {
                statusMessage = "That orientation does not fit inside the grid.";
                return;
            }

            BlockPlacementData updated = selectedPlacement;
            updated.rotationEuler = euler;
            CommitLevelChange("Set Block Orientation", () => activeLevel.TrySetPlacement(updated));
            selectedPlacement = updated;
            if (isMovingExistingBlock)
            {
                moveRotationEuler = euler;
                moveOriginPlacement.rotationEuler = euler;
            }

            AssetDatabase.SaveAssets();
            statusMessage = message;
            hoverChanged = true;
            Repaint();
            SceneView.RepaintAll();
        }

        private bool TryGetActiveDefinition(out BlockDefinition definition)
        {
            definition = default;
            if (activeCatalog == null)
                return false;

            int blockId = brushBlockId;
            if (!hasBrush)
            {
                if (!hasSelectedPlacement)
                    return false;

                blockId = selectedPlacement.blockId;
            }

            return activeCatalog.TryGetBlockDefinition(blockId, out definition);
        }

        private void DrawLayerBar()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (activeLevel == null)
            {
                EditorGUILayout.LabelField("No level loaded.");
                EditorGUILayout.EndVertical();
                return;
            }

            int layerCount = activeLevel.GridDimensions.z;
            EnsureLayerLabels(layerCount);
            int maxLayer = layerCount - 1;
            int toolbarLayer = GUILayout.Toolbar(selectedZLayer, layerLabels, GUILayout.MinHeight(22f));
            if (toolbarLayer != selectedZLayer)
            {
                selectedZLayer = toolbarLayer;
                SyncPreview();
                SceneView.RepaintAll();
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(selectedZLayer <= 0);
            bool copyPrevious = GUILayout.Button("Copy Prev", GUILayout.MinWidth(60f));
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(selectedZLayer >= maxLayer);
            bool copyNext = GUILayout.Button("Copy Next", GUILayout.MinWidth(60f));
            EditorGUI.EndDisabledGroup();
            bool clearLayer = GUILayout.Button("Clear Layer", GUILayout.MinWidth(60f));
            EditorGUILayout.EndHorizontal();

            if (copyPrevious)
                CopyPreviousLayer();

            if (copyNext)
                CopyLayerToNext();

            if (clearLayer)
                ClearActiveLayer();

            EditorGUILayout.EndVertical();
        }
    }
}
