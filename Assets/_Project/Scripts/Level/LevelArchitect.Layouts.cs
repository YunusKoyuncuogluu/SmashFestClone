using SmashFest.Data;
using UnityEngine;

namespace SmashFest.Level
{
    public partial class LevelArchitect
    {
        private string ForceMinimalTableLevel(int index)
        {
            BlockDefinition table;
            if (!TryGetTable(out table))
                return "Catalog needs a foundation table.";

            Vector3Int tableSize = new Vector3Int(3, 1, 2);
            if (tableSize.x > level.GridDimensions.x)
                tableSize.x = Mathf.Max(1, level.GridDimensions.x);

            if (tableSize.z > level.GridDimensions.z)
                tableSize.z = Mathf.Max(1, level.GridDimensions.z);

            int originX = CenterX(tableSize.x);
            int originZ = FitZAt(0, tableSize.z);
            PlaceTable(table, originX, originZ, 0f, tableSize);
            BuildStructureOnTable(blocks.wood, originX, tableSize, originZ, Mathf.Min(4, level.GridDimensions.y), false);
            return "Fallback table filled for level " + index.ToString() + ".";
        }

        private string ComposeAuto(int index)
        {
            if (index <= 25)
                return ComposeEarlyTable(index);

            if (index <= 100)
            {
                return random.Next(0, 2) == 0
                    ? ComposeWideSingleTable(index)
                    : ComposeTwinSeparateTables(index);
            }

            if (index <= 400)
            {
                return random.Next(0, 2) == 0
                    ? ComposeDeepWideTable(index)
                    : ComposeFrontRearTables(index);
            }

            return random.Next(0, 2) == 0
                ? ComposeMegaPlatform(index)
                : ComposeTripleIslandTables(index);
        }

        private bool TryGetTable(out BlockDefinition table)
        {
            if (catalog.TryGetFoundationTable(out table))
                return true;

            return TryFindFoundation(false, out table);
        }

        private string ComposeEarlyTable(int index)
        {
            BlockDefinition table;
            if (!TryGetTable(out table))
                return "Catalog needs a foundation table.";

            int width = random.Next(0, 2) == 0 ? 3 : 4;
            Vector3Int tableSize = new Vector3Int(width, 1, 2);
            int originX = CenterX(tableSize.x);
            int originZ = FitZAt(0, tableSize.z);
            PlaceTable(table, originX, originZ, 0f, tableSize);
            BuildStructureOnTable(blocks.Column(false), originX, tableSize, originZ, HeightCapFor(index), false);
            return "Early table " + width.ToString() + "x1x2 for level " + index.ToString() + ".";
        }

        private string ComposeWideSingleTable(int index)
        {
            BlockDefinition table;
            if (!TryGetTable(out table))
                return "Catalog needs a foundation table.";

            int width = Mathf.Min(level.GridDimensions.x, 5 + random.Next(0, 2));
            int depth = Mathf.Min(level.GridDimensions.z, 2 + random.Next(0, 2));
            Vector3Int tableSize = new Vector3Int(Mathf.Max(3, width), 1, Mathf.Max(1, depth));
            if (tableSize.x > level.GridDimensions.x || tableSize.z > level.GridDimensions.z)
                return ComposeEarlyTable(index);

            int originX = CenterX(tableSize.x);
            int originZ = FitZAt(0, tableSize.z);
            PlaceTable(table, originX, originZ, 0f, tableSize);
            BuildStructureOnTable(blocks.Column(true), originX, tableSize, originZ, HeightCapFor(index), false);
            return "Wide single table for level " + index.ToString() + ".";
        }

        private string ComposeTwinSeparateTables(int index)
        {
            BlockDefinition table;
            if (!TryGetTable(out table))
                return "Catalog needs a foundation table.";

            Vector3Int tableSize = new Vector3Int(3, 1, 2);
            int gap = 1;
            int span = (tableSize.x * 2) + gap;
            if (span > level.GridDimensions.x)
                return ComposeWideSingleTable(index);

            int startX = CenterX(span);
            int originZ = FitZAt(0, tableSize.z);
            bool angled = random.Next(0, 2) == 0;
            float leftYaw = angled ? -30f : 0f;
            float rightYaw = angled ? 30f : 0f;
            int leftX = startX;
            int rightX = startX + tableSize.x + gap;
            PlaceTable(table, leftX, originZ, leftYaw, tableSize);
            PlaceTable(table, rightX, originZ, rightYaw, tableSize);
            int heightCap = HeightCapFor(index);
            BlockDefinition column = blocks.Column(index > 25);
            BuildStructureOnTable(column, leftX, tableSize, originZ, heightCap, false);
            BuildStructureOnTable(column, rightX, tableSize, originZ, heightCap, false);
            return "Twin separate tables for level " + index.ToString() + ".";
        }

