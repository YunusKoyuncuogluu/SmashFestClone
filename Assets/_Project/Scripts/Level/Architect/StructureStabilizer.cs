using System.Collections.Generic;
using SmashFest.Data;
using UnityEngine;

namespace SmashFest.Level
{
    public class StructureStabilizer
    {
        private readonly LevelBuildContext context;
        private readonly List<Vector3Int> anchorBuffer = new List<Vector3Int>(64);

        public StructureStabilizer(LevelBuildContext context) => this.context = context;

        private LevelData Level => context.Level;
        private BlockCatalogSO Catalog => context.Catalog;

        public bool HasValidSupport(Vector3Int targetCell, Vector3Int blockSize)
        {
            if (targetCell.y <= 0 || Level == null || Catalog == null)
                return true;

            return ScanSupports(targetCell, context.Footprint.ClampSize(blockSize), false);
        }

        public void Stabilize()
        {
            if (Level == null || Catalog == null)
                return;

            SettleUnsupported();
            BalanceTableLoads();
            SettleUnsupported();
        }

        private void SettleUnsupported()
        {
            int height = Level.GridDimensions.y;
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
            if (!TryGetAnchorDefinition(anchor, out BlockPlacementData placement, out BlockDefinition definition))
                return;

            if (definition.IsTable())
                return;

            Vector3Int size = Level.GetEffectiveSize(placement, Catalog);
            if (HasValidSupport(placement.gridPosition, size))
                return;

            if (TryDropPlacement(placement, size))
                return;

            Level.TryRemoveAt(placement.gridPosition);
        }

        private bool TryDropPlacement(BlockPlacementData placement, Vector3Int size)
        {
            if (placement.gridPosition.y <= 0)
                return false;

            Vector3Int clamped = context.Footprint.ClampSize(size);
            if (ScanSupports(placement.gridPosition, clamped, true))
                return false;

            Vector3Int dropped = placement.gridPosition;
            dropped.y -= 1;
            if (!HasValidSupport(dropped, clamped))
                return false;

            PlacementValidationStatus status = Level.ValidatePlacementIgnoring(dropped, clamped, Catalog, placement.gridPosition);
            if (status != PlacementValidationStatus.ValidEmpty)
                return false;

            Level.TryRemoveAt(placement.gridPosition);
            placement.gridPosition = dropped;
            Level.PlaceBlock(placement, clamped, Catalog);
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
            if (!TryGetAnchorDefinition(tableAnchor, out BlockPlacementData table, out _))
                return;

            Vector3Int size = Level.GetEffectiveSize(table, Catalog);
            int top = table.gridPosition.y + size.y;
            int height = Level.GridDimensions.y;
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
            bool leftHeavy = TryGetStructuralUnit(leftCell, out BlockPlacementData leftPlacement);
            bool rightHeavy = TryGetStructuralUnit(rightCell, out BlockPlacementData rightPlacement);
            if (leftHeavy == rightHeavy)
                return;

            Vector3Int emptyCell = leftHeavy ? rightCell : leftCell;
            BlockPlacementData source = leftHeavy ? leftPlacement : rightPlacement;
            if (Level.TryGetBlockAt(emptyCell, Catalog, out _, out _))
                return;

            if (!HasValidSupport(emptyCell, Vector3Int.one))
            {
                Level.TryRemoveAt(source.gridPosition);
                return;
            }

            PlacementValidationStatus status = Level.ValidatePlacement(emptyCell, Vector3Int.one, Catalog);
            if (status != PlacementValidationStatus.ValidEmpty)
            {
                Level.TryRemoveAt(source.gridPosition);
                return;
            }

            BlockPlacementData copy = source;
            copy.gridPosition = emptyCell;
            Level.PlaceBlock(copy, Vector3Int.one, Catalog);
        }

        private void CollectAnchors(int rowY, bool foundationsOnly)
        {
            anchorBuffer.Clear();
            int count = Level.PlacementCount;
            for (int i = 0; i < count; i++)
            {
                if (!Level.TryGetPlacementByIndex(i, out BlockPlacementData placement))
                    continue;

                if (placement.gridPosition.y != rowY)
                    continue;

                if (foundationsOnly)
                {
                    if (!Catalog.TryGetBlockDefinition(placement.blockId, out BlockDefinition definition))
                        continue;

                    if (!definition.IsTable())
                        continue;
                }

                anchorBuffer.Add(placement.gridPosition);
            }
        }

        private bool TryGetAnchorDefinition(Vector3Int anchor, out BlockPlacementData placement, out BlockDefinition definition)
        {
            definition = default;
            if (!Level.TryGetBlockAt(anchor, Catalog, out placement, out bool isAnchor))
                return false;

            if (!isAnchor)
                return false;

            return Catalog.TryGetBlockDefinition(placement.blockId, out definition);
        }

        private bool TryGetStructuralUnit(Vector3Int cell, out BlockPlacementData placement)
        {
            if (!TryGetAnchorDefinition(cell, out placement, out BlockDefinition definition))
                return false;

            if (!definition.canSupportBlocksAbove || definition.IsTable())
                return false;

            Vector3Int size = Level.GetEffectiveSize(placement, Catalog);
            return size.x == 1 && size.y == 1 && size.z == 1;
        }

        private bool ScanSupports(Vector3Int targetCell, Vector3Int size, bool stopOnFragile)
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

                    bool found = TryGetCellDefinition(x, belowY, targetCell.z + z, out BlockDefinition definition);
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

            return definition.shapeRole == BlockShapeRole.FoundationTable
                || definition.shapeRole == BlockShapeRole.StandardBlock
                || definition.shapeRole == BlockShapeRole.VerticalPillar
                || definition.shapeRole == BlockShapeRole.HorizontalPlank;
        }

        private bool TryGetCellDefinition(int x, int y, int z, out BlockDefinition definition)
        {
            definition = default;
            if (!Level.TryGetBlockAt(new Vector3Int(x, y, z), Catalog, out BlockPlacementData placement, out _))
                return false;

            return Catalog.TryGetBlockDefinition(placement.blockId, out definition);
        }
    }
}
