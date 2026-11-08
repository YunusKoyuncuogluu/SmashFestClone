using System.Collections.Generic;
using SmashFast.Data;
using UnityEngine;

namespace SmashFast.Level
{
    public class LevelArchitect
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
            public bool hasPlank;
            public bool hasStone;
            public bool hasGlass;
            public bool hasOrnament;

            public BlockDefinition Column(bool allowStone)
            {
                if (allowStone && hasStone)
                {
                    return stone;
                }

                return wood;
            }

            public BlockDefinition Crown()
            {
                if (hasOrnament)
                {
                    return ornament;
                }

                return jar;
            }
        }

        private readonly BlockFootprint footprint = new BlockFootprint();
        private readonly float[] yawSteps = new float[] { -45f, -30f, 0f, 30f, 45f };
        private readonly int[] moduleOrder = new int[4];
        private readonly List<Vector3Int> anchorBuffer = new List<Vector3Int>(64);
        private System.Random random = new System.Random(1);
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
            {
                shape = ShapeKind.Angled;
            }
            else if (presetIndex == 2)
            {
                shape = ShapeKind.GlassWeak;
            }
            else if (presetIndex == 3)
            {
                shape = ShapeKind.Shelf;
            }
            else if (presetIndex == 4)
            {
                shape = ShapeKind.Twin;
            }

            int index = 1;
            if (levelData != null && levelData.LevelIndex > 0)
            {
                index = levelData.LevelIndex;
            }

            return Build(levelData, blockCatalog, index, shape, layerZ);
        }

        public LevelData GenerateRuntimeLevel(int displayLevelNumber, BlockCatalogSO blockCatalog, LevelData gridSource)
        {
            if (blockCatalog == null)
            {
                return null;
            }

            LevelData runtimeLevel = ScriptableObject.CreateInstance<LevelData>();
            runtimeLevel.name = "RuntimeLevel";
            runtimeLevel.hideFlags = HideFlags.HideAndDontSave;
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
            {
                return "Select a level and a catalog first.";
            }

            level = sourceLevel;
            catalog = sourceCatalog;
            int index = levelIndex < 1 ? 1 : levelIndex;
            random = new System.Random(index);
            preferredLayer = layerZ < 0 ? 0 : layerZ;
            LastAnchorLayer = preferredLayer;
            if (!TryLoadBlocks())
            {
                return "Catalog needs a standard block.";
            }

            level.ClearPlacements();
            blockBudget = BudgetFor(index);
            string message;
            if (shape == ShapeKind.Auto)
            {
                message = ComposeAuto(index);
            }
            else
            {
                message = ComposePreset(index, shape);
            }

            EnsureStructuralStability();
            ApplyRules(index);
            return message;
        }

        private bool TryLoadBlocks()
        {
            blocks = new BlockSet();
            BlockDefinition wood;
            bool hasWood = TryFindBlock(BlockShapeRole.StandardBlock, true, BlockMaterialType.Wood, out wood);
            BlockDefinition stone;
            blocks.hasStone = TryFindBlock(BlockShapeRole.StandardBlock, true, BlockMaterialType.Stone, out stone);
            BlockDefinition glass;
            blocks.hasGlass = TryFindBlock(BlockShapeRole.StandardBlock, true, BlockMaterialType.Glass, out glass);
            BlockDefinition jar;
            bool hasJar = TryFindBlock(BlockShapeRole.FragileProp, false, BlockMaterialType.Wood, out jar);
            BlockDefinition plank;
            blocks.hasPlank = TryFindBlock(BlockShapeRole.HorizontalPlank, false, BlockMaterialType.Wood, out plank);
            BlockDefinition ornament;
            blocks.hasOrnament = TryFindBlock(BlockShapeRole.TopOrnament, false, BlockMaterialType.Wood, out ornament);
            if (!hasWood && !blocks.hasStone && !blocks.hasGlass)
            {
                return false;
            }

            if (!hasWood)
            {
                wood = blocks.hasGlass ? glass : stone;
            }

            if (!hasJar)
            {
                jar = blocks.hasGlass ? glass : wood;
            }

            if (!blocks.hasGlass)
            {
                glass = wood;
            }

            if (!blocks.hasStone)
            {
                stone = wood;
            }

            blocks.wood = wood;
            blocks.stone = stone;
            blocks.glass = glass;
            blocks.jar = jar;
            blocks.plank = plank;
            blocks.ornament = ornament;
            return true;
        }

        private string ComposeAuto(int index)
        {
            if (index <= 8)
            {
                return ComposeTutorial(index);
            }

            if (index <= 25)
            {
                return ComposeDiscovery(index);
            }

            if (index <= 70)
            {
                return ComposeTactical(index);
            }

            return ComposeMaster(index);
        }

        private string ComposeTutorial(int index)
        {
            Vector3Int size;
            int originX;
            int originZ;
            if (!TryPlaceTable(true, 0, 0f, out size, out originX, out originZ))
            {
                return "Catalog needs a foundation table.";
            }

            int y = size.y;
            int postRows = index >= 6 ? 2 : 1;
            for (int row = 0; row < postRows; row++)
            {
                PlaceSupports(blocks.wood, blocks.wood, false, false, originX, y, originZ, size.x);
                y++;
            }

            y = PlaceBridge(blocks.wood, false, originX, y, originZ, size.x);
            PlaceRow(blocks.jar, originX, y, originZ, size.x);
            return "Tutorial arch built for level " + index.ToString() + ".";
        }

        private string ComposeDiscovery(int index)
        {
            bool twin = random.Next(0, 2) == 0;
            float yaw = PickYaw(false);
            if (!twin)
            {
                Vector3Int size;
                int originX;
                int originZ;
                if (!TryPlaceTable(false, 1, yaw, out size, out originX, out originZ))
                {
                    return "Catalog needs a foundation table.";
                }

                int y = size.y;
                y = PlaceWeakBase(blocks.glass, blocks.wood, originX, y, originZ, size.x);
                y = PlaceArch(blocks.wood, blocks.jar, true, originX, y, originZ, size.x);
                PlaceShelf(originX, y, originZ, size.x);
                return "Shelf puzzle built for level " + index.ToString() + ".";
            }

            return ComposeTwin(index, blocks.wood, false, yaw);
        }

        private string ComposeTactical(int index)
        {
            bool layered = (index % 2) == 0 || level.GridDimensions.z < 2;
            BlockDefinition column = blocks.Column(true);
            if (!layered)
            {
                return ComposeTwin(index, column, true, 30f);
            }

            float yaw = random.Next(0, 2) == 0 ? 30f : -30f;
            Vector3Int size;
            int originX;
            int originZ;
            if (!TryPlaceTable(false, 0, yaw, out size, out originX, out originZ))
            {
                return "Catalog needs a foundation table.";
            }

            int frontY = size.y;
            if (blocks.hasGlass)
            {
                frontY = PlaceWeakBase(blocks.glass, column, originX, frontY, originZ, size.x);
            }

            PlaceArch(column, blocks.hasGlass ? blocks.glass : blocks.jar, true, originX, frontY, originZ, size.x);
            int backZ = originZ + 1;
            if (size.z < 2 || backZ >= level.GridDimensions.z)
            {
                return "Front arch built for level " + index.ToString() + ".";
            }

            int y = size.y;
            y = PlaceArch(blocks.wood, blocks.jar, true, originX, y, backZ, size.x);
            y = PlaceShelf(originX, y, backZ, size.x);
            y = PlaceShelf(originX, y, backZ, size.x, false);
            PlaceCrown(blocks.Crown(), originX, y, backZ, size.x);
            return "Layered arch and shelf built for level " + index.ToString() + ".";
        }

        private string ComposeMaster(int index)
        {
            if ((index % 2) == 0 && level.GridDimensions.z >= 3)
            {
                string castle = ComposeVolumetric(index);
                if (castle != null)
                {
                    return castle;
                }

                return ComposeLinked(index + 1);
            }

            return ComposeLinked(index);
        }

        private string ComposeLinked(int index)
        {
            bool slim = (index % 4) == 3;
            BlockDefinition table = default;
            bool foundTable = false;
            if (!slim)
            {
                foundTable = TryFindFoundation(false, out table);
                if (foundTable)
                {
                    Vector3Int wideSize = footprint.ClampSize(table.sizeInCells);
                    int wideSpan = (wideSize.x * 2) + 1;
                    if (wideSpan > level.GridDimensions.x || wideSize.z > level.GridDimensions.z)
                    {
                        foundTable = false;
                    }
                }
            }

            if (!foundTable)
            {
                slim = true;
                foundTable = TryFindFoundation(true, out table);
            }

            if (!foundTable)
            {
                return "Catalog needs a foundation table.";
            }

            Vector3Int size = footprint.ClampSize(table.sizeInCells);
            int tableCount = 2;
            int gap = 1;
            if (slim && (size.x * 3) + 2 <= level.GridDimensions.x)
            {
                tableCount = 3;
            }

            int span = (tableCount * size.x) + ((tableCount - 1) * gap);
            int startX = CenterX(span);
            int originZ = FitZ(size.z);
            int previousX = startX;
            BlockDefinition column = blocks.Column(true);
            for (int i = 0; i < tableCount; i++)
            {
                if (level.PlacementCount >= blockBudget)
                {
                    break;
                }

                int originX = startX + (i * (size.x + gap));
                float yaw = yawSteps[random.Next(0, yawSteps.Length)];
                if (i == 0)
                {
                    yaw = Mathf.Abs(yaw);
                }
                else if (i == tableCount - 1)
                {
                    yaw = -Mathf.Abs(yaw);
                }

                Place(table, new Vector3Int(originX, 0, originZ), yaw);
                if (i > 0 && blocks.hasPlank)
                {
                    Place(blocks.plank, new Vector3Int(previousX + size.x - 1, size.y, originZ));
                }

                int y = size.y;
                ShuffleModules();
                for (int module = 0; module < moduleOrder.Length; module++)
                {
                    if (level.PlacementCount >= blockBudget || y >= level.GridDimensions.y)
                    {
                        break;
                    }

                    y = ApplyModule(moduleOrder[module], column, originX, y, originZ, size.x);
                }

                previousX = originX;
            }

            return "Linked towers built for level " + index.ToString() + ".";
        }

        private string ComposeVolumetric(int index)
        {
            BlockDefinition wide;
            if (!TryFindFoundation(false, out wide))
            {
                return "Catalog needs a foundation table.";
            }

            BlockDefinition rear;
            if (!TryFindFoundation(true, out rear))
            {
                rear = wide;
            }

            Vector3Int wideSize = footprint.ClampSize(wide.sizeInCells);
            Vector3Int rearSize = footprint.ClampSize(rear.sizeInCells);
            if (wideSize.z >= level.GridDimensions.z)
            {
                return null;
            }

            int originZ = 0;
            LastAnchorLayer = originZ;
            int originX = CenterX(wideSize.x);
            int rearX = originX + ((wideSize.x - rearSize.x) / 2);
            if (rearX < 0)
            {
                rearX = 0;
            }

            BlockDefinition armor = blocks.Column(true);
            Place(wide, new Vector3Int(originX, 0, originZ), PickYaw(true));
            int y = wideSize.y;
            if (blocks.hasGlass)
            {
                y = PlaceWeakBase(blocks.glass, armor, originX, y, originZ, wideSize.x);
            }

            PlaceFortress(armor, originX, y, originZ, wideSize.x);
            int midZ = originZ + 1;
            if (wideSize.z > 1 && midZ < level.GridDimensions.z)
            {
                int midY = wideSize.y;
                midY = PlaceBridge(blocks.wood, blocks.hasPlank, originX, midY, midZ, wideSize.x);
                PlaceShelf(originX, midY, midZ, wideSize.x);
            }

            int backZ = originZ + wideSize.z;
            if (backZ >= level.GridDimensions.z)
            {
                return "Volumetric castle built for level " + index.ToString() + ".";
            }

            Place(rear, new Vector3Int(rearX, 0, backZ), PickYaw(true));
            int backY = rearSize.y;
            ShuffleModules();
            for (int module = 0; module < moduleOrder.Length; module++)
            {
                if (level.PlacementCount >= blockBudget || backY >= level.GridDimensions.y)
                {
                    break;
                }

                backY = ApplyModule(moduleOrder[module], armor, rearX, backY, backZ, rearSize.x);
            }

            while (level.PlacementCount < 25 && backY < level.GridDimensions.y)
            {
                int before = level.PlacementCount;
                backY = PlaceShelf(rearX, backY, backZ, rearSize.x);
                if (level.PlacementCount == before)
                {
                    break;
                }
            }

            return "Volumetric castle built for level " + index.ToString() + ".";
        }

        private string ComposePreset(int index, ShapeKind shape)
        {
            bool allowStone = index > 25;
            BlockDefinition column = blocks.Column(allowStone);
            if (shape == ShapeKind.Twin)
            {
                float twinYaw = allowStone ? 30f : PickYaw(false);
                return ComposeTwin(index, column, allowStone, twinYaw);
            }

            bool wide = index > 8;
            float yaw = 0f;
            if (shape == ShapeKind.Angled)
            {
                yaw = index > 70 ? PickYaw(true) : PickYaw(false);
                if (Mathf.Abs(yaw) < 1f)
                {
                    yaw = 30f;
                }
            }

            Vector3Int size;
            int originX;
            int originZ;
            if (!TryPlaceTable(!wide, 1, yaw, out size, out originX, out originZ))
            {
                return "Catalog needs a foundation table.";
            }

            int y = size.y;
            if (shape == ShapeKind.GlassWeak)
            {
                y = PlaceWeakBase(blocks.glass, column, originX, y, originZ, size.x);
                PlaceShelf(originX, y, originZ, size.x);
                return "Glass weak-point built for level " + index.ToString() + ".";
            }

            if (shape == ShapeKind.Shelf)
            {
                y = PlaceBridge(blocks.wood, blocks.hasPlank, originX, y, originZ, size.x);
                y = PlaceShelf(originX, y, originZ, size.x);
                if (index > 8)
                {
                    y = PlaceArch(blocks.wood, blocks.jar, true, originX, y, originZ, size.x);
                    PlaceShelf(originX, y, originZ, size.x);
                }

                return "Shelf gallery built for level " + index.ToString() + ".";
            }

            y = PlaceArch(column, blocks.jar, true, originX, y, originZ, size.x);
            if (index > 8)
            {
                y = PlaceShelf(originX, y, originZ, size.x);
            }

            if (allowStone)
            {
                PlaceFortress(column, originX, y, originZ, size.x);
            }

            return "Arch composition built for level " + index.ToString() + ".";
        }

        private string ComposeTwin(int index, BlockDefinition column, bool useWeakBase, float yaw)
        {
            BlockDefinition table;
            if (!TryFindFoundation(true, out table))
            {
                return "Catalog needs a foundation table.";
            }

            Vector3Int size = footprint.ClampSize(table.sizeInCells);
            int gap = 1;
            int span = (size.x * 2) + gap;
            if (span > level.GridDimensions.x)
            {
                return "Grid is too narrow for twin tables.";
            }

            int leftX = CenterX(span);
            int rightX = leftX + size.x + gap;
            int originZ = FitZ(size.z);
            Place(table, new Vector3Int(leftX, 0, originZ), Mathf.Abs(yaw));
            Place(table, new Vector3Int(rightX, 0, originZ), -Mathf.Abs(yaw));
            if (blocks.hasPlank)
            {
                Place(blocks.plank, new Vector3Int(leftX + size.x - 1, size.y, originZ));
            }

            StackTower(column, useWeakBase, leftX, size, originZ);
            StackTower(column, useWeakBase, rightX, size, originZ);
            return "Twin module towers built for level " + index.ToString() + ".";
        }

        private void StackTower(BlockDefinition column, bool useWeakBase, int originX, Vector3Int tableSize, int originZ)
        {
            int y = tableSize.y;
            if (useWeakBase)
            {
                y = PlaceWeakBase(blocks.glass, column, originX, y, originZ, tableSize.x);
            }

            y = PlaceArch(column, blocks.jar, true, originX, y, originZ, tableSize.x);
            y = PlaceShelf(originX, y, originZ, tableSize.x);
            PlaceCrown(blocks.Crown(), originX, y, originZ, tableSize.x);
        }

        private int ApplyModule(int kind, BlockDefinition column, int originX, int y, int originZ, int width)
        {
            if (kind == 0)
            {
                return PlaceArch(column, blocks.jar, true, originX, y, originZ, width);
            }

            if (kind == 1)
            {
                return PlaceShelf(originX, y, originZ, width);
            }

            if (kind == 2)
            {
                return PlaceTower(column, originX, y, originZ, width);
            }

            return PlaceCrown(blocks.Crown(), originX, y, originZ, width);
        }

        private int PlaceArch(
            BlockDefinition column,
            BlockDefinition opening,
            bool fillOpening,
            int originX,
            int y,
            int originZ,
            int width)
        {
            if (y >= level.GridDimensions.y)
            {
                return y;
            }

            PlaceSupports(column, opening, fillOpening, blocks.hasPlank, originX, y, originZ, width);
            return PlaceBridge(column, blocks.hasPlank, originX, y + 1, originZ, width);
        }

        private int PlaceTower(BlockDefinition column, int originX, int y, int originZ, int width)
        {
            int next = y;
            for (int row = 0; row < 2; row++)
            {
                if (next >= level.GridDimensions.y)
                {
                    return next;
                }

                PlaceSupports(column, blocks.wood, false, blocks.hasPlank, originX, next, originZ, width);
                next++;
            }

            return PlaceBridge(blocks.wood, blocks.hasPlank, originX, next, originZ, width);
        }

        private int PlaceWeakBase(BlockDefinition glass, BlockDefinition heavy, int originX, int y, int originZ, int width)
        {
            if (y >= level.GridDimensions.y)
            {
                return y;
            }

            int plankWidth;
            int margin;
            bool plankFits = TryPlankSpan(width, out plankWidth, out margin);
            bool endsOnly = width > 2 && plankFits && plankWidth == width && random.Next(0, 2) == 0;
            for (int x = 0; x < width; x++)
            {
                bool foot = !endsOnly || x == 0 || x == width - 1;
                if (!foot)
                {
                    continue;
                }

                Place(glass, new Vector3Int(originX + x, y, originZ));
            }

            int top = PlaceBridge(blocks.wood, blocks.hasPlank, originX, y + 1, originZ, width);
            PlaceRow(heavy, originX, top, originZ, width);
            return top + 1;
        }

        private int PlaceFortress(BlockDefinition column, int originX, int y, int originZ, int width)
        {
            bool glassWindow = blocks.hasGlass && random.Next(0, 2) == 0;
            bool span = blocks.hasPlank;
            bool fill = glassWindow || !span;
            BlockDefinition filler = glassWindow ? blocks.glass : column;
            int next = y;
            for (int row = 0; row < 2; row++)
            {
                if (next >= level.GridDimensions.y)
                {
                    return next;
                }

                PlaceSupports(column, filler, fill, span, originX, next, originZ, width);
                next++;
            }

            int roof = PlaceBridge(column, span, originX, next, originZ, width);
            return PlaceCrown(blocks.Crown(), originX, roof, originZ, width);
        }

        private void PlaceSupports(
            BlockDefinition column,
            BlockDefinition opening,
            bool fillOpening,
            bool spanWithPlank,
            int originX,
            int y,
            int originZ,
            int width)
        {
            int plankWidth = 0;
            int margin = 0;
            bool spanned = spanWithPlank && TryPlankSpan(width, out plankWidth, out margin);
            for (int x = 0; x < width; x++)
            {
                bool plankEnd = spanned && (x == margin || x == margin + plankWidth - 1);
                bool underPlank = spanned && x >= margin && x < margin + plankWidth;
                bool hole = underPlank && !plankEnd;
                if (!spanned)
                {
                    hole = x > 0 && x < width - 1;
                }

                if (hole)
                {
                    if (fillOpening)
                    {
                        Place(opening, new Vector3Int(originX + x, y, originZ));
                    }
                }
                else
                {
                    Place(column, new Vector3Int(originX + x, y, originZ));
                }
            }
        }

        private int PlaceShelf(int originX, int y, int originZ, int width, bool mixGlass = true)
        {
            if (y >= level.GridDimensions.y)
            {
                return y;
            }

            for (int x = 0; x < width; x++)
            {
                int roll = random.Next(0, 3);
                BlockDefinition prop = blocks.jar;
                if (roll == 1 && mixGlass)
                {
                    prop = blocks.glass;
                }
                else if (roll == 2)
                {
                    prop = blocks.Crown();
                }

                Place(prop, new Vector3Int(originX + x, y, originZ));
            }

            return y + 1;
        }

        private int PlaceCrown(BlockDefinition crown, int originX, int y, int originZ, int width)
        {
            if (y >= level.GridDimensions.y)
            {
                return y;
            }

            if (width <= 2)
            {
                PlaceRow(crown, originX, y, originZ, width);
                return y + 1;
            }

            PlaceRow(crown, originX + 1, y, originZ, width - 2);
            return y + 1;
        }

        private int PlaceBridge(BlockDefinition filler, bool spanWithPlank, int originX, int y, int originZ, int width)
        {
            if (y >= level.GridDimensions.y)
            {
                return y;
            }

            int plankWidth = 0;
            int margin = 0;
            if (!spanWithPlank || !TryPlankSpan(width, out plankWidth, out margin))
            {
                PlaceRow(filler, originX, y, originZ, width);
                return y + 1;
            }

            Place(blocks.plank, new Vector3Int(originX + margin, y, originZ));
            for (int i = 0; i < margin; i++)
            {
                Place(filler, new Vector3Int(originX + i, y, originZ));
                Place(filler, new Vector3Int(originX + width - 1 - i, y, originZ));
            }

            int covered = margin + plankWidth;
            for (int i = covered; i < width - margin; i++)
            {
                Place(filler, new Vector3Int(originX + i, y, originZ));
            }

            return y + 1;
        }

        private bool TryPlankSpan(int width, out int plankWidth, out int margin)
        {
            plankWidth = 0;
            margin = 0;
            if (!blocks.hasPlank)
            {
                return false;
            }

            int candidate = footprint.ClampSize(blocks.plank.sizeInCells).x;
            if (candidate <= 1 || candidate > width)
            {
                return false;
            }

            plankWidth = candidate;
            margin = (width - candidate) / 2;
            return true;
        }

        private void PlaceRow(BlockDefinition block, int originX, int y, int originZ, int width)
        {
            Vector3Int size = footprint.ClampSize(block.sizeInCells);
            if (size.x != 1 || size.z != 1)
            {
                return;
            }

            for (int x = 0; x < width; x++)
            {
                Place(block, new Vector3Int(originX + x, y, originZ));
            }
        }

        private void ShuffleModules()
        {
            moduleOrder[0] = 0;
            moduleOrder[1] = 1;
            moduleOrder[2] = 2;
            moduleOrder[3] = 3;
            for (int i = moduleOrder.Length - 1; i > 0; i--)
            {
                int swap = random.Next(0, i + 1);
                int held = moduleOrder[i];
                moduleOrder[i] = moduleOrder[swap];
                moduleOrder[swap] = held;
            }
        }

        private float PickYaw(bool steep)
        {
            if (steep)
            {
                return yawSteps[random.Next(0, yawSteps.Length)];
            }

            int roll = random.Next(0, 3);
            if (roll == 0)
            {
                return -30f;
            }

            if (roll == 1)
            {
                return 30f;
            }

            return 0f;
        }

        private int BudgetFor(int index)
        {
            if (index <= 8)
            {
                return 9;
            }

            if (index <= 25)
            {
                return 22;
            }

            if (index <= 70)
            {
                return 34;
            }

            return 45;
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
                {
                    continue;
                }

                BlockDefinition definition;
                if (!catalog.TryGetBlockDefinition(placement.blockId, out definition))
                {
                    continue;
                }

                if (definition.IsTarget())
                {
                    targets++;
                }
            }

            int par = targets / 3;
            if (par < 1)
            {
                par = 1;
            }

            int bonus = 4;
            if (levelIndex > 70)
            {
                bonus = 1;
            }
            else if (levelIndex > 25)
            {
                bonus = 2;
            }
            else if (levelIndex > 8)
            {
                bonus = 3;
            }

            level.ParAmmo = par;
            level.MaxAmmo = par + bonus;
        }

        private bool TryPlaceTable(bool smallest, int layerSpan, float yaw, out Vector3Int size, out int originX, out int originZ)
        {
            size = Vector3Int.one;
            originX = 0;
            originZ = 0;
            BlockDefinition table;
            if (!TryFindFoundation(smallest, out table))
            {
                return false;
            }

            size = footprint.ClampSize(table.sizeInCells);
            int span = layerSpan;
            if (span < 1)
            {
                span = size.z;
            }

            originX = CenterX(size.x);
            originZ = FitZ(span);
            Place(table, new Vector3Int(originX, 0, originZ), yaw);
            return true;
        }

        private int CenterX(int width)
        {
            int x = (level.GridDimensions.x - width) / 2;
            if (x < 0)
            {
                return 0;
            }

            return x;
        }

        private int FitZ(int depth)
        {
            int span = depth < 1 ? 1 : depth;
            int maxAnchor = level.GridDimensions.z - span;
            if (maxAnchor < 0)
            {
                maxAnchor = 0;
            }

            int layer = preferredLayer;
            if (layer < 0)
            {
                layer = 0;
            }

            if (layer > maxAnchor)
            {
                layer = maxAnchor;
            }

            LastAnchorLayer = layer;
            return layer;
        }

        private void Place(BlockDefinition definition, Vector3Int anchor, float yaw = 0f)
        {
            if (blockBudget > 0 && level.PlacementCount >= blockBudget)
            {
                return;
            }

            Vector3 euler = new Vector3(0f, yaw, 0f);
            Vector3Int size = footprint.GetEffectiveSize(definition.sizeInCells, euler);
            if (anchor.x < 0 || anchor.y < 0 || anchor.z < 0)
            {
                return;
            }

            Vector3Int end = anchor + footprint.ClampSize(size) - Vector3Int.one;
            if (!level.IsInsideGrid(end))
            {
                return;
            }

            PlacementValidationStatus status = level.ValidatePlacement(anchor, size, catalog);
            if (status != PlacementValidationStatus.ValidEmpty)
            {
                return;
            }

            if (!HasValidSupport(level, catalog, anchor, size))
            {
                return;
            }

            BlockPlacementData placement = new BlockPlacementData();
            placement.blockId = definition.blockId;
            placement.gridPosition = anchor;
            placement.rotationEuler = euler;
            level.PlaceBlock(placement, size, catalog);
        }

        private bool HasValidSupport(LevelData levelData, BlockCatalogSO blockCatalog, Vector3Int targetCell, Vector3Int blockSize)
        {
            if (targetCell.y <= 0 || levelData == null || blockCatalog == null)
            {
                return true;
            }

            return ScanSupports(levelData, blockCatalog, targetCell, footprint.ClampSize(blockSize), false);
        }

        private void EnsureStructuralStability()
        {
            if (level == null || catalog == null)
            {
                return;
            }

            SettleUnsupported();
            BalanceTableLoads();
            SettleUnsupported();
        }

        private void SettleUnsupported()
        {
            int height = level.GridDimensions.y;
            for (int y = 1; y < height; y++)
            {
                CollectAnchors(y, false);
                int pending = anchorBuffer.Count;
                for (int i = 0; i < pending; i++)
                {
                    ResolveUnsupported(anchorBuffer[i]);
                }
            }
        }

        private void ResolveUnsupported(Vector3Int anchor)
        {
            BlockDefinition definition;
            BlockPlacementData placement;
            if (!TryGetAnchorDefinition(anchor, out placement, out definition))
            {
                return;
            }

            if (IsFoundation(definition))
            {
                return;
            }

            Vector3Int size = footprint.GetEffectiveSize(definition.sizeInCells, placement.rotationEuler);
            if (HasValidSupport(level, catalog, placement.gridPosition, size))
            {
                return;
            }

            if (TryDropPlacement(placement, size))
            {
                return;
            }

            level.TryRemoveAt(placement.gridPosition);
        }

        private bool TryDropPlacement(BlockPlacementData placement, Vector3Int size)
        {
            if (placement.gridPosition.y <= 0)
            {
                return false;
            }

            Vector3Int clamped = footprint.ClampSize(size);
            if (ScanSupports(level, catalog, placement.gridPosition, clamped, true))
            {
                return false;
            }

            Vector3Int dropped = placement.gridPosition;
            dropped.y -= 1;
            if (!HasValidSupport(level, catalog, dropped, clamped))
            {
                return false;
            }

            PlacementValidationStatus status = level.ValidatePlacementIgnoring(dropped, clamped, catalog, placement.gridPosition);
            if (status != PlacementValidationStatus.ValidEmpty)
            {
                return false;
            }

            level.TryRemoveAt(placement.gridPosition);
            placement.gridPosition = dropped;
            level.PlaceBlock(placement, clamped, catalog);
            return true;
        }

        private void BalanceTableLoads()
        {
            CollectAnchors(0, true);
            int tables = anchorBuffer.Count;
            for (int i = 0; i < tables; i++)
            {
                BalanceOneTable(anchorBuffer[i]);
            }
        }

        private void BalanceOneTable(Vector3Int tableAnchor)
        {
            BlockDefinition definition;
            BlockPlacementData table;
            if (!TryGetAnchorDefinition(tableAnchor, out table, out definition))
            {
                return;
            }

            Vector3Int size = footprint.GetEffectiveSize(definition.sizeInCells, table.rotationEuler);
            int top = table.gridPosition.y + size.y;
            int height = level.GridDimensions.y;
            for (int y = top; y < height; y++)
            {
                for (int z = 0; z < size.z; z++)
                {
                    int pairCount = size.x / 2;
                    for (int pair = 0; pair < pairCount; pair++)
                    {
                        int leftX = table.gridPosition.x + pair;
                        int rightX = table.gridPosition.x + size.x - 1 - pair;
                        BalancePair(leftX, rightX, y, table.gridPosition.z + z);
                    }
                }
            }
        }

        private void BalancePair(int leftX, int rightX, int y, int z)
        {
            if (rightX <= leftX)
            {
                return;
            }

            Vector3Int leftCell = new Vector3Int(leftX, y, z);
            Vector3Int rightCell = new Vector3Int(rightX, y, z);
            BlockPlacementData leftPlacement;
            BlockPlacementData rightPlacement;
            bool leftHeavy = TryGetStructuralUnit(leftCell, out leftPlacement);
            bool rightHeavy = TryGetStructuralUnit(rightCell, out rightPlacement);
            if (leftHeavy == rightHeavy)
            {
                return;
            }

            Vector3Int emptyCell = leftHeavy ? rightCell : leftCell;
            BlockPlacementData source = leftHeavy ? leftPlacement : rightPlacement;
            BlockPlacementData occupied;
            bool isAnchor;
            if (level.TryGetBlockAt(emptyCell, catalog, out occupied, out isAnchor))
            {
                return;
            }

            if (!HasValidSupport(level, catalog, emptyCell, Vector3Int.one))
            {
                level.TryRemoveAt(source.gridPosition);
                return;
            }

            PlacementValidationStatus status = level.ValidatePlacement(emptyCell, Vector3Int.one, catalog);
            if (status != PlacementValidationStatus.ValidEmpty)
            {
                level.TryRemoveAt(source.gridPosition);
                return;
            }

            BlockPlacementData copy = source;
            copy.gridPosition = emptyCell;
            level.PlaceBlock(copy, Vector3Int.one, catalog);
        }

        private void CollectAnchors(int rowY, bool foundationsOnly)
        {
            anchorBuffer.Clear();
            int count = level.PlacementCount;
            for (int i = 0; i < count; i++)
            {
                BlockPlacementData placement;
                if (!level.TryGetPlacementByIndex(i, out placement))
                {
                    continue;
                }

                if (placement.gridPosition.y != rowY)
                {
                    continue;
                }

                if (foundationsOnly)
                {
                    BlockDefinition definition;
                    if (!catalog.TryGetBlockDefinition(placement.blockId, out definition))
                    {
                        continue;
                    }

                    if (!IsFoundation(definition))
                    {
                        continue;
                    }
                }

                anchorBuffer.Add(placement.gridPosition);
            }
        }

        private bool TryGetAnchorDefinition(Vector3Int anchor, out BlockPlacementData placement, out BlockDefinition definition)
        {
            placement = default;
            definition = default;
            bool isAnchor;
            if (!level.TryGetBlockAt(anchor, catalog, out placement, out isAnchor))
            {
                return false;
            }

            if (!isAnchor)
            {
                return false;
            }

            return catalog.TryGetBlockDefinition(placement.blockId, out definition);
        }

        private bool TryGetStructuralUnit(Vector3Int cell, out BlockPlacementData placement)
        {
            BlockDefinition definition;
            if (!TryGetAnchorDefinition(cell, out placement, out definition))
            {
                return false;
            }

            if (!definition.canSupportBlocksAbove || IsFoundation(definition))
            {
                return false;
            }

            Vector3Int size = footprint.GetEffectiveSize(definition.sizeInCells, placement.rotationEuler);
            if (size.x != 1 || size.y != 1 || size.z != 1)
            {
                return false;
            }

            return true;
        }

        private bool ScanSupports(LevelData levelData, BlockCatalogSO blockCatalog, Vector3Int targetCell, Vector3Int size, bool stopOnFragile)
        {
            bool rejectPlank = !stopOnFragile && size.x > 1 && size.y <= 1 && size.z <= 1;
            int belowY = targetCell.y - 1;
            int steps = size.x <= 1 ? size.x : 2;
            for (int z = 0; z < size.z; z++)
            {
                for (int step = 0; step < steps; step++)
                {
                    int x = targetCell.x + step;
                    if (size.x > 1 && step == 1)
                    {
                        x = targetCell.x + size.x - 1;
                    }

                    BlockDefinition definition;
                    bool found = TryGetCellDefinition(levelData, blockCatalog, x, belowY, targetCell.z + z, out definition);
                    if (stopOnFragile)
                    {
                        if (found && !definition.canSupportBlocksAbove)
                        {
                            return true;
                        }

                        continue;
                    }

                    if (!found || !CanCarry(definition, rejectPlank))
                    {
                        return false;
                    }
                }
            }

            return !stopOnFragile;
        }

        private bool CanCarry(BlockDefinition definition, bool rejectPlank)
        {
            if (!definition.canSupportBlocksAbove)
            {
                return false;
            }

            if (rejectPlank && definition.shapeRole == BlockShapeRole.HorizontalPlank)
            {
                return false;
            }

            if (definition.shapeRole == BlockShapeRole.FoundationTable)
            {
                return true;
            }

            if (definition.shapeRole == BlockShapeRole.StandardBlock)
            {
                return true;
            }

            return definition.shapeRole == BlockShapeRole.HorizontalPlank;
        }

        private bool TryGetCellDefinition(LevelData levelData, BlockCatalogSO blockCatalog, int x, int y, int z, out BlockDefinition definition)
        {
            definition = default;
            BlockPlacementData placement;
            bool isAnchor;
            if (!levelData.TryGetBlockAt(new Vector3Int(x, y, z), blockCatalog, out placement, out isAnchor))
            {
                return false;
            }

            return blockCatalog.TryGetBlockDefinition(placement.blockId, out definition);
        }

        private bool IsFoundation(BlockDefinition definition)
        {
            if (definition.shapeRole == BlockShapeRole.FoundationTable)
            {
                return true;
            }

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
                {
                    continue;
                }

                if (!IsFoundation(candidate))
                {
                    continue;
                }

                Vector3Int size = footprint.ClampSize(candidate.sizeInCells);
                int area = size.x * size.z;
                if (found)
                {
                    bool better = smallest ? area < bestArea : area > bestArea;
                    if (!better)
                    {
                        continue;
                    }
                }

                bestArea = area;
                definition = candidate;
                found = true;
            }

            return found;
        }

        private bool TryFindBlock(BlockShapeRole role, bool matchMaterial, BlockMaterialType material, out BlockDefinition definition)
        {
            int count = catalog.BlockCount;
            for (int i = 0; i < count; i++)
            {
                BlockDefinition candidate;
                if (!catalog.TryGetBlockByIndex(i, out candidate))
                {
                    continue;
                }

                if (candidate.shapeRole != role)
                {
                    continue;
                }

                if (matchMaterial && candidate.materialType != material)
                {
                    continue;
                }

                definition = candidate;
                return true;
            }

            definition = default;
            return false;
        }
    }
}