        private string ComposeDeepWideTable(int index)
        {
            BlockDefinition table;
            if (!TryGetTable(out table))
                return "Catalog needs a foundation table.";

            int width = Mathf.Min(level.GridDimensions.x, 6 + random.Next(0, 3));
            int depth = Mathf.Min(level.GridDimensions.z, 3 + random.Next(0, 2));
            Vector3Int tableSize = new Vector3Int(Mathf.Max(4, width), 1, Mathf.Max(2, depth));
            if (tableSize.x > level.GridDimensions.x || tableSize.z > level.GridDimensions.z)
                return ComposeWideSingleTable(index);

            int originX = CenterX(tableSize.x);
            int originZ = FitZAt(0, tableSize.z);
            PlaceTable(table, originX, originZ, 0f, tableSize);
            BuildStructureOnTable(blocks.Column(true), originX, tableSize, originZ, HeightCapFor(index), true);
            return "Deep wide table for level " + index.ToString() + ".";
        }

        private string ComposeFrontRearTables(int index)
        {
            BlockDefinition table;
            if (!TryGetTable(out table))
                return "Catalog needs a foundation table.";

            Vector3Int frontSize = new Vector3Int(3, 1, 2);
            int rearWidth = Mathf.Min(7, level.GridDimensions.x);
            int rearDepth = Mathf.Max(2, Mathf.Min(3, level.GridDimensions.z - frontSize.z));
            Vector3Int rearSize = new Vector3Int(rearWidth, 1, rearDepth);
            if (rearSize.x < 3 || rearSize.z < 2 || frontSize.z + rearSize.z > level.GridDimensions.z)
                return ComposeDeepWideTable(index);

            int rearX = CenterX(rearSize.x);
            int rearZ = FitZAt(frontSize.z + 1, rearSize.z);
            PlaceTable(table, rearX, rearZ, 0f, rearSize);
            BuildStructureOnTable(blocks.Column(true), rearX, rearSize, rearZ, HeightCapFor(index), true);

            int gap = 1;
            int span = (frontSize.x * 2) + gap;
            int frontZ = FitZAt(0, frontSize.z);
            if (span <= level.GridDimensions.x)
            {
                int startX = CenterX(span);
                PlaceTable(table, startX, frontZ, -30f, frontSize);
                PlaceTable(table, startX + frontSize.x + gap, frontZ, 30f, frontSize);
                int frontHeight = Mathf.Max(4, HeightCapFor(index) - 3);
                BuildStructureOnTable(blocks.wood, startX, frontSize, frontZ, frontHeight, false);
                BuildStructureOnTable(
                    blocks.wood,
                    startX + frontSize.x + gap,
                    frontSize,
                    frontZ,
                    frontHeight,
                    false);
            }

            return "Front-rear tables for level " + index.ToString() + ".";
        }

        private string ComposeMegaPlatform(int index)
        {
            BlockDefinition table;
            if (!TryGetTable(out table))
                return "Catalog needs a foundation table.";

            int width = Mathf.Min(level.GridDimensions.x, 7 + random.Next(0, 2));
            int depth = Mathf.Min(level.GridDimensions.z, 4 + random.Next(0, 2));
            Vector3Int tableSize = new Vector3Int(Mathf.Max(5, width), 1, Mathf.Max(3, depth));
            if (tableSize.x > level.GridDimensions.x || tableSize.z > level.GridDimensions.z)
                return ComposeDeepWideTable(index);

            int originX = CenterX(tableSize.x);
            int originZ = FitZAt(0, tableSize.z);
            PlaceTable(table, originX, originZ, 0f, tableSize);
            BuildStructureOnTable(blocks.Column(true), originX, tableSize, originZ, HeightCapFor(index), true);
            return "Mega platform table for level " + index.ToString() + ".";
        }

        private string ComposeTripleIslandTables(int index)
        {
            BlockDefinition table;
            if (!TryGetTable(out table))
                return "Catalog needs a foundation table.";

            int centerWidth = Mathf.Min(6, level.GridDimensions.x - 8);
            int centerDepth = Mathf.Min(4, level.GridDimensions.z);
            if (centerWidth < 4)
                return ComposeMegaPlatform(index);

            Vector3Int centerSize = new Vector3Int(centerWidth, 1, Mathf.Max(2, centerDepth));
            Vector3Int wingSize = new Vector3Int(3, 1, 2);
            int gap = 1;
            int span = wingSize.x + gap + centerSize.x + gap + wingSize.x;
            if (span > level.GridDimensions.x || centerSize.z > level.GridDimensions.z)
                return ComposeMegaPlatform(index);

            int leftX = CenterX(span);
            int centerX = leftX + wingSize.x + gap;
            int rightX = centerX + centerSize.x + gap;
            int originZ = FitZAt(0, centerSize.z);
            PlaceTable(table, leftX, originZ, -30f, wingSize);
            PlaceTable(table, centerX, originZ, 0f, centerSize);
            PlaceTable(table, rightX, originZ, 30f, wingSize);
            int height = HeightCapFor(index);
            BuildStructureOnTable(blocks.wood, leftX, wingSize, originZ, Mathf.Max(4, height - 2), false);
            BuildStructureOnTable(blocks.Column(true), centerX, centerSize, originZ, height, true);
            BuildStructureOnTable(blocks.wood, rightX, wingSize, originZ, Mathf.Max(4, height - 2), false);
            return "Triple island tables for level " + index.ToString() + ".";
        }

