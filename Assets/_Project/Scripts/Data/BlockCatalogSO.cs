using System;
using System.Collections.Generic;
using UnityEngine;

namespace SmashFest.Data
{
    [CreateAssetMenu(fileName = "BlockCatalog", menuName = "SmashFest/Block Catalog", order = 1)]
    public class BlockCatalogSO : ScriptableObject
    {
        public const float MinGlobalBlockScale = 0.2f;
        public const float MaxGlobalBlockScale = 1.5f;

        [Range(MinGlobalBlockScale, MaxGlobalBlockScale)] public float globalBlockScale = 0.65f;
        [Range(0.05f, 1f)] public float blockMassMultiplier = 0.25f;
        [Min(0.1f)] public float tableMinHeight = 0.65f;

        [SerializeField] private List<BlockFamilyDefinition> families = new List<BlockFamilyDefinition>(4);
        [SerializeField] private List<BlockDefinition> standaloneBlocks = new List<BlockDefinition>(8);

        [NonSerialized] private readonly List<BlockDefinition> blocks = new List<BlockDefinition>(64);
        [NonSerialized] private readonly Dictionary<int, int> indexById = new Dictionary<int, int>(64);
        [NonSerialized] private readonly BlockVariantBuilder builder = new BlockVariantBuilder();
        [NonSerialized] private readonly BlockFootprint footprint = new BlockFootprint();
        [NonSerialized] private float lastNotifiedScale = -1f;
        [NonSerialized] private bool isBuilt;

        public event Action ScaleChanged;

        public int BlockCount
        {
            get
            {
                BuildBlocks();
                return blocks.Count;
            }
        }

        public int FamilyCount => families != null ? families.Count : 0;
        public int StandaloneCount => standaloneBlocks != null ? standaloneBlocks.Count : 0;

        private void OnEnable()
        {
            families ??= new List<BlockFamilyDefinition>(4);
            standaloneBlocks ??= new List<BlockDefinition>(8);
            ApplyFamilyRules();
            lastNotifiedScale = GetScale();
            InvalidateRuntimeBlocks();
        }

        private void OnValidate()
        {
            ApplyFamilyRules();
            InvalidateRuntimeBlocks();
            NotifyScaleChanged();
        }

        public float GetMassMultiplier() => Mathf.Max(0.01f, blockMassMultiplier);

        public float GetScale() => Mathf.Max(0.1f, globalBlockScale);

        public float GetTableHeightFactor(float cellHeight)
        {
            float tableHeight = Mathf.Max(0.01f, cellHeight) * GetScale();
            return Mathf.Max(1f, tableMinHeight / tableHeight);
        }

        public void SetScale(float scale)
        {
            globalBlockScale = Mathf.Clamp(scale, MinGlobalBlockScale, MaxGlobalBlockScale);
            NotifyScaleChanged();
        }

        public bool TryGetFamily(int index, out BlockFamilyDefinition family)
        {
            family = families != null && index >= 0 && index < families.Count ? families[index] : null;
            return family != null;
        }

        public bool TryGetStandalone(int index, out BlockDefinition definition)
        {
            bool isValid = standaloneBlocks != null && index >= 0 && index < standaloneBlocks.Count;
            definition = isValid ? standaloneBlocks[index] : default;
            return isValid;
        }

        public bool TryGetFamilyVariant(int familyIndex, int length, int axis, out BlockDefinition definition)
        {
            definition = default;
            if (!TryGetFamily(familyIndex, out BlockFamilyDefinition family))
                return false;

            if (family.foundationTable)
                return TryGetFoundationTable(out definition);

            if (length < 1 || length > BlockFamilyDefinition.MaxLength || axis < 0 || axis > 2)
                return false;

            if (family.onlyVerticalAxis && axis != 1 && length > 1)
                return false;

            if (family.GetLengthPrefab(length) == null)
                return false;

            return TryGetBlockDefinition(ResolveFamilyBlockId(family, length, axis), out definition);
        }

        public bool TryGetFoundationTable(out BlockDefinition definition)
        {
            BuildBlocks();
            foreach (BlockDefinition candidate in blocks)
            {
                if (candidate.prefab == null || !candidate.IsTable())
                    continue;

                definition = candidate;
                return true;
            }

            definition = default;
            return false;
        }

        public bool TryGetBlockByIndex(int index, out BlockDefinition definition)
        {
            BuildBlocks();
            bool isValid = index >= 0 && index < blocks.Count;
            definition = isValid ? blocks[index] : default;
            return isValid;
        }

        public bool TryGetBlockDefinition(int blockId, out BlockDefinition definition)
        {
            BuildBlocks();
            bool found = indexById.TryGetValue(blockId, out int index);
            definition = found ? blocks[index] : default;
            return found;
        }

        public bool TryGetFirstByMaterialAndSize(BlockMaterialType material, Vector3Int sizeInCells, out BlockDefinition definition)
        {
            BuildBlocks();
            Vector3Int targetSize = footprint.ClampSize(sizeInCells);
            definition = default;
            bool found = false;

            foreach (BlockDefinition candidate in blocks)
            {
                if (candidate.materialType != material || candidate.prefab == null || !IsStructural(candidate.shapeRole))
                    continue;

                if (footprint.ClampSize(candidate.sizeInCells) != targetSize)
                    continue;

                if (candidate.meshEuler == Vector3.zero)
                {
                    definition = candidate;
                    return true;
                }

                if (found)
                    continue;

                definition = candidate;
                found = true;
            }

            return found;
        }

        public void InvalidateRuntimeBlocks() => isBuilt = false;

        public int ResolveFamilyBlockId(BlockFamilyDefinition family, int length, int axis) => builder.ResolveFamilyBlockId(family, length, axis);

        private void NotifyScaleChanged()
        {
            float scale = GetScale();
            if (Mathf.Approximately(scale, lastNotifiedScale))
                return;

            lastNotifiedScale = scale;
            ScaleChanged?.Invoke();
        }

        private void ApplyFamilyRules()
        {
            if (families == null)
                return;

            foreach (BlockFamilyDefinition family in families)
                family?.ApplyNamingRules();
        }

        private void BuildBlocks()
        {
            if (isBuilt)
                return;

            builder.Build(families, standaloneBlocks, blocks);
            indexById.Clear();
            for (int i = 0; i < blocks.Count; i++)
                indexById.TryAdd(blocks[i].blockId, i);

            isBuilt = true;
        }

        private bool IsStructural(BlockShapeRole role)
        {
            return role == BlockShapeRole.StandardBlock
                || role == BlockShapeRole.HorizontalPlank
                || role == BlockShapeRole.VerticalPillar;
        }
    }
}
