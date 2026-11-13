using System.Collections.Generic;
using UnityEngine;

namespace SmashFest.Data
{
    public class JsonLevelRepository
    {
        public const string ResourcesPath = "Levels/LevelsDatabase";
        public const int MaxLevelCount = 1000;

        private readonly List<LevelRecord> records = new List<LevelRecord>(MaxLevelCount);
        private readonly Dictionary<int, LevelData> cache = new Dictionary<int, LevelData>(MaxLevelCount);
        private bool isLoaded;

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
            if (!isLoaded)
                LoadFromResources();
        }

        public void LoadFromResources()
        {
            TextAsset asset = Resources.Load<TextAsset>(ResourcesPath);
            LoadFromJson(asset != null ? asset.text : null);
        }

        public void LoadFromJson(string json)
        {
            records.Clear();
            cache.Clear();
            isLoaded = true;
            if (string.IsNullOrEmpty(json))
                return;

            LevelCollectionData collection = JsonUtility.FromJson<LevelCollectionData>(json);
            if (collection?.levels == null)
                return;

            foreach (LevelRecord record in collection.levels)
            {
                if (record == null)
                    continue;

                record.placements ??= new List<BlockPlacementData>(8);
                records.Add(record);
            }
        }

        public string ToJson(bool prettyPrint)
        {
            EnsureLoaded();
            return JsonUtility.ToJson(new LevelCollectionData { levels = records }, prettyPrint);
        }

        public bool TryGetRecord(int levelIndex, out LevelRecord record)
        {
            EnsureLoaded();
            record = records.Find(candidate => candidate.levelIndex == levelIndex);
            return record != null;
        }

        public LevelData GetLevelDataForInfiniteIndex(int levelNumber)
        {
            EnsureLoaded();
            if (records.Count == 0)
                return null;

            int wrappedIndex = (Mathf.Max(1, levelNumber) - 1) % records.Count + 1;
            return GetOrCreateLevelData(wrappedIndex);
        }

        public int GetCycleIndex(int levelNumber)
        {
            int count = LevelCount;
            return count > 0 ? (Mathf.Max(1, levelNumber) - 1) / count : 0;
        }

        public LevelData GetOrCreateLevelData(int levelIndex)
        {
            EnsureLoaded();
            if (cache.TryGetValue(levelIndex, out LevelData cached) && cached != null)
                return cached;

            if (!TryGetRecord(levelIndex, out LevelRecord record))
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
                return;

            EnsureLoaded();
            int index = records.FindIndex(candidate => candidate.levelIndex == record.levelIndex);
            if (index >= 0)
                records[index] = record;
            else
                records.Add(record);
        }

        public void ReplaceAll(List<LevelRecord> newRecords)
        {
            records.Clear();
            cache.Clear();
            isLoaded = true;
            if (newRecords == null)
                return;

            foreach (LevelRecord record in newRecords)
            {
                if (record != null)
                    records.Add(record);
            }
        }

        public void CaptureLevel(LevelData level)
        {
            if (level == null)
                return;

            UpsertRecord(CreateRecordFromLevel(level));
            cache[level.LevelIndex] = level;
        }

        public LevelRecord CreateRecordFromLevel(LevelData level)
        {
            LevelRecord record = new LevelRecord
            {
                levelIndex = level.LevelIndex,
                maxAmmo = level.MaxAmmo,
                parAmmo = level.ParAmmo,
                gridDimensions = level.GridDimensions,
                cellSize = level.CellSize,
                originOffset = level.OriginOffset,
                fallEliminationY = level.FallEliminationY,
                placements = new List<BlockPlacementData>(level.PlacementCount)
            };

            for (int i = 0; i < level.PlacementCount; i++)
            {
                if (level.TryGetPlacementByIndex(i, out BlockPlacementData placement))
                    record.placements.Add(placement);
            }

            return record;
        }

        public void ApplyRecordToLevel(LevelRecord record, LevelData level)
        {
            if (record == null || level == null)
                return;

            level.LevelIndex = record.levelIndex;
            level.MaxAmmo = record.maxAmmo;
            level.ParAmmo = record.parAmmo;
            level.GridDimensions = record.gridDimensions;
            level.CellSize = record.cellSize;
            level.OriginOffset = record.originOffset;
            level.FallEliminationY = record.fallEliminationY;
            level.ClearPlacements();
            if (record.placements == null)
                return;

            foreach (BlockPlacementData placement in record.placements)
                level.TryAddPlacement(placement);
        }

        public LevelRecord CreateDefaultRecord(int levelIndex)
        {
            return new LevelRecord
            {
                levelIndex = Mathf.Max(1, levelIndex),
                maxAmmo = 10,
                parAmmo = 1,
                gridDimensions = new Vector3Int(16, 14, 6),
                cellSize = Vector3.one,
                originOffset = Vector3.zero,
                fallEliminationY = -1.5f,
                placements = new List<BlockPlacementData>(8)
            };
        }
    }
}
