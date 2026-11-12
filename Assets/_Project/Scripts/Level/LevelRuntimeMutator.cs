using SmashFast.Data;
using UnityEngine;

namespace SmashFast.Level
{
    public class LevelRuntimeMutator
    {
        private readonly float[] extraYaws = { 25f, -25f, 35f, -35f, 45f, -45f };

        public float PickExtraYaw(System.Random random) => extraYaws[random.Next(0, extraYaws.Length)];

        public int ResolveMaxAmmo(LevelData level, int levelNumber, int cycle)
        {
            int authored = level.MaxAmmo;
            if (cycle < 1)
                return authored;

            int par = level.ParAmmo;
            if (par <= 0)
                par = authored > 4 ? authored / 2 : 1;

            int ammo = authored - cycle;
            if (IsBossLevel(levelNumber))
                ammo--;
            else if (IsReliefLevel(levelNumber))
                ammo++;

            int minAmmo = Mathf.Min(Mathf.Max(1, par + 1), authored);
            return Mathf.Clamp(ammo, minAmmo, authored);
        }

        public BlockDefinition ResolveBlock(BlockDefinition source, BlockCatalogSO catalog, System.Random random, int levelNumber, int cycle, int gridY)
        {
            if (cycle < 1 || catalog == null || random == null)
                return source;

            bool isPlainWoodBlock = source.materialType == BlockMaterialType.Wood
                && !source.isKinematicBase
                && source.shapeRole == BlockShapeRole.StandardBlock;
            if (!isPlainWoodBlock)
                return source;

            GetMaterialChances(levelNumber, cycle, gridY, out float glassChance, out float stoneChance);
            float roll = (float)random.NextDouble();

            if (roll < glassChance && catalog.TryGetFirstByMaterialAndSize(BlockMaterialType.Glass, source.sizeInCells, out BlockDefinition glass))
                return glass;

            if (roll < glassChance + stoneChance && catalog.TryGetFirstByMaterialAndSize(BlockMaterialType.Stone, source.sizeInCells, out BlockDefinition stone))
                return stone;

            return source;
        }

        public bool IsBossLevel(int levelNumber) => levelNumber > 0 && levelNumber % 10 == 0;

        public bool IsReliefLevel(int levelNumber) => levelNumber > 0 && !IsBossLevel(levelNumber) && levelNumber % 5 == 0;

        private void GetMaterialChances(int levelNumber, int cycle, int gridY, out float glassChance, out float stoneChance)
        {
            float cycleFactor = Mathf.Max(1, cycle);
            glassChance = 0.08f + cycleFactor * 0.03f;
            stoneChance = 0.14f + cycleFactor * 0.07f;

            if (IsBossLevel(levelNumber))
            {
                stoneChance = 0.5f + cycleFactor * 0.05f;
                glassChance = 0.06f;
            }
            else if (IsReliefLevel(levelNumber))
            {
                glassChance = 0.42f + cycleFactor * 0.04f;
                stoneChance = 0.08f;
                if (gridY <= 3)
                    glassChance += 0.15f;
            }
            else if (gridY <= 2)
            {
                glassChance += 0.05f;
            }

            glassChance = Mathf.Min(glassChance, 0.75f);
            stoneChance = Mathf.Min(stoneChance, 0.8f);
            if (glassChance + stoneChance > 0.9f)
                stoneChance = Mathf.Max(0f, 0.9f - glassChance);
        }
    }
}
