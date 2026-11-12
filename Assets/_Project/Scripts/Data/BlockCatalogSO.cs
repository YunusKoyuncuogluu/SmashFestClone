using System.Collections.Generic;
using UnityEngine;

namespace SmashFast.Data
{
    [CreateAssetMenu(fileName = "BlockCatalog", menuName = "SmashFast/Block Catalog", order = 1)]
    public class BlockCatalogSO : ScriptableObject
    {
        public const float MinGlobalBlockScale = 0.2f;
        public const float MaxGlobalBlockScale = 1.5f;

        [Range(MinGlobalBlockScale, MaxGlobalBlockScale)] public float globalBlockScale = 0.65f;
        [Range(0.05f, 1f)] public float blockMassMultiplier = 0.25f;
        [Min(0.1f)] public float tableMinHeight = 0.65f;

        [SerializeField] private List<BlockFamilyDefinition> families = new List<BlockFamilyDefinition>(4);
        [SerializeField] private List<BlockDefinition> standaloneBlocks = new List<BlockDefinition>(8);

        [System.NonSerialized] private float lastNotifiedScale = -1f;

        public event System.Action ScaleChanged;

        [System.NonSerialized] private List<BlockDefinition> runtimeBlocks;
        [System.NonSerialized] private Dictionary<int, int> idToIndex;
        [System.NonSerialized] private BlockFootprint blockFootprint;
        [System.NonSerialized] private bool runtimeBlocksReady;

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

        public int BlockCount
        {
            get
            {
                EnsureRuntimeBlocks();
                return runtimeBlocks.Count;
            }
        }

        public int FamilyCount
        {
            get
            {
                if (families == null)
                {
                    return 0;
                }

                return families.Count;
            }
        }

        public int StandaloneCount
        {
            get
            {
                if (standaloneBlocks == null)
                {
                    return 0;
                }

                return standaloneBlocks.Count;
            }
        }

