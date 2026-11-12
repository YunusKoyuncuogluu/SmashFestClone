using SmashFast.Data;
using UnityEngine;

namespace SmashFast.Level
{
    public partial class LevelArchitect
    {
        private bool TryFindBestPlank(int width, out BlockDefinition definition)
        {
            if (blocks.plankFamily != 0 && TryFindBestPlank(width, blocks.plankFamily, out definition))
                return true;

            if (blocks.woodFamily != 0 && TryFindBestPlank(width, blocks.woodFamily, out definition))
                return true;

            return TryFindBestPlank(width, 0, out definition);
        }

        private bool TryFindBestPlank(int width, int familyKey, out BlockDefinition definition)
        {
            definition = default;
            bool found = false;
            int bestWidth = 0;
            int count = catalog.BlockCount;
            for (int i = 0; i < count; i++)
            {
                BlockDefinition candidate;
                if (!catalog.TryGetBlockByIndex(i, out candidate))
                    continue;

                if (candidate.shapeRole != BlockShapeRole.HorizontalPlank || candidate.prefab == null)
                    continue;

                if (familyKey != 0 && candidate.familyKey != familyKey)
                    continue;

                Vector3Int size = footprint.ClampSize(candidate.sizeInCells);
                if (size.y != 1 || size.z != 1 || size.x <= 1 || size.x > width)
                    continue;

                if (found && size.x <= bestWidth)
                    continue;

                bestWidth = size.x;
                definition = candidate;
                found = true;
            }

            return found;
        }

        private bool TryFindBestPillar(int preferredHeight, int familyKey, out BlockDefinition definition)
        {
            if (familyKey != 0 && TryFindPillarInFamily(preferredHeight, familyKey, out definition))
                return true;

            if (blocks.woodFamily != 0 && TryFindPillarInFamily(preferredHeight, blocks.woodFamily, out definition))
                return true;

            return TryFindPillarInFamily(preferredHeight, 0, out definition);
        }

        private bool TryFindPillarInFamily(int preferredHeight, int familyKey, out BlockDefinition definition)
        {
            definition = default;
            bool found = false;
            int bestHeight = 0;
            int count = catalog.BlockCount;
            for (int i = 0; i < count; i++)
            {
                BlockDefinition candidate;
                if (!catalog.TryGetBlockByIndex(i, out candidate))
                    continue;

                if (candidate.shapeRole != BlockShapeRole.VerticalPillar || candidate.prefab == null)
                    continue;

                if (familyKey != 0 && candidate.familyKey != familyKey)
                    continue;

                Vector3Int size = footprint.ClampSize(candidate.sizeInCells);
                if (size.x != 1 || size.z != 1 || size.y <= 1 || size.y > preferredHeight)
                    continue;

                if (found && size.y <= bestHeight)
                    continue;

                bestHeight = size.y;
                definition = candidate;
                found = true;
            }

            return found;
        }

        private int PickFamily(BlockMaterialType material, bool preferFirst)
        {
            familyBuffer.Clear();
            int count = catalog.BlockCount;
            for (int i = 0; i < count; i++)
            {
                BlockDefinition candidate;
                if (!catalog.TryGetBlockByIndex(i, out candidate))
                    continue;

                if (candidate.familyKey == 0 || candidate.materialType != material || candidate.prefab == null)
                    continue;

                if (candidate.shapeRole != BlockShapeRole.StandardBlock)
                    continue;

                if (!familyBuffer.Contains(candidate.familyKey))
                    familyBuffer.Add(candidate.familyKey);
            }

            if (familyBuffer.Count == 0)
                return 0;

            int pick = paletteRandom.Next(0, familyBuffer.Count);
            return preferFirst ? familyBuffer[0] : familyBuffer[pick];
        }

        private int PickPlankFamily(int index, int woodFamily, int stoneFamily, int glassFamily)
        {
            int roll = paletteRandom.Next(0, 10);
            if (index <= 8)
                return woodFamily;

            if (index <= 25)
                return roll >= 8 && glassFamily != 0 ? glassFamily : woodFamily;

            if (roll >= 8 && glassFamily != 0)
                return glassFamily;

            if (roll >= 5 && stoneFamily != 0)
                return stoneFamily;

            return woodFamily;
        }

        private bool TryFindUnit(BlockMaterialType material, int familyKey, out BlockDefinition definition)
        {
            if (familyKey != 0 && TryFindUnitInFamily(material, familyKey, out definition))
                return true;

            return TryFindUnitInFamily(material, 0, out definition);
        }

