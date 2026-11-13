using SmashFest.Data;
using UnityEngine;

namespace SmashFest.Level
{
    public class TableLayoutComposer
    {
        private const float WingYaw = 30f;
        private const float TableClearance = 0.25f;
        private const string MissingTableMessage = "Catalog needs a foundation table.";

        private readonly LevelBuildContext context;
        private readonly BlockPlacer placer;
        private readonly StructureModules modules;
        private readonly BlockPaletteFinder palette;

        public TableLayoutComposer(LevelBuildContext context, BlockPlacer placer, StructureModules modules, BlockPaletteFinder palette)
        {
            this.context = context;
            this.placer = placer;
            this.modules = modules;
            this.palette = palette;
        }

        private LevelData Level => context.Level;
        private BlockSet Blocks => context.Blocks;
        private System.Random Random => context.Random;

        public string ComposeAuto(int index)
        {
            if (index <= 25)
                return ComposeEarlyTable(index);

            if (index <= 100)
                return Random.Next(0, 2) == 0 ? ComposeWideSingleTable(index) : ComposeTwinSeparateTables(index);

            if (index <= 400)
                return Random.Next(0, 2) == 0 ? ComposeDeepWideTable(index) : ComposeFrontRearTables(index);

            return Random.Next(0, 2) == 0 ? ComposeMegaPlatform(index) : ComposeTripleIslandTables(index);
        }

        public string ComposePreset(int index, ShapeKind shape)
        {
            bool allowStone = index > 25;
            BlockDefinition column = Blocks.Column(allowStone);
            if (shape == ShapeKind.Twin)
            {
                float twinYaw = allowStone ? 30f : context.PickYaw(false);
                return ComposeTwin(index, column, allowStone, twinYaw);
            }

            bool wide = index > 8;
            float yaw = 0f;
            if (shape == ShapeKind.Angled)
            {
                yaw = context.PickYaw(index > 70);
                if (Mathf.Abs(yaw) < 1f)
                    yaw = 30f;
            }

            if (!TryPlaceTable(!wide, 1, yaw, out Vector3Int size, out int originX, out int originZ))
                return MissingTableMessage;

            int y = size.y;
            if (shape == ShapeKind.GlassWeak)
            {
                y = modules.PlaceWeakBase(Blocks.glass, column, originX, y, originZ, size.x);
                modules.PlaceShelf(originX, y, originZ, size.x);
                return "Glass weak-point built for level " + index.ToString() + ".";
            }

            if (shape == ShapeKind.Shelf)
            {
                y = modules.PlaceBridge(Blocks.wood, Blocks.hasPlank, originX, y, originZ, size.x);
                y = modules.PlaceShelf(originX, y, originZ, size.x);
                if (index > 8)
                {
                    y = modules.PlaceArch(Blocks.wood, Blocks.jar, true, originX, y, originZ, size.x);
                    modules.PlaceShelf(originX, y, originZ, size.x);
                }

                return "Shelf gallery built for level " + index.ToString() + ".";
            }

            y = modules.PlaceArch(column, Blocks.jar, true, originX, y, originZ, size.x);
            if (index > 8)
                y = modules.PlaceShelf(originX, y, originZ, size.x);

            if (allowStone)
                modules.PlaceFortress(column, originX, y, originZ, size.x);

            return "Arch composition built for level " + index.ToString() + ".";
        }

        public string ComposeFallback(int index)
        {
            if (!TryGetTable(out BlockDefinition table))
                return MissingTableMessage;

            Vector3Int tableSize = new Vector3Int(3, 1, 2);
            if (tableSize.x > Level.GridDimensions.x)
                tableSize.x = Mathf.Max(1, Level.GridDimensions.x);

            if (tableSize.z > Level.GridDimensions.z)
                tableSize.z = Mathf.Max(1, Level.GridDimensions.z);

            int originX = context.CenterX(tableSize.x);
            int originZ = context.FitZAt(0, tableSize.z);
            PlaceTable(table, originX, originZ, 0f, tableSize);
            BuildStructureOnTable(Blocks.wood, originX, tableSize, originZ, Mathf.Min(4, Level.GridDimensions.y), false);
            return "Fallback table filled for level " + index.ToString() + ".";
        }

