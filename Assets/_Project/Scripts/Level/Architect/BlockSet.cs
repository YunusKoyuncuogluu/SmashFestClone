using SmashFest.Data;

namespace SmashFest.Level
{
    public class BlockSet
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
}
