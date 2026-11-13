using UnityEngine;

namespace SmashFest.Data
{
    public class LevelLayout
    {
        private readonly LevelData level;
        private readonly BlockCatalogSO catalog;
        private readonly BlockFootprint footprint = new BlockFootprint();

        public LevelLayout(LevelData level, BlockCatalogSO catalog)
        {
            this.level = level;
            this.catalog = catalog;
        }

        public Vector3 CellSize => level.CellSize * (catalog != null ? catalog.GetScale() : 1f);

        public float TableLift => catalog != null ? (catalog.GetTableHeightFactor(level.CellSize.y) - 1f) * CellSize.y : 0f;

        public Vector3 GetCenterShift()
        {
            level.GetOccupiedGridBounds(catalog, out Vector3 minGrid, out Vector3 maxGrid);
            float centerX = (minGrid.x + maxGrid.x) * 0.5f;
            float centerZ = (minGrid.z + maxGrid.z) * 0.5f;
            Vector3 cell = CellSize;
            return new Vector3(-(centerX + 0.5f) * cell.x, 0f, -(centerZ + 0.5f) * cell.z);
        }

        public Vector3 GetCellScale(Vector3 baseScale)
        {
            if (baseScale == Vector3.zero)
                baseScale = Vector3.one;

            return Vector3.Scale(baseScale, level.CellSize);
        }

        public Vector3 GetFootprintScale(Vector3 baseScale, Vector3Int sizeInCells)
        {
            return Vector3.Scale(GetCellScale(baseScale), footprint.ClampSize(sizeInCells));
        }

        public Matrix4x4 GetWorldMatrix(in BlockPlacementData placement, Vector3 baseScale, Vector3Int sizeInCells)
        {
            Vector3Int size = footprint.ClampSize(level.HasCustomSize(placement) ? placement.customSize : sizeInCells);
            Vector3Int effectiveSize = footprint.GetEffectiveSize(size, placement.rotationEuler);
            GetWorldPose(placement, effectiveSize, 0f, GetCenterShift(), out Vector3 position, out Quaternion rotation);
            float scale = catalog != null ? catalog.GetScale() : 1f;
            return Matrix4x4.TRS(position, rotation, GetFootprintScale(baseScale, effectiveSize) * scale);
        }

        public void GetWorldPose(in BlockPlacementData placement, Vector3Int effectiveSize, float extraYaw, Vector3 centerShift, out Vector3 position, out Quaternion rotation)
        {
            Vector3Int size = footprint.ClampSize(effectiveSize);
            Vector3 center = GetFootprintCenter(placement.gridPosition, size, IsFoundationTable(placement));

            if (TryGetSupportingTable(placement, size, out Vector3 tableCenter, out float tableYaw))
            {
                Quaternion orbit = Quaternion.Euler(0f, tableYaw + extraYaw, 0f);
                position = tableCenter + orbit * (center - tableCenter) + centerShift;
                rotation = orbit * Quaternion.Euler(placement.rotationEuler);
                return;
            }

            Vector3 euler = placement.rotationEuler;
            euler.y += extraYaw;
            position = center + centerShift;
            rotation = Quaternion.Euler(euler);
        }

        public bool IsFoundationTable(in BlockPlacementData placement)
        {
            if (catalog == null || placement.gridPosition.y != 0)
                return false;

            if (!catalog.TryGetBlockDefinition(placement.blockId, out BlockDefinition definition))
                return false;

            return definition.IsTable();
        }

        public Bounds GetWorldBounds()
        {
            Vector3 cell = CellSize;
            Vector3 centerShift = GetCenterShift();
            Vector3 size = Vector3.Scale(level.GridDimensions, cell) + Vector3.up * TableLift;
            Bounds bounds = new Bounds(level.OriginOffset + size * 0.5f + centerShift, size);
            float scale = catalog != null ? catalog.GetScale() : 1f;

            for (int i = 0; i < level.PlacementCount; i++)
            {
                level.TryGetPlacementByIndex(i, out BlockPlacementData placement);
                Vector3Int effectiveSize = level.GetEffectiveSize(placement, catalog);
                GetWorldPose(placement, effectiveSize, 0f, centerShift, out Vector3 position, out _);
                float reach = (GetFootprintScale(Vector3.one, effectiveSize) * scale * 0.5f).magnitude;
                bounds.Encapsulate(position + Vector3.one * reach);
                bounds.Encapsulate(position - Vector3.one * reach);
            }

            return bounds;
        }

        private Vector3 GetFootprintCenter(Vector3Int anchor, Vector3Int effectiveSize, bool isFoundationTable)
        {
            Vector3Int size = footprint.ClampSize(effectiveSize);
            Vector3 cell = CellSize;
            float lift = TableLift;
            float bottom = isFoundationTable ? 0f : anchor.y * cell.y + (anchor.y > 0 ? lift : 0f);
            float height = size.y * cell.y + (isFoundationTable ? lift : 0f);

            return level.OriginOffset + new Vector3(
                (anchor.x + (size.x - 1) * 0.5f + 0.5f) * cell.x,
                bottom + height * 0.5f,
                (anchor.z + (size.z - 1) * 0.5f + 0.5f) * cell.z);
        }

        private bool TryGetSupportingTable(in BlockPlacementData block, Vector3Int blockSize, out Vector3 tableCenter, out float tableYaw)
        {
            tableCenter = Vector3.zero;
            tableYaw = 0f;
            if (catalog == null || block.gridPosition.y <= 0)
                return false;

            int centerX = block.gridPosition.x + (blockSize.x - 1) / 2;
            int centerZ = block.gridPosition.z + (blockSize.z - 1) / 2;
            for (int i = 0; i < level.PlacementCount; i++)
            {
                level.TryGetPlacementByIndex(i, out BlockPlacementData candidate);
                if (!IsFoundationTable(candidate))
                    continue;

                Vector3Int tableSize = level.GetEffectiveSize(candidate, catalog);
                if (!footprint.ContainsXZ(candidate.gridPosition, tableSize, centerX, centerZ))
                    continue;

                tableCenter = GetFootprintCenter(candidate.gridPosition, tableSize, true);
                tableYaw = candidate.rotationEuler.y;
                return true;
            }

            return false;
        }
    }
}