        private bool TryGetTable(out BlockDefinition table)
        {
            if (context.Catalog.TryGetFoundationTable(out table))
                return true;

            return palette.TryFindFoundation(false, out table);
        }

        private string ComposeEarlyTable(int index)
        {
            if (!TryGetTable(out BlockDefinition table))
                return MissingTableMessage;

            int width = Random.Next(0, 2) == 0 ? 3 : 4;
            Vector3Int tableSize = new Vector3Int(width, 1, 2);
            int originX = context.CenterX(tableSize.x);
            int originZ = context.FitZAt(0, tableSize.z);
            PlaceTable(table, originX, originZ, 0f, tableSize);
            BuildStructureOnTable(Blocks.Column(false), originX, tableSize, originZ, context.HeightCapFor(index), false);
            return "Early table " + width.ToString() + "x1x2 for level " + index.ToString() + ".";
        }

        private string ComposeWideSingleTable(int index)
        {
            if (!TryGetTable(out BlockDefinition table))
                return MissingTableMessage;

            int width = Mathf.Min(Level.GridDimensions.x, 5 + Random.Next(0, 2));
            int depth = Mathf.Min(Level.GridDimensions.z, 2 + Random.Next(0, 2));
            Vector3Int tableSize = new Vector3Int(Mathf.Max(3, width), 1, Mathf.Max(1, depth));
            if (!FitsGrid(tableSize))
                return ComposeEarlyTable(index);

            int originX = context.CenterX(tableSize.x);
            int originZ = context.FitZAt(0, tableSize.z);
            PlaceTable(table, originX, originZ, 0f, tableSize);
            BuildStructureOnTable(Blocks.Column(true), originX, tableSize, originZ, context.HeightCapFor(index), false);
            return "Wide single table for level " + index.ToString() + ".";
        }

        private string ComposeTwinSeparateTables(int index)
        {
            if (!TryGetTable(out BlockDefinition table))
                return MissingTableMessage;

            Vector3Int tableSize = new Vector3Int(3, 1, 2);
            bool angled = Random.Next(0, 2) == 0;
            float leftYaw = angled ? -WingYaw : 0f;
            float rightYaw = angled ? WingYaw : 0f;
            float overhang = RotatedOverhang(tableSize, rightYaw).x;
            int gap = GapBetween(overhang, overhang);
            int span = (tableSize.x * 2) + gap;
            if (span > Level.GridDimensions.x)
                return ComposeWideSingleTable(index);

            int startX = context.CenterX(span);
            int originZ = context.FitZAt(0, tableSize.z);
            int leftX = startX;
            int rightX = startX + tableSize.x + gap;
            PlaceTable(table, leftX, originZ, leftYaw, tableSize);
            PlaceTable(table, rightX, originZ, rightYaw, tableSize);
            int heightCap = context.HeightCapFor(index);
            BlockDefinition column = Blocks.Column(index > 25);
            int remainingArea = Area(tableSize) * 2;
            BuildStructureOnTable(column, leftX, tableSize, originZ, heightCap, false, ShareFor(tableSize, ref remainingArea));
            BuildStructureOnTable(column, rightX, tableSize, originZ, heightCap, false, ShareFor(tableSize, ref remainingArea));
            return "Twin separate tables for level " + index.ToString() + ".";
        }

