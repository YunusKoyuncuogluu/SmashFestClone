using System.Collections.Generic;
using UnityEngine;

namespace SmashFest.Data
{
    public class BlockVariantBuilder
    {
        private const string TableName = "Table";

        private readonly BlockFootprint footprint = new BlockFootprint();

        public void Build(List<BlockFamilyDefinition> families, List<BlockDefinition> standaloneBlocks, List<BlockDefinition> output)
        {
            output.Clear();
            if (families != null)
            {
                for (int i = 0; i < families.Count; i++)
                {
                    BlockFamilyDefinition family = families[i];
                    if (family == null)
                        continue;

                    family.ApplyNamingRules();
                    AddFamilyVariants(family, i + 1, output);
                }
            }

            if (standaloneBlocks == null)
                return;

            foreach (BlockDefinition source in standaloneBlocks)
            {
                BlockDefinition block = Sanitize(source);
                if (block.prefab == null)
                    continue;

                if (block.IsTable())
                    MakeTable(ref block);

                output.Add(block);
            }
        }

        public int ResolveFamilyBlockId(BlockFamilyDefinition family, int length, int axis)
        {
            if (family != null && family.baseBlockId > 0)
                return family.baseBlockId + axis * 10 + length;

            return axis switch
            {
                0 => length == 1 ? 14 : length,
                1 => length == 1 ? 1 : length + 4,
                _ => length == 1 ? 15 : length + 8
            };
        }

        private void AddFamilyVariants(BlockFamilyDefinition family, int familyKey, List<BlockDefinition> output)
        {
            if (family.foundationTable)
            {
                if (family.length1Prefab == null)
                    return;

                BlockDefinition table = CreateTable(family);
                table.familyKey = familyKey;
                output.Add(Sanitize(table));
                return;
            }

            string familyName = string.IsNullOrEmpty(family.familyName) ? "Family" : family.familyName;
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
                    output.Add(Sanitize(variant));
                }
            }
        }

        private void MakeTable(ref BlockDefinition block)
        {
            block.blockName = TableName;
            if (block.sizeInCells.x < 1 || block.sizeInCells.y < 1 || block.sizeInCells.z < 1)
                block.sizeInCells = new Vector3Int(4, 1, 2);

            block.isKinematicBase = true;
            block.canSupportBlocksAbove = true;
            block.materialType = BlockMaterialType.TableFoundation;
            block.shapeRole = BlockShapeRole.FoundationTable;
        }

        private BlockDefinition CreateTable(BlockFamilyDefinition family)
        {
            return new BlockDefinition
            {
                blockId = family.baseBlockId > 0 ? family.baseBlockId : ResolveFamilyBlockId(family, 4, 0),
                blockName = TableName,
                prefab = family.length1Prefab,
                baseScale = Vector3.one,
                meshEuler = Vector3.zero,
                sizeInCells = new Vector3Int(4, 1, 2),
                editorColor = family.yColor,
                isKinematicBase = true,
                mass = family.baseMass,
                materialType = BlockMaterialType.TableFoundation,
                shapeRole = BlockShapeRole.FoundationTable,
                canSupportBlocksAbove = true
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

        private BlockDefinition Sanitize(BlockDefinition definition)
        {
            if (string.IsNullOrEmpty(definition.blockName))
                definition.blockName = $"Block {definition.blockId}";

            if (definition.baseScale == Vector3.zero)
                definition.baseScale = Vector3.one;

            definition.sizeInCells = footprint.ClampSize(definition.sizeInCells);
            if (definition.editorColor.a <= 0f)
                definition.editorColor = definition.editorColor.maxColorComponent <= 0f ? new Color(0.25f, 0.8f, 0.95f, 1f) : new Color(definition.editorColor.r, definition.editorColor.g, definition.editorColor.b, 1f);

            if (definition.mass <= 0f)
                definition.mass = 1f;

            BlockShapeRole role = definition.shapeRole;
            if (role == BlockShapeRole.TopOrnament || role == BlockShapeRole.FragileProp)
                definition.canSupportBlocksAbove = false;

            Vector3Int size = definition.sizeInCells;
            bool isTallPillar = size.y > 1 && size.x == 1 && size.z == 1;
            bool keepsRole = role == BlockShapeRole.FoundationTable || role == BlockShapeRole.FragileProp || role == BlockShapeRole.TopOrnament;
            if (isTallPillar && !keepsRole)
                definition.shapeRole = BlockShapeRole.VerticalPillar;

            return definition;
        }
    }
}