        private bool TryFindUnitInFamily(BlockMaterialType material, int familyKey, out BlockDefinition definition)
        {
            definition = default;
            bool found = false;
            int count = catalog.BlockCount;
            for (int i = 0; i < count; i++)
            {
                BlockDefinition candidate;
                if (!catalog.TryGetBlockByIndex(i, out candidate))
                    continue;

                if (candidate.materialType != material || candidate.shapeRole != BlockShapeRole.StandardBlock)
                    continue;

                if (candidate.prefab == null || !candidate.canSupportBlocksAbove)
                    continue;

                if (familyKey != 0 && candidate.familyKey != familyKey)
                    continue;

                if (footprint.ClampSize(candidate.sizeInCells) != Vector3Int.one)
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

        private bool TryFindProp(BlockShapeRole role, bool matchMaterial, BlockMaterialType material, out BlockDefinition definition)
        {
            definition = default;
            int matchCount = 0;
            int uprightCount = 0;
            int count = catalog.BlockCount;
            for (int i = 0; i < count; i++)
            {
                BlockDefinition candidate;
                if (!catalog.TryGetBlockByIndex(i, out candidate))
                    continue;

                if (candidate.shapeRole != role || candidate.prefab == null)
                    continue;

                if (matchMaterial && candidate.materialType != material)
                    continue;

                if (footprint.ClampSize(candidate.sizeInCells) != Vector3Int.one)
                    continue;

                matchCount++;
                if (candidate.meshEuler == Vector3.zero)
                    uprightCount++;
            }

            if (matchCount == 0)
                return false;

            int targetIndex = random.Next(0, uprightCount > 0 ? uprightCount : matchCount);
            int seen = 0;
            for (int i = 0; i < count; i++)
            {
                BlockDefinition candidate;
                if (!catalog.TryGetBlockByIndex(i, out candidate))
                    continue;

                if (candidate.shapeRole != role || candidate.prefab == null)
                    continue;

                if (matchMaterial && candidate.materialType != material)
                    continue;

                if (footprint.ClampSize(candidate.sizeInCells) != Vector3Int.one)
                    continue;

                bool uprightOnly = uprightCount > 0;
                if (uprightOnly && candidate.meshEuler != Vector3.zero)
                    continue;

                if (seen == targetIndex)
                {
                    definition = candidate;
                    return true;
                }

                seen++;
            }

            return false;
        }

        private bool TryFindTallProp(BlockDefinition unitProp, int height, out BlockDefinition definition)
        {
            definition = default;
            if (height < 2 || unitProp.familyKey == 0 || unitProp.shapeRole != BlockShapeRole.FragileProp)
                return false;

            Vector3Int wanted = new Vector3Int(1, height, 1);
            int count = catalog.BlockCount;
            for (int i = 0; i < count; i++)
            {
                BlockDefinition candidate;
                if (!catalog.TryGetBlockByIndex(i, out candidate))
                    continue;

                if (candidate.familyKey != unitProp.familyKey || candidate.shapeRole != BlockShapeRole.FragileProp)
                    continue;

                if (candidate.prefab == null || candidate.meshEuler != Vector3.zero)
                    continue;

                if (footprint.ClampSize(candidate.sizeInCells) != wanted)
                    continue;

                definition = candidate;
                return true;
            }

            return false;
        }

        private bool IsFoundation(BlockDefinition definition)
        {
            if (definition.shapeRole == BlockShapeRole.FoundationTable)
                return true;

            return definition.materialType == BlockMaterialType.TableFoundation;
        }

        private bool TryFindFoundation(bool smallest, out BlockDefinition definition)
        {
            bool found = false;
            definition = default;
            int bestArea = smallest ? int.MaxValue : -1;
            int count = catalog.BlockCount;
            for (int i = 0; i < count; i++)
            {
                BlockDefinition candidate;
                if (!catalog.TryGetBlockByIndex(i, out candidate))
                    continue;

                if (!IsFoundation(candidate))
                    continue;

                Vector3Int size = footprint.ClampSize(candidate.sizeInCells);
                int area = size.x * size.z;
                if (found)
                {
                    bool better = smallest ? area < bestArea : area > bestArea;
                    if (!better)
                        continue;
                }

                bestArea = area;
                definition = candidate;
                found = true;
            }

            return found;
        }
    }
}
