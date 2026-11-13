using System;
using System.Collections.Generic;
using SmashFest.Data;
using UnityEditor;
using UnityEngine;

namespace SmashFest.LevelEditor
{
    public partial class LevelBlueprintEditorWindow
    {
        private void PaintCell(Vector3Int cell)
        {
            if (activeLevel == null || !hasBrush)
            {
                statusMessage = "Select a block from the palette.";
                return;
            }

            BlockDefinition definition;
            if (activeCatalog == null || !activeCatalog.TryGetBlockDefinition(brushBlockId, out definition))
            {
                statusMessage = "The brush block is missing from the catalog.";
                return;
            }

            if (hasLastEditedCell && lastEditedCell == cell)
                return;

            Vector3Int customSize = ResolveBrushCustomSize(definition);
            Vector3Int sizeInCells = customSize.x > 0 ? customSize : definition.sizeInCells;
            Vector3Int effectiveSize = blockFootprint.GetEffectiveSize(sizeInCells, brushRotationEuler);
            PlacementValidationStatus status = activeLevel.ValidatePlacement(cell, effectiveSize, activeCatalog);
            if (status == PlacementValidationStatus.InvalidOutOfBounds)
            {
                lastEditedCell = cell;
                hasLastEditedCell = true;
                statusMessage = "OUT OF BOUNDS. " + GetBlockLabel(definition) + " does not fit at " + cell.x + ", " + cell.y + ", " + cell.z + ".";
                Repaint();
                SceneView.RepaintAll();
                return;
            }

            BlockPlacementData existing;
            if (activeLevel.TryGetPlacement(cell, out existing)
                && existing.blockId == brushBlockId
                && existing.rotationEuler == brushRotationEuler
                && existing.customSize == customSize)
            {
                lastEditedCell = cell;
                hasLastEditedCell = true;
                return;
            }

            BeginLevelEdit("Paint Block");
            BlockPlacementData placement = new BlockPlacementData();
            placement.blockId = brushBlockId;
            placement.gridPosition = cell;
            placement.rotationEuler = brushRotationEuler;
            placement.customSize = customSize;
            bool placed = activeLevel.PlaceBlock(placement, effectiveSize, activeCatalog);
            if (!placed)
            {
                EndLevelEdit();
                statusMessage = "OUT OF BOUNDS. The footprint does not fit.";
                return;
            }

            lastEditedCell = cell;
            hasLastEditedCell = true;
            statusMessage = "Placed " + GetBlockLabel(definition) + " " + FormatFootprint(effectiveSize) + " at " + cell.x + ", " + cell.y + ", " + cell.z + ".";
            Repaint();
            SceneView.RepaintAll();
        }

        private void EraseCell(Vector3Int cell)
        {
            if (activeLevel == null)
                return;

            BlockPlacementData placement;
            bool isAnchorCell;
            if (!activeLevel.TryGetBlockAt(cell, activeCatalog, out placement, out isAnchorCell))
                return;

            if (hasLastEditedCell && lastEditedCell == placement.gridPosition)
                return;

            BeginLevelEdit("Erase Block");
            activeLevel.TryRemoveAt(placement.gridPosition);
            lastEditedCell = placement.gridPosition;
            hasLastEditedCell = true;
            if (isAnchorCell)
                statusMessage = "Erased the anchor at " + placement.gridPosition.x + ", " + placement.gridPosition.y + ", " + placement.gridPosition.z + ".";
            else
                statusMessage = "Erased the block covering " + cell.x + ", " + cell.y + ", " + cell.z + ".";
            Repaint();
            SceneView.RepaintAll();
        }

        private void CopyPreviousLayer()
        {
            if (activeLevel == null || selectedZLayer <= 0)
                return;

            int copied = 0;
            int sourceLayer = selectedZLayer - 1;
            CommitLevelChange("Copy Previous Layer", () =>
            {
                copied = layerTools.Copy(activeLevel, sourceLayer, selectedZLayer, activeCatalog);
            });
            statusMessage = "Copied " + copied.ToString() + " blocks from layer " + sourceLayer.ToString() + ".";
        }

