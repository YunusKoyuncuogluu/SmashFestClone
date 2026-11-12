using SmashFast.Data;
using UnityEngine;

namespace SmashFast.Level
{
    public partial class LevelArchitect
    {
        private int PlaceArch(
            BlockDefinition column,
            BlockDefinition opening,
            bool fillOpening,
            int originX,
            int y,
            int originZ,
            int width)
        {
            return PlaceArch(column, opening, fillOpening, originX, y, originZ, width, 2);
        }

        private int PlaceArch(
            BlockDefinition column,
            BlockDefinition opening,
            bool fillOpening,
            int originX,
            int y,
            int originZ,
            int width,
            int preferredHeight)
        {
            if (y >= level.GridDimensions.y)
                return y;

            int postHeight = PlacePillarPosts(column, opening, fillOpening, originX, y, originZ, width, preferredHeight);
            return PlaceBridge(column, blocks.hasPlank, originX, y + postHeight, originZ, width);
        }

        private int PlacePillarPosts(
            BlockDefinition column,
            BlockDefinition opening,
            bool fillOpening,
            int originX,
            int y,
            int originZ,
            int width,
            int preferredHeight)
        {
            BlockDefinition pillar = column;
            int height = 1;
            BlockDefinition foundPillar;
            if (blocks.hasPillar && TryFindBestPillar(preferredHeight, column.familyKey, out foundPillar))
            {
                pillar = foundPillar;
                height = footprint.ClampSize(foundPillar.sizeInCells).y;
            }
            else if (preferredHeight > 1)
                height = preferredHeight;

            if (y + height > level.GridDimensions.y)
                height = level.GridDimensions.y - y;

            if (height < 1)
                return 0;

            int plankWidth = 0;
            int margin = 0;
            bool spanned = blocks.hasPlank && TryPlankSpan(width, out plankWidth, out margin);
            bool useSinglePillar = footprint.ClampSize(pillar.sizeInCells).y == height && height > 1;
            if (useSinglePillar)
            {
                Place(pillar, new Vector3Int(originX, y, originZ));
                if (width > 1)
                    Place(pillar, new Vector3Int(originX + width - 1, y, originZ));

                if (fillOpening && width > 2)
                {
                    BlockDefinition tallOpening;
                    if (TryFindTallProp(opening, height, out tallOpening) && paletteRandom.Next(0, 2) == 0)
                    {
                        PlaceInteriorOpenings(tallOpening, spanned, plankWidth, margin, originX, y, originZ, width);
                        return height;
                    }

                    for (int row = 0; row < height; row++)
                        PlaceInteriorOpenings(opening, spanned, plankWidth, margin, originX, y + row, originZ, width);
                }

                return height;
            }

            for (int row = 0; row < height; row++)
                PlaceSupports(column, opening, fillOpening, blocks.hasPlank, originX, y + row, originZ, width);

            return height;
        }

        private void PlaceInteriorOpenings(
            BlockDefinition opening,
            bool spanned,
            int plankWidth,
            int margin,
            int originX,
            int y,
            int originZ,
            int width)
        {
            for (int x = 0; x < width; x++)
            {
                bool plankEnd = spanned && (x == margin || x == margin + plankWidth - 1);
                bool underPlank = spanned && x >= margin && x < margin + plankWidth;
                bool hole = underPlank && !plankEnd;
                if (!spanned)
                    hole = x > 0 && x < width - 1;

                if (!hole)
                    continue;

                Place(opening, new Vector3Int(originX + x, y, originZ));
            }
        }

        private int PlaceWeakBase(BlockDefinition glass, BlockDefinition heavy, int originX, int y, int originZ, int width)
        {
            if (y >= level.GridDimensions.y)
                return y;

            int plankWidth;
            int margin;
            bool plankFits = TryPlankSpan(width, out plankWidth, out margin);
            bool endsOnly = width > 2 && plankFits && plankWidth == width && random.Next(0, 2) == 0;
            for (int x = 0; x < width; x++)
            {
                bool foot = !endsOnly || x == 0 || x == width - 1;
                if (!foot)
                    continue;

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
            BlockDefinition filler = glassWindow ? blocks.glass : PickFillProp();
            bool fill = true;
            int postHeight = PlacePillarPosts(column, filler, fill, originX, y, originZ, width, 2);
            int roof = PlaceBridge(column, span, originX, y + postHeight, originZ, width);
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
                    hole = x > 0 && x < width - 1;

                if (hole)
                {
                    if (fillOpening)
                        Place(opening, new Vector3Int(originX + x, y, originZ));
                }
                else
                    Place(column, new Vector3Int(originX + x, y, originZ));
            }
        }

        private int PlaceShelf(int originX, int y, int originZ, int width, bool mixGlass = true)
        {
            if (y >= level.GridDimensions.y)
                return y;

            for (int x = 0; x < width; x++)
            {
                int roll = random.Next(0, 3);
                BlockDefinition prop = blocks.jar;
                if (roll == 1 && mixGlass)
                    prop = blocks.glass;
                else if (roll == 2)
                    prop = blocks.Crown();

                Place(prop, new Vector3Int(originX + x, y, originZ));
            }

            return y + 1;
        }

        private int PlaceCrown(BlockDefinition crown, int originX, int y, int originZ, int width)
        {
            if (y >= level.GridDimensions.y)
                return y;

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
                return y;

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
                Place(filler, new Vector3Int(originX + i, y, originZ));

            return y + 1;
        }

        private bool TryPlankSpan(int width, out int plankWidth, out int margin)
        {
            plankWidth = 0;
            margin = 0;
            BlockDefinition plank;
            if (!TryFindBestPlank(width, out plank))
                return false;

            blocks.plank = plank;
            blocks.hasPlank = true;
            plankWidth = footprint.ClampSize(plank.sizeInCells).x;
            margin = (width - plankWidth) / 2;
            return true;
        }

        private void PlaceRow(BlockDefinition block, int originX, int y, int originZ, int width)
        {
            Vector3Int size = footprint.ClampSize(block.sizeInCells);
            if (size.x != 1 || size.z != 1)
                return;

            for (int x = 0; x < width; x++)
                Place(block, new Vector3Int(originX + x, y, originZ));
        }
    }
}
