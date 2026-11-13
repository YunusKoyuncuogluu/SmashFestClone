using System;
using System.Collections.Generic;
using SmashFest.Data;
using UnityEditor;
using UnityEngine;

namespace SmashFest.LevelEditor
{
    [Serializable]
    internal class LevelEditorActions
    {
        [NonSerialized] private LevelBlueprintEditorWindow window;
        [NonSerialized] private LevelEditorState state;
        [NonSerialized] private LevelEditorMatrixView matrix;
        [NonSerialized] private LevelEditorPaletteView palette;
        private LevelDatabaseSO levelDatabase;
        private readonly JsonLevelDatabaseIO jsonIo = new JsonLevelDatabaseIO();
        private Vector3Int lastEditedCell;
        private bool undoGroupOpen;
        private string undoGroupName = "Edit Level";
        private Vector3Int moveGrabOffset;
        private Vector3Int moveTargetAnchor;
        private Vector3Int moveFootprintSize;
        private PlacementValidationStatus moveStatus;
        private int cachedLayerCount = -1;

        public void Bind(LevelBlueprintEditorWindow window, LevelEditorState state, LevelEditorMatrixView matrix, LevelEditorPaletteView palette)
        {
            this.window = window;
            this.state = state;
            this.matrix = matrix;
            this.palette = palette;
        }

        public void PaintCell(Vector3Int cell)
        {
            if (state.ActiveLevel == null || !state.hasBrush)
            {
                state.statusMessage = "Select a block from the palette.";
                return;
            }

            BlockDefinition definition;
            if (state.activeCatalog == null || !state.activeCatalog.TryGetBlockDefinition(state.brushBlockId, out definition))
            {
                state.statusMessage = "The brush block is missing from the catalog.";
                return;
            }

            if (state.hasLastEditedCell && lastEditedCell == cell)
                return;

            Vector3Int customSize = palette.ResolveBrushCustomSize(definition);
            Vector3Int sizeInCells = customSize.x > 0 ? customSize : definition.sizeInCells;
            Vector3Int effectiveSize = state.blockFootprint.GetEffectiveSize(sizeInCells, state.brushRotationEuler);
            PlacementValidationStatus status = state.ActiveLevel.ValidatePlacement(cell, effectiveSize, state.activeCatalog);
            if (status == PlacementValidationStatus.InvalidOutOfBounds)
            {
                lastEditedCell = cell;
                state.hasLastEditedCell = true;
                state.statusMessage = "OUT OF BOUNDS. " + palette.GetBlockLabel(definition) + " does not fit at " + cell.x + ", " + cell.y + ", " + cell.z + ".";
                window.Repaint();
                SceneView.RepaintAll();
                return;
            }

            BlockPlacementData existing;
            if (state.ActiveLevel.TryGetPlacement(cell, out existing)
                && existing.blockId == state.brushBlockId
                && existing.rotationEuler == state.brushRotationEuler
                && existing.customSize == customSize)
            {
                lastEditedCell = cell;
                state.hasLastEditedCell = true;
                return;
            }

            BeginLevelEdit("Paint Block");
            BlockPlacementData placement = new BlockPlacementData();
            placement.blockId = state.brushBlockId;
            placement.gridPosition = cell;
            placement.rotationEuler = state.brushRotationEuler;
            placement.customSize = customSize;
            bool placed = state.ActiveLevel.PlaceBlock(placement, effectiveSize, state.activeCatalog);
            if (!placed)
            {
                EndLevelEdit();
                state.statusMessage = "OUT OF BOUNDS. The footprint does not fit.";
                return;
            }

            lastEditedCell = cell;
            state.hasLastEditedCell = true;
            state.statusMessage = "Placed " + palette.GetBlockLabel(definition) + " " + matrix.FormatFootprint(effectiveSize) + " at " + cell.x + ", " + cell.y + ", " + cell.z + ".";
            window.Repaint();
            SceneView.RepaintAll();
        }

