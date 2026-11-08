using System.Collections.Generic;
using UnityEngine;

namespace SmashFast.Data
{
    [CreateAssetMenu(fileName = "LevelData", menuName = "SmashFast/Level Data", order = 0)]
    public class LevelData : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField, Min(0)] private int levelIndex;
        [SerializeField, Min(0)] private int maxAmmo = 10;

        [Header("Grid")]
        [SerializeField] private Vector3Int gridDimensions = new Vector3Int(9, 12, 5);
        [SerializeField] private Vector3 cellSize = Vector3.one;
        [SerializeField] private Vector3 originOffset = Vector3.zero;

        [Header("Rules")]
        [SerializeField] private float fallEliminationY = -1.5f;
        [SerializeField, Min(0)] private int parAmmo;

        [Header("Placements")]
        [SerializeField] private List<BlockPlacementData> placements = new List<BlockPlacementData>(64);

        [System.NonSerialized] private BlockFootprint blockFootprint;

        private BlockFootprint Footprint
        {
            get
            {
                if (blockFootprint == null)
                {
                    blockFootprint = new BlockFootprint();
                }

                return blockFootprint;
            }
        }

        public int LevelIndex
        {
            get
            {
                return levelIndex;
            }
            set
            {
                levelIndex = value < 0 ? 0 : value;
            }
        }

        public int MaxAmmo
        {
            get
            {
                return maxAmmo;
            }
            set
            {
                maxAmmo = value < 0 ? 0 : value;
            }
        }

        public Vector3Int GridDimensions
        {
            get
            {
                return new Vector3Int(
                    Mathf.Max(1, gridDimensions.x),
                    Mathf.Max(1, gridDimensions.y),
                    Mathf.Max(1, gridDimensions.z));
            }
            set
            {
                gridDimensions = new Vector3Int(
                    Mathf.Max(1, value.x),
                    Mathf.Max(1, value.y),
                    Mathf.Max(1, value.z));
            }
        }

        public Vector3 CellSize
        {
            get
            {
                return new Vector3(
                    Mathf.Max(0.01f, cellSize.x),
                    Mathf.Max(0.01f, cellSize.y),
                    Mathf.Max(0.01f, cellSize.z));
            }
            set
            {
                cellSize = new Vector3(
                    Mathf.Max(0.01f, value.x),
                    Mathf.Max(0.01f, value.y),
                    Mathf.Max(0.01f, value.z));
            }
        }

        public Vector3 OriginOffset
        {
            get
            {
                return originOffset;
            }
            set
            {
                originOffset = value;
            }
        }

        public float FallEliminationY
        {
            get
            {
                return fallEliminationY;
            }
            set
            {
                fallEliminationY = value;
            }
        }

        public int ParAmmo
        {
            get
            {
                return parAmmo < 0 ? 0 : parAmmo;
            }
            set
            {
                parAmmo = value < 0 ? 0 : value;
            }
        }

        public int PlacementCount
        {
            get
            {
                return Placements.Count;
            }
        }

        private List<BlockPlacementData> Placements
        {
            get
            {
                if (placements == null)
                {
                    placements = new List<BlockPlacementData>(64);
                }

                return placements;
            }
        }

        private void OnEnable()
        {
            if (placements == null)
            {
                placements = new List<BlockPlacementData>(64);
            }
        }

        private void OnValidate()
        {
            Vector3Int clampedDimensions = new Vector3Int(
                Mathf.Max(1, gridDimensions.x),
                Mathf.Max(1, gridDimensions.y),
                Mathf.Max(1, gridDimensions.z));
            if (gridDimensions != clampedDimensions)
            {
                gridDimensions = clampedDimensions;
            }

            Vector3 clampedCell = new Vector3(
                Mathf.Max(0.01f, cellSize.x),
                Mathf.Max(0.01f, cellSize.y),
                Mathf.Max(0.01f, cellSize.z));
            if (cellSize != clampedCell)
            {
                cellSize = clampedCell;
            }

            if (maxAmmo < 0)
            {
                maxAmmo = 0;
            }

            if (levelIndex < 0)
            {
                levelIndex = 0;
            }

            if (parAmmo < 0)
            {
                parAmmo = 0;
            }
        }

        public bool IsInsideGrid(Vector3Int gridPosition)
        {
            Vector3Int dimensions = GridDimensions;
            if (gridPosition.x < 0 || gridPosition.y < 0 || gridPosition.z < 0)
            {
                return false;
            }

            if (gridPosition.x >= dimensions.x || gridPosition.y >= dimensions.y || gridPosition.z >= dimensions.z)
            {
                return false;
            }

            return true;
        }

        public bool HasBlockAt(Vector3Int gridPosition)
        {
            BlockPlacementData placement;
            return TryGetPlacement(gridPosition, out placement);
        }

        public bool TryGetPlacement(Vector3Int gridPosition, out BlockPlacementData placement)
        {
            List<BlockPlacementData> list = Placements;
            int count = list.Count;
            for (int i = 0; i < count; i++)
            {
                BlockPlacementData candidate = list[i];
                if (candidate.gridPosition != gridPosition)
                {
                    continue;
                }

                placement = candidate;
                return true;
            }

            placement = default;
            return false;
        }

        public bool TryGetPlacementByIndex(int index, out BlockPlacementData placement)
        {
            List<BlockPlacementData> list = Placements;
            if (index < 0 || index >= list.Count)
            {
                placement = default;
                return false;
            }

            placement = list[index];
            return true;
        }

        public bool TryAddPlacement(in BlockPlacementData placement)
        {
            if (!IsInsideGrid(placement.gridPosition))
            {
                return false;
            }

            if (HasBlockAt(placement.gridPosition))
            {
                return false;
            }

            Placements.Add(placement);
            return true;
        }

        public bool TrySetPlacement(in BlockPlacementData placement)
        {
            if (!IsInsideGrid(placement.gridPosition))
            {
                return false;
            }

            List<BlockPlacementData> list = Placements;
            bool replaced = false;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].gridPosition != placement.gridPosition)
                {
                    continue;
                }

                if (!replaced)
                {
                    list[i] = placement;
                    replaced = true;
                }
                else
                {
                    list.RemoveAt(i);
                }
            }

            if (!replaced)
            {
                list.Add(placement);
            }

            return true;
        }

        public bool TryRemoveAt(Vector3Int gridPosition)
        {
            List<BlockPlacementData> list = Placements;
            bool removed = false;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].gridPosition != gridPosition)
                {
                    continue;
                }

                list.RemoveAt(i);
                removed = true;
            }

            return removed;
        }

        public void ClearPlacements()
        {
            Placements.Clear();
        }

        public int ClearLayer(int layerZ, BlockCatalogSO catalog = null)
        {
            List<BlockPlacementData> list = Placements;
            int removed = 0;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (!IntersectsLayer(list[i], layerZ, catalog))
                {
                    continue;
                }

                list.RemoveAt(i);
                removed++;
            }

            return removed;
        }

        public int CopyLayer(int sourceLayerZ, int destinationLayerZ, BlockCatalogSO catalog = null)
        {
            if (sourceLayerZ == destinationLayerZ)
            {
                return 0;
            }

            Vector3Int dimensions = GridDimensions;
            if (sourceLayerZ < 0 || destinationLayerZ < 0 || sourceLayerZ >= dimensions.z || destinationLayerZ >= dimensions.z)
            {
                return 0;
            }

            List<BlockPlacementData> list = Placements;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                bool hitsDestination = IntersectsLayer(list[i], destinationLayerZ, catalog);
                bool hitsSource = IntersectsLayer(list[i], sourceLayerZ, catalog);
                if (!hitsDestination || hitsSource)
                {
                    continue;
                }

                list.RemoveAt(i);
            }

            int delta = destinationLayerZ - sourceLayerZ;
            int count = list.Count;
            int copied = 0;
            for (int i = 0; i < count; i++)
            {
                BlockPlacementData source = list[i];
                if (!IntersectsLayer(source, sourceLayerZ, catalog))
                {
                    continue;
                }

                Vector3Int position = source.gridPosition;
                position.z += delta;
                source.gridPosition = position;
                list.Add(source);
                copied++;
            }

            return copied;
        }

        public int CountLayer(int layerZ, BlockCatalogSO catalog = null)
        {
            List<BlockPlacementData> list = Placements;
            int count = list.Count;
            int total = 0;
            for (int i = 0; i < count; i++)
            {
                if (!IntersectsLayer(list[i], layerZ, catalog))
                {
                    continue;
                }

                total++;
            }

            return total;
        }

        public int RemoveOutOfBounds(BlockCatalogSO catalog = null)
        {
            List<BlockPlacementData> list = Placements;
            int removed = 0;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                Vector3Int size = GetEffectiveSize(list[i], catalog);
                if (IsFootprintInsideGrid(list[i].gridPosition, size))
                {
                    continue;
                }

                list.RemoveAt(i);
                removed++;
            }

            return removed;
        }

        public Vector3Int GetEffectiveSize(in BlockPlacementData placement, BlockCatalogSO catalog)
        {
            Vector3Int sizeInCells = Vector3Int.one;
            BlockDefinition definition;
            if (catalog != null && catalog.TryGetBlockDefinition(placement.blockId, out definition))
            {
                sizeInCells = definition.sizeInCells;
            }

            return Footprint.GetEffectiveSize(sizeInCells, placement.rotationEuler);
        }

        public PlacementValidationStatus ValidatePlacement(Vector3Int anchorCell, Vector3Int effectiveSize, BlockCatalogSO catalog = null)
        {
            return ValidatePlacement(anchorCell, effectiveSize, catalog, false, Vector3Int.zero);
        }

        public PlacementValidationStatus ValidatePlacementIgnoring(
            Vector3Int anchorCell,
            Vector3Int effectiveSize,
            BlockCatalogSO catalog,
            Vector3Int ignoredAnchor)
        {
            return ValidatePlacement(anchorCell, effectiveSize, catalog, true, ignoredAnchor);
        }

        private PlacementValidationStatus ValidatePlacement(
            Vector3Int anchorCell,
            Vector3Int effectiveSize,
            BlockCatalogSO catalog,
            bool ignoreAnchor,
            Vector3Int ignoredAnchor)
        {
            Vector3Int size = Footprint.ClampSize(effectiveSize);
            if (!IsFootprintInsideGrid(anchorCell, size))
            {
                return PlacementValidationStatus.InvalidOutOfBounds;
            }

            if (OverlapsAny(anchorCell, size, catalog, ignoreAnchor, ignoredAnchor))
            {
                return PlacementValidationStatus.ValidOverwrite;
            }

            return PlacementValidationStatus.ValidEmpty;
        }

        public bool TryGetBlockAt(Vector3Int cell, BlockCatalogSO catalog, out BlockPlacementData placement, out bool isAnchorCell)
        {
            List<BlockPlacementData> list = Placements;
            int count = list.Count;
            for (int i = 0; i < count; i++)
            {
                BlockPlacementData candidate = list[i];
                Vector3Int size = GetEffectiveSize(candidate, catalog);
                if (!Footprint.ContainsCell(candidate.gridPosition, size, cell))
                {
                    continue;
                }

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
            {
                return false;
            }

            if (status == PlacementValidationStatus.ValidOverwrite)
            {
                RemoveOverlapping(placement.gridPosition, size, catalog);
            }

            Placements.Add(placement);
            return true;
        }

        public void GetOccupiedGridBounds(BlockCatalogSO catalog, out Vector3 minGrid, out Vector3 maxGrid)
        {
            int minX = 0;
            int minY = 0;
            int minZ = 0;
            int maxX = 0;
            int maxY = 0;
            int maxZ = 0;
            bool any = false;
            List<BlockPlacementData> list = Placements;
            int count = list.Count;
            for (int i = 0; i < count; i++)
            {
                BlockPlacementData placement = list[i];
                Vector3Int size = GetEffectiveSize(placement, catalog);
                int endX = placement.gridPosition.x + size.x - 1;
                int endY = placement.gridPosition.y + size.y - 1;
                int endZ = placement.gridPosition.z + size.z - 1;
                if (!any)
                {
                    minX = placement.gridPosition.x;
                    minY = placement.gridPosition.y;
                    minZ = placement.gridPosition.z;
                    maxX = endX;
                    maxY = endY;
                    maxZ = endZ;
                    any = true;
                    continue;
                }

                if (placement.gridPosition.x < minX)
                {
                    minX = placement.gridPosition.x;
                }

                if (placement.gridPosition.y < minY)
                {
                    minY = placement.gridPosition.y;
                }

                if (placement.gridPosition.z < minZ)
                {
                    minZ = placement.gridPosition.z;
                }

                if (endX > maxX)
                {
                    maxX = endX;
                }

                if (endY > maxY)
                {
                    maxY = endY;
                }

                if (endZ > maxZ)
                {
                    maxZ = endZ;
                }
            }

            if (!any)
            {
                Vector3Int dimensions = GridDimensions;
                maxX = dimensions.x - 1;
                maxY = dimensions.y - 1;
                maxZ = dimensions.z - 1;
            }

            minGrid = new Vector3(minX, minY, minZ);
            maxGrid = new Vector3(maxX, maxY, maxZ);
        }

        public Vector3 GetHorizontalCenterShift(BlockCatalogSO catalog)
        {
            Vector3 minGrid;
            Vector3 maxGrid;
            GetOccupiedGridBounds(catalog, out minGrid, out maxGrid);
            float occupiedCenterX = (minGrid.x + maxGrid.x) * 0.5f;
            float occupiedCenterZ = (minGrid.z + maxGrid.z) * 0.5f;
            Vector3 cell = CellSize;
            return new Vector3(-(occupiedCenterX + 0.5f) * cell.x, 0f, -(occupiedCenterZ + 0.5f) * cell.z);
        }

        public Vector3 GetFootprintWorldCenter(Vector3Int anchor, Vector3Int effectiveSize)
        {
            Vector3Int size = Footprint.ClampSize(effectiveSize);
            Vector3 cell = CellSize;
            Vector3 centerGridOffset = new Vector3(
                anchor.x + ((size.x - 1) * 0.5f),
                anchor.y + ((size.y - 1) * 0.5f),
                anchor.z + ((size.z - 1) * 0.5f));
            return OriginOffset + new Vector3(
                (centerGridOffset.x + 0.5f) * cell.x,
                (centerGridOffset.y + 0.5f) * cell.y,
                (centerGridOffset.z + 0.5f) * cell.z);
        }

        public Vector3 GetCellWorldScale(Vector3 baseScale)
        {
            if (baseScale == Vector3.zero)
            {
                baseScale = Vector3.one;
            }

            Vector3 cell = CellSize;
            return new Vector3(baseScale.x * cell.x, baseScale.y * cell.y, baseScale.z * cell.z);
        }

        public Vector3 GetFootprintWorldScale(Vector3 baseScale, Vector3Int sizeInCells)
        {
            Vector3Int localSize = Footprint.ClampSize(sizeInCells);
            Vector3 cellScale = GetCellWorldScale(baseScale);
            return new Vector3(cellScale.x * localSize.x, cellScale.y * localSize.y, cellScale.z * localSize.z);
        }

        public Matrix4x4 GetWorldMatrix(in BlockPlacementData placement, Vector3 baseScale, Vector3Int sizeInCells, BlockCatalogSO catalog)
        {
            Vector3Int effectiveSize = Footprint.GetEffectiveSize(sizeInCells, placement.rotationEuler);
            Vector3 worldPos;
            Quaternion worldRot;
            GetWorldPose(placement, effectiveSize, catalog, 0f, out worldPos, out worldRot);
            Vector3 scale = GetFootprintWorldScale(baseScale, sizeInCells);
            return Matrix4x4.TRS(worldPos, worldRot, scale);
        }

        public void GetWorldPose(
            in BlockPlacementData placement,
            Vector3Int effectiveSize,
            BlockCatalogSO catalog,
            float extraYawOffset,
            out Vector3 worldPos,
            out Quaternion worldRot)
        {
            Vector3Int size = Footprint.ClampSize(effectiveSize);
            Vector3 unrotatedCenter = GetFootprintWorldCenter(placement.gridPosition, size);
            Vector3 centerShift = GetHorizontalCenterShift(catalog);
            Vector3 tableCenterPos;
            float tableYaw;
            if (TryGetSupportingTable(placement, size, catalog, out tableCenterPos, out tableYaw))
            {
                Quaternion orbit = Quaternion.Euler(0f, tableYaw + extraYawOffset, 0f);
                worldPos = tableCenterPos + (orbit * (unrotatedCenter - tableCenterPos));
                worldPos += centerShift;
                worldRot = orbit * Quaternion.Euler(placement.rotationEuler);
                return;
            }

            if (IsFoundationTable(placement, catalog))
            {
                worldPos = unrotatedCenter + centerShift;
                Vector3 euler = placement.rotationEuler;
                euler.y += extraYawOffset;
                worldRot = Quaternion.Euler(euler);
                return;
            }

            worldPos = unrotatedCenter + centerShift;
            worldRot = Quaternion.Euler(placement.rotationEuler);
        }

        public bool IsFoundationTable(in BlockPlacementData placement, BlockCatalogSO catalog)
        {
            if (catalog == null || placement.gridPosition.y != 0)
            {
                return false;
            }

            BlockDefinition definition;
            if (!catalog.TryGetBlockDefinition(placement.blockId, out definition))
            {
                return false;
            }

            if (definition.shapeRole == BlockShapeRole.FoundationTable)
            {
                return true;
            }

            return definition.materialType == BlockMaterialType.TableFoundation;
        }

        private bool IsFootprintInsideGrid(Vector3Int anchor, Vector3Int effectiveSize)
        {
            Vector3Int size = Footprint.ClampSize(effectiveSize);
            if (anchor.x < 0 || anchor.y < 0 || anchor.z < 0)
            {
                return false;
            }

            Vector3Int end = anchor + size - Vector3Int.one;
            return IsInsideGrid(end);
        }

        private bool OverlapsAny(
            Vector3Int anchor,
            Vector3Int effectiveSize,
            BlockCatalogSO catalog,
            bool ignoreAnchor,
            Vector3Int ignoredAnchor)
        {
            List<BlockPlacementData> list = Placements;
            int count = list.Count;
            for (int i = 0; i < count; i++)
            {
                if (ignoreAnchor && list[i].gridPosition == ignoredAnchor)
                {
                    continue;
                }

                Vector3Int existingSize = GetEffectiveSize(list[i], catalog);
                if (Footprint.Overlaps(anchor, effectiveSize, list[i].gridPosition, existingSize))
                {
                    return true;
                }
            }

            return false;
        }

        private void RemoveOverlapping(Vector3Int anchor, Vector3Int effectiveSize, BlockCatalogSO catalog)
        {
            List<BlockPlacementData> list = Placements;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                Vector3Int existingSize = GetEffectiveSize(list[i], catalog);
                if (!Footprint.Overlaps(anchor, effectiveSize, list[i].gridPosition, existingSize))
                {
                    continue;
                }

                list.RemoveAt(i);
            }
        }

        private bool IntersectsLayer(in BlockPlacementData placement, int layerZ, BlockCatalogSO catalog)
        {
            Vector3Int size = GetEffectiveSize(placement, catalog);
            int minZ = placement.gridPosition.z;
            int maxZ = minZ + size.z - 1;
            return layerZ >= minZ && layerZ <= maxZ;
        }

        public Bounds GetWorldBounds(BlockCatalogSO catalog)
        {
            Vector3Int dimensions = GridDimensions;
            Vector3 cell = CellSize;
            Vector3 size = new Vector3(dimensions.x * cell.x, dimensions.y * cell.y, dimensions.z * cell.z);
            Vector3 boundsCenter = OriginOffset + (size * 0.5f) + GetHorizontalCenterShift(catalog);
            Bounds bounds = new Bounds(boundsCenter, size);
            int count = PlacementCount;
            for (int i = 0; i < count; i++)
            {
                BlockPlacementData placement;
                if (!TryGetPlacementByIndex(i, out placement))
                {
                    continue;
                }

                Vector3Int footprint = GetEffectiveSize(placement, catalog);
                Vector3 worldPos;
                Quaternion worldRot;
                GetWorldPose(placement, footprint, catalog, 0f, out worldPos, out worldRot);
                Vector3 extent = GetFootprintWorldScale(Vector3.one, footprint) * 0.5f;
                float reach = extent.magnitude;
                bounds.Encapsulate(worldPos + new Vector3(reach, reach, reach));
                bounds.Encapsulate(worldPos - new Vector3(reach, reach, reach));
            }

            return bounds;
        }

        private bool TryGetSupportingTable(
            in BlockPlacementData block,
            Vector3Int blockSize,
            BlockCatalogSO catalog,
            out Vector3 tableCenterPos,
            out float tableYaw)
        {
            tableCenterPos = Vector3.zero;
            tableYaw = 0f;
            if (catalog == null || block.gridPosition.y <= 0)
            {
                return false;
            }

            int centerX = block.gridPosition.x + ((blockSize.x - 1) / 2);
            int centerZ = block.gridPosition.z + ((blockSize.z - 1) / 2);
            List<BlockPlacementData> list = Placements;
            int count = list.Count;
            for (int i = 0; i < count; i++)
            {
                BlockPlacementData candidate = list[i];
                if (!IsFoundationTable(candidate, catalog))
                {
                    continue;
                }

                Vector3Int tableSize = GetEffectiveSize(candidate, catalog);
                if (!Footprint.ContainsXZ(candidate.gridPosition, tableSize, centerX, centerZ))
                {
                    continue;
                }

                tableCenterPos = GetFootprintWorldCenter(candidate.gridPosition, tableSize);
                tableYaw = candidate.rotationEuler.y;
                return true;
            }

            return false;
        }
    }
}
