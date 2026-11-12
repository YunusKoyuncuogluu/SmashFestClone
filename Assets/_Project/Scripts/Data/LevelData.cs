using System;
using System.Collections.Generic;
using UnityEngine;

namespace SmashFast.Data
{
    [Serializable]
    public class LevelData
    {
        [SerializeField] private int levelIndex;
        [SerializeField] private int maxAmmo = 10;
        [SerializeField] private Vector3Int gridDimensions = new Vector3Int(16, 14, 6);
        [SerializeField] private Vector3 cellSize = Vector3.one;
        [SerializeField] private Vector3 originOffset = Vector3.zero;
        [SerializeField] private float fallEliminationY = -1.5f;
        [SerializeField] private int parAmmo;
        [SerializeField] private List<BlockPlacementData> placements = new List<BlockPlacementData>(64);

        [NonSerialized] private BlockFootprint footprint;

        private BlockFootprint Footprint => footprint ??= new BlockFootprint();
        private List<BlockPlacementData> Placements => placements ??= new List<BlockPlacementData>(64);

        public int LevelIndex
        {
            get => levelIndex;
            set => levelIndex = Mathf.Max(0, value);
        }

        public int MaxAmmo
        {
            get => maxAmmo;
            set => maxAmmo = Mathf.Max(0, value);
        }

        public int ParAmmo
        {
            get => Mathf.Max(0, parAmmo);
            set => parAmmo = Mathf.Max(0, value);
        }

        public Vector3Int GridDimensions
        {
            get => Vector3Int.Max(gridDimensions, Vector3Int.one);
            set => gridDimensions = Vector3Int.Max(value, Vector3Int.one);
        }

        public Vector3 CellSize
        {
            get => Vector3.Max(cellSize, Vector3.one * 0.01f);
            set => cellSize = Vector3.Max(value, Vector3.one * 0.01f);
        }

        public Vector3 OriginOffset
        {
            get => originOffset;
            set => originOffset = value;
        }

        public float FallEliminationY
        {
            get => fallEliminationY;
            set => fallEliminationY = value;
        }

        public int PlacementCount => Placements.Count;

        public bool IsInsideGrid(Vector3Int cell)
        {
            Vector3Int size = GridDimensions;
            return cell.x >= 0 && cell.y >= 0 && cell.z >= 0
                && cell.x < size.x && cell.y < size.y && cell.z < size.z;
        }

        public bool HasBlockAt(Vector3Int cell) => TryGetPlacement(cell, out _);

        public bool TryGetPlacement(Vector3Int cell, out BlockPlacementData placement)
        {
            foreach (BlockPlacementData candidate in Placements)
            {
                if (candidate.gridPosition == cell)
                {
                    placement = candidate;
                    return true;
                }
            }

            placement = default;
            return false;
        }

        public bool TryGetPlacementByIndex(int index, out BlockPlacementData placement)
        {
            bool isValid = index >= 0 && index < Placements.Count;
            placement = isValid ? Placements[index] : default;
            return isValid;
        }

        public bool TryAddPlacement(in BlockPlacementData placement)
        {
            if (!IsInsideGrid(placement.gridPosition) || HasBlockAt(placement.gridPosition))
                return false;

            Placements.Add(placement);
            return true;
        }

        public bool TrySetPlacement(in BlockPlacementData placement)
        {
            if (!IsInsideGrid(placement.gridPosition))
                return false;

            List<BlockPlacementData> list = Placements;
            bool replaced = false;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].gridPosition != placement.gridPosition)
                    continue;

                if (replaced)
                {
                    list.RemoveAt(i);
                    continue;
                }