        public void EraseCell(Vector3Int cell)
        {
            if (state.ActiveLevel == null)
                return;

            BlockPlacementData placement;
            bool isAnchorCell;
            if (!state.ActiveLevel.TryGetBlockAt(cell, state.activeCatalog, out placement, out isAnchorCell))
                return;

            if (state.hasLastEditedCell && lastEditedCell == placement.gridPosition)
                return;

            BeginLevelEdit("Erase Block");
            state.ActiveLevel.TryRemoveAt(placement.gridPosition);
            lastEditedCell = placement.gridPosition;
            state.hasLastEditedCell = true;
            if (isAnchorCell)
                state.statusMessage = "Erased the anchor at " + placement.gridPosition.x + ", " + placement.gridPosition.y + ", " + placement.gridPosition.z + ".";
            else
                state.statusMessage = "Erased the block covering " + cell.x + ", " + cell.y + ", " + cell.z + ".";
            window.Repaint();
            SceneView.RepaintAll();
        }

        public void CopyPreviousLayer()
        {
            if (state.ActiveLevel == null || state.selectedZLayer <= 0)
                return;

            int copied = 0;
            int sourceLayer = state.selectedZLayer - 1;
            CommitLevelChange("Copy Previous Layer", () =>
            {
                copied = state.layerTools.Copy(state.ActiveLevel, sourceLayer, state.selectedZLayer, state.activeCatalog);
            });
            state.statusMessage = "Copied " + copied.ToString() + " blocks from layer " + sourceLayer.ToString() + ".";
        }

        public void CopyLayerToNext()
        {
            if (state.ActiveLevel == null)
                return;

            int maxLayer = state.ActiveLevel.GridDimensions.z - 1;
            if (state.selectedZLayer >= maxLayer)
                return;

            int copied = 0;
            int destinationLayer = state.selectedZLayer + 1;
            CommitLevelChange("Copy Layer To Next", () =>
            {
                copied = state.layerTools.Copy(state.ActiveLevel, state.selectedZLayer, destinationLayer, state.activeCatalog);
            });
            state.statusMessage = "Copied " + copied.ToString() + " blocks to layer " + destinationLayer.ToString() + ".";
        }

        public void ClearActiveLayer()
        {
            if (state.ActiveLevel == null)
                return;

            if (state.layerTools.Count(state.ActiveLevel, state.selectedZLayer, state.activeCatalog) == 0)
            {
                state.statusMessage = "Layer " + state.selectedZLayer.ToString() + " is already empty.";
                return;
            }

            int removed = 0;
            CommitLevelChange("Clear Layer", () =>
            {
                removed = state.layerTools.Clear(state.ActiveLevel, state.selectedZLayer, state.activeCatalog);
            });
            state.statusMessage = "Cleared " + removed.ToString() + " blocks from layer " + state.selectedZLayer.ToString() + ".";
        }

        public void CommitLevelChange(string undoName, Action mutation)
        {
            if (state.ActiveLevel == null || mutation == null)
                return;

            Undo.RecordObject(window, undoName);
            mutation();
            state.jsonDirty = true;
            EditorUtility.SetDirty(window);
            ClampActiveLayer();
            SyncPreview();
            window.Repaint();
            SceneView.RepaintAll();
        }

        public void BeginLevelEdit(string undoName)
        {
            if (undoGroupOpen)
                return;

            undoGroupName = undoName;
            Undo.RecordObject(window, undoName);
            undoGroupOpen = true;
        }

        public void EndLevelEdit()
        {
            if (!undoGroupOpen)
                return;

            undoGroupOpen = false;
            state.jsonDirty = true;
            EditorUtility.SetDirty(window);
        }

        public void PersistJson(bool forcePretty)
        {
            EnsureRepository();
            if (state.ActiveLevel != null)
                state.levelRepository.CaptureLevel(state.ActiveLevel);

            jsonIo.Save(state.levelRepository, forcePretty);
            state.jsonDirty = false;
            if (levelDatabase != null)
                levelDatabase.ReloadFromText(state.levelRepository.ToJson(false));
        }

