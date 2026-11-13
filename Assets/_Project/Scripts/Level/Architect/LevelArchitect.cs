using SmashFest.Data;
using UnityEngine;

namespace SmashFest.Level
{
    public class LevelArchitect
    {
        private const float MinAmmoPerTarget = 0.4f;

        private readonly LevelBuildContext context = new LevelBuildContext();
        private readonly BlockPaletteFinder palette;
        private readonly StructureStabilizer stabilizer;
        private readonly TableLayoutComposer composer;

        public LevelArchitect()
        {
            palette = new BlockPaletteFinder(context);
            stabilizer = new StructureStabilizer(context);
            BlockPlacer placer = new BlockPlacer(context, stabilizer);
            StructureModules modules = new StructureModules(context, placer, palette);
            composer = new TableLayoutComposer(context, placer, modules, palette);
        }

        public int LastAnchorLayer => context.LastAnchorLayer;

        public string GenerateForLevelIndex(LevelData levelData, BlockCatalogSO blockCatalog, int levelIndex)
        {
            return Build(levelData, blockCatalog, levelIndex, ShapeKind.Auto, 0);
        }

        public string ApplyPreset(LevelData levelData, BlockCatalogSO blockCatalog, int presetIndex, int layerZ)
        {
            int index = levelData != null && levelData.LevelIndex > 0 ? levelData.LevelIndex : 1;
            return Build(levelData, blockCatalog, index, PresetShape(presetIndex), layerZ);
        }

        public LevelData GenerateRuntimeLevel(int displayLevelNumber, BlockCatalogSO blockCatalog, LevelData gridSource)
        {
            if (blockCatalog == null)
                return null;

            LevelData runtimeLevel = new LevelData();
            if (gridSource != null)
            {
                runtimeLevel.GridDimensions = gridSource.GridDimensions;
                runtimeLevel.CellSize = gridSource.CellSize;
                runtimeLevel.OriginOffset = gridSource.OriginOffset;
                runtimeLevel.FallEliminationY = gridSource.FallEliminationY;
            }

            GenerateForLevelIndex(runtimeLevel, blockCatalog, displayLevelNumber);
            return runtimeLevel;
        }

        private ShapeKind PresetShape(int presetIndex)
        {
            switch (presetIndex)
            {
                case 1:
                    return ShapeKind.Angled;
                case 2:
                    return ShapeKind.GlassWeak;
                case 3:
                    return ShapeKind.Shelf;
                case 4:
                    return ShapeKind.Twin;
                default:
                    return ShapeKind.Straight;
            }
        }

        private string Build(LevelData level, BlockCatalogSO catalog, int levelIndex, ShapeKind shape, int layerZ)
        {
            if (level == null || catalog == null)
                return "Select a level and a catalog first.";

            int index = levelIndex < 1 ? 1 : levelIndex;
            context.Begin(level, catalog, index, layerZ);
            if (!palette.TryLoad(index))
                return "Catalog needs a standard block.";

            level.GridDimensions = GridFor(index);
            level.ClearPlacements();
            context.BlockBudget = BudgetFor(index);
            string message = shape == ShapeKind.Auto ? composer.ComposeAuto(index) : composer.ComposePreset(index, shape);
            if (level.PlacementCount <= 0)
                message = composer.ComposeFallback(index);

            stabilizer.Stabilize();
            ApplyAmmoRules(level, catalog, index);
            if (level.PlacementCount <= 0)
                return "Level " + index.ToString() + " failed to place any blocks.";

            return message;
        }

        private Vector3Int GridFor(int index)
        {
            if (index <= 25)
                return new Vector3Int(8, 8, 3);

            if (index <= 100)
                return new Vector3Int(10, 10, 4);

            if (index <= 400)
                return new Vector3Int(12, 12, 5);

            return new Vector3Int(16, 14, 6);
        }

        private int BudgetFor(int index)
        {
            if (index <= 20)
                return 18;

            if (index <= 80)
                return 40;

            if (index <= 300)
                return 75;

            return 120;
        }

        private void ApplyAmmoRules(LevelData level, BlockCatalogSO catalog, int levelIndex)
        {
            level.LevelIndex = levelIndex;
            int targets = 0;
            int count = level.PlacementCount;
            for (int i = 0; i < count; i++)
            {
                if (!level.TryGetPlacementByIndex(i, out BlockPlacementData placement))
                    continue;

                if (catalog.TryGetBlockDefinition(placement.blockId, out BlockDefinition definition) && definition.IsTarget())
                    targets++;
            }

            int par = Mathf.Max(1, targets / 3);
            int bonus = 4;
            if (levelIndex > 70)
                bonus = 1;
            else if (levelIndex > 25)
                bonus = 2;
            else if (levelIndex > 8)
                bonus = 3;

            level.ParAmmo = par;
            level.MaxAmmo = Mathf.Max(par + bonus, Mathf.CeilToInt(targets * MinAmmoPerTarget) + 1);
        }
    }
}