        private string ComposeDeepWideTable(int index)
        {
            if (!TryGetTable(out BlockDefinition table))
                return MissingTableMessage;

            int width = Mathf.Min(Level.GridDimensions.x, 6 + Random.Next(0, 3));
            int depth = Mathf.Min(Level.GridDimensions.z, 3 + Random.Next(0, 2));
            Vector3Int tableSize = new Vector3Int(Mathf.Max(4, width), 1, Mathf.Max(2, depth));
            if (!FitsGrid(tableSize))
                return ComposeWideSingleTable(index);

            int originX = context.CenterX(tableSize.x);
            int originZ = context.FitZAt(0, tableSize.z);
            PlaceTable(table, originX, originZ, 0f, tableSize);
            BuildStructureOnTable(Blocks.Column(true), originX, tableSize, originZ, context.HeightCapFor(index), true);
            return "Deep wide table for level " + index.ToString() + ".";
        }

        private string ComposeFrontRearTables(int index)
        {
            if (!TryGetTable(out BlockDefinition table))
                return MissingTableMessage;

            Vector3Int frontSize = new Vector3Int(3, 1, 2);
            float frontYaw = WingYaw;
            int rowGap = GapBetween(RotatedOverhang(frontSize, frontYaw).y, 0f);
            int rearDepth = Mathf.Min(3, Level.GridDimensions.z - frontSize.z - rowGap);
            if (rearDepth < 2)
            {
                frontYaw = 0f;
                rowGap = 0;
                rearDepth = Mathf.Min(3, Level.GridDimensions.z - frontSize.z);
            }

            Vector3Int rearSize = new Vector3Int(Mathf.Min(7, Level.GridDimensions.x), 1, rearDepth);
            if (rearSize.x < 3 || rearSize.z < 2 || frontSize.z + rowGap + rearSize.z > Level.GridDimensions.z)
                return ComposeDeepWideTable(index);

            int rearX = context.CenterX(rearSize.x);
            int rearZ = context.FitZAt(frontSize.z + rowGap, rearSize.z);
            PlaceTable(table, rearX, rearZ, 0f, rearSize);

            float frontOverhang = RotatedOverhang(frontSize, frontYaw).x;
            int gap = GapBetween(frontOverhang, frontOverhang);
            int span = (frontSize.x * 2) + gap;
            int frontZ = context.FitZAt(0, frontSize.z);
            int startX = context.CenterX(span);
            bool hasFront = span <= Level.GridDimensions.x;
            if (hasFront)
            {
                PlaceTable(table, startX, frontZ, -frontYaw, frontSize);
                PlaceTable(table, startX + frontSize.x + gap, frontZ, frontYaw, frontSize);
            }

            int remainingArea = Area(rearSize) + (hasFront ? Area(frontSize) * 2 : 0);
            BuildStructureOnTable(Blocks.Column(true), rearX, rearSize, rearZ, context.HeightCapFor(index), true, ShareFor(rearSize, ref remainingArea));
            if (hasFront)
            {
                int frontHeight = Mathf.Max(4, context.HeightCapFor(index) - 3);
                BuildStructureOnTable(Blocks.wood, startX, frontSize, frontZ, frontHeight, false, ShareFor(frontSize, ref remainingArea));
                BuildStructureOnTable(Blocks.wood, startX + frontSize.x + gap, frontSize, frontZ, frontHeight, false, ShareFor(frontSize, ref remainingArea));
            }

            return "Front-rear tables for level " + index.ToString() + ".";
        }

        private string ComposeMegaPlatform(int index)
        {
            if (!TryGetTable(out BlockDefinition table))
                return MissingTableMessage;

            int width = Mathf.Min(Level.GridDimensions.x, 7 + Random.Next(0, 2));
            int depth = Mathf.Min(Level.GridDimensions.z, 4 + Random.Next(0, 2));
            Vector3Int tableSize = new Vector3Int(Mathf.Max(5, width), 1, Mathf.Max(3, depth));
            if (!FitsGrid(tableSize))
                return ComposeDeepWideTable(index);

            int originX = context.CenterX(tableSize.x);
            int originZ = context.FitZAt(0, tableSize.z);
            PlaceTable(table, originX, originZ, 0f, tableSize);
            BuildStructureOnTable(Blocks.Column(true), originX, tableSize, originZ, context.HeightCapFor(index), true);
            return "Mega platform table for level " + index.ToString() + ".";
        }