        public void EnsureRepository()
        {
            if (state.levelRepository != null)
                return;

            levelDatabase = AssetDatabase.LoadAssetAtPath<LevelDatabaseSO>(LevelAssetPaths.LevelDatabasePath);
            state.levelRepository = jsonIo.LoadOrCreateRepository();
            if (levelDatabase != null)
                levelDatabase.ReloadFromText(state.levelRepository.ToJson(false));
        }

        public void NavigateToLevel(int levelIndex)
        {
            EnsureRepository();
            int clamped = levelIndex;
            if (clamped < 1)
                clamped = 1;

            if (clamped > JsonLevelRepository.MaxLevelCount)
                clamped = JsonLevelRepository.MaxLevelCount;

            if (state.ActiveLevel != null && state.jsonDirty)
                state.levelRepository.CaptureLevel(state.ActiveLevel);

            state.editingLevelIndex = clamped;
            state.jumpLevelField = clamped;
            state.ActiveLevel = state.levelRepository.GetOrCreateLevelData(clamped);
            state.hasLastEditedCell = false;
            state.hasSelectedPlacement = false;
            CancelMove();
            ClampActiveLayer();
            window.SaveAssetPrefs();
            SyncPreview();
            window.Repaint();
            SceneView.RepaintAll();
        }

        public bool TryBeginMove(Vector3Int cell)
        {
            if (state.ActiveLevel == null || state.activeCatalog == null)
                return false;

            BlockPlacementData placement;
            bool isAnchorCell;
            if (!state.ActiveLevel.TryGetBlockAt(cell, state.activeCatalog, out placement, out isAnchorCell))
                return false;

            if (isAnchorCell && placement.gridPosition != cell)
                return false;

            state.hasSelectedPlacement = true;
            state.selectedPlacement = placement;
            state.isMovingExistingBlock = true;
            state.moveOriginPlacement = placement;
            state.moveRotationEuler = placement.rotationEuler;
            moveGrabOffset = cell - placement.gridPosition;
            RefreshMovePreview(true, cell);
            state.statusMessage = "Moving block. Release to drop. R rotates. Drop outside the grid to cancel.";
            window.Repaint();
            SceneView.RepaintAll();
            return true;
        }

        public void RefreshMovePreview(bool previewVisible, Vector3Int hovered)
        {
            state.hasMovePreview = false;
            state.hasFootprintPreview = false;
            if (!state.isMovingExistingBlock || !previewVisible || state.ActiveLevel == null || state.activeCatalog == null)
                return;

            BlockDefinition definition;
            if (!state.activeCatalog.TryGetBlockDefinition(state.moveOriginPlacement.blockId, out definition))
                return;

            moveTargetAnchor = hovered - moveGrabOffset;
            Vector3Int sizeInCells = state.ActiveLevel.GetSizeInCells(state.moveOriginPlacement, state.activeCatalog);
            moveFootprintSize = state.blockFootprint.GetEffectiveSize(sizeInCells, state.moveRotationEuler);
            moveStatus = state.ActiveLevel.ValidatePlacementIgnoring(
                moveTargetAnchor,
                moveFootprintSize,
                state.activeCatalog,
                state.moveOriginPlacement.gridPosition);
            state.footprintAnchor = moveTargetAnchor;
            state.footprintSize = moveFootprintSize;
            state.footprintStatus = moveStatus;
            state.hasFootprintPreview = true;
            state.hasMovePreview = true;
        }

        public void StepMoveYaw(int direction)
        {
            float snapped = Mathf.Round(state.moveRotationEuler.y / 90f) * 90f;
            state.moveRotationEuler.y = Mathf.Repeat(snapped + (90f * direction), 360f);
            if (state.hasHoveredCell)
                RefreshMovePreview(true, state.hoveredCell);

            state.statusMessage = "Move yaw " + state.moveRotationEuler.y.ToString("0") + ".";
            window.Repaint();
            SceneView.RepaintAll();
        }

