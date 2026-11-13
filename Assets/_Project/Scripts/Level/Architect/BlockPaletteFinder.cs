using System.Collections.Generic;
using SmashFest.Data;
using UnityEngine;

namespace SmashFest.Level
{
    public class BlockPaletteFinder
    {
        private const int StoneColumnChance = 55;

        private readonly LevelBuildContext context;
        private readonly List<int> familyBuffer = new List<int>(8);

        public BlockPaletteFinder(LevelBuildContext context) => this.context = context;

        private BlockCatalogSO Catalog => context.Catalog;
        private BlockFootprint Footprint => context.Footprint;
        private BlockSet Blocks => context.Blocks;

        public bool TryLoad(int index)
        {
            BlockSet blocks = new BlockSet();
            context.Blocks = blocks;
            int woodFamily = PickFamily(BlockMaterialType.Wood, index <= 3);
            int stoneFamily = PickFamily(BlockMaterialType.Stone, false);
            int glassFamily = PickFamily(BlockMaterialType.Glass, false);
            blocks.woodFamily = woodFamily;
            blocks.plankFamily = PickPlankFamily(index, woodFamily, stoneFamily, glassFamily);
            blocks.stoneColumns = context.PaletteRandom.Next(0, 100) < StoneColumnChance;

            bool hasWood = TryFindUnit(BlockMaterialType.Wood, woodFamily, out BlockDefinition wood);
            blocks.hasStone = TryFindUnit(BlockMaterialType.Stone, stoneFamily, out BlockDefinition stone);
            blocks.hasGlass = TryFindUnit(BlockMaterialType.Glass, glassFamily, out BlockDefinition glass);
            bool hasJar = TryFindProp(BlockShapeRole.FragileProp, true, BlockMaterialType.CeramicJar, out BlockDefinition jar);
            if (!hasJar)
                hasJar = TryFindProp(BlockShapeRole.FragileProp, false, BlockMaterialType.CeramicJar, out jar);

            blocks.hasPlank = TryFindBestPlank(3, out BlockDefinition plank);
            blocks.hasOrnament = TryFindProp(BlockShapeRole.TopOrnament, false, BlockMaterialType.CeramicJar, out BlockDefinition ornament);
            blocks.hasPillar = TryFindBestPillar(2, woodFamily, out BlockDefinition pillar);

            if (!hasWood && !blocks.hasStone && !blocks.hasGlass && !blocks.hasPillar && !blocks.hasPlank)
                return false;

            if (!hasWood)
            {
                if (blocks.hasPillar)
                    wood = pillar;
                else if (blocks.hasPlank)
                    wood = plank;
                else
                    wood = blocks.hasGlass ? glass : stone;
            }

            if (!hasJar)
                jar = blocks.hasGlass ? glass : wood;

            if (!blocks.hasGlass)
                glass = wood;

            if (!blocks.hasStone)
                stone = wood;

            blocks.wood = wood;
            blocks.stone = stone;
            blocks.glass = glass;
            blocks.jar = jar;
            blocks.plank = plank;
            blocks.ornament = ornament;
            blocks.pillar = pillar;
            return true;
        }

        public bool TryFindBestPlank(int width, out BlockDefinition definition)
        {
            if (Blocks.plankFamily != 0 && TryFindBestPlank(width, Blocks.plankFamily, out definition))
                return true;

            if (Blocks.woodFamily != 0 && TryFindBestPlank(width, Blocks.woodFamily, out definition))
                return true;

            return TryFindBestPlank(width, 0, out definition);
        }

        public bool TryFindBestPillar(int preferredHeight, int familyKey, out BlockDefinition definition)
        {
            if (familyKey != 0 && TryFindPillarInFamily(preferredHeight, familyKey, out definition))
                return true;

            if (Blocks.woodFamily != 0 && TryFindPillarInFamily(preferredHeight, Blocks.woodFamily, out definition))
                return true;

            return TryFindPillarInFamily(preferredHeight, 0, out definition);
        }

        public bool TryFindTallProp(BlockDefinition unitProp, int height, out BlockDefinition definition)
        {
            definition = default;
            if (height < 2 || unitProp.familyKey == 0 || unitProp.shapeRole != BlockShapeRole.FragileProp)
                return false;

            Vector3Int wanted = new Vector3Int(1, height, 1);
            int count = Catalog.BlockCount;
            for (int i = 0; i < count; i++)
            {
                if (!Catalog.TryGetBlockByIndex(i, out BlockDefinition candidate))
                    continue;

                if (candidate.familyKey != unitProp.familyKey || candidate.shapeRole != BlockShapeRole.FragileProp)
                    continue;

                if (candidate.prefab == null || candidate.meshEuler != Vector3.zero)
                    continue;

                if (Footprint.ClampSize(candidate.sizeInCells) != wanted)
                    continue;

                definition = candidate;
                return true;
            }

            return false;
        }

