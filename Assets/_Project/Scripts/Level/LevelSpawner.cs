using System;
using System.Collections.Generic;
using SmashFast.Core.Effects;
using SmashFast.Core.Pooling;
using SmashFast.Data;
using UnityEngine;
using Zenject;

namespace SmashFast.Level
{
    public class LevelSpawner : ILevelSpawner, IDestructibleTracker, IInitializable, IDisposable
    {
        public const string GroundBindingId = "LevelGround";

        private readonly IUniversalObjectPool objectPool;
        private readonly BlockCatalogSO blockCatalog;
        private readonly FadeMaterialLibrary fadeLibrary;
        private readonly Dictionary<int, bool> prefabIsCompound;
        private Transform levelRoot;
        private readonly Collider groundCollider;
        private readonly LevelGroundAnchor groundAnchorResolver = new LevelGroundAnchor();
        private LevelData lastLevelData;
        private int lastLevelNumber;
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
            FadeMaterialLibrary fadeLibrary,
            [Inject(Id = GroundBindingId)] Collider groundCollider,
            [InjectOptional] BlockCatalogSO blockCatalog,
            [InjectOptional] LevelDatabaseSO levelDatabase)
        {
            this.objectPool = objectPool;
            this.fadeLibrary = fadeLibrary;
            this.groundCollider = groundCollider;
            this.blockCatalog = blockCatalog;
            this.levelDatabase = levelDatabase;
            prefabIsCompound = new Dictionary<int, bool>(16);
        }

        public void Initialize()
        {
            if (blockCatalog != null)
            {
                blockCatalog.ScaleChanged += OnScaleChanged;
            }
        }

        public void Dispose()
        {
            if (blockCatalog != null)
            {
                blockCatalog.ScaleChanged -= OnScaleChanged;
            }
        }

        private void OnScaleChanged()
        {
            if (lastLevelData == null)
            {
                return;
            }

            SpawnLevel(lastLevelData, lastLevelNumber);
        }

        public void SpawnLevel(LevelData levelData, int displayLevelNumber)
        {
            lastLevelData = levelData;
            lastLevelNumber = displayLevelNumber;
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

            Vector3 groundAnchor = groundAnchorResolver.Resolve(groundCollider);

            int count = levelData.PlacementCount;
            for (int i = 0; i < count; i++)
            {
                BlockPlacementData placement;
                if (!levelData.TryGetPlacementByIndex(i, out placement))
                {
                    continue;
                }

                SpawnPlacement(levelData, placement, groundAnchor, random, displayLevelNumber, cycleIndex);
            }

            HoldSpawnedBodies();
        }

        private void HoldSpawnedBodies()
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

                block.Hold();
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
            runtimeLevel = null;
        }

        private void SpawnPlacement(
            LevelData levelData,
            BlockPlacementData placement,
            Vector3 groundAnchor,
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
                return;
            }

            Vector3Int sizeInCells = levelData.GetSizeInCells(placement, blockCatalog);
            Vector3Int effectiveSize = blockFootprint.GetEffectiveSize(sizeInCells, placement.rotationEuler);
            Vector3 center;
            Quaternion rotation;
            levelData.GetWorldPose(placement, effectiveSize, blockCatalog, spawnYawOffset, out center, out rotation);
            center += groundAnchor;
            Vector3 targetScale = ResolveTargetScale(definition, levelData, sizeInCells);
            Quaternion spawnRotation = rotation * Quaternion.Euler(definition.meshEuler);
            GameObject instance = objectPool.Spawn(definition.prefab, center, spawnRotation, levelRoot);
            if (instance == null)
            {
                return;
            }

            SpawnedBlock block = instance.GetComponent<SpawnedBlock>();
            if (block == null)
            {
                objectPool.Despawn(instance);
                return;
            }

            block.Initialize(definition, targetScale, blockCatalog.GetScale(), blockCatalog.GetTableHeightFactor(levelData.CellSize.y), sizeInCells, blockCatalog.GetMassMultiplier(), this, objectPool, fadeLibrary);
            if (definition.IsTarget())
            {
                activeDestructibleCount++;
            }
        }

        public void UnregisterTarget()
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

        private Vector3 ResolveTargetScale(BlockDefinition definition, LevelData levelData, Vector3Int sizeInCells)
        {
            bool foundationTable = definition.shapeRole == BlockShapeRole.FoundationTable
                || definition.materialType == BlockMaterialType.TableFoundation;
            if (foundationTable)
            {
                return Vector3.one;
            }

            if (IsCompoundPrefab(definition.prefab))
            {
                return levelData.GetCellWorldScale(definition.baseScale);
            }

            return levelData.GetFootprintWorldScale(definition.baseScale, sizeInCells);
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
    }
}
