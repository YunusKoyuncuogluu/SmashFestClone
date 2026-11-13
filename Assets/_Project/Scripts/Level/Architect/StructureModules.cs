using SmashFest.Data;
using UnityEngine;

namespace SmashFest.Level
{
    public class StructureModules
    {
        private readonly LevelBuildContext context;
        private readonly BlockPlacer placer;
        private readonly BlockPaletteFinder palette;

        public StructureModules(LevelBuildContext context, BlockPlacer placer, BlockPaletteFinder palette)
        {
            this.context = context;
            this.placer = placer;
            this.palette = palette;
        }

        private int GridHeight => context.Level.GridDimensions.y;
        private BlockSet Blocks => context.Blocks;

        public int PlaceArch(BlockDefinition column, BlockDefinition opening, bool fillOpening, int originX, int y, int originZ, int width, int preferredHeight = 2)
        {
            if (y >= GridHeight)
                return y;

            int postHeight = PlacePillarPosts(column, opening, fillOpening, originX, y, originZ, width, preferredHeight);
            return PlaceBridge(column, Blocks.hasPlank, originX, y + postHeight, originZ, width);
        }

        public int PlaceWeakBase(BlockDefinition glass, BlockDefinition heavy, int originX, int y, int originZ, int width)
        {
            if (y >= GridHeight)
                return y;

            bool plankFits = TryPlankSpan(width, out int plankWidth, out _);
            bool endsOnly = width > 2 && plankFits && plankWidth == width && context.Random.Next(0, 2) == 0;
            for (int x = 0; x < width; x++)
            {
                bool foot = !endsOnly || x == 0 || x == width - 1;
                if (foot)
                    placer.Place(glass, new Vector3Int(originX + x, y, originZ));
            }

            int top = PlaceBridge(Blocks.wood, Blocks.hasPlank, originX, y + 1, originZ, width);
            placer.PlaceRow(heavy, originX, top, originZ, width);
            return top + 1;
        }

        public int PlaceFortress(BlockDefinition column, int originX, int y, int originZ, int width)
        {
            bool glassWindow = Blocks.hasGlass && context.Random.Next(0, 2) == 0;
            bool spanWithPlank = Blocks.hasPlank;
            BlockDefinition filler = glassWindow ? Blocks.glass : PickFillProp();
            int postHeight = PlacePillarPosts(column, filler, true, originX, y, originZ, width, 2);
            int roof = PlaceBridge(column, spanWithPlank, originX, y + postHeight, originZ, width);
            return PlaceCrown(Blocks.Crown(), originX, roof, originZ, width);
        }

        public int PlaceShelf(int originX, int y, int originZ, int width, bool mixGlass = true)
        {
            if (y >= GridHeight)
                return y;

            for (int x = 0; x < width; x++)
            {
                int roll = context.Random.Next(0, 3);
                BlockDefinition prop = Blocks.jar;
                if (roll == 1 && mixGlass)
                    prop = Blocks.glass;
                else if (roll == 2)
                    prop = Blocks.Crown();

                placer.Place(prop, new Vector3Int(originX + x, y, originZ));
            }

            return y + 1;
        }

        public int PlaceCrown(BlockDefinition crown, int originX, int y, int originZ, int width)
        {
            if (y >= GridHeight)
                return y;

            if (width <= 2)
                placer.PlaceRow(crown, originX, y, originZ, width);
            else
                placer.PlaceRow(crown, originX + 1, y, originZ, width - 2);

            return y + 1;
        }

        public int PlaceBridge(BlockDefinition filler, bool spanWithPlank, int originX, int y, int originZ, int width)
        {
            if (y >= GridHeight)
                return y;

            int plankWidth = 0;
            int margin = 0;
            if (!spanWithPlank || !TryPlankSpan(width, out plankWidth, out margin))
            {
                placer.PlaceRow(filler, originX, y, originZ, width);
                return y + 1;
            }

            placer.Place(Blocks.plank, new Vector3Int(originX + margin, y, originZ));
            for (int i = 0; i < margin; i++)
            {
                placer.Place(filler, new Vector3Int(originX + i, y, originZ));
                placer.Place(filler, new Vector3Int(originX + width - 1 - i, y, originZ));
            }

            int covered = margin + plankWidth;
            for (int i = covered; i < width - margin; i++)
                placer.Place(filler, new Vector3Int(originX + i, y, originZ));

            return y + 1;
        }

