using SmashFest.Data;
using UnityEngine;

namespace SmashFest.Level
{
    public class LevelRuntimeMutator
    {
        public const int BonusInterval = 10;

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

            GetMaterialChances(cycle, gridY, out float glassChance, out float stoneChance);
            float roll = (float)random.NextDouble();

            if (roll < glassChance && catalog.TryGetFirstByMaterialAndSize(BlockMaterialType.Glass, source.sizeInCells, out BlockDefinition glass))
                return glass;

            if (roll < glassChance + stoneChance && catalog.TryGetFirstByMaterialAndSize(BlockMaterialType.Stone, source.sizeInCells, out BlockDefinition stone))
                return stone;

            return source;
        }

        public bool IsBonusLevel(int levelNumber) => levelNumber > 0 && levelNumber % BonusInterval == 0;

        public BlockDefinition ToBonusBlock(BlockDefinition source, BlockCatalogSO catalog, int levelNumber, Vector3Int gridPosition)
        {
            if (!source.IsTarget())
                return source;

            int seed = unchecked(levelNumber * 7919 + gridPosition.x * 104729 + gridPosition.y * 1299709 + gridPosition.z * 15485863);
            if (catalog.TryPickByMaterialAndSize(BlockMaterialType.CeramicJar, source.sizeInCells, seed, out BlockDefinition jar))
                return jar;

            if (source.materialType == BlockMaterialType.Glass || source.materialType == BlockMaterialType.CeramicJar)
                return source;

            return catalog.TryGetFirstByMaterialAndSize(BlockMaterialType.Glass, source.sizeInCells, out BlockDefinition glass) ? glass : source;
        }

        private void GetMaterialChances(int cycle, int gridY, out float glassChance, out float stoneChance)
        {
            float cycleFactor = Mathf.Max(1, cycle);
            glassChance = 0.08f + cycleFactor * 0.03f;
            stoneChance = 0.14f + cycleFactor * 0.07f;
            if (gridY <= 2)
                glassChance += 0.05f;

            glassChance = Mathf.Min(glassChance, 0.75f);
            stoneChance = Mathf.Min(stoneChance, 0.8f);
            if (glassChance + stoneChance > 0.9f)
                stoneChance = Mathf.Max(0f, 0.9f - glassChance);
        }
    }
}
