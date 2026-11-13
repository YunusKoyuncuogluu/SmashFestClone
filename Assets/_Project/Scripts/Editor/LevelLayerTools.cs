using System.Collections.Generic;
using SmashFest.Data;

namespace SmashFest.LevelEditor
{
    internal class LevelLayerTools
    {
        private readonly List<BlockPlacementData> buffer = new List<BlockPlacementData>(64);

        public int Count(LevelData level, int layer, BlockCatalogSO catalog)
        {
            int total = 0;
            for (int i = 0; i < level.PlacementCount; i++)
            {
                level.TryGetPlacementByIndex(i, out BlockPlacementData placement);
                if (Intersects(level, placement, layer, catalog))
                    total++;
            }

            return total;
        }

        public int Clear(LevelData level, int layer, BlockCatalogSO catalog)
        {
            return level.RemoveWhere(placement => Intersects(level, placement, layer, catalog));
        }

        public int Copy(LevelData level, int sourceLayer, int targetLayer, BlockCatalogSO catalog)
        {
            int depth = level.GridDimensions.z;
            if (sourceLayer == targetLayer || sourceLayer < 0 || targetLayer < 0 || sourceLayer >= depth || targetLayer >= depth)
                return 0;

            level.RemoveWhere(placement => Intersects(level, placement, targetLayer, catalog) && !Intersects(level, placement, sourceLayer, catalog));

            buffer.Clear();
            for (int i = 0; i < level.PlacementCount; i++)
            {
                level.TryGetPlacementByIndex(i, out BlockPlacementData placement);
                if (!Intersects(level, placement, sourceLayer, catalog))
                    continue;

                placement.gridPosition.z += targetLayer - sourceLayer;
                buffer.Add(placement);
            }

            foreach (BlockPlacementData copy in buffer)
                level.AddPlacement(copy);

            return buffer.Count;
        }

        public int RemoveOutOfBounds(LevelData level, BlockCatalogSO catalog)
        {
            return level.RemoveWhere(placement => !level.IsFootprintInsideGrid(placement.gridPosition, level.GetEffectiveSize(placement, catalog)));
        }

        private bool Intersects(LevelData level, in BlockPlacementData placement, int layer, BlockCatalogSO catalog)
        {
            int minZ = placement.gridPosition.z;
            int maxZ = minZ + level.GetEffectiveSize(placement, catalog).z - 1;
            return layer >= minZ && layer <= maxZ;
        }
    }
}
