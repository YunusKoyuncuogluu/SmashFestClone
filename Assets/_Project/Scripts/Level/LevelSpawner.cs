using System;
using System.Collections.Generic;
using SmashFest.Core.Effects;
using SmashFest.Core.Pooling;
using SmashFest.Data;
using UnityEngine;
using Zenject;

namespace SmashFest.Level
{
    public class LevelSpawner : ILevelSpawner, IDestructibleTracker, IInitializable, IDisposable
    {
        public const string GroundBindingId = "LevelGround";
        public const string RootBindingId = "LevelRoot";
        private const float FallMargin = 0.1f;

        private readonly IPrefabPool pool;
        private readonly IImpactFeedback feedback;
        private readonly Collider ground;
        private readonly BlockCatalogSO catalog;
        private readonly LevelDatabaseSO levelDatabase;

        private readonly LevelGroundAnchor groundAnchor = new LevelGroundAnchor();
        private readonly LevelRuntimeMutator mutator = new LevelRuntimeMutator();
        private readonly LevelArchitect architect = new LevelArchitect();
        private readonly BlockFootprint footprint = new BlockFootprint();
        private readonly Dictionary<int, bool> compoundPrefabs = new Dictionary<int, bool>(16);
        private readonly List<SpawnedBlock> blocks = new List<SpawnedBlock>(64);

        private readonly Transform levelRoot;
        private LevelData lastLevel;
        private int lastLevelNumber;
        private float yawOffset;
        private bool isClearing;

        public int ActiveDestructibleCount { get; private set; }
        public int ResolvedMaxAmmo { get; private set; }
        public Bounds LevelBounds { get; private set; }
        public bool IsBonusLevel { get; private set; }
        public float FallLineY { get; private set; } = float.MinValue;
        public IReadOnlyList<SpawnedBlock> Blocks => blocks;

        public LevelSpawner(
            IPrefabPool pool,
            [Inject(Id = GroundBindingId)] Collider ground,
            [Inject(Id = RootBindingId)] Transform levelRoot,
            [InjectOptional] BlockCatalogSO catalog,
            [InjectOptional] LevelDatabaseSO levelDatabase,
            [InjectOptional] IImpactFeedback feedback)
        {
            this.feedback = feedback;
            this.pool = pool;
            this.ground = ground;
            this.levelRoot = levelRoot;
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
            IsBonusLevel = mutator.IsBonusLevel(levelNumber);
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

            Vector3 anchor = groundAnchor.Resolve(ground);
            LevelLayout layout = new LevelLayout(level, catalog);
            Vector3 centerShift = layout.GetCenterShift();
            for (int i = 0; i < level.PlacementCount; i++)
            {
                if (level.TryGetPlacementByIndex(i, out BlockPlacementData placement))
                    SpawnBlock(level, layout, placement, anchor, centerShift, random, levelNumber, cycle);
            }

            LevelBounds = MeasureBounds(anchor);
            FallLineY = MeasureFallLine();
        }

        private Bounds MeasureBounds(Vector3 anchor)
        {
            Bounds bounds = new Bounds(anchor, Vector3.one);
            bool hasBounds = false;
            foreach (SpawnedBlock block in blocks)
            {
                foreach (Renderer blockRenderer in block.GetComponentsInChildren<Renderer>())
                {
                    if (hasBounds)
                    {
                        bounds.Encapsulate(blockRenderer.bounds);
                        continue;
                    }

                    bounds = blockRenderer.bounds;
                    hasBounds = true;
                }
            }

            return bounds;
        }

        public void EliminateFallenTargets()
        {
            for (int i = 0; i < blocks.Count; i++)
                blocks[i].EliminateIfFallen(FallLineY);
        }

        private float TopOf(SpawnedBlock table)
        {
            float top = float.MinValue;
            foreach (Collider part in table.GetComponentsInChildren<Collider>())
                top = Mathf.Max(top, part.bounds.max.y);

            return top;
        }

        private float MeasureFallLine()
        {
            float lowestTop = float.MaxValue;
            for (int i = 0; i < blocks.Count; i++)
            {
                if (blocks[i].IsFoundation)
                    lowestTop = Mathf.Min(lowestTop, TopOf(blocks[i]));
            }

            return lowestTop < float.MaxValue ? lowestTop - FallMargin : float.MinValue;
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

        private void SpawnBlock(LevelData level, LevelLayout layout, BlockPlacementData placement, Vector3 anchor, Vector3 centerShift, System.Random random, int levelNumber, int cycle)
        {
            if (!catalog.TryGetBlockDefinition(placement.blockId, out BlockDefinition definition))
                return;

            definition = mutator.ResolveBlock(definition, catalog, random, levelNumber, cycle, placement.gridPosition.y);
            if (IsBonusLevel)
                definition = mutator.ToBonusBlock(definition, catalog, levelNumber, placement.gridPosition);

            if (definition.prefab == null)
                return;

            Vector3Int size = level.GetSizeInCells(placement, catalog);
            Vector3Int effectiveSize = footprint.GetEffectiveSize(size, placement.rotationEuler);
            layout.GetWorldPose(placement, effectiveSize, yawOffset, centerShift, out Vector3 center, out Quaternion rotation);

            Quaternion spawnRotation = rotation * Quaternion.Euler(definition.meshEuler);
            GameObject instance = pool.Spawn(definition.prefab, center + anchor, spawnRotation, levelRoot);
            if (instance == null)
                return;

            if (!instance.TryGetComponent(out SpawnedBlock block))
            {
                pool.Despawn(instance);
                return;
            }

            block.Initialize(definition, new BlockSetup
            {
                LayoutScale = GetLayoutScale(definition, layout, size),
                GlobalScale = catalog.GetScale(),
                TableHeightFactor = catalog.GetTableHeightFactor(level.CellSize.y),
                SizeInCells = size,
                MassMultiplier = catalog.GetMassMultiplier(),
                Tracker = this,
                Pool = pool,
                Feedback = feedback
            });

            blocks.Add(block);

            if (definition.IsTarget())
                ActiveDestructibleCount++;
        }

        private Vector3 GetLayoutScale(BlockDefinition definition, LevelLayout layout, Vector3Int size)
        {
            if (definition.IsTable())
                return Vector3.one;

            if (IsCompound(definition.prefab))
                return layout.GetCellScale(definition.baseScale);

            return layout.GetFootprintScale(definition.baseScale, size);
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
    }
}