                list[i] = placement;
                replaced = true;
            }

            if (!replaced)
                list.Add(placement);

            return true;
        }

        public bool TryRemoveAt(Vector3Int cell)
        {
            Vector3Int target = cell;
            return Placements.RemoveAll(placement => placement.gridPosition == target) > 0;
        }

        public void ClearPlacements() => Placements.Clear();

        public int ClearLayer(int layerZ, BlockCatalogSO catalog = null)
        {
            return Placements.RemoveAll(placement => IntersectsLayer(placement, layerZ, catalog));
        }

        public int CopyLayer(int sourceZ, int targetZ, BlockCatalogSO catalog = null)
        {
            Vector3Int size = GridDimensions;
            if (sourceZ == targetZ || sourceZ < 0 || targetZ < 0 || sourceZ >= size.z || targetZ >= size.z)
                return 0;

            List<BlockPlacementData> list = Placements;
            list.RemoveAll(placement => IntersectsLayer(placement, targetZ, catalog) && !IntersectsLayer(placement, sourceZ, catalog));

            int offset = targetZ - sourceZ;
            int count = list.Count;
            int copied = 0;
            for (int i = 0; i < count; i++)
            {
                BlockPlacementData copy = list[i];
                if (!IntersectsLayer(copy, sourceZ, catalog))
                    continue;

                copy.gridPosition.z += offset;
                list.Add(copy);
                copied++;
            }

            return copied;
        }

        public int CountLayer(int layerZ, BlockCatalogSO catalog = null)
        {
            int total = 0;
            foreach (BlockPlacementData placement in Placements)
            {
                if (IntersectsLayer(placement, layerZ, catalog))
                    total++;
            }

            return total;
        }

        public int RemoveOutOfBounds(BlockCatalogSO catalog = null)
        {
            return Placements.RemoveAll(placement => !IsFootprintInsideGrid(placement.gridPosition, GetEffectiveSize(placement, catalog)));
        }

        public Vector3Int GetSizeInCells(in BlockPlacementData placement, BlockCatalogSO catalog)
        {
            if (HasCustomSize(placement))
                return Footprint.ClampSize(placement.customSize);

            if (catalog != null && catalog.TryGetBlockDefinition(placement.blockId, out BlockDefinition definition))
                return Footprint.ClampSize(definition.sizeInCells);

            return Vector3Int.one;
        }

        public Vector3Int GetEffectiveSize(in BlockPlacementData placement, BlockCatalogSO catalog)
        {
            return Footprint.GetEffectiveSize(GetSizeInCells(placement, catalog), placement.rotationEuler);
        }

        public PlacementValidationStatus ValidatePlacement(Vector3Int anchor, Vector3Int effectiveSize, BlockCatalogSO catalog = null)
        {
            return ValidatePlacement(anchor, effectiveSize, catalog, false, Vector3Int.zero);
        }

        public PlacementValidationStatus ValidatePlacementIgnoring(Vector3Int anchor, Vector3Int effectiveSize, BlockCatalogSO catalog, Vector3Int ignoredAnchor)
        {
            return ValidatePlacement(anchor, effectiveSize, catalog, true, ignoredAnchor);
        }

        public bool TryGetBlockAt(Vector3Int cell, BlockCatalogSO catalog, out BlockPlacementData placement, out bool isAnchorCell)
        {
            foreach (BlockPlacementData candidate in Placements)
            {
                if (!Footprint.ContainsCell(candidate.gridPosition, GetEffectiveSize(candidate, catalog), cell))
                    continue;

                placement = candidate;
                isAnchorCell = candidate.gridPosition == cell;
                return true;
            }

            placement = default;
            isAnchorCell = false;
            return false;
        }

        public bool PlaceBlock(in BlockPlacementData placement, Vector3Int effectiveSize, BlockCatalogSO catalog = null)
        {
            Vector3Int size = Footprint.ClampSize(effectiveSize);
            PlacementValidationStatus status = ValidatePlacement(placement.gridPosition, size, catalog);
            if (status == PlacementValidationStatus.InvalidOutOfBounds)
                return false;

            if (status == PlacementValidationStatus.ValidOverwrite)
                RemoveOverlapping(placement.gridPosition, size, catalog);

            Placements.Add(placement);
            return true;
        }

        public void GetOccupiedGridBounds(BlockCatalogSO catalog, out Vector3 minGrid, out Vector3 maxGrid)
        {
            List<BlockPlacementData> list = Placements;
            if (list.Count == 0)
            {
                minGrid = Vector3.zero;
                maxGrid = GridDimensions - Vector3Int.one;
                return;
            }

            Vector3Int min = list[0].gridPosition;
            Vector3Int max = min + GetEffectiveSize(list[0], catalog) - Vector3Int.one;
            for (int i = 1; i < list.Count; i++)
            {
                Vector3Int start = list[i].gridPosition;
                Vector3Int end = start + GetEffectiveSize(list[i], catalog) - Vector3Int.one;
                min = Vector3Int.Min(min, start);
                max = Vector3Int.Max(max, end);
            }

            minGrid = min;
            maxGrid = max;
        }

        public Vector3 GetHorizontalCenterShift(BlockCatalogSO catalog)
        {
            GetOccupiedGridBounds(catalog, out Vector3 minGrid, out Vector3 maxGrid);
            float centerX = (minGrid.x + maxGrid.x) * 0.5f;
            float centerZ = (minGrid.z + maxGrid.z) * 0.5f;
            Vector3 cell = GetScaledCellSize(catalog);
            return new Vector3(-(centerX + 0.5f) * cell.x, 0f, -(centerZ + 0.5f) * cell.z);
        }

        public Vector3 GetScaledCellSize(BlockCatalogSO catalog)
        {
            return CellSize * (catalog != null ? catalog.GetScale() : 1f);
        }

        public float GetTableLift(BlockCatalogSO catalog)
        {
            if (catalog == null)
                return 0f;

            return (catalog.GetTableHeightFactor(CellSize.y) - 1f) * GetScaledCellSize(catalog).y;
        }

        public Vector3 GetFootprintWorldCenter(Vector3Int anchor, Vector3Int effectiveSize, BlockCatalogSO catalog, bool isFoundationTable)
        {
            Vector3Int size = Footprint.ClampSize(effectiveSize);
            Vector3 cell = GetScaledCellSize(catalog);
            float lift = GetTableLift(catalog);
            float bottom = isFoundationTable ? 0f : anchor.y * cell.y + (anchor.y > 0 ? lift : 0f);
            float height = size.y * cell.y + (isFoundationTable ? lift : 0f);

            return OriginOffset + new Vector3(
                (anchor.x + (size.x - 1) * 0.5f + 0.5f) * cell.x,
                bottom + height * 0.5f,
                (anchor.z + (size.z - 1) * 0.5f + 0.5f) * cell.z);
        }

        public Vector3 GetCellWorldScale(Vector3 baseScale)
        {
            if (baseScale == Vector3.zero)
                baseScale = Vector3.one;

            return Vector3.Scale(baseScale, CellSize);
        }

        public Vector3 GetFootprintWorldScale(Vector3 baseScale, Vector3Int sizeInCells)
        {
            return Vector3.Scale(GetCellWorldScale(baseScale), Footprint.ClampSize(sizeInCells));
        }

        public Vector3 GetFootprintWorldScale(Vector3 baseScale, Vector3Int sizeInCells, BlockCatalogSO catalog)
        {
            return GetFootprintWorldScale(baseScale, sizeInCells) * (catalog != null ? catalog.GetScale() : 1f);
        }

        public Matrix4x4 GetWorldMatrix(in BlockPlacementData placement, Vector3 baseScale, Vector3Int sizeInCells, BlockCatalogSO catalog)
        {
            Vector3Int size = Footprint.ClampSize(HasCustomSize(placement) ? placement.customSize : sizeInCells);
            Vector3Int effectiveSize = Footprint.GetEffectiveSize(size, placement.rotationEuler);
            GetWorldPose(placement, effectiveSize, catalog, 0f, out Vector3 position, out Quaternion rotation);
            return Matrix4x4.TRS(position, rotation, GetFootprintWorldScale(baseScale, effectiveSize, catalog));
        }

        public void GetWorldPose(in BlockPlacementData placement, Vector3Int effectiveSize, BlockCatalogSO catalog, float extraYaw, out Vector3 position, out Quaternion rotation)
        {
            GetWorldPose(placement, effectiveSize, catalog, extraYaw, GetHorizontalCenterShift(catalog), out position, out rotation);
        }

        public void GetWorldPose(
            in BlockPlacementData placement,
            Vector3Int effectiveSize,
            BlockCatalogSO catalog,
            float extraYaw,
            Vector3 centerShift,
            out Vector3 position,
            out Quaternion rotation)
        {
            Vector3Int size = Footprint.ClampSize(effectiveSize);
            Vector3 center = GetFootprintWorldCenter(placement.gridPosition, size, catalog, IsFoundationTable(placement, catalog));

            if (TryGetSupportingTable(placement, size, catalog, out Vector3 tableCenter, out float tableYaw))
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

        public bool IsFoundationTable(in BlockPlacementData placement, BlockCatalogSO catalog)
        {
            if (catalog == null || placement.gridPosition.y != 0)
                return false;

            if (!catalog.TryGetBlockDefinition(placement.blockId, out BlockDefinition definition))
                return false;

            return definition.shapeRole == BlockShapeRole.FoundationTable
                || definition.materialType == BlockMaterialType.TableFoundation;
        }

        public Bounds GetWorldBounds(BlockCatalogSO catalog)
        {
            Vector3 cell = GetScaledCellSize(catalog);
            Vector3 centerShift = GetHorizontalCenterShift(catalog);
            Vector3 size = Vector3.Scale(GridDimensions, cell) + Vector3.up * GetTableLift(catalog);
            Bounds bounds = new Bounds(OriginOffset + size * 0.5f + centerShift, size);

            foreach (BlockPlacementData placement in Placements)
            {
                Vector3Int effectiveSize = GetEffectiveSize(placement, catalog);
                GetWorldPose(placement, effectiveSize, catalog, 0f, centerShift, out Vector3 position, out _);
                float reach = (GetFootprintWorldScale(Vector3.one, effectiveSize, catalog) * 0.5f).magnitude;
                bounds.Encapsulate(position + Vector3.one * reach);
                bounds.Encapsulate(position - Vector3.one * reach);
            }

            return bounds;
        }

        private bool HasCustomSize(in BlockPlacementData placement)
        {
            return placement.customSize.x > 0 && placement.customSize.y > 0 && placement.customSize.z > 0;
        }

        private PlacementValidationStatus ValidatePlacement(Vector3Int anchor, Vector3Int effectiveSize, BlockCatalogSO catalog, bool ignoreAnchor, Vector3Int ignoredAnchor)
        {
            Vector3Int size = Footprint.ClampSize(effectiveSize);
            if (!IsFootprintInsideGrid(anchor, size))
                return PlacementValidationStatus.InvalidOutOfBounds;

            return OverlapsAny(anchor, size, catalog, ignoreAnchor, ignoredAnchor)
                ? PlacementValidationStatus.ValidOverwrite
                : PlacementValidationStatus.ValidEmpty;
        }

        private bool IsFootprintInsideGrid(Vector3Int anchor, Vector3Int effectiveSize)
        {
            if (anchor.x < 0 || anchor.y < 0 || anchor.z < 0)
                return false;

            return IsInsideGrid(anchor + Footprint.ClampSize(effectiveSize) - Vector3Int.one);
        }

        private bool OverlapsAny(Vector3Int anchor, Vector3Int effectiveSize, BlockCatalogSO catalog, bool ignoreAnchor, Vector3Int ignoredAnchor)
        {
            foreach (BlockPlacementData existing in Placements)
            {
                if (ignoreAnchor && existing.gridPosition == ignoredAnchor)
                    continue;

                if (Footprint.Overlaps(anchor, effectiveSize, existing.gridPosition, GetEffectiveSize(existing, catalog)))
                    return true;
            }

            return false;
        }

        private void RemoveOverlapping(Vector3Int anchor, Vector3Int effectiveSize, BlockCatalogSO catalog)
        {
            Placements.RemoveAll(existing => Footprint.Overlaps(anchor, effectiveSize, existing.gridPosition, GetEffectiveSize(existing, catalog)));
        }

        private bool IntersectsLayer(in BlockPlacementData placement, int layerZ, BlockCatalogSO catalog)
        {
            int minZ = placement.gridPosition.z;
            int maxZ = minZ + GetEffectiveSize(placement, catalog).z - 1;
            return layerZ >= minZ && layerZ <= maxZ;
        }

        private bool TryGetSupportingTable(in BlockPlacementData block, Vector3Int blockSize, BlockCatalogSO catalog, out Vector3 tableCenter, out float tableYaw)
        {
            tableCenter = Vector3.zero;
            tableYaw = 0f;
            if (catalog == null || block.gridPosition.y <= 0)
                return false;

            int centerX = block.gridPosition.x + (blockSize.x - 1) / 2;
            int centerZ = block.gridPosition.z + (blockSize.z - 1) / 2;
            foreach (BlockPlacementData candidate in Placements)
            {
                if (!IsFoundationTable(candidate, catalog))
                    continue;

                Vector3Int tableSize = GetEffectiveSize(candidate, catalog);
                if (!Footprint.ContainsXZ(candidate.gridPosition, tableSize, centerX, centerZ))
                    continue;

                tableCenter = GetFootprintWorldCenter(candidate.gridPosition, tableSize, catalog, true);
                tableYaw = candidate.rotationEuler.y;
                return true;
            }

            return false;
        }
    }
}
