using UnityEngine;

namespace SmashFest.Data
{
    [CreateAssetMenu(fileName = "LevelDatabase", menuName = "SmashFest/Level Database", order = 4)]
    public class LevelDatabaseSO : ScriptableObject
    {
        [SerializeField] private BlockCatalogSO blockCatalog;
        [SerializeField] private TextAsset levelsJson;

        private readonly JsonLevelRepository repository = new JsonLevelRepository();
        private bool isReady;

        public BlockCatalogSO BlockCatalog => blockCatalog;

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
