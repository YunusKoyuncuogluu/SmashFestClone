using SmashFast.Data;
using SmashFast.Level;

namespace SmashFast.LevelEditor
{
    internal class LevelArchitecturePresetBuilder
    {
        private readonly LevelArchitect architect = new LevelArchitect();

        public int LastAnchorLayer
        {
            get
            {
                return architect.LastAnchorLayer;
            }
        }

        public string Apply(LevelData level, BlockCatalogSO catalog, int presetIndex, int layerZ)
        {
            return architect.ApplyPreset(level, catalog, presetIndex, layerZ);
        }

        public string GenerateForLevelIndex(LevelData level, BlockCatalogSO catalog, int levelIndex)
        {
            return architect.GenerateForLevelIndex(level, catalog, levelIndex);
        }
    }
}