        public void FinishMove()
        {
            if (!state.isMovingExistingBlock)
                return;

            bool changed = moveTargetAnchor != state.moveOriginPlacement.gridPosition || state.moveRotationEuler != state.moveOriginPlacement.rotationEuler;
            bool canCommit = state.hasHoveredCell && state.hasMovePreview && moveStatus != PlacementValidationStatus.InvalidOutOfBounds && changed;
            if (!canCommit)
            {
                state.statusMessage = "Move cancelled. The block stayed in place.";
                CancelMove();
                return;
            }

            BlockPlacementData origin = state.moveOriginPlacement;
            Vector3Int targetAnchor = moveTargetAnchor;
            Vector3 rotation = state.moveRotationEuler;
            CommitLevelChange("Move Block", () =>
            {
                BlockPlacementData placed = origin;
                placed.gridPosition = targetAnchor;
                placed.rotationEuler = rotation;
                Vector3Int effectiveSize = state.ActiveLevel.GetEffectiveSize(placed, state.activeCatalog);
                state.ActiveLevel.TryRemoveAt(origin.gridPosition);
                bool placedOk = state.ActiveLevel.PlaceBlock(placed, effectiveSize, state.activeCatalog);
                if (!placedOk)
                    state.ActiveLevel.TryAddPlacement(origin);
            });
            state.statusMessage = "Moved block to " + targetAnchor.x + ", " + targetAnchor.y + ", " + targetAnchor.z + ".";
            CancelMove();
        }

        public void CancelMove()
        {
            if (!state.isMovingExistingBlock && !state.hasMovePreview)
                return;

            state.isMovingExistingBlock = false;
            state.hasMovePreview = false;
            state.hasFootprintPreview = false;
            moveGrabOffset = Vector3Int.zero;
            moveTargetAnchor = Vector3Int.zero;
            moveFootprintSize = Vector3Int.one;
            state.moveRotationEuler = Vector3.zero;
            state.moveOriginPlacement = default;
            state.hoverChanged = true;
            window.Repaint();
            SceneView.RepaintAll();
        }

        public bool IsMoveOriginCell(Vector3Int cell)
        {
            if (!state.isMovingExistingBlock || state.ActiveLevel == null)
                return false;

            Vector3Int originSize = state.ActiveLevel.GetEffectiveSize(state.moveOriginPlacement, state.activeCatalog);
            return state.blockFootprint.ContainsCell(state.moveOriginPlacement.gridPosition, originSize, cell);
        }

        public void StepBrushYaw(int direction)
        {
            if (state.isMovingExistingBlock)
            {
                StepMoveYaw(direction);
                return;
            }

            float snapped = Mathf.Round(state.brushRotationEuler.y / 90f) * 90f;
            state.brushRotationEuler.y = Mathf.Repeat(snapped + (90f * direction), 360f);
            state.statusMessage = "Brush yaw " + state.brushRotationEuler.y.ToString("0") + ".";
            window.Repaint();
            SceneView.RepaintAll();
        }

        public void FrameLevel()
        {
            if (state.ActiveLevel == null)
                return;

            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                state.statusMessage = "Open a Scene view to frame the blueprint.";
                return;
            }

            sceneView.Frame(new LevelLayout(state.ActiveLevel, state.activeCatalog).GetWorldBounds(), false);
            sceneView.Repaint();
        }

        public void EnsureLayerLabels(int layerCount)
        {
            if (state.layerLabels == null || state.layerLabels.Length != layerCount)
                state.layerLabels = new string[layerCount];

            for (int i = 0; i < layerCount; i++)
            {
                int blockCount = state.ActiveLevel != null ? state.layerTools.Count(state.ActiveLevel, i, state.activeCatalog) : 0;
                state.layerLabels[i] = (window.position.width < LevelEditorState.NarrowPaletteWidth ? "L" : "Layer ") + i.ToString() + " (" + blockCount.ToString() + ")";
            }

            cachedLayerCount = layerCount;
        }

        public string FormatMatrixBlockLabel(string blockName, Vector3Int size, bool multiLine)
        {
            string shortName = GetShortBlockName(blockName);
            string footprint = matrix.FormatFootprint(size);
            if (multiLine)
                return shortName + "\n" + footprint;

            return shortName + " " + footprint;
        }