        private string ComposeTripleIslandTables(int index)
        {
            if (!TryGetTable(out BlockDefinition table))
                return MissingTableMessage;

            Vector3Int wingSize = new Vector3Int(3, 1, 2);
            int gap = GapBetween(RotatedOverhang(wingSize, WingYaw).x, 0f);
            int centerWidth = Mathf.Min(6, Level.GridDimensions.x - wingSize.x * 2 - gap * 2);
            int centerDepth = Mathf.Min(4, Level.GridDimensions.z);
            if (centerWidth < 4)
                return ComposeMegaPlatform(index);

            Vector3Int centerSize = new Vector3Int(centerWidth, 1, Mathf.Max(2, centerDepth));
            int span = wingSize.x + gap + centerSize.x + gap + wingSize.x;
            if (span > Level.GridDimensions.x || centerSize.z > Level.GridDimensions.z)
                return ComposeMegaPlatform(index);

            int leftX = context.CenterX(span);
            int centerX = leftX + wingSize.x + gap;
            int rightX = centerX + centerSize.x + gap;
            int originZ = context.FitZAt(0, centerSize.z);
            PlaceTable(table, leftX, originZ, -WingYaw, wingSize);
            PlaceTable(table, centerX, originZ, 0f, centerSize);
            PlaceTable(table, rightX, originZ, WingYaw, wingSize);
            int height = context.HeightCapFor(index);
            int wingHeight = Mathf.Max(4, height - 2);
            int remainingArea = Area(wingSize) * 2 + Area(centerSize);
            BuildStructureOnTable(Blocks.wood, leftX, wingSize, originZ, wingHeight, false, ShareFor(wingSize, ref remainingArea));
            BuildStructureOnTable(Blocks.Column(true), centerX, centerSize, originZ, height, true, ShareFor(centerSize, ref remainingArea));
            BuildStructureOnTable(Blocks.wood, rightX, wingSize, originZ, wingHeight, false, ShareFor(wingSize, ref remainingArea));
            return "Triple island tables for level " + index.ToString() + ".";
        }

        private string ComposeTwin(int index, BlockDefinition column, bool useWeakBase, float yaw)
        {
            if (!palette.TryFindFoundation(true, out BlockDefinition table))
                return MissingTableMessage;

            Vector3Int size = context.Footprint.ClampSize(table.sizeInCells);
            int gap = 1;
            int span = (size.x * 2) + gap;
            if (span > Level.GridDimensions.x)
                return "Grid is too narrow for twin tables.";

            int leftX = context.CenterX(span);
            int rightX = leftX + size.x + gap;
            int originZ = context.FitZ(size.z);
            placer.Place(table, new Vector3Int(leftX, 0, originZ), Mathf.Abs(yaw));
            placer.Place(table, new Vector3Int(rightX, 0, originZ), -Mathf.Abs(yaw));
            if (Blocks.hasPlank)
                placer.Place(Blocks.plank, new Vector3Int(leftX + size.x - 1, size.y, originZ));

            StackTower(column, useWeakBase, leftX, size, originZ);
            StackTower(column, useWeakBase, rightX, size, originZ);
            return "Twin module towers built for level " + index.ToString() + ".";
        }

        private void StackTower(BlockDefinition column, bool useWeakBase, int originX, Vector3Int tableSize, int originZ)
        {
            int y = tableSize.y;
            if (useWeakBase)
                y = modules.PlaceWeakBase(Blocks.glass, column, originX, y, originZ, tableSize.x);

            y = modules.PlaceArch(column, Blocks.jar, true, originX, y, originZ, tableSize.x);
            y = modules.PlaceShelf(originX, y, originZ, tableSize.x);
            modules.PlaceCrown(Blocks.Crown(), originX, y, originZ, tableSize.x);
        }