        private void CopyLayerToNext()
        {
            if (activeLevel == null)
                return;

            int maxLayer = activeLevel.GridDimensions.z - 1;
            if (selectedZLayer >= maxLayer)
                return;

            int copied = 0;
            int destinationLayer = selectedZLayer + 1;
            CommitLevelChange("Copy Layer To Next", () =>
            {
                copied = layerTools.Copy(activeLevel, selectedZLayer, destinationLayer, activeCatalog);
            });
            statusMessage = "Copied " + copied.ToString() + " blocks to layer " + destinationLayer.ToString() + ".";
        }

        private void ClearActiveLayer()
        {
            if (activeLevel == null)
                return;

            if (layerTools.Count(activeLevel, selectedZLayer, activeCatalog) == 0)
            {
                statusMessage = "Layer " + selectedZLayer.ToString() + " is already empty.";
                return;
            }

            int removed = 0;
            CommitLevelChange("Clear Layer", () =>
            {
                removed = layerTools.Clear(activeLevel, selectedZLayer, activeCatalog);
            });
            statusMessage = "Cleared " + removed.ToString() + " blocks from layer " + selectedZLayer.ToString() + ".";
        }

        private void CommitLevelChange(string undoName, Action mutation)
        {
            if (activeLevel == null || mutation == null)
                return;

            Undo.RecordObject(this, undoName);
            mutation();
            jsonDirty = true;
            EditorUtility.SetDirty(this);
            ClampActiveLayer();
            SyncPreview();
            Repaint();
            SceneView.RepaintAll();
        }

        private void BeginLevelEdit(string undoName)
        {
            if (undoGroupOpen)
                return;

            undoGroupName = undoName;
            Undo.RecordObject(this, undoName);
            undoGroupOpen = true;
        }

        private void EndLevelEdit()
        {
            if (!undoGroupOpen)
                return;

            undoGroupOpen = false;
            jsonDirty = true;
            EditorUtility.SetDirty(this);
        }

        private void PersistJson(bool forcePretty)
        {
            EnsureRepository();
            if (activeLevel != null)
                levelRepository.CaptureLevel(activeLevel);

            jsonIo.Save(levelRepository, forcePretty);
            jsonDirty = false;
            if (levelDatabase != null)
                levelDatabase.ReloadFromText(levelRepository.ToJson(false));
        }

        private void EnsureRepository()
        {
            if (levelRepository != null)
                return;

            levelDatabase = AssetDatabase.LoadAssetAtPath<LevelDatabaseSO>(LevelAssetPaths.LevelDatabasePath);
            levelRepository = jsonIo.LoadOrCreateRepository();
            if (levelDatabase != null)
                levelDatabase.ReloadFromText(levelRepository.ToJson(false));
        }

        private void NavigateToLevel(int levelIndex)
        {
            EnsureRepository();
            int clamped = levelIndex;
            if (clamped < 1)
                clamped = 1;

            if (clamped > JsonLevelRepository.MaxLevelCount)
                clamped = JsonLevelRepository.MaxLevelCount;

            if (activeLevel != null && jsonDirty)
                levelRepository.CaptureLevel(activeLevel);

            editingLevelIndex = clamped;
            jumpLevelField = clamped;
            activeLevel = levelRepository.GetOrCreateLevelData(clamped);
            hasLastEditedCell = false;
            hasSelectedPlacement = false;
            CancelMove();
            ClampActiveLayer();
            SaveAssetPrefs();
            SyncPreview();
            Repaint();
            SceneView.RepaintAll();
        }

        private bool TryBeginMove(Vector3Int cell)
        {
            if (activeLevel == null || activeCatalog == null)
                return false;

            BlockPlacementData placement;
            bool isAnchorCell;
            if (!activeLevel.TryGetBlockAt(cell, activeCatalog, out placement, out isAnchorCell))
                return false;

            if (isAnchorCell && placement.gridPosition != cell)
                return false;

            hasSelectedPlacement = true;
            selectedPlacement = placement;
            isMovingExistingBlock = true;
            moveOriginPlacement = placement;
            moveRotationEuler = placement.rotationEuler;
            moveGrabOffset = cell - placement.gridPosition;
            RefreshMovePreview(true, cell);
            statusMessage = "Moving block. Release to drop. R rotates. Drop outside the grid to cancel.";
            Repaint();
            SceneView.RepaintAll();
            return true;
        }

