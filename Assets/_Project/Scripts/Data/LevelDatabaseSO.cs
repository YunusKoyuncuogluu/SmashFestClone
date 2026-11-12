using UnityEngine;

namespace SmashFast.Data
{
    [CreateAssetMenu(fileName = "LevelDatabase", menuName = "SmashFast/Level Database", order = 4)]
    public class LevelDatabaseSO : ScriptableObject
    {
        [SerializeField] private BlockCatalogSO blockCatalog;
        [SerializeField] private TextAsset levelsJson;

        private readonly JsonLevelRepository repository = new JsonLevelRepository();
        private bool isReady;

        public BlockCatalogSO BlockCatalog => blockCatalog;

        public TextAsset LevelsJson
        {
            get => levelsJson;
            set
            {
                levelsJson = value;
                isReady = false;
            }
        }

        public JsonLevelRepository Repository
        {
            get
            {
                Load();
                return repository;
            }
        }

        public int LevelCount => Repository.LevelCount;

        private void OnEnable() => isReady = false;

        public LevelData GetLevelDataForInfiniteIndex(int levelNumber) => Repository.GetLevelDataForInfiniteIndex(levelNumber);

        public int GetCycleIndex(int levelNumber) => Repository.GetCycleIndex(levelNumber);

        public LevelData GetLevelByIndex(int levelIndex) => Repository.GetOrCreateLevelData(levelIndex);

        public void Invalidate() => isReady = false;

        public void ReloadFromText(string json)
        {
            repository.LoadFromJson(json);
            isReady = true;
        }

        private void Load()
        {
            if (isReady)
                return;

            if (levelsJson != null && !string.IsNullOrEmpty(levelsJson.text))
                repository.LoadFromJson(levelsJson.text);
            else
                repository.LoadFromResources();

            isReady = true;
        }
    }
}
