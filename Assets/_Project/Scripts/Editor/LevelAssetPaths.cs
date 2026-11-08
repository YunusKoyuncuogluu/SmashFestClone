using System;

namespace SmashFast.LevelEditor
{
    internal static class LevelAssetPaths
    {
        public const string ProjectFolder = "Assets/_Project";
        public const string GameDataFolder = "Assets/_Project/GameData";
        public const string LevelsFolder = "Assets/_Project/GameData/Levels";
        public const string CatalogPath = "Assets/_Project/GameData/BlockCatalog.asset";
        public const string LevelPath = "Assets/_Project/GameData/Levels/Level_01.asset";
        public const string LevelDatabasePath = "Assets/_Project/GameData/LevelDatabase.asset";

        public static bool IsProjectAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            return path.StartsWith(ProjectFolder + "/", StringComparison.Ordinal)
                || path.StartsWith(ProjectFolder + "\\", StringComparison.Ordinal);
        }
    }
}
