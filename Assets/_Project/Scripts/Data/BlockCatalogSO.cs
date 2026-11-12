using System;
using System.Collections.Generic;
using UnityEngine;

namespace SmashFast.Data
{
    [CreateAssetMenu(fileName = "BlockCatalog", menuName = "SmashFast/Block Catalog", order = 1)]
    public class BlockCatalogSO : ScriptableObject
    {
        public const float MinGlobalBlockScale = 0.2f;
        public const float MaxGlobalBlockScale = 1.5f;

        private const float DefaultBreakThreshold = 3.5f;

        [Range(MinGlobalBlockScale, MaxGlobalBlockScale)] public float globalBlockScale = 0.65f;
        [Range(0.05f, 1f)] public float blockMassMultiplier = 0.25f;
        [Min(0.1f)] public float tableMinHeight = 0.65f;

        [SerializeField] private List<BlockFamilyDefinition> families = new List<BlockFamilyDefinition>(4);
        [SerializeField] private List<BlockDefinition> standaloneBlocks = new List<BlockDefinition>(8);

        [NonSerialized] private readonly List<BlockDefinition> blocks = new List<BlockDefinition>(64);
        [NonSerialized] private readonly Dictionary<int, int> indexById = new Dictionary<int, int>(64);
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

        public IReadOnlyList<BlockDefinition> GetAllBlocks()
        {
            BuildBlocks();
            return blocks;
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
                if (candidate.prefab != null && IsTable(candidate))
                {
                    definition = candidate;
                    return true;
                }
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

                if (!found)
                {
                    definition = candidate;
                    found = true;
                }
            }

            return found;
        }

        public void InvalidateRuntimeBlocks() => isBuilt = false;

        public void RebuildExpandedBlocks()
        {
            InvalidateRuntimeBlocks();
            BuildBlocks();
        }

        public int ResolveFamilyBlockId(BlockFamilyDefinition family, int length, int axis)
        {
            if (family != null && family.baseBlockId > 0)
                return family.baseBlockId + axis * 10 + length;

            return GetLegacyId(length, axis);
        }

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

            blocks.Clear();

            if (families != null)
            {
                for (int i = 0; i < families.Count; i++)
                {
                    BlockFamilyDefinition family = families[i];
                    if (family == null)
                        continue;

                    family.ApplyNamingRules();
                    AddFamilyVariants(family, i + 1);
                }
            }

            if (standaloneBlocks != null)
            {
                foreach (BlockDefinition source in standaloneBlocks)
                {
                    BlockDefinition block = Sanitize(source);
                    if (block.prefab == null)
                        continue;

                    if (IsTable(block))
                        MakeTable(ref block);

                    blocks.Add(block);
                }
            }

            indexById.Clear();
            for (int i = 0; i < blocks.Count; i++)
                indexById.TryAdd(blocks[i].blockId, i);

