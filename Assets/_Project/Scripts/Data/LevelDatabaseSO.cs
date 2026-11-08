using System.Collections.Generic;
using UnityEngine;

namespace SmashFast.Data
{
    [CreateAssetMenu(fileName = "LevelDatabase", menuName = "SmashFast/Level Database", order = 4)]
    public class LevelDatabaseSO : ScriptableObject
    {
        [SerializeField] private BlockCatalogSO blockCatalog;
        [SerializeField] private List<LevelData> levels = new List<LevelData>(16);

        public BlockCatalogSO BlockCatalog
        {
            get
            {
                return blockCatalog;
            }
        }

        public int LevelCount
        {
            get
            {
                if (levels == null)
                {
                    return 0;
                }

                return levels.Count;
            }
        }

        public LevelData GetLevelDataForInfiniteIndex(int displayLevelNumber)
        {
            if (levels == null || levels.Count == 0)
            {
                return null;
            }

            int safeNumber = displayLevelNumber < 1 ? 1 : displayLevelNumber;
            int count = levels.Count;
            int startIndex = (safeNumber - 1) % count;
            for (int offset = 0; offset < count; offset++)
            {
                int index = startIndex + offset;
                if (index >= count)
                {
                    index -= count;
                }

                LevelData levelData = levels[index];
                if (levelData != null)
                {
                    return levelData;
                }
            }

            return null;
        }

        public int GetCycleIndex(int displayLevelNumber)
        {
            int count = LevelCount;
            if (count <= 0)
            {
                return 0;
            }

            int safeNumber = displayLevelNumber < 1 ? 1 : displayLevelNumber;
            return (safeNumber - 1) / count;
        }

        private void OnEnable()
        {
            if (levels == null)
            {
                levels = new List<LevelData>(16);
            }
        }
    }
}
