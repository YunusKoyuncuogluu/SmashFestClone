using SmashFest.Data;
using UnityEngine;

namespace SmashFest.Level
{
    public partial class LevelArchitect
    {
        private bool HasValidSupport(LevelData levelData, BlockCatalogSO blockCatalog, Vector3Int targetCell, Vector3Int blockSize)
        {
            if (targetCell.y <= 0 || levelData == null || blockCatalog == null)
                return true;

            return ScanSupports(levelData, blockCatalog, targetCell, footprint.ClampSize(blockSize), false);
        }

        private void EnsureStructuralStability()
        {
            if (level == null || catalog == null)
                return;

            SettleUnsupported();
            BalanceTableLoads();
            SettleUnsupported();
        }

        private void SettleUnsupported()
        {
            int height = level.GridDimensions.y;
            for (int y = 1; y < height; y++)
            {
                CollectAnchors(y, false);
                int pending = anchorBuffer.Count;
                for (int i = 0; i < pending; i++)
                    ResolveUnsupported(anchorBuffer[i]);
            }
        }

        private void ResolveUnsupported(Vector3Int anchor)
        {
            BlockDefinition definition;
            BlockPlacementData placement;
            if (!TryGetAnchorDefinition(anchor, out placement, out definition))
                return;

            if (definition.IsTable())
                return;

            Vector3Int size = level.GetEffectiveSize(placement, catalog);
            if (HasValidSupport(level, catalog, placement.gridPosition, size))
                return;

            if (TryDropPlacement(placement, size))
                return;

            level.TryRemoveAt(placement.gridPosition);
        }

        private bool TryDropPlacement(BlockPlacementData placement, Vector3Int size)
        {
            if (placement.gridPosition.y <= 0)
                return false;

            Vector3Int clamped = footprint.ClampSize(size);
            if (ScanSupports(level, catalog, placement.gridPosition, clamped, true))
                return false;

            Vector3Int dropped = placement.gridPosition;
            dropped.y -= 1;
            if (!HasValidSupport(level, catalog, dropped, clamped))
                return false;

            PlacementValidationStatus status = level.ValidatePlacementIgnoring(dropped, clamped, catalog, placement.gridPosition);
            if (status != PlacementValidationStatus.ValidEmpty)
                return false;

            level.TryRemoveAt(placement.gridPosition);
            placement.gridPosition = dropped;
            level.PlaceBlock(placement, clamped, catalog);
            return true;
        }

        private void BalanceTableLoads()
        {
            CollectAnchors(0, true);
            int tables = anchorBuffer.Count;
            for (int i = 0; i < tables; i++)
                BalanceOneTable(anchorBuffer[i]);
        }

        private void BalanceOneTable(Vector3Int tableAnchor)
        {
            BlockDefinition definition;
            BlockPlacementData table;
            if (!TryGetAnchorDefinition(tableAnchor, out table, out definition))
                return;

            Vector3Int size = level.GetEffectiveSize(table, catalog);
            int top = table.gridPosition.y + size.y;
            int height = level.GridDimensions.y;
            for (int y = top; y < height; y++)
            {
                for (int z = 0; z < size.z; z++)
                {
                    int pairCount = size.x / 2;
                    for (int pair = 0; pair < pairCount; pair++)
                    {
                        int leftX = table.gridPosition.x + pair;
                        int rightX = table.gridPosition.x + size.x - 1 - pair;
                        BalancePair(leftX, rightX, y, table.gridPosition.z + z);
                    }
                }
            }
        }

        private void BalancePair(int leftX, int rightX, int y, int z)
        {
            if (rightX <= leftX)
                return;

            Vector3Int leftCell = new Vector3Int(leftX, y, z);
            Vector3Int rightCell = new Vector3Int(rightX, y, z);
            BlockPlacementData leftPlacement;
            BlockPlacementData rightPlacement;
            bool leftHeavy = TryGetStructuralUnit(leftCell, out leftPlacement);
            bool rightHeavy = TryGetStructuralUnit(rightCell, out rightPlacement);
            if (leftHeavy == rightHeavy)
                return;

            Vector3Int emptyCell = leftHeavy ? rightCell : leftCell;
            BlockPlacementData source = leftHeavy ? leftPlacement : rightPlacement;
            BlockPlacementData occupied;
            bool isAnchor;
            if (level.TryGetBlockAt(emptyCell, catalog, out occupied, out isAnchor))
                return;

            if (!HasValidSupport(level, catalog, emptyCell, Vector3Int.one))
            {
                level.TryRemoveAt(source.gridPosition);
                return;
            }

            PlacementValidationStatus status = level.ValidatePlacement(emptyCell, Vector3Int.one, catalog);
            if (status != PlacementValidationStatus.ValidEmpty)
            {
                level.TryRemoveAt(source.gridPosition);
                return;
            }

            BlockPlacementData copy = source;
            copy.gridPosition = emptyCell;
            level.PlaceBlock(copy, Vector3Int.one, catalog);
        }

