using SmashFest.Data;
using UnityEngine;

namespace SmashFest.Level
{
    public class LevelBuildContext
    {
        private readonly float[] yawSteps = { -45f, -30f, 0f, 30f, 45f };

        public BlockFootprint Footprint { get; } = new BlockFootprint();
        public LevelData Level { get; private set; }
        public BlockCatalogSO Catalog { get; private set; }
        public BlockSet Blocks { get; set; }
        public System.Random Random { get; private set; } = new System.Random(1);
        public System.Random PaletteRandom { get; private set; } = new System.Random(1);
        public int PreferredLayer { get; private set; }
        public int BlockBudget { get; set; }
        public int LastAnchorLayer { get; private set; }

        public void Begin(LevelData level, BlockCatalogSO catalog, int index, int layerZ)
        {
            Level = level;
            Catalog = catalog;
            Random = new System.Random(index);
            PaletteRandom = new System.Random((index * 7919) + 17);
            PreferredLayer = layerZ < 0 ? 0 : layerZ;
            LastAnchorLayer = PreferredLayer;
        }

        public int HeightCapFor(int index)
        {
            int gridY = Level != null ? Level.GridDimensions.y : 8;
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

        public int CenterX(int width)
        {
            int x = (Level.GridDimensions.x - width) / 2;
            if (x < 0)
                return 0;

            return x;
        }

        public int FitZ(int depth) => FitZAt(PreferredLayer, depth);

        public int FitZAt(int desiredZ, int depth)
        {
            int span = depth < 1 ? 1 : depth;
            int maxAnchor = Level.GridDimensions.z - span;
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

        public float PickYaw(bool steep)
        {
            if (steep)
                return yawSteps[Random.Next(0, yawSteps.Length)];

            int roll = Random.Next(0, 3);
            if (roll == 0)
                return -30f;

            if (roll == 1)
                return 30f;

            return 0f;
        }
    }
}