        private void PlaceTable(BlockDefinition table, int originX, int originZ, float yaw, Vector3Int customSize)
        {
            Place(table, new Vector3Int(originX, 0, originZ), yaw, customSize);
        }

        private void BuildStructureOnTable(
            BlockDefinition column,
            int originX,
            Vector3Int tableSize,
            int originZ,
            int heightCap,
            bool tallPeak)
        {
            int maxY = heightCap;
            if (maxY > level.GridDimensions.y)
                maxY = level.GridDimensions.y;

            for (int z = 0; z < tableSize.z; z++)
            {
                int layerZ = originZ + z;
                int y = tableSize.y;
                bool peak = tallPeak && z == tableSize.z - 1;
                if (blocks.hasGlass && random.Next(0, 3) != 0)
                    y = PlaceWeakBase(blocks.glass, column, originX, y, layerZ, tableSize.x);

                while (y < maxY - 2 && level.PlacementCount < blockBudget)
                {
                    int before = level.PlacementCount;
                    int postRows = peak ? 3 : 2;
                    y = PlaceArch(column, PickFillProp(), true, originX, y, layerZ, tableSize.x, postRows);
                    if (y < maxY - 1)
                        y = PlaceShelf(originX, y, layerZ, tableSize.x, true);

                    if (level.PlacementCount == before)
                        break;
                }

                PlaceCrown(blocks.Crown(), originX, y, layerZ, tableSize.x);
            }
        }

        private BlockDefinition PickFillProp()
        {
            int roll = random.Next(0, 4);
            if (roll == 0 && blocks.hasGlass)
                return blocks.glass;

            if (roll == 1)
                return blocks.jar;

            if (roll == 2 && blocks.hasOrnament)
                return blocks.ornament;

            return blocks.wood;
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
                    yaw = 30f;
            }

            Vector3Int size;
            int originX;
            int originZ;
            if (!TryPlaceTable(!wide, 1, yaw, out size, out originX, out originZ))
                return "Catalog needs a foundation table.";

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
                y = PlaceShelf(originX, y, originZ, size.x);

            if (allowStone)
                PlaceFortress(column, originX, y, originZ, size.x);

            return "Arch composition built for level " + index.ToString() + ".";
        }

        private string ComposeTwin(int index, BlockDefinition column, bool useWeakBase, float yaw)
        {
            BlockDefinition table;
            if (!TryFindFoundation(true, out table))
                return "Catalog needs a foundation table.";

            Vector3Int size = footprint.ClampSize(table.sizeInCells);
            int gap = 1;
            int span = (size.x * 2) + gap;
            if (span > level.GridDimensions.x)
                return "Grid is too narrow for twin tables.";

            int leftX = CenterX(span);
            int rightX = leftX + size.x + gap;
            int originZ = FitZ(size.z);
            Place(table, new Vector3Int(leftX, 0, originZ), Mathf.Abs(yaw));
            Place(table, new Vector3Int(rightX, 0, originZ), -Mathf.Abs(yaw));
            if (blocks.hasPlank)
                Place(blocks.plank, new Vector3Int(leftX + size.x - 1, size.y, originZ));

            StackTower(column, useWeakBase, leftX, size, originZ);
            StackTower(column, useWeakBase, rightX, size, originZ);
            return "Twin module towers built for level " + index.ToString() + ".";
        }

        private void StackTower(BlockDefinition column, bool useWeakBase, int originX, Vector3Int tableSize, int originZ)
        {
            int y = tableSize.y;
            if (useWeakBase)
                y = PlaceWeakBase(blocks.glass, column, originX, y, originZ, tableSize.x);

            y = PlaceArch(column, blocks.jar, true, originX, y, originZ, tableSize.x);
            y = PlaceShelf(originX, y, originZ, tableSize.x);
            PlaceCrown(blocks.Crown(), originX, y, originZ, tableSize.x);
        }

        private bool TryPlaceTable(bool smallest, int layerSpan, float yaw, out Vector3Int size, out int originX, out int originZ)
        {
            size = Vector3Int.one;
            originX = 0;
            originZ = 0;
            BlockDefinition table;
            if (!TryFindFoundation(smallest, out table))
                return false;

            size = footprint.ClampSize(table.sizeInCells);
            int span = layerSpan;
            if (span < 1)
                span = size.z;

            originX = CenterX(size.x);
            originZ = FitZ(span);
            Place(table, new Vector3Int(originX, 0, originZ), yaw);
            return true;
        }
    }
}
