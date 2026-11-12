using System.Collections.Generic;
using UnityEngine;

namespace SmashFast.Data
{
    public class JsonLevelRepository
    {
        public const string ResourcesPath = "Levels/LevelsDatabase";
        public const int MaxLevelCount = 1000;

        private readonly List<LevelRecord> records = new List<LevelRecord>(MaxLevelCount);
        private readonly Dictionary<int, LevelData> cache = new Dictionary<int, LevelData>(MaxLevelCount);
        private bool loaded;

        public int LevelCount
        {
            get
            {
                EnsureLoaded();
                return records.Count;
            }
        }

        public void EnsureLoaded()
        {
            if (loaded)
            {
                return;
            }

            LoadFromResources();
        }

        public void LoadFromResources()
        {
            TextAsset asset = Resources.Load<TextAsset>(ResourcesPath);
            if (asset == null)
            {
                records.Clear();
                loaded = true;
                return;
            }

            LoadFromJson(asset.text);
        }

        public void LoadFromJson(string json)
        {
            records.Clear();
            ClearCache();
            if (string.IsNullOrEmpty(json))
            {
                loaded = true;
                return;
            }

            LevelCollectionData collection = JsonUtility.FromJson<LevelCollectionData>(json);
            if (collection != null && collection.levels != null)
            {
                int count = collection.levels.Count;
                for (int i = 0; i < count; i++)
                {
                    LevelRecord record = collection.levels[i];
                    if (record == null)
                    {
                        continue;
                    }

                    if (record.placements == null)
                    {
                        record.placements = new List<BlockPlacementData>(8);
                    }

                    records.Add(record);
                }
            }

            loaded = true;
        }

        public string ToJson(bool prettyPrint)
        {
            EnsureLoaded();
            LevelCollectionData collection = new LevelCollectionData();
            collection.levels = records;
            return JsonUtility.ToJson(collection, prettyPrint);
        }

        public bool TryGetRecord(int levelIndex, out LevelRecord record)
        {
            EnsureLoaded();
            record = null;
            int count = records.Count;
            for (int i = 0; i < count; i++)
            {
                if (records[i].levelIndex != levelIndex)
                {
                    continue;
                }

                record = records[i];
                return true;
            }

            return false;
        }

        public LevelData GetLevelDataForInfiniteIndex(int displayLevelNumber)
        {
            EnsureLoaded();
            if (records.Count == 0)
            {
                return null;
            }

            int safeNumber = displayLevelNumber < 1 ? 1 : displayLevelNumber;
            int wrappedIndex = ((safeNumber - 1) % records.Count) + 1;
            return GetOrCreateLevelData(wrappedIndex);
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

        public LevelData GetOrCreateLevelData(int levelIndex)
        {
            EnsureLoaded();
            LevelData cached;
            if (cache.TryGetValue(levelIndex, out cached) && cached != null)
            {
                return cached;
            }

            LevelRecord record;
            if (!TryGetRecord(levelIndex, out record))
            {
                record = CreateDefaultRecord(levelIndex);
                UpsertRecord(record);
            }

            LevelData level = new LevelData();
            ApplyRecordToLevel(record, level);
            cache[levelIndex] = level;
            return level;
        }

        public void UpsertRecord(LevelRecord record)
        {
            if (record == null)
            {
                return;
            }

            EnsureLoaded();
            int count = records.Count;
            for (int i = 0; i < count; i++)
            {
                if (records[i].levelIndex != record.levelIndex)
                {
                    continue;
                }

                records[i] = record;
                return;
            }

            records.Add(record);
        }

        public void ReplaceAll(List<LevelRecord> nextRecords)
        {
            records.Clear();
            ClearCache();
            if (nextRecords != null)
            {
                int count = nextRecords.Count;
                for (int i = 0; i < count; i++)
                {
                    LevelRecord record = nextRecords[i];
                    if (record == null)
                    {
                        continue;
                    }

                    records.Add(record);
                }
            }

            loaded = true;
        }

        public void CaptureLevel(LevelData level)
        {
            if (level == null)
            {
                return;
            }

            LevelRecord record = CreateRecordFromLevel(level);
            UpsertRecord(record);
            cache[level.LevelIndex] = level;
        }

        public LevelRecord CreateRecordFromLevel(LevelData level)
        {
            LevelRecord record = new LevelRecord();
            record.levelIndex = level.LevelIndex;
            record.maxAmmo = level.MaxAmmo;
            record.parAmmo = level.ParAmmo;
            record.gridDimensions = level.GridDimensions;
            record.cellSize = level.CellSize;
            record.originOffset = level.OriginOffset;
            record.fallEliminationY = level.FallEliminationY;
            record.placements = new List<BlockPlacementData>(level.PlacementCount);
            int count = level.PlacementCount;
            for (int i = 0; i < count; i++)
            {
                BlockPlacementData placement;
                if (!level.TryGetPlacementByIndex(i, out placement))
                {
                    continue;
                }

                record.placements.Add(placement);
            }

            return record;
        }

        public void ApplyRecordToLevel(LevelRecord record, LevelData level)
        {
            if (record == null || level == null)
            {
                return;
            }

            level.LevelIndex = record.levelIndex;
            level.MaxAmmo = record.maxAmmo;
            level.ParAmmo = record.parAmmo;
            level.GridDimensions = record.gridDimensions;
            level.CellSize = record.cellSize;
            level.OriginOffset = record.originOffset;
            level.FallEliminationY = record.fallEliminationY;
            level.ClearPlacements();
            if (record.placements == null)
            {
                return;
            }

            int count = record.placements.Count;
            for (int i = 0; i < count; i++)
            {
                level.TryAddPlacement(record.placements[i]);
            }
        }

        public LevelRecord CreateDefaultRecord(int levelIndex)
        {
            LevelRecord record = new LevelRecord();
            record.levelIndex = levelIndex < 1 ? 1 : levelIndex;
            record.maxAmmo = 10;
            record.parAmmo = 1;
            record.gridDimensions = new Vector3Int(16, 14, 6);
            record.cellSize = Vector3.one;
            record.originOffset = Vector3.zero;
            record.fallEliminationY = -1.5f;
            record.placements = new List<BlockPlacementData>(8);
            return record;
        }

        private void ClearCache()
        {
            cache.Clear();
        }
    }
}
