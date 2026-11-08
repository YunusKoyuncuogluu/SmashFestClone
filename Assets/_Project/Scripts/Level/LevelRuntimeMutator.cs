using SmashFast.Data;

namespace SmashFast.Level
{
    public class LevelRuntimeMutator
    {
        public float PickExtraYaw(System.Random random)
        {
            int pick = random.Next(0, 6);
            if (pick == 0)
            {
                return 25f;
            }

            if (pick == 1)
            {
                return -25f;
            }

            if (pick == 2)
            {
                return 35f;
            }

            if (pick == 3)
            {
                return -35f;
            }

            if (pick == 4)
            {
                return 45f;
            }

            return -45f;
        }

        public int ResolveMaxAmmo(LevelData levelData, int displayLevelNumber, int cycleIndex)
        {
            int authored = levelData.MaxAmmo;
            if (cycleIndex < 1)
            {
                return authored;
            }

            int par = levelData.ParAmmo;
            if (par <= 0)
            {
                par = authored > 4 ? authored / 2 : 1;
            }

            int floor = par + 1;
            if (floor < 1)
            {
                floor = 1;
            }

            if (floor > authored)
            {
                floor = authored;
            }

            int ammo = authored - cycleIndex;
            if (IsBossLevel(displayLevelNumber))
            {
                ammo -= 1;
            }
            else if (IsReliefLevel(displayLevelNumber))
            {
                ammo += 1;
            }

            if (ammo < floor)
            {
                ammo = floor;
            }

            if (ammo > authored)
            {
                ammo = authored;
            }

            return ammo;
        }

        public BlockDefinition ResolveBlock(
            BlockDefinition source,
            BlockCatalogSO catalog,
            System.Random random,
            int displayLevelNumber,
            int cycleIndex,
            int gridY)
        {
            if (cycleIndex < 1 || catalog == null || random == null)
            {
                return source;
            }

            if (source.materialType != BlockMaterialType.Wood)
            {
                return source;
            }

            if (source.isKinematicBase || source.shapeRole != BlockShapeRole.StandardBlock)
            {
                return source;
            }

            float glassChance;
            float stoneChance;
            ResolveMaterialChances(displayLevelNumber, cycleIndex, gridY, out glassChance, out stoneChance);
            float roll = (float)random.NextDouble();
            if (roll < glassChance)
            {
                BlockDefinition glassBlock;
                if (catalog.TryGetFirstByMaterialAndSize(BlockMaterialType.Glass, source.sizeInCells, out glassBlock))
                {
                    return glassBlock;
                }
            }

            if (roll < glassChance + stoneChance)
            {
                BlockDefinition stoneBlock;
                if (catalog.TryGetFirstByMaterialAndSize(BlockMaterialType.Stone, source.sizeInCells, out stoneBlock))
                {
                    return stoneBlock;
                }
            }

            return source;
        }

        public bool IsBossLevel(int displayLevelNumber)
        {
            if (displayLevelNumber <= 0)
            {
                return false;
            }

            return displayLevelNumber % 10 == 0;
        }

        public bool IsReliefLevel(int displayLevelNumber)
        {
            if (displayLevelNumber <= 0 || IsBossLevel(displayLevelNumber))
            {
                return false;
            }

            return displayLevelNumber % 5 == 0;
        }

        private void ResolveMaterialChances(
            int displayLevelNumber,
            int cycleIndex,
            int gridY,
            out float glassChance,
            out float stoneChance)
        {
            float cycle = cycleIndex < 1 ? 1f : cycleIndex;
            glassChance = 0.08f + (cycle * 0.03f);
            stoneChance = 0.14f + (cycle * 0.07f);
            if (IsBossLevel(displayLevelNumber))
            {
                stoneChance = 0.5f + (cycle * 0.05f);
                glassChance = 0.06f;
            }
            else if (IsReliefLevel(displayLevelNumber))
            {
                glassChance = 0.42f + (cycle * 0.04f);
                stoneChance = 0.08f;
                if (gridY <= 3)
                {
                    glassChance += 0.15f;
                }
            }
            else if (gridY <= 2)
            {
                glassChance += 0.05f;
            }

            if (glassChance > 0.75f)
            {
                glassChance = 0.75f;
            }

            if (stoneChance > 0.8f)
            {
                stoneChance = 0.8f;
            }

            if (glassChance + stoneChance > 0.9f)
            {
                stoneChance = 0.9f - glassChance;
                if (stoneChance < 0f)
                {
                    stoneChance = 0f;
                }
            }
        }
    }
}
