using System.Collections.Generic;
using SmashFast.Core.Pooling;
using SmashFast.Data;
using UnityEngine;
using Zenject;

namespace SmashFast.Level
{
    public class LevelSpawner : ILevelSpawner, IDestructibleTracker
    {
        private readonly IUniversalObjectPool objectPool;
        private readonly BlockCatalogSO blockCatalog;
        private readonly Dictionary<int, bool> prefabIsCompound;
        private readonly HashSet<int> missingPrefabIds;
        private Transform levelRoot;
        private GameObject groundObject;
        private bool groundMaterialAssigned;
        private readonly int baseColorId = Shader.PropertyToID("_BaseColor");
        private readonly int colorId = Shader.PropertyToID("_Color");
        private int activeDestructibleCount;
        private int resolvedMaxAmmo;
        private float spawnYawOffset;
        private bool suppressDestructibleNotifications;
        private readonly LevelDatabaseSO levelDatabase;
        private readonly LevelRuntimeMutator runtimeMutator = new LevelRuntimeMutator();
        private readonly LevelArchitect levelArchitect = new LevelArchitect();
        private readonly BlockFootprint blockFootprint = new BlockFootprint();
        private LevelData runtimeLevel;

        public int ActiveDestructibleCount
        {
            get
            {
                return activeDestructibleCount;
            }
        }

        public int ResolvedMaxAmmo
        {
            get
            {
                return resolvedMaxAmmo;
            }
        }

        public LevelSpawner(
            IUniversalObjectPool objectPool,
            [InjectOptional] BlockCatalogSO blockCatalog,
            [InjectOptional] LevelDatabaseSO levelDatabase)
        {
            this.objectPool = objectPool;
            this.blockCatalog = blockCatalog;
            this.levelDatabase = levelDatabase;
            prefabIsCompound = new Dictionary<int, bool>(16);
            missingPrefabIds = new HashSet<int>();
        }

        public void SpawnLevel(LevelData levelData, int displayLevelNumber)
        {
            resolvedMaxAmmo = 0;
            spawnYawOffset = 0f;
            if (levelData == null || objectPool == null)
            {
                return;
            }

            int cycleIndex = 0;
            if (levelDatabase != null)
            {
                cycleIndex = levelDatabase.GetCycleIndex(displayLevelNumber);
            }

            System.Random random = null;
            LevelData spawnSource = levelData;
            bool generated = TryUseRuntimeLevel(displayLevelNumber, cycleIndex, levelData, out spawnSource);
            if (generated)
            {
                resolvedMaxAmmo = spawnSource.MaxAmmo;
                spawnYawOffset = 0f;
                cycleIndex = 0;
            }
            else
            {
                resolvedMaxAmmo = runtimeMutator.ResolveMaxAmmo(levelData, displayLevelNumber, cycleIndex);
                if (cycleIndex >= 1)
                {
                    random = new System.Random(displayLevelNumber);
                    spawnYawOffset = runtimeMutator.PickExtraYaw(random);
                }
            }

            levelData = spawnSource;

            if (blockCatalog == null)
            {
                Debug.LogError("LevelSpawner has no BlockCatalogSO.");
                return;
            }

            suppressDestructibleNotifications = true;
            try
            {
                objectPool.DespawnAllActive();
            }
            finally
            {
                suppressDestructibleNotifications = false;
            }

            activeDestructibleCount = 0;
            EnsureLevelRoot();
            EnsureGround(levelData);

            int count = levelData.PlacementCount;
            for (int i = 0; i < count; i++)
            {
                BlockPlacementData placement;
                if (!levelData.TryGetPlacementByIndex(i, out placement))
                {
                    continue;
                }

                SpawnPlacement(levelData, placement, random, displayLevelNumber, cycleIndex);
            }

            SettleSpawnedBodies();
        }

        private void SettleSpawnedBodies()
        {
            if (levelRoot == null)
            {
                return;
            }

            int count = levelRoot.childCount;
            for (int i = 0; i < count; i++)
            {
                Transform child = levelRoot.GetChild(i);
                SpawnedBlock block = child.GetComponent<SpawnedBlock>();
                if (block == null)
                {
                    continue;
                }

                block.SettleInPlace();
            }
        }

        private bool TryUseRuntimeLevel(int displayLevelNumber, int cycleIndex, LevelData authored, out LevelData spawnSource)
        {
            spawnSource = authored;
            ReleaseRuntimeLevel();
            if (cycleIndex < 1 || blockCatalog == null)
            {
                return false;
            }

            runtimeLevel = levelArchitect.GenerateRuntimeLevel(displayLevelNumber, blockCatalog, authored);
            if (runtimeLevel == null || runtimeLevel.PlacementCount == 0)
            {
                ReleaseRuntimeLevel();
                return false;
            }

            spawnSource = runtimeLevel;
            return true;
        }

        private void ReleaseRuntimeLevel()
        {
            if (runtimeLevel == null)
            {
                return;
            }

            Object.Destroy(runtimeLevel);
            runtimeLevel = null;
        }