        private void OnEnable()
        {
            if (families == null)
            {
                families = new List<BlockFamilyDefinition>(4);
            }

            if (standaloneBlocks == null)
            {
                standaloneBlocks = new List<BlockDefinition>(8);
            }

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

        private void ApplyFamilyRules()
        {
            if (families == null)
            {
                return;
            }

            int count = families.Count;
            for (int i = 0; i < count; i++)
            {
                if (families[i] != null)
                {
                    families[i].ApplyNamingRules();
                }
            }
        }

        public float GetMassMultiplier()
        {
            return Mathf.Max(0.01f, blockMassMultiplier);
        }

        public float GetTableHeightFactor(float cellHeight)
        {
            float tableHeight = Mathf.Max(0.01f, cellHeight) * GetScale();
            return Mathf.Max(1f, tableMinHeight / tableHeight);
        }

        public float GetScale()
        {
            return Mathf.Max(0.1f, globalBlockScale);
        }

        public void SetScale(float scale)
        {
            globalBlockScale = Mathf.Clamp(scale, MinGlobalBlockScale, MaxGlobalBlockScale);
            NotifyScaleChanged();
        }

        private void NotifyScaleChanged()
        {
            float current = GetScale();
            if (Mathf.Approximately(current, lastNotifiedScale))
            {
                return;
            }

            lastNotifiedScale = current;
            System.Action handler = ScaleChanged;
            if (handler != null)
            {
                handler();
            }
        }

        public IReadOnlyList<BlockDefinition> GetAllBlocks()
        {
            EnsureRuntimeBlocks();
            return runtimeBlocks;
        }

        public bool TryGetFamily(int index, out BlockFamilyDefinition family)
        {
            if (families == null || index < 0 || index >= families.Count)
            {
                family = null;
                return false;
            }

            family = families[index];
            return family != null;
        }

        public bool TryGetStandalone(int index, out BlockDefinition definition)
        {
            if (standaloneBlocks == null || index < 0 || index >= standaloneBlocks.Count)
            {
                definition = default;
                return false;
            }

            definition = standaloneBlocks[index];
            return true;
        }

        public bool TryGetFamilyVariant(int familyIndex, int length, int axis, out BlockDefinition definition)
        {
            definition = default;
            BlockFamilyDefinition family;
            if (!TryGetFamily(familyIndex, out family))
            {
                return false;
            }

            if (family.foundationTable)
            {
                return TryGetFoundationTable(out definition);
            }

            if (length < 1 || length > BlockFamilyDefinition.MaxLength || axis < 0 || axis > 2)
            {
                return false;
            }

            if (family.onlyVerticalAxis && axis != 1 && length > 1)
            {
                return false;
            }

            if (family.GetLengthPrefab(length) == null)
            {
                return false;
            }

            int blockId = ResolveFamilyBlockId(family, length, axis);
            return TryGetBlockDefinition(blockId, out definition);
        }

        public bool TryGetFoundationTable(out BlockDefinition definition)
        {
            definition = default;
            EnsureRuntimeBlocks();
            int count = runtimeBlocks.Count;
            for (int i = 0; i < count; i++)
            {
                BlockDefinition candidate = runtimeBlocks[i];
                if (candidate.prefab == null)
                {
                    continue;
                }

                if (candidate.shapeRole == BlockShapeRole.FoundationTable
                    || candidate.materialType == BlockMaterialType.TableFoundation)
                {
                    definition = candidate;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetBlockByIndex(int index, out BlockDefinition definition)
        {
            EnsureRuntimeBlocks();
            if (index < 0 || index >= runtimeBlocks.Count)
            {
                definition = default;
                return false;
            }

            definition = runtimeBlocks[index];
            return true;
        }

        public bool TryGetBlockDefinition(int blockId, out BlockDefinition definition)
        {
            EnsureRuntimeBlocks();
            if (idToIndex != null && idToIndex.TryGetValue(blockId, out int index))
            {
                definition = runtimeBlocks[index];
                return true;
            }

            definition = default;
            return false;
        }

        public bool TryGetFirstByMaterialAndSize(BlockMaterialType materialType, Vector3Int sizeInCells, out BlockDefinition definition)
        {
            EnsureRuntimeBlocks();
            Vector3Int targetSize = Footprint.ClampSize(sizeInCells);
            definition = default;
            bool found = false;
            int count = runtimeBlocks.Count;
            for (int i = 0; i < count; i++)
            {
                BlockDefinition candidate = runtimeBlocks[i];
                if (candidate.materialType != materialType)
                {
                    continue;
                }

                if (candidate.prefab == null || !IsStructuralRole(candidate.shapeRole))
                {
                    continue;
                }

                if (Footprint.ClampSize(candidate.sizeInCells) != targetSize)
                {
                    continue;
                }

                if (candidate.meshEuler == Vector3.zero)
                {
                    definition = candidate;
                    return true;
                }

                if (!found)
                {
                    definition = candidate;
                    found = true;
                }
            }

            return found;
        }

        private bool IsStructuralRole(BlockShapeRole role)
        {
            return role == BlockShapeRole.StandardBlock
                || role == BlockShapeRole.HorizontalPlank
                || role == BlockShapeRole.VerticalPillar;
        }

        public void InvalidateRuntimeBlocks()
        {
            runtimeBlocksReady = false;
            idToIndex = null;
        }

        public void RebuildExpandedBlocks()
        {
            InvalidateRuntimeBlocks();
            EnsureRuntimeBlocks();
        }

        private void EnsureRuntimeBlocks()
        {
            if (runtimeBlocksReady && runtimeBlocks != null)
            {
                return;
            }

            if (runtimeBlocks == null)
            {
                runtimeBlocks = new List<BlockDefinition>(64);
            }
            else
            {
                runtimeBlocks.Clear();
            }

            if (families != null)
            {
                int familyCount = families.Count;
                for (int familyIndex = 0; familyIndex < familyCount; familyIndex++)
                {
                    BlockFamilyDefinition family = families[familyIndex];
                    if (family == null)
                    {
                        continue;
                    }

                    family.ApplyNamingRules();
                    AppendFamilyVariants(family, familyIndex + 1);
                }
            }

            if (standaloneBlocks != null)
            {
                int standaloneCount = standaloneBlocks.Count;
                for (int i = 0; i < standaloneCount; i++)
                {
                    BlockDefinition standalone = SanitizeDefinition(standaloneBlocks[i]);
                    if (standalone.prefab == null)
                    {
                        continue;
                    }

                    if (standalone.shapeRole == BlockShapeRole.FoundationTable
                        || standalone.materialType == BlockMaterialType.TableFoundation)
                    {
                        standalone.blockName = "Table";
                        if (standalone.sizeInCells.x < 1 || standalone.sizeInCells.y < 1 || standalone.sizeInCells.z < 1)
                        {
                            standalone.sizeInCells = new Vector3Int(4, 1, 2);
                        }

                        standalone.isKinematicBase = true;
                        standalone.canSupportBlocksAbove = true;
                        standalone.materialType = BlockMaterialType.TableFoundation;
                        standalone.shapeRole = BlockShapeRole.FoundationTable;
                    }

                    runtimeBlocks.Add(standalone);
                }
            }

            RebuildIdCache();
            WarnDuplicateIds();
            runtimeBlocksReady = true;
        }

        private void AppendFamilyVariants(BlockFamilyDefinition family, int familyKey)
        {
            string familyName = string.IsNullOrEmpty(family.familyName) ? "Family" : family.familyName;
            if (family.foundationTable)
            {
                AppendFoundationTableVariants(family, familyName, familyKey);
                return;
            }

            for (int length = 1; length <= BlockFamilyDefinition.MaxLength; length++)
            {
                GameObject prefab = family.GetLengthPrefab(length);
                if (prefab == null)
                {
                    continue;
                }

                for (int axis = 0; axis < 3; axis++)
                {
                    if (family.onlyVerticalAxis && axis != 1 && length > 1)
                    {
                        continue;
                    }

                    BlockDefinition variant = BuildFamilyVariant(family, familyName, prefab, length, axis);
                    variant.familyKey = familyKey;
                    runtimeBlocks.Add(SanitizeDefinition(variant));
                }
            }
        }

        private void AppendFoundationTableVariants(BlockFamilyDefinition family, string familyName, int familyKey)
        {
            GameObject sourcePrefab = ResolveFoundationSourcePrefab(family);
            if (sourcePrefab == null)
            {
                return;
            }

            BlockDefinition table = BuildFoundationTableDefinition(family, familyName, sourcePrefab);
            table.familyKey = familyKey;
            runtimeBlocks.Add(SanitizeDefinition(table));
        }

        private GameObject ResolveFoundationSourcePrefab(BlockFamilyDefinition family)
        {
            if (family == null)
            {
                return null;
            }

            return family.length1Prefab;
        }

        private BlockDefinition BuildFoundationTableDefinition(
            BlockFamilyDefinition family,
            string familyName,
            GameObject prefab)
        {
            BlockDefinition definition = new BlockDefinition();
            definition.blockId = family.baseBlockId > 0
                ? family.baseBlockId
                : ResolveFamilyBlockId(family, 4, 0);
            definition.blockName = "Table";
            definition.prefab = prefab;
            definition.baseScale = Vector3.one;
            definition.meshEuler = Vector3.zero;
            definition.sizeInCells = new Vector3Int(4, 1, 2);
            definition.editorColor = family.yColor;
            definition.isKinematicBase = true;
            definition.mass = family.baseMass;
            definition.materialType = BlockMaterialType.TableFoundation;
            definition.shapeRole = BlockShapeRole.FoundationTable;
            definition.canSupportBlocksAbove = true;
            definition.impactBreakThreshold = 99f;
            return definition;
        }

        private BlockDefinition BuildFamilyVariant(
            BlockFamilyDefinition family,
            string familyName,
            GameObject prefab,
            int length,
            int axis)
        {
            BlockDefinition definition = new BlockDefinition();
            definition.blockId = ResolveFamilyBlockId(family, length, axis);
            definition.prefab = prefab;
            definition.baseScale = Vector3.one;
            definition.mass = family.baseMass + (family.massPerExtraLength * (length - 1));
            definition.materialType = family.materialType;
            definition.canSupportBlocksAbove = !family.fragileProp;
            definition.impactBreakThreshold = family.impactBreakThreshold > 0f ? family.impactBreakThreshold : 3.5f;
            definition.isKinematicBase = false;

            string axisLabel = "x";
            if (axis == 0)
            {
                definition.blockName = familyName + "_" + length.ToString() + "x";
                definition.meshEuler = new Vector3(0f, 0f, 90f);
                definition.sizeInCells = new Vector3Int(length, 1, 1);
                definition.editorColor = family.xColor;
                definition.shapeRole = length > 1 ? BlockShapeRole.HorizontalPlank : BlockShapeRole.StandardBlock;
                axisLabel = "x";
            }
            else if (axis == 1)
            {
                definition.blockName = familyName + "_" + length.ToString() + "y";
                definition.meshEuler = Vector3.zero;
                definition.sizeInCells = new Vector3Int(1, length, 1);
                definition.editorColor = family.yColor;
                definition.shapeRole = length > 1 ? BlockShapeRole.VerticalPillar : BlockShapeRole.StandardBlock;
                axisLabel = "y";
            }
            else
            {
                definition.blockName = familyName + "_" + length.ToString() + "z";
                definition.meshEuler = new Vector3(-90f, 0f, 0f);
                definition.sizeInCells = new Vector3Int(1, 1, length);
                definition.editorColor = family.zColor;
                definition.shapeRole = length > 1 ? BlockShapeRole.HorizontalPlank : BlockShapeRole.StandardBlock;
                axisLabel = "z";
            }

            if (string.IsNullOrEmpty(definition.blockName))
            {
                definition.blockName = familyName + "_" + length.ToString() + axisLabel;
            }

            if (family.onlyVerticalAxis && length == 1)
            {
                definition.meshEuler = Vector3.zero;
                definition.sizeInCells = Vector3Int.one;
                definition.shapeRole = BlockShapeRole.StandardBlock;
            }

            if (family.fragileProp)
            {
                definition.shapeRole = BlockShapeRole.FragileProp;
            }

            return definition;
        }

        public int ResolveFamilyBlockId(BlockFamilyDefinition family, int length, int axis)
        {
            if (family != null && family.baseBlockId > 0)
            {
                return family.baseBlockId + (axis * 10) + length;
            }

            return ResolveLegacySnowLogId(length, axis);
        }

        private int ResolveLegacySnowLogId(int length, int axis)
        {
            if (axis == 0)
            {
                if (length == 1)
                {
                    return 14;
                }

                return length;
            }

            if (axis == 1)
            {
                if (length == 1)
                {
                    return 1;
                }

                return length + 4;
            }

            if (length == 1)
            {
                return 15;
            }

            return length + 8;
        }

        private void RebuildIdCache()
        {
            int count = runtimeBlocks.Count;
            if (idToIndex == null)
            {
                idToIndex = new Dictionary<int, int>(count);
            }
            else
            {
                idToIndex.Clear();
            }

            for (int i = 0; i < count; i++)
            {
                int id = runtimeBlocks[i].blockId;
                if (idToIndex.ContainsKey(id))
                {
                    continue;
                }

                idToIndex.Add(id, i);
            }
        }

        private BlockDefinition SanitizeDefinition(BlockDefinition definition)
        {
            if (string.IsNullOrEmpty(definition.blockName))
            {
                definition.blockName = "Block " + definition.blockId.ToString();
            }

            if (definition.baseScale == Vector3.zero)
            {
                definition.baseScale = Vector3.one;
            }

            definition.sizeInCells = Footprint.ClampSize(definition.sizeInCells);

            if (definition.editorColor.a <= 0f)
            {
                Color color = definition.editorColor;
                if (color.maxColorComponent <= 0f)
                {
                    color = new Color(0.25f, 0.8f, 0.95f, 1f);
                }
                else
                {
                    color.a = 1f;
                }

                definition.editorColor = color;
            }

            if (definition.mass <= 0f)
            {
                definition.mass = 1f;
            }

            if (definition.impactBreakThreshold <= 0f)
            {
                definition.impactBreakThreshold = 3.5f;
            }

            if (definition.shapeRole == BlockShapeRole.TopOrnament || definition.shapeRole == BlockShapeRole.FragileProp)
            {
                definition.canSupportBlocksAbove = false;
            }

            bool tallPillar = definition.sizeInCells.y > 1
                && definition.sizeInCells.x == 1
                && definition.sizeInCells.z == 1;
            if (tallPillar
                && definition.shapeRole != BlockShapeRole.FoundationTable
                && definition.shapeRole != BlockShapeRole.FragileProp
                && definition.shapeRole != BlockShapeRole.TopOrnament
                && definition.shapeRole != BlockShapeRole.VerticalPillar)
            {
                definition.shapeRole = BlockShapeRole.VerticalPillar;
            }

            return definition;
        }

        private void WarnDuplicateIds()
        {
            int count = runtimeBlocks.Count;
            if (count < 2)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                int id = runtimeBlocks[i].blockId;
                for (int j = i + 1; j < count; j++)
                {
                    if (runtimeBlocks[j].blockId != id)
                    {
                        continue;
                    }

                    return;
                }
            }
        }
    }
}