            isBuilt = true;
        }

        private void MakeTable(ref BlockDefinition block)
        {
            block.blockName = "Table";
            if (block.sizeInCells.x < 1 || block.sizeInCells.y < 1 || block.sizeInCells.z < 1)
                block.sizeInCells = new Vector3Int(4, 1, 2);

            block.isKinematicBase = true;
            block.canSupportBlocksAbove = true;
            block.materialType = BlockMaterialType.TableFoundation;
            block.shapeRole = BlockShapeRole.FoundationTable;
        }

        private void AddFamilyVariants(BlockFamilyDefinition family, int familyKey)
        {
            string familyName = string.IsNullOrEmpty(family.familyName) ? "Family" : family.familyName;
            if (family.foundationTable)
            {
                if (family.length1Prefab == null)
                    return;

                BlockDefinition table = CreateTable(family);
                table.familyKey = familyKey;
                blocks.Add(Sanitize(table));
                return;
            }

            for (int length = 1; length <= BlockFamilyDefinition.MaxLength; length++)
            {
                GameObject prefab = family.GetLengthPrefab(length);
                if (prefab == null)
                    continue;

                for (int axis = 0; axis < 3; axis++)
                {
                    if (family.onlyVerticalAxis && axis != 1 && length > 1)
                        continue;

                    BlockDefinition variant = CreateVariant(family, familyName, prefab, length, axis);
                    variant.familyKey = familyKey;
                    blocks.Add(Sanitize(variant));
                }
            }
        }

        private BlockDefinition CreateTable(BlockFamilyDefinition family)
        {
            return new BlockDefinition
            {
                blockId = family.baseBlockId > 0 ? family.baseBlockId : ResolveFamilyBlockId(family, 4, 0),
                blockName = "Table",
                prefab = family.length1Prefab,
                baseScale = Vector3.one,
                meshEuler = Vector3.zero,
                sizeInCells = new Vector3Int(4, 1, 2),
                editorColor = family.yColor,
                isKinematicBase = true,
                mass = family.baseMass,
                materialType = BlockMaterialType.TableFoundation,
                shapeRole = BlockShapeRole.FoundationTable,
                canSupportBlocksAbove = true,
                impactBreakThreshold = 99f
            };
        }

        private BlockDefinition CreateVariant(BlockFamilyDefinition family, string familyName, GameObject prefab, int length, int axis)
        {
            BlockShapeRole longRole = axis == 1 ? BlockShapeRole.VerticalPillar : BlockShapeRole.HorizontalPlank;
            BlockDefinition definition = new BlockDefinition
            {
                blockId = ResolveFamilyBlockId(family, length, axis),
                prefab = prefab,
                baseScale = Vector3.one,
                mass = family.baseMass + family.massPerExtraLength * (length - 1),
                materialType = family.materialType,
                canSupportBlocksAbove = !family.fragileProp,
                impactBreakThreshold = family.impactBreakThreshold > 0f ? family.impactBreakThreshold : DefaultBreakThreshold,
                isKinematicBase = false,
                shapeRole = length > 1 ? longRole : BlockShapeRole.StandardBlock
            };

            switch (axis)
            {
                case 0:
                    definition.blockName = $"{familyName}_{length}x";
                    definition.meshEuler = new Vector3(0f, 0f, 90f);
                    definition.sizeInCells = new Vector3Int(length, 1, 1);
                    definition.editorColor = family.xColor;
                    break;
                case 1:
                    definition.blockName = $"{familyName}_{length}y";
                    definition.meshEuler = Vector3.zero;
                    definition.sizeInCells = new Vector3Int(1, length, 1);
                    definition.editorColor = family.yColor;
                    break;
                default:
                    definition.blockName = $"{familyName}_{length}z";
                    definition.meshEuler = new Vector3(-90f, 0f, 0f);
                    definition.sizeInCells = new Vector3Int(1, 1, length);
                    definition.editorColor = family.zColor;
                    break;
            }

            if (family.onlyVerticalAxis && length == 1)
            {
                definition.meshEuler = Vector3.zero;
                definition.sizeInCells = Vector3Int.one;
                definition.shapeRole = BlockShapeRole.StandardBlock;
            }

            if (family.fragileProp)
                definition.shapeRole = BlockShapeRole.FragileProp;

            return definition;
        }

        private int GetLegacyId(int length, int axis)
        {
            return axis switch
            {
                0 => length == 1 ? 14 : length,
                1 => length == 1 ? 1 : length + 4,
                _ => length == 1 ? 15 : length + 8
            };
        }

        private BlockDefinition Sanitize(BlockDefinition definition)
        {
            if (string.IsNullOrEmpty(definition.blockName))
                definition.blockName = $"Block {definition.blockId}";

            if (definition.baseScale == Vector3.zero)
                definition.baseScale = Vector3.one;

            definition.sizeInCells = footprint.ClampSize(definition.sizeInCells);

            if (definition.editorColor.a <= 0f)
            {
                Color color = definition.editorColor;
                if (color.maxColorComponent <= 0f)
                    color = new Color(0.25f, 0.8f, 0.95f, 1f);
                else
                    color.a = 1f;

                definition.editorColor = color;
            }

            if (definition.mass <= 0f)
                definition.mass = 1f;

            if (definition.impactBreakThreshold <= 0f)
                definition.impactBreakThreshold = DefaultBreakThreshold;

            BlockShapeRole role = definition.shapeRole;
            if (role == BlockShapeRole.TopOrnament || role == BlockShapeRole.FragileProp)
                definition.canSupportBlocksAbove = false;

            Vector3Int size = definition.sizeInCells;
            bool isTallPillar = size.y > 1 && size.x == 1 && size.z == 1;
            bool keepsRole = role == BlockShapeRole.FoundationTable
                || role == BlockShapeRole.FragileProp
                || role == BlockShapeRole.TopOrnament;
            if (isTallPillar && !keepsRole)
                definition.shapeRole = BlockShapeRole.VerticalPillar;

            return definition;
        }

        private bool IsTable(BlockDefinition block)
        {
            return block.shapeRole == BlockShapeRole.FoundationTable
                || block.materialType == BlockMaterialType.TableFoundation;
        }

        private bool IsStructural(BlockShapeRole role)
        {
            return role == BlockShapeRole.StandardBlock
                || role == BlockShapeRole.HorizontalPlank
                || role == BlockShapeRole.VerticalPillar;
        }
    }
}