        private void RefreshMovePreview(bool previewVisible, Vector3Int hovered)
        {
            hasMovePreview = false;
            hasFootprintPreview = false;
            if (!isMovingExistingBlock || !previewVisible || activeLevel == null || activeCatalog == null)
                return;

            BlockDefinition definition;
            if (!activeCatalog.TryGetBlockDefinition(moveOriginPlacement.blockId, out definition))
                return;

            moveTargetAnchor = hovered - moveGrabOffset;
            Vector3Int sizeInCells = activeLevel.GetSizeInCells(moveOriginPlacement, activeCatalog);
            moveFootprintSize = blockFootprint.GetEffectiveSize(sizeInCells, moveRotationEuler);
            moveStatus = activeLevel.ValidatePlacementIgnoring(
                moveTargetAnchor,
                moveFootprintSize,
                activeCatalog,
                moveOriginPlacement.gridPosition);
            footprintAnchor = moveTargetAnchor;
            footprintSize = moveFootprintSize;
            footprintStatus = moveStatus;
            hasFootprintPreview = true;
            hasMovePreview = true;
        }

        private void StepMoveYaw(int direction)
        {
            float snapped = Mathf.Round(moveRotationEuler.y / 90f) * 90f;
            moveRotationEuler.y = Mathf.Repeat(snapped + (90f * direction), 360f);
            if (hasHoveredCell)
                RefreshMovePreview(true, hoveredCell);

            statusMessage = "Move yaw " + moveRotationEuler.y.ToString("0") + ".";
            Repaint();
            SceneView.RepaintAll();
        }

        private void FinishMove()
        {
            if (!isMovingExistingBlock)
                return;

            bool changed = moveTargetAnchor != moveOriginPlacement.gridPosition || moveRotationEuler != moveOriginPlacement.rotationEuler;
            bool canCommit = hasHoveredCell && hasMovePreview && moveStatus != PlacementValidationStatus.InvalidOutOfBounds && changed;
            if (!canCommit)
            {
                statusMessage = "Move cancelled. The block stayed in place.";
                CancelMove();
                return;
            }

            BlockPlacementData origin = moveOriginPlacement;
            Vector3Int targetAnchor = moveTargetAnchor;
            Vector3 rotation = moveRotationEuler;
            CommitLevelChange("Move Block", () =>
            {
                BlockPlacementData placed = origin;
                placed.gridPosition = targetAnchor;
                placed.rotationEuler = rotation;
                Vector3Int effectiveSize = activeLevel.GetEffectiveSize(placed, activeCatalog);
                activeLevel.TryRemoveAt(origin.gridPosition);
                bool placedOk = activeLevel.PlaceBlock(placed, effectiveSize, activeCatalog);
                if (!placedOk)
                    activeLevel.TryAddPlacement(origin);
            });
            statusMessage = "Moved block to " + targetAnchor.x + ", " + targetAnchor.y + ", " + targetAnchor.z + ".";
            CancelMove();
        }

        private void CancelMove()
        {
            if (!isMovingExistingBlock && !hasMovePreview)
                return;

            isMovingExistingBlock = false;
            hasMovePreview = false;
            hasFootprintPreview = false;
            moveGrabOffset = Vector3Int.zero;
            moveTargetAnchor = Vector3Int.zero;
            moveFootprintSize = Vector3Int.one;
            moveRotationEuler = Vector3.zero;
            moveOriginPlacement = default;
            hoverChanged = true;
            Repaint();
            SceneView.RepaintAll();
        }

        private bool IsMoveOriginCell(Vector3Int cell)
        {
            if (!isMovingExistingBlock || activeLevel == null)
                return false;

            Vector3Int originSize = activeLevel.GetEffectiveSize(moveOriginPlacement, activeCatalog);
            return blockFootprint.ContainsCell(moveOriginPlacement.gridPosition, originSize, cell);
        }

