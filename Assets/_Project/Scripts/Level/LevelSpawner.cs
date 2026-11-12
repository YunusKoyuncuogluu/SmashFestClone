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

        private readonly IUniversalObjectPool pool;
        private readonly FadeMaterialLibrary fadeLibrary;
        private readonly Collider ground;
        private readonly BlockCatalogSO catalog;
        private readonly LevelDatabaseSO levelDatabase;

        private readonly LevelGroundAnchor groundAnchor = new LevelGroundAnchor();
        private readonly LevelRuntimeMutator mutator = new LevelRuntimeMutator();
        private readonly LevelArchitect architect = new LevelArchitect();
        private readonly BlockFootprint footprint = new BlockFootprint();
        private readonly Dictionary<int, bool> compoundPrefabs = new Dictionary<int, bool>(16);
        private readonly List<SpawnedBlock> blocks = new List<SpawnedBlock>(64);

        private Transform levelRoot;
        private LevelData lastLevel;
        private int lastLevelNumber;
        private float yawOffset;
        private bool isClearing;

        public int ActiveDestructibleCount { get; private set; }
        public int ResolvedMaxAmmo { get; private set; }

        public LevelSpawner(
            IUniversalObjectPool pool,
            FadeMaterialLibrary fadeLibrary,
            [Inject(Id = GroundBindingId)] Collider ground,
            [InjectOptional] BlockCatalogSO catalog,
            [InjectOptional] LevelDatabaseSO levelDatabase)
        {
            this.pool = pool;
            this.fadeLibrary = fadeLibrary;
            this.ground = ground;
            this.catalog = catalog;
            this.levelDatabase = levelDatabase;
        }

        public void Initialize()
        {
            if (catalog != null)
                catalog.ScaleChanged += OnScaleChanged;
        }

        public void Dispose()
        {
            if (catalog != null)
                catalog.ScaleChanged -= OnScaleChanged;
        }

        private void OnScaleChanged()
        {
            if (lastLevel != null)
                SpawnLevel(lastLevel, lastLevelNumber);
        }

        public void SpawnLevel(LevelData level, int levelNumber)
        {
            lastLevel = level;
            lastLevelNumber = levelNumber;
            ResolvedMaxAmmo = 0;
            yawOffset = 0f;
            if (level == null)
                return;

            int cycle = levelDatabase != null ? levelDatabase.GetCycleIndex(levelNumber) : 0;
            System.Random random = null;

            LevelData generated = cycle >= 1 ? GenerateLevel(levelNumber, level) : null;
            if (generated != null)
            {
                level = generated;
                ResolvedMaxAmmo = generated.MaxAmmo;
                cycle = 0;
            }
            else
            {
                ResolvedMaxAmmo = mutator.ResolveMaxAmmo(level, levelNumber, cycle);
                if (cycle >= 1)
                {
                    random = new System.Random(levelNumber);
                    yawOffset = mutator.PickExtraYaw(random);
                }
            }

            if (catalog == null)
                return;

            ClearLevel();
            EnsureLevelRoot();

            Vector3 anchor = groundAnchor.Resolve(ground);
            Vector3 centerShift = level.GetHorizontalCenterShift(catalog);
            for (int i = 0; i < level.PlacementCount; i++)
            {
                if (level.TryGetPlacementByIndex(i, out BlockPlacementData placement))
                    SpawnBlock(level, placement, anchor, centerShift, random, levelNumber, cycle);
            }

            foreach (SpawnedBlock block in blocks)
                block.Hold();
        }

        public void UnregisterTarget()
        {
            if (!isClearing && ActiveDestructibleCount > 0)
                ActiveDestructibleCount--;
        }

        private void ClearLevel()
        {
            isClearing = true;
            try
            {
                pool.DespawnAllActive();
            }
            finally
            {
                isClearing = false;
            }

            blocks.Clear();
            ActiveDestructibleCount = 0;
        }

        private LevelData GenerateLevel(int levelNumber, LevelData template)
        {
            if (catalog == null)
                return null;

            LevelData generated = architect.GenerateRuntimeLevel(levelNumber, catalog, template);
            return generated != null && generated.PlacementCount > 0 ? generated : null;
        }

        private void SpawnBlock(LevelData level, BlockPlacementData placement, Vector3 anchor, Vector3 centerShift, System.Random random, int levelNumber, int cycle)
        {
            if (!catalog.TryGetBlockDefinition(placement.blockId, out BlockDefinition definition))
                return;

            definition = mutator.ResolveBlock(definition, catalog, random, levelNumber, cycle, placement.gridPosition.y);
            if (definition.prefab == null)
                return;

            Vector3Int size = level.GetSizeInCells(placement, catalog);
            Vector3Int effectiveSize = footprint.GetEffectiveSize(size, placement.rotationEuler);
            level.GetWorldPose(placement, effectiveSize, catalog, yawOffset, centerShift, out Vector3 center, out Quaternion rotation);

            Quaternion spawnRotation = rotation * Quaternion.Euler(definition.meshEuler);
            GameObject instance = pool.Spawn(definition.prefab, center + anchor, spawnRotation, levelRoot);
            if (instance == null)
                return;

            if (!instance.TryGetComponent(out SpawnedBlock block))
            {
                pool.Despawn(instance);
                return;
            }

            block.Initialize(
                definition,
                GetLayoutScale(definition, level, size),
                catalog.GetScale(),
                catalog.GetTableHeightFactor(level.CellSize.y),
                size,
                catalog.GetMassMultiplier(),
                this,
                pool,
                fadeLibrary);

            blocks.Add(block);
            if (definition.IsTarget())
                ActiveDestructibleCount++;
        }

        private Vector3 GetLayoutScale(BlockDefinition definition, LevelData level, Vector3Int size)
        {
            bool isTable = definition.shapeRole == BlockShapeRole.FoundationTable
                || definition.materialType == BlockMaterialType.TableFoundation;
            if (isTable)
                return Vector3.one;

            if (IsCompound(definition.prefab))
                return level.GetCellWorldScale(definition.baseScale);

            return level.GetFootprintWorldScale(definition.baseScale, size);
        }

        private bool IsCompound(GameObject prefab)
        {
            int id = prefab.GetInstanceID();
            if (compoundPrefabs.TryGetValue(id, out bool isCompound))
                return isCompound;

            int meshCount = 0;
            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh != null)
                    meshCount++;
            }

            isCompound = meshCount > 1;
            compoundPrefabs.Add(id, isCompound);
            return isCompound;
        }

        private void EnsureLevelRoot()
        {
            if (levelRoot == null)
                levelRoot = new GameObject("SpawnedLevel").transform;
        }
    }
}
