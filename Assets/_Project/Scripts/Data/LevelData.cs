using System;
using System.Collections.Generic;
using UnityEngine;

namespace SmashFest.Data
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

        public bool TryGetPlacement(Vector3Int cell, out BlockPlacementData placement)
        {
            foreach (BlockPlacementData candidate in Placements)
            {
                if (candidate.gridPosition != cell)
                    continue;

                placement = candidate;
                return true;
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
            if (!IsInsideGrid(placement.gridPosition) || TryGetPlacement(placement.gridPosition, out _))
                return false;

            Placements.Add(placement);
            return true;
        }

        public void AddPlacement(in BlockPlacementData placement) => Placements.Add(placement);

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
            List<BlockPlacementData> list = Placements;
            int removed = 0;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].gridPosition != cell)
                    continue;

                list.RemoveAt(i);
                removed++;
            }

            return removed > 0;
        }

        public int RemoveWhere(Predicate<BlockPlacementData> match) => Placements.RemoveAll(match);

        public void ClearPlacements() => Placements.Clear();

        public bool HasCustomSize(in BlockPlacementData placement)
        {
            return placement.customSize.x > 0 && placement.customSize.y > 0 && placement.customSize.z > 0;
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

        public bool IsFootprintInsideGrid(Vector3Int anchor, Vector3Int effectiveSize)
        {
            if (anchor.x < 0 || anchor.y < 0 || anchor.z < 0)
                return false;

            return IsInsideGrid(anchor + Footprint.ClampSize(effectiveSize) - Vector3Int.one);
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

        private PlacementValidationStatus ValidatePlacement(Vector3Int anchor, Vector3Int effectiveSize, BlockCatalogSO catalog, bool ignoreAnchor, Vector3Int ignoredAnchor)
        {
            Vector3Int size = Footprint.ClampSize(effectiveSize);
            if (!IsFootprintInsideGrid(anchor, size))
                return PlacementValidationStatus.InvalidOutOfBounds;

            return OverlapsAny(anchor, size, catalog, ignoreAnchor, ignoredAnchor)
                ? PlacementValidationStatus.ValidOverwrite
                : PlacementValidationStatus.ValidEmpty;
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
            List<BlockPlacementData> list = Placements;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (Footprint.Overlaps(anchor, effectiveSize, list[i].gridPosition, GetEffectiveSize(list[i], catalog)))
                    list.RemoveAt(i);
            }
        }
    }
}