        public bool TryFindFoundation(bool smallest, out BlockDefinition definition)
        {
            bool found = false;
            definition = default;
            int bestArea = smallest ? int.MaxValue : -1;
            int count = Catalog.BlockCount;
            for (int i = 0; i < count; i++)
            {
                if (!Catalog.TryGetBlockByIndex(i, out BlockDefinition candidate))
                    continue;

                if (!candidate.IsTable())
                    continue;

                Vector3Int size = Footprint.ClampSize(candidate.sizeInCells);
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

        private bool TryFindBestPlank(int width, int familyKey, out BlockDefinition definition)
        {
            definition = default;
            bool found = false;
            int bestWidth = 0;
            int count = Catalog.BlockCount;
            for (int i = 0; i < count; i++)
            {
                if (!Catalog.TryGetBlockByIndex(i, out BlockDefinition candidate))
                    continue;

                if (candidate.shapeRole != BlockShapeRole.HorizontalPlank || candidate.prefab == null)
                    continue;

                if (familyKey != 0 && candidate.familyKey != familyKey)
                    continue;

                Vector3Int size = Footprint.ClampSize(candidate.sizeInCells);
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

        private bool TryFindPillarInFamily(int preferredHeight, int familyKey, out BlockDefinition definition)
        {
            definition = default;
            bool found = false;
            int bestHeight = 0;
            int count = Catalog.BlockCount;
            for (int i = 0; i < count; i++)
            {
                if (!Catalog.TryGetBlockByIndex(i, out BlockDefinition candidate))
                    continue;

                if (candidate.shapeRole != BlockShapeRole.VerticalPillar || candidate.prefab == null)
                    continue;

                if (familyKey != 0 && candidate.familyKey != familyKey)
                    continue;

                Vector3Int size = Footprint.ClampSize(candidate.sizeInCells);
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
            int count = Catalog.BlockCount;
            for (int i = 0; i < count; i++)
            {
                if (!Catalog.TryGetBlockByIndex(i, out BlockDefinition candidate))
                    continue;

                if (candidate.familyKey == 0 || !candidate.MatchesBuildMaterial(material) || candidate.prefab == null)
                    continue;

                if (candidate.shapeRole != BlockShapeRole.StandardBlock)
                    continue;

                if (!familyBuffer.Contains(candidate.familyKey))
                    familyBuffer.Add(candidate.familyKey);
            }

            if (familyBuffer.Count == 0)
                return 0;

            int pick = context.PaletteRandom.Next(0, familyBuffer.Count);
            return preferFirst ? familyBuffer[0] : familyBuffer[pick];
        }

        private int PickPlankFamily(int index, int woodFamily, int stoneFamily, int glassFamily)
        {
            int roll = context.PaletteRandom.Next(0, 10);
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
            int count = Catalog.BlockCount;
            for (int i = 0; i < count; i++)
            {
                if (!Catalog.TryGetBlockByIndex(i, out BlockDefinition candidate))
                    continue;

                if (!candidate.MatchesBuildMaterial(material) || candidate.shapeRole != BlockShapeRole.StandardBlock)
                    continue;

                if (candidate.prefab == null || !candidate.canSupportBlocksAbove)
                    continue;

                if (familyKey != 0 && candidate.familyKey != familyKey)
                    continue;

                if (Footprint.ClampSize(candidate.sizeInCells) != Vector3Int.one)
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
            int count = Catalog.BlockCount;
            for (int i = 0; i < count; i++)
            {
                if (!IsPropCandidate(i, role, matchMaterial, material, out BlockDefinition candidate))
                    continue;

                matchCount++;
                if (candidate.meshEuler == Vector3.zero)
                    uprightCount++;
            }

            if (matchCount == 0)
                return false;

            int targetIndex = context.Random.Next(0, uprightCount > 0 ? uprightCount : matchCount);
            bool uprightOnly = uprightCount > 0;
            int seen = 0;
            for (int i = 0; i < count; i++)
            {
                if (!IsPropCandidate(i, role, matchMaterial, material, out BlockDefinition candidate))
                    continue;

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

        private bool IsPropCandidate(int index, BlockShapeRole role, bool matchMaterial, BlockMaterialType material, out BlockDefinition candidate)
        {
            if (!Catalog.TryGetBlockByIndex(index, out candidate))
                return false;

            if (candidate.shapeRole != role || candidate.prefab == null)
                return false;

            if (matchMaterial && candidate.materialType != material)
                return false;

            return Footprint.ClampSize(candidate.sizeInCells) == Vector3Int.one;
        }
    }
}
