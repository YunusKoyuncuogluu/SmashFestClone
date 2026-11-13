using System.Collections.Generic;
using SmashFest.Data;
using UnityEngine;

namespace SmashFest.Level
{
    public partial class LevelArchitect
    {
        private enum ShapeKind
        {
            Auto = 0,
            Straight = 1,
            Angled = 2,
            GlassWeak = 3,
            Shelf = 4,
            Twin = 5
        }

        private class BlockSet
        {
            public BlockDefinition wood;
            public BlockDefinition stone;
            public BlockDefinition glass;
            public BlockDefinition jar;
            public BlockDefinition plank;
            public BlockDefinition ornament;
            public BlockDefinition pillar;
            public bool hasPlank;
            public bool hasPillar;
            public bool hasStone;
            public bool hasGlass;
            public bool hasOrnament;
            public bool stoneColumns;
            public int woodFamily;
            public int plankFamily;

            public BlockDefinition Column(bool allowStone)
            {
                if (allowStone && hasStone && stoneColumns)
                    return stone;

                return wood;
            }

            public BlockDefinition Crown()
            {
                if (hasOrnament)
                    return ornament;

                return jar;
            }
        }

        private const int StoneColumnChance = 55;

        private readonly BlockFootprint footprint = new BlockFootprint();
        private readonly float[] yawSteps = new float[] { -45f, -30f, 0f, 30f, 45f };
        private readonly List<Vector3Int> anchorBuffer = new List<Vector3Int>(64);
        private readonly List<int> familyBuffer = new List<int>(8);
        private System.Random random = new System.Random(1);
        private System.Random paletteRandom = new System.Random(1);
        private LevelData level;
        private BlockCatalogSO catalog;
        private BlockSet blocks;
        private int preferredLayer;
        private int blockBudget;

        public int LastAnchorLayer { get; private set; }

        public string GenerateForLevelIndex(LevelData levelData, BlockCatalogSO blockCatalog, int levelIndex)
        {
            return Build(levelData, blockCatalog, levelIndex, ShapeKind.Auto, 0);
        }