        private void SpawnPlacement(
            LevelData levelData,
            BlockPlacementData placement,
            System.Random random,
            int displayLevelNumber,
            int cycleIndex)
        {
            if (blockCatalog == null)
            {
                return;
            }

            BlockDefinition definition;
            if (!blockCatalog.TryGetBlockDefinition(placement.blockId, out definition))
            {
                return;
            }

            definition = runtimeMutator.ResolveBlock(
                definition,
                blockCatalog,
                random,
                displayLevelNumber,
                cycleIndex,
                placement.gridPosition.y);

            if (definition.prefab == null)
            {
                if (missingPrefabIds.Add(definition.blockId))
                {
                    Debug.LogWarning("Block " + definition.blockId.ToString() + " has no prefab and was not spawned.");
                }

                return;
            }

            Vector3Int effectiveSize = blockFootprint.GetEffectiveSize(definition.sizeInCells, placement.rotationEuler);
            Vector3 center;
            Quaternion rotation;
            levelData.GetWorldPose(placement, effectiveSize, blockCatalog, spawnYawOffset, out center, out rotation);
            Vector3 targetScale = ResolveTargetScale(definition, levelData);
            GameObject instance = objectPool.Spawn(definition.prefab, center, rotation, levelRoot);
            if (instance == null)
            {
                return;
            }

            MatchGroundMaterial(instance);

            SpawnedBlock block = instance.GetComponent<SpawnedBlock>();
            if (block == null)
            {
                block = instance.AddComponent<SpawnedBlock>();
                objectPool.NotifyInstanceComponentsChanged(instance);
                block.OnSpawned();
            }

            block.InitializeBlock(definition, targetScale);
            block.ConfigureLifetime(this, objectPool, levelData.FallEliminationY);
            if (definition.IsTarget())
            {
                activeDestructibleCount++;
            }
        }

        public void NotifyDestructibleDespawned()
        {
            if (suppressDestructibleNotifications)
            {
                return;
            }

            if (activeDestructibleCount <= 0)
            {
                return;
            }

            activeDestructibleCount--;
        }

        private Vector3 ResolveTargetScale(BlockDefinition definition, LevelData levelData)
        {
            if (IsCompoundPrefab(definition.prefab))
            {
                return levelData.GetCellWorldScale(definition.baseScale);
            }

            return levelData.GetFootprintWorldScale(definition.baseScale, definition.sizeInCells);
        }

        private bool IsCompoundPrefab(GameObject prefab)
        {
            int prefabId = prefab.GetInstanceID();
            bool isCompound;
            if (prefabIsCompound.TryGetValue(prefabId, out isCompound))
            {
                return isCompound;
            }

            MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            int meshCount = 0;
            int filterCount = filters.Length;
            for (int i = 0; i < filterCount; i++)
            {
                MeshFilter filter = filters[i];
                if (filter == null || filter.sharedMesh == null)
                {
                    continue;
                }

                meshCount++;
            }

            isCompound = meshCount > 1;
            prefabIsCompound.Add(prefabId, isCompound);
            return isCompound;
        }

        private void EnsureLevelRoot()
        {
            if (levelRoot != null)
            {
                return;
            }

            GameObject rootObject = new GameObject("SpawnedLevel");
            levelRoot = rootObject.transform;
        }

        private void EnsureGround(LevelData levelData)
        {
            if (groundObject == null)
            {
                groundObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                groundObject.name = "Ground";
                groundObject.transform.SetParent(levelRoot, false);
            }

            Vector3 cell = levelData.CellSize;
            Vector3Int grid = levelData.GridDimensions;
            Vector3 origin = levelData.OriginOffset;
            Vector3 center = new Vector3(origin.x, origin.y, origin.z);
            float width = grid.x * cell.x;
            float depth = grid.z * cell.z;
            float thickness = Mathf.Max(0.2f, cell.y);
            float yawSpan = FindMaxPlacementYaw(levelData) + Mathf.Abs(spawnYawOffset);
            float coverage = 1f;
            if (yawSpan > 0.5f)
            {
                coverage = 1.35f + (yawSpan / 90f);
            }

            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0)
            {
                groundObject.layer = groundLayer;
            }

            groundObject.transform.SetPositionAndRotation(
                new Vector3(center.x, origin.y - (thickness * 0.5f), center.z),
                Quaternion.identity);
            groundObject.transform.localScale = new Vector3(
                (width + (cell.x * 2f)) * coverage,
                thickness,
                (depth + (cell.z * 2f)) * coverage);
        }

        private float FindMaxPlacementYaw(LevelData levelData)
        {
            float maxYaw = 0f;
            int count = levelData.PlacementCount;
            for (int i = 0; i < count; i++)
            {
                BlockPlacementData placement;
                if (!levelData.TryGetPlacementByIndex(i, out placement))
                {
                    continue;
                }

                float yaw = Mathf.Abs(placement.rotationEuler.y);
                if (yaw > maxYaw)
                {
                    maxYaw = yaw;
                }
            }

            return maxYaw;
        }

        private void MatchGroundMaterial(GameObject blockInstance)
        {
            if (groundMaterialAssigned || groundObject == null || blockInstance == null)
            {
                return;
            }

            Renderer blockRenderer = blockInstance.GetComponent<Renderer>();
            Renderer groundRenderer = groundObject.GetComponent<Renderer>();
            if (blockRenderer == null || groundRenderer == null || blockRenderer.sharedMaterial == null)
            {
                return;
            }

            groundRenderer.sharedMaterial = blockRenderer.sharedMaterial;
            MaterialPropertyBlock tint = new MaterialPropertyBlock();
            tint.SetColor(baseColorId, new Color(0.32f, 0.33f, 0.36f, 1f));
            tint.SetColor(colorId, new Color(0.32f, 0.33f, 0.36f, 1f));
            groundRenderer.SetPropertyBlock(tint);
            groundMaterialAssigned = true;
        }
    }
}
