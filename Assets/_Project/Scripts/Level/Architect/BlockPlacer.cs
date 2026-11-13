using SmashFest.Data;
using UnityEngine;

namespace SmashFest.Level
{
    public class BlockPlacer
    {
        private readonly LevelBuildContext context;
        private readonly StructureStabilizer stabilizer;

        public BlockPlacer(LevelBuildContext context, StructureStabilizer stabilizer)
        {
            this.context = context;
            this.stabilizer = stabilizer;
        }

        public void Place(BlockDefinition definition, Vector3Int anchor, float yaw = 0f) => Place(definition, anchor, yaw, Vector3Int.zero);

        public void Place(BlockDefinition definition, Vector3Int anchor, float yaw, Vector3Int customSize)
        {
            LevelData level = context.Level;
            if (context.BlockBudget > 0 && level.PlacementCount >= context.BlockBudget)
                return;

            BlockFootprint footprint = context.Footprint;
            bool hasCustomSize = customSize.x > 0 && customSize.y > 0 && customSize.z > 0;
            Vector3 euler = new Vector3(0f, yaw, 0f);
            Vector3Int sizeInCells = footprint.ClampSize(hasCustomSize ? customSize : definition.sizeInCells);
            Vector3Int size = footprint.GetEffectiveSize(sizeInCells, euler);
            if (anchor.x < 0 || anchor.y < 0 || anchor.z < 0)
                return;

            Vector3Int end = anchor + footprint.ClampSize(size) - Vector3Int.one;
            if (!level.IsInsideGrid(end))
                return;

            if (level.ValidatePlacement(anchor, size, context.Catalog) != PlacementValidationStatus.ValidEmpty)
                return;

            if (!stabilizer.HasValidSupport(anchor, size))
                return;

            BlockPlacementData placement = new BlockPlacementData
            {
                blockId = definition.blockId,
                gridPosition = anchor,
                rotationEuler = euler
            };

            if (hasCustomSize)
                placement.customSize = footprint.ClampSize(customSize);

            level.PlaceBlock(placement, size, context.Catalog);
        }

        public void PlaceRow(BlockDefinition block, int originX, int y, int originZ, int width)
        {
            Vector3Int size = context.Footprint.ClampSize(block.sizeInCells);
            if (size.x != 1 || size.z != 1)
                return;

            for (int x = 0; x < width; x++)
                Place(block, new Vector3Int(originX + x, y, originZ));
        }
    }
}