        private bool TryPlaceTable(bool smallest, int layerSpan, float yaw, out Vector3Int size, out int originX, out int originZ)
        {
            size = Vector3Int.one;
            originX = 0;
            originZ = 0;
            if (!palette.TryFindFoundation(smallest, out BlockDefinition table))
                return false;

            size = context.Footprint.ClampSize(table.sizeInCells);
            int span = layerSpan < 1 ? size.z : layerSpan;
            originX = context.CenterX(size.x);
            originZ = context.FitZ(span);
            placer.Place(table, new Vector3Int(originX, 0, originZ), yaw);
            return true;
        }

        private void PlaceTable(BlockDefinition table, int originX, int originZ, float yaw, Vector3Int customSize)
        {
            placer.Place(table, new Vector3Int(originX, 0, originZ), yaw, customSize);
        }

        private void BuildStructureOnTable(BlockDefinition column, int originX, Vector3Int tableSize, int originZ, int heightCap, bool tallPeak, int share = int.MaxValue)
        {
            int fullBudget = context.BlockBudget;
            if (share < int.MaxValue)
                context.BlockBudget = Mathf.Min(fullBudget, Level.PlacementCount + share);

            FillTable(column, originX, tableSize, originZ, Mathf.Min(heightCap, Level.GridDimensions.y), tallPeak);
            context.BlockBudget = fullBudget;
        }

        private void FillTable(BlockDefinition column, int originX, Vector3Int tableSize, int originZ, int maxY, bool tallPeak)
        {
            for (int z = 0; z < tableSize.z; z++)
            {
                int layerZ = originZ + z;
                int y = tableSize.y;
                bool peak = tallPeak && z == tableSize.z - 1;
                if (Blocks.hasGlass && Random.Next(0, 3) != 0)
                    y = modules.PlaceWeakBase(Blocks.glass, column, originX, y, layerZ, tableSize.x);

                while (y < maxY - 2 && Level.PlacementCount < context.BlockBudget)
                {
                    int before = Level.PlacementCount;
                    int postRows = peak ? 3 : 2;
                    y = modules.PlaceArch(column, modules.PickFillProp(), true, originX, y, layerZ, tableSize.x, postRows);
                    if (y < maxY - 1)
                        y = modules.PlaceShelf(originX, y, layerZ, tableSize.x, true);

                    if (Level.PlacementCount == before)
                        break;
                }

                modules.PlaceCrown(Blocks.Crown(), originX, y, layerZ, tableSize.x);
            }
        }

        private bool FitsGrid(Vector3Int tableSize) => tableSize.x <= Level.GridDimensions.x && tableSize.z <= Level.GridDimensions.z;

        private int Area(Vector3Int tableSize) => tableSize.x * tableSize.z;

        private Vector2 RotatedOverhang(Vector3Int tableSize, float yaw)
        {
            float radians = Mathf.Abs(yaw) * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            float halfX = (tableSize.x * cos + tableSize.z * sin) * 0.5f;
            float halfZ = (tableSize.x * sin + tableSize.z * cos) * 0.5f;
            return new Vector2(halfX - tableSize.x * 0.5f, halfZ - tableSize.z * 0.5f);
        }

        private int GapBetween(float overhangA, float overhangB) => Mathf.CeilToInt(overhangA + overhangB + TableClearance);

        private int ShareFor(Vector3Int tableSize, ref int remainingArea)
        {
            int area = Area(tableSize);
            int remaining = Mathf.Max(0, context.BlockBudget - Level.PlacementCount);
            int share = remainingArea > area ? Mathf.CeilToInt(remaining * (float)area / remainingArea) : remaining;
            remainingArea -= area;
            return share;
        }
    }
}