        public BlockDefinition PickFillProp()
        {
            int roll = context.Random.Next(0, 4);
            if (roll == 0 && Blocks.hasGlass)
                return Blocks.glass;

            if (roll == 1)
                return Blocks.jar;

            if (roll == 2 && Blocks.hasOrnament)
                return Blocks.ornament;

            return Blocks.wood;
        }

        private int PlacePillarPosts(BlockDefinition column, BlockDefinition opening, bool fillOpening, int originX, int y, int originZ, int width, int preferredHeight)
        {
            BlockDefinition pillar = column;
            int height = 1;
            if (Blocks.hasPillar && palette.TryFindBestPillar(preferredHeight, column.familyKey, out BlockDefinition foundPillar))
            {
                pillar = foundPillar;
                height = context.Footprint.ClampSize(foundPillar.sizeInCells).y;
            }
            else if (preferredHeight > 1)
                height = preferredHeight;

            if (y + height > GridHeight)
                height = GridHeight - y;

            if (height < 1)
                return 0;

            int plankWidth = 0;
            int margin = 0;
            bool spanned = Blocks.hasPlank && TryPlankSpan(width, out plankWidth, out margin);
            bool useSinglePillar = context.Footprint.ClampSize(pillar.sizeInCells).y == height && height > 1;
            if (!useSinglePillar)
            {
                for (int row = 0; row < height; row++)
                    PlaceSupports(column, opening, fillOpening, Blocks.hasPlank, originX, y + row, originZ, width);

                return height;
            }

            placer.Place(pillar, new Vector3Int(originX, y, originZ));
            if (width > 1)
                placer.Place(pillar, new Vector3Int(originX + width - 1, y, originZ));

            if (!fillOpening || width <= 2)
                return height;

            if (palette.TryFindTallProp(opening, height, out BlockDefinition tallOpening) && context.PaletteRandom.Next(0, 2) == 0)
            {
                PlaceInteriorOpenings(tallOpening, spanned, plankWidth, margin, originX, y, originZ, width);
                return height;
            }

            for (int row = 0; row < height; row++)
                PlaceInteriorOpenings(opening, spanned, plankWidth, margin, originX, y + row, originZ, width);

            return height;
        }

        private void PlaceInteriorOpenings(BlockDefinition opening, bool spanned, int plankWidth, int margin, int originX, int y, int originZ, int width)
        {
            for (int x = 0; x < width; x++)
            {
                if (IsOpening(x, width, spanned, plankWidth, margin))
                    placer.Place(opening, new Vector3Int(originX + x, y, originZ));
            }
        }

        private void PlaceSupports(BlockDefinition column, BlockDefinition opening, bool fillOpening, bool spanWithPlank, int originX, int y, int originZ, int width)
        {
            int plankWidth = 0;
            int margin = 0;
            bool spanned = spanWithPlank && TryPlankSpan(width, out plankWidth, out margin);
            for (int x = 0; x < width; x++)
            {
                if (!IsOpening(x, width, spanned, plankWidth, margin))
                    placer.Place(column, new Vector3Int(originX + x, y, originZ));
                else if (fillOpening)
                    placer.Place(opening, new Vector3Int(originX + x, y, originZ));
            }
        }

        private bool IsOpening(int x, int width, bool spanned, int plankWidth, int margin)
        {
            if (!spanned)
                return x > 0 && x < width - 1;

            bool plankEnd = x == margin || x == margin + plankWidth - 1;
            bool underPlank = x >= margin && x < margin + plankWidth;
            return underPlank && !plankEnd;
        }

        private bool TryPlankSpan(int width, out int plankWidth, out int margin)
        {
            plankWidth = 0;
            margin = 0;
            if (!palette.TryFindBestPlank(width, out BlockDefinition plank))
                return false;

            Blocks.plank = plank;
            Blocks.hasPlank = true;
            plankWidth = context.Footprint.ClampSize(plank.sizeInCells).x;
            margin = (width - plankWidth) / 2;
            return true;
        }
    }
}
