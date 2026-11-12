using UnityEngine;

namespace SmashFast.Data
{
    [CreateAssetMenu(fileName = "LevelDatabase", menuName = "SmashFast/Level Database", order = 4)]
    public class LevelDatabaseSO : ScriptableObject
    {
        [SerializeField] private BlockCatalogSO blockCatalog;
        [SerializeField] private TextAsset levelsJson;

        private readonly JsonLevelRepository repository = new JsonLevelRepository();
        private bool repositoryReady;

        public BlockCatalogSO BlockCatalog
        {
            get
            {
                return blockCatalog;
            }
        }

        public TextAsset LevelsJson
        {
            get
            {
                return levelsJson;
            }
            set
            {
                levelsJson = value;
                repositoryReady = false;
            }
        }

        public JsonLevelRepository Repository
        {
            get
            {
                EnsureRepository();
                return repository;
            }
        }

        public int LevelCount
        {
            get
            {
                EnsureRepository();
                return repository.LevelCount;
            }
        }

        public LevelData GetLevelDataForInfiniteIndex(int displayLevelNumber)
        {
            EnsureRepository();
            return repository.GetLevelDataForInfiniteIndex(displayLevelNumber);
        }

        public int GetCycleIndex(int displayLevelNumber)
        {
            EnsureRepository();
            return repository.GetCycleIndex(displayLevelNumber);
        }

        public LevelData GetLevelByIndex(int levelIndex)
        {
            EnsureRepository();
            return repository.GetOrCreateLevelData(levelIndex);
        }

        private void OnEnable()
        {
            repositoryReady = false;
        }

        public void Invalidate()
        {
            repositoryReady = false;
        }

        public void ReloadFromText(string json)
        {
            repository.LoadFromJson(json);
            repositoryReady = true;
        }

        private void EnsureRepository()
        {
            if (repositoryReady)
            {
                return;
            }

            if (levelsJson != null && !string.IsNullOrEmpty(levelsJson.text))
            {
                repository.LoadFromJson(levelsJson.text);
            }
            else
            {
                repository.LoadFromResources();
            }

            repositoryReady = true;
        }
    }
}