        public string GetShortBlockName(string blockName)
        {
            if (string.IsNullOrEmpty(blockName))
                return "Block";

            string trimmed = blockName;
            int underscore = trimmed.LastIndexOf('_');
            if (underscore > 0 && underscore < trimmed.Length - 1)
            {
                string suffix = trimmed.Substring(underscore + 1);
                if (suffix.Length >= 2
                    && suffix.Length <= 3
                    && char.IsDigit(suffix[0])
                    && (suffix[suffix.Length - 1] == 'x'
                        || suffix[suffix.Length - 1] == 'y'
                        || suffix[suffix.Length - 1] == 'z'
                        || suffix[suffix.Length - 1] == 'X'
                        || suffix[suffix.Length - 1] == 'Y'
                        || suffix[suffix.Length - 1] == 'Z'))
                {
                    trimmed = trimmed.Substring(0, underscore);
                }
            }

            if (trimmed.EndsWith("Cylinder", StringComparison.Ordinal))
                trimmed = trimmed.Substring(0, trimmed.Length - "Cylinder".Length);
            else if (trimmed.EndsWith("Block", StringComparison.Ordinal))
                trimmed = trimmed.Substring(0, trimmed.Length - "Block".Length);

            if (trimmed.Length > 10)
                trimmed = trimmed.Substring(0, 8);

            if (string.IsNullOrEmpty(trimmed))
                return "Block";

            return trimmed;
        }

        public void ClampActiveLayer()
        {
            if (state.ActiveLevel == null)
            {
                state.selectedZLayer = 0;
                return;
            }

            int maxLayer = state.ActiveLevel.GridDimensions.z - 1;
            if (maxLayer < 0)
                maxLayer = 0;

            if (state.selectedZLayer < 0)
                state.selectedZLayer = 0;
            else if (state.selectedZLayer > maxLayer)
                state.selectedZLayer = maxLayer;
        }

        public bool CatalogContainsBrush()
        {
            if (state.activeCatalog == null)
                return false;

            BlockDefinition definition;
            if (!state.activeCatalog.TryGetBlockDefinition(state.brushBlockId, out definition))
                return false;

            return definition.blockId == state.brushBlockId;
        }

        public void SyncPreview()
        {
            if (state.scenePreview == null)
                return;

            int ghostBlockId = 0;
            bool showGhost = !state.isMovingExistingBlock && state.hasHoveredCell && matrix.TryResolveFootprintBlock(out ghostBlockId);
            BlueprintMoveGhost moveGhost = new BlueprintMoveGhost();
            moveGhost.showOrigin = state.isMovingExistingBlock;
            moveGhost.origin = state.moveOriginPlacement;
            moveGhost.showTarget = state.isMovingExistingBlock && state.hasMovePreview;
            moveGhost.status = moveStatus;
            moveGhost.target.blockId = state.moveOriginPlacement.blockId;
            moveGhost.target.gridPosition = moveTargetAnchor;
            moveGhost.target.rotationEuler = state.moveRotationEuler;
            moveGhost.target.customSize = state.moveOriginPlacement.customSize;
            Vector3Int ghostCustomSize = Vector3Int.zero;
            if (showGhost && state.activeCatalog != null)
            {
                BlockDefinition ghostDefinition;
                if (state.activeCatalog.TryGetBlockDefinition(ghostBlockId, out ghostDefinition))
                    ghostCustomSize = palette.ResolveBrushCustomSize(ghostDefinition);
            }

            state.scenePreview.Bind(
                state.ActiveLevel,
                state.activeCatalog,
                state.selectedZLayer,
                state.hoveredCell,
                showGhost,
                ghostBlockId,
                state.brushRotationEuler,
                ghostCustomSize,
                state.hasExistingHover,
                state.existingHoverPlacement,
                state.overwritePlacements,
                state.overwriteCount,
                moveGhost);
        }
    }
}