        private void StepBrushYaw(int direction)
        {
            if (isMovingExistingBlock)
            {
                StepMoveYaw(direction);
                return;
            }

            float snapped = Mathf.Round(brushRotationEuler.y / 90f) * 90f;
            brushRotationEuler.y = Mathf.Repeat(snapped + (90f * direction), 360f);
            statusMessage = "Brush yaw " + brushRotationEuler.y.ToString("0") + ".";
            Repaint();
            SceneView.RepaintAll();
        }

        private void FrameLevel()
        {
            if (activeLevel == null)
                return;

            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                statusMessage = "Open a Scene view to frame the blueprint.";
                return;
            }

            sceneView.Frame(new LevelLayout(activeLevel, activeCatalog).GetWorldBounds(), false);
            sceneView.Repaint();
        }

        private void EnsureLayerLabels(int layerCount)
        {
            if (layerLabels == null || layerLabels.Length != layerCount)
                layerLabels = new string[layerCount];

            for (int i = 0; i < layerCount; i++)
            {
                int blockCount = activeLevel != null ? layerTools.Count(activeLevel, i, activeCatalog) : 0;
                layerLabels[i] = (position.width < NarrowPaletteWidth ? "L" : "Layer ") + i.ToString() + " (" + blockCount.ToString() + ")";
            }

            cachedLayerCount = layerCount;
        }

        private string FormatMatrixBlockLabel(string blockName, Vector3Int size, bool multiLine)
        {
            string shortName = GetShortBlockName(blockName);
            string footprint = FormatFootprint(size);
            if (multiLine)
                return shortName + "\n" + footprint;

            return shortName + " " + footprint;
        }

        private string GetShortBlockName(string blockName)
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

        private void ClampActiveLayer()
        {
            if (activeLevel == null)
            {
                selectedZLayer = 0;
                return;
            }

            int maxLayer = activeLevel.GridDimensions.z - 1;
            if (maxLayer < 0)
                maxLayer = 0;

            if (selectedZLayer < 0)
                selectedZLayer = 0;
            else if (selectedZLayer > maxLayer)
                selectedZLayer = maxLayer;
        }

        private bool CatalogContainsBrush()
        {
            if (activeCatalog == null)
                return false;

            BlockDefinition definition;
            if (!activeCatalog.TryGetBlockDefinition(brushBlockId, out definition))
                return false;

            return definition.blockId == brushBlockId;
        }

        private void SyncPreview()
        {
            if (scenePreview == null)
                return;

            int ghostBlockId = 0;
            bool showGhost = !isMovingExistingBlock && hasHoveredCell && TryResolveFootprintBlock(out ghostBlockId);
            BlueprintMoveGhost moveGhost = new BlueprintMoveGhost();
            moveGhost.showOrigin = isMovingExistingBlock;
            moveGhost.origin = moveOriginPlacement;
            moveGhost.showTarget = isMovingExistingBlock && hasMovePreview;
            moveGhost.status = moveStatus;
            moveGhost.target.blockId = moveOriginPlacement.blockId;
            moveGhost.target.gridPosition = moveTargetAnchor;
            moveGhost.target.rotationEuler = moveRotationEuler;
            moveGhost.target.customSize = moveOriginPlacement.customSize;
            Vector3Int ghostCustomSize = Vector3Int.zero;
            if (showGhost && activeCatalog != null)
            {
                BlockDefinition ghostDefinition;
                if (activeCatalog.TryGetBlockDefinition(ghostBlockId, out ghostDefinition))
                    ghostCustomSize = ResolveBrushCustomSize(ghostDefinition);
            }

            scenePreview.Bind(
                activeLevel,
                activeCatalog,
                selectedZLayer,
                hoveredCell,
                showGhost,
                ghostBlockId,
                brushRotationEuler,
                ghostCustomSize,
                hasExistingHover,
                existingHoverPlacement,
                overwritePlacements,
                overwriteCount,
                moveGhost);
        }
    }
}