        public string ApplyPreset(LevelData levelData, BlockCatalogSO blockCatalog, int presetIndex, int layerZ)
        {
            ShapeKind shape = ShapeKind.Straight;
            if (presetIndex == 1)
                shape = ShapeKind.Angled;
            else if (presetIndex == 2)
                shape = ShapeKind.GlassWeak;
            else if (presetIndex == 3)
                shape = ShapeKind.Shelf;
            else if (presetIndex == 4)
                shape = ShapeKind.Twin;

            int index = 1;
            if (levelData != null && levelData.LevelIndex > 0)
                index = levelData.LevelIndex;

            return Build(levelData, blockCatalog, index, shape, layerZ);
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

        private string Build(LevelData sourceLevel, BlockCatalogSO sourceCatalog, int levelIndex, ShapeKind shape, int layerZ)
        {
            if (sourceLevel == null || sourceCatalog == null)
                return "Select a level and a catalog first.";

            level = sourceLevel;
            catalog = sourceCatalog;
            int index = levelIndex < 1 ? 1 : levelIndex;
            random = new System.Random(index);
            paletteRandom = new System.Random((index * 7919) + 17);
            preferredLayer = layerZ < 0 ? 0 : layerZ;
            LastAnchorLayer = preferredLayer;
            if (!TryLoadBlocks(index))
                return "Catalog needs a standard block.";

            ApplyGridForLevel(index);
            level.ClearPlacements();
            blockBudget = BudgetFor(index);
            string message;
            if (shape == ShapeKind.Auto)
                message = ComposeAuto(index);
            else
                message = ComposePreset(index, shape);

            if (level.PlacementCount <= 0)
                message = ForceMinimalTableLevel(index);

            EnsureStructuralStability();
            ApplyRules(index);
            if (level.PlacementCount <= 0)
                return "Level " + index.ToString() + " failed to place any blocks.";

            return message;
        }

        private bool TryLoadBlocks(int index)
        {
            blocks = new BlockSet();
            int woodFamily = PickFamily(BlockMaterialType.Wood, index <= 3);
            int stoneFamily = PickFamily(BlockMaterialType.Stone, false);
            int glassFamily = PickFamily(BlockMaterialType.Glass, false);
            blocks.woodFamily = woodFamily;
            blocks.plankFamily = PickPlankFamily(index, woodFamily, stoneFamily, glassFamily);
            blocks.stoneColumns = paletteRandom.Next(0, 100) < StoneColumnChance;

            BlockDefinition wood;
            bool hasWood = TryFindUnit(BlockMaterialType.Wood, woodFamily, out wood);
            BlockDefinition stone;
            blocks.hasStone = TryFindUnit(BlockMaterialType.Stone, stoneFamily, out stone);
            BlockDefinition glass;
            blocks.hasGlass = TryFindUnit(BlockMaterialType.Glass, glassFamily, out glass);
            BlockDefinition jar;
            bool hasJar = TryFindProp(BlockShapeRole.FragileProp, true, BlockMaterialType.CeramicJar, out jar);
            if (!hasJar)
                hasJar = TryFindProp(BlockShapeRole.FragileProp, false, BlockMaterialType.CeramicJar, out jar);

            BlockDefinition plank;
            blocks.hasPlank = TryFindBestPlank(3, out plank);
            BlockDefinition ornament;
            blocks.hasOrnament = TryFindProp(BlockShapeRole.TopOrnament, false, BlockMaterialType.CeramicJar, out ornament);
            BlockDefinition pillar;
            blocks.hasPillar = TryFindBestPillar(2, woodFamily, out pillar);

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

        private void ApplyGridForLevel(int index)
        {
            if (index <= 25)
            {
                level.GridDimensions = new Vector3Int(8, 8, 3);
                return;
            }

            if (index <= 100)
            {
                level.GridDimensions = new Vector3Int(10, 10, 4);
                return;
            }

            if (index <= 400)
            {
                level.GridDimensions = new Vector3Int(12, 12, 5);
                return;
            }

            level.GridDimensions = new Vector3Int(16, 14, 6);
        }

        private int HeightCapFor(int index)
        {
            int gridY = level != null ? level.GridDimensions.y : 8;
            int desired;
            if (index <= 25)
                desired = 5;
            else if (index <= 100)
                desired = 7;
            else if (index <= 400)
                desired = 10;
            else
                desired = 12;

            if (desired > gridY)
                return gridY;

            return desired;
        }

        private int FitZAt(int desiredZ, int depth)
        {
            int span = depth < 1 ? 1 : depth;
            int maxAnchor = level.GridDimensions.z - span;
            if (maxAnchor < 0)
                maxAnchor = 0;

            int layer = desiredZ;
            if (layer < 0)
                layer = 0;

            if (layer > maxAnchor)
                layer = maxAnchor;

            LastAnchorLayer = layer;
            return layer;
        }

        private float PickYaw(bool steep)
        {
            if (steep)
                return yawSteps[random.Next(0, yawSteps.Length)];

            int roll = random.Next(0, 3);
            if (roll == 0)
                return -30f;

            if (roll == 1)
                return 30f;

            return 0f;
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

        private void ApplyRules(int levelIndex)
        {
            level.LevelIndex = levelIndex;
            int targets = 0;
            int count = level.PlacementCount;
            for (int i = 0; i < count; i++)
            {
                BlockPlacementData placement;
                if (!level.TryGetPlacementByIndex(i, out placement))
                    continue;

                BlockDefinition definition;
                if (!catalog.TryGetBlockDefinition(placement.blockId, out definition))
                    continue;

                if (definition.IsTarget())
                    targets++;
            }

            int par = targets / 3;
            if (par < 1)
                par = 1;

            int bonus = 4;
            if (levelIndex > 70)
                bonus = 1;
            else if (levelIndex > 25)
                bonus = 2;
            else if (levelIndex > 8)
                bonus = 3;

            level.ParAmmo = par;
            level.MaxAmmo = par + bonus;
        }

        private int CenterX(int width)
        {
            int x = (level.GridDimensions.x - width) / 2;
            if (x < 0)
                return 0;

            return x;
        }

        private int FitZ(int depth)
        {
            int span = depth < 1 ? 1 : depth;
            int maxAnchor = level.GridDimensions.z - span;
            if (maxAnchor < 0)
                maxAnchor = 0;

            int layer = preferredLayer;
            if (layer < 0)
                layer = 0;

            if (layer > maxAnchor)
                layer = maxAnchor;

            LastAnchorLayer = layer;
            return layer;
        }

        private void Place(BlockDefinition definition, Vector3Int anchor, float yaw = 0f)
        {
            Place(definition, anchor, yaw, Vector3Int.zero);
        }

        private void Place(BlockDefinition definition, Vector3Int anchor, float yaw, Vector3Int customSize)
        {
            if (blockBudget > 0 && level.PlacementCount >= blockBudget)
                return;

            Vector3 euler = new Vector3(0f, yaw, 0f);
            Vector3Int sizeInCells = customSize.x > 0 && customSize.y > 0 && customSize.z > 0
                ? footprint.ClampSize(customSize)
                : footprint.ClampSize(definition.sizeInCells);
            Vector3Int size = footprint.GetEffectiveSize(sizeInCells, euler);
            if (anchor.x < 0 || anchor.y < 0 || anchor.z < 0)
                return;

            Vector3Int end = anchor + footprint.ClampSize(size) - Vector3Int.one;
            if (!level.IsInsideGrid(end))
                return;

            PlacementValidationStatus status = level.ValidatePlacement(anchor, size, catalog);
            if (status != PlacementValidationStatus.ValidEmpty)
                return;

            if (!HasValidSupport(level, catalog, anchor, size))
                return;

            BlockPlacementData placement = new BlockPlacementData();
            placement.blockId = definition.blockId;
            placement.gridPosition = anchor;
            placement.rotationEuler = euler;
            if (customSize.x > 0 && customSize.y > 0 && customSize.z > 0)
                placement.customSize = footprint.ClampSize(customSize);

            level.PlaceBlock(placement, size, catalog);
        }
    }
}