        private void CollectAnchors(int rowY, bool foundationsOnly)
        {
            anchorBuffer.Clear();
            int count = level.PlacementCount;
            for (int i = 0; i < count; i++)
            {
                BlockPlacementData placement;
                if (!level.TryGetPlacementByIndex(i, out placement))
                    continue;

                if (placement.gridPosition.y != rowY)
                    continue;

                if (foundationsOnly)
                {
                    BlockDefinition definition;
                    if (!catalog.TryGetBlockDefinition(placement.blockId, out definition))
                        continue;

                    if (!definition.IsTable())
                        continue;
                }

                anchorBuffer.Add(placement.gridPosition);
            }
        }

        private bool TryGetAnchorDefinition(Vector3Int anchor, out BlockPlacementData placement, out BlockDefinition definition)
        {
            placement = default;
            definition = default;
            bool isAnchor;
            if (!level.TryGetBlockAt(anchor, catalog, out placement, out isAnchor))
                return false;

            if (!isAnchor)
                return false;

            return catalog.TryGetBlockDefinition(placement.blockId, out definition);
        }

        private bool TryGetStructuralUnit(Vector3Int cell, out BlockPlacementData placement)
        {
            BlockDefinition definition;
            if (!TryGetAnchorDefinition(cell, out placement, out definition))
                return false;

            if (!definition.canSupportBlocksAbove || definition.IsTable())
                return false;

            Vector3Int size = level.GetEffectiveSize(placement, catalog);
            if (size.x != 1 || size.y != 1 || size.z != 1)
                return false;

            return true;
        }

        private bool ScanSupports(LevelData levelData, BlockCatalogSO blockCatalog, Vector3Int targetCell, Vector3Int size, bool stopOnFragile)
        {
            bool rejectPlank = !stopOnFragile && size.x > 1 && size.y <= 1 && size.z <= 1;
            int belowY = targetCell.y - 1;
            int steps = size.x <= 1 ? size.x : 2;
            for (int z = 0; z < size.z; z++)
            {
                for (int step = 0; step < steps; step++)
                {
                    int x = targetCell.x + step;
                    if (size.x > 1 && step == 1)
                        x = targetCell.x + size.x - 1;

                    BlockDefinition definition;
                    bool found = TryGetCellDefinition(levelData, blockCatalog, x, belowY, targetCell.z + z, out definition);
                    if (stopOnFragile)
                    {
                        if (found && !definition.canSupportBlocksAbove)
                            return true;

                        continue;
                    }

                    if (!found || !CanCarry(definition, rejectPlank))
                        return false;
                }
            }

            return !stopOnFragile;
        }

        private bool CanCarry(BlockDefinition definition, bool rejectPlank)
        {
            if (!definition.canSupportBlocksAbove)
                return false;

            if (rejectPlank && definition.shapeRole == BlockShapeRole.HorizontalPlank)
                return false;

            if (definition.shapeRole == BlockShapeRole.FoundationTable)
                return true;

            if (definition.shapeRole == BlockShapeRole.StandardBlock)
                return true;

            if (definition.shapeRole == BlockShapeRole.VerticalPillar)
                return true;

            return definition.shapeRole == BlockShapeRole.HorizontalPlank;
        }

        private bool TryGetCellDefinition(LevelData levelData, BlockCatalogSO blockCatalog, int x, int y, int z, out BlockDefinition definition)
        {
            definition = default;
            BlockPlacementData placement;
            bool isAnchor;
            if (!levelData.TryGetBlockAt(new Vector3Int(x, y, z), blockCatalog, out placement, out isAnchor))
                return false;

            return blockCatalog.TryGetBlockDefinition(placement.blockId, out definition);
        }
    }
}
