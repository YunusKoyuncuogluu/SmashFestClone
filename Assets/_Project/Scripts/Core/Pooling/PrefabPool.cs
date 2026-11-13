using System.Collections.Generic;
using SmashFest.Data;
using UnityEngine;
using Zenject;

namespace SmashFest.Core.Pooling
{
    public class PrefabPool : IPrefabPool, IInitializable
    {
        public const string RootBindingId = "PoolRoot";

        private const int DefaultCatalogPrewarm = 4;

        private readonly PoolConfigSO config;
        private readonly BlockCatalogSO catalog;
        private readonly Dictionary<int, Stack<GameObject>> pools = new Dictionary<int, Stack<GameObject>>(32);
        private readonly Dictionary<int, int> prefabIdByInstance = new Dictionary<int, int>(256);
        private readonly Dictionary<int, IPoolable[]> poolables = new Dictionary<int, IPoolable[]>(256);
        private readonly List<GameObject> active = new List<GameObject>(256);

        private readonly Transform root;
        private bool isReady;

        public PrefabPool([Inject(Id = RootBindingId)] Transform root, [InjectOptional] PoolConfigSO config, [InjectOptional] BlockCatalogSO catalog)
        {
            this.root = root;
            this.config = config;
            this.catalog = catalog;
        }

        public void Initialize()
        {
            if (isReady)
                return;

            isReady = true;
            PrewarmConfigPools();
            PrewarmCatalogPools();
        }

        public void RegisterPrefab(GameObject prefab, int initialSize)
        {
            Initialize();
            if (prefab == null)
                return;

            int prefabId = prefab.GetInstanceID();
            Stack<GameObject> pool = GetPool(prefabId);
            int missing = Mathf.Max(0, initialSize) - pool.Count;
            for (int i = 0; i < missing; i++)
            {
                GameObject instance = CreateInstance(prefab);
                NotifyDespawned(instance);
                Store(instance, prefabId);
            }
        }

        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            Initialize();
            if (prefab == null)
                return null;

            int prefabId = prefab.GetInstanceID();
            GameObject instance = TakeFromPool(GetPool(prefabId));
            if (instance == null)
                instance = CreateInstance(prefab);

            Transform instanceTransform = instance.transform;
            instanceTransform.SetParent(parent != null ? parent : root, false);
            instanceTransform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);

            prefabIdByInstance[instance.GetInstanceID()] = prefabId;
            active.Add(instance);
            NotifySpawned(instance);
            return instance;
        }

        public T Spawn<T>(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null) where T : Component
        {
            GameObject instance = Spawn(prefab, position, rotation, parent);
            if (instance == null)
                return null;

            T component = instance.GetComponentInChildren<T>(true);
            if (component != null)
                return component;

            Despawn(instance);
            return null;
        }

        public void Despawn(GameObject instance)
        {
            if (instance == null)
                return;

            int instanceId = instance.GetInstanceID();
            if (!prefabIdByInstance.TryGetValue(instanceId, out int prefabId))
                return;

            prefabIdByInstance.Remove(instanceId);
            RemoveActive(instance);
            NotifyDespawned(instance);
            Store(instance, prefabId);
        }

        public void DespawnAllActive()
        {
            while (active.Count > 0)
            {
                int last = active.Count - 1;
                GameObject instance = active[last];
                if (instance == null)
                {
                    active.RemoveAt(last);
                    continue;
                }

                Despawn(instance);
                if (active.Count == last + 1)
                    active.RemoveAt(last);
            }
        }

        public void NotifyInstanceComponentsChanged(GameObject instance)
        {
            if (instance != null)
                CachePoolables(instance);
        }

        private void PrewarmConfigPools()
        {
            if (config == null || config.CustomPools == null)
                return;

            foreach (PoolItemConfig item in config.CustomPools)
                RegisterPrefab(item.prefab, item.initialSize);
        }

        private void PrewarmCatalogPools()
        {
            if (catalog == null)
                return;

            int prewarm = config != null ? config.CatalogPrewarmCount : DefaultCatalogPrewarm;
            for (int i = 0; i < catalog.BlockCount; i++)
            {
                if (catalog.TryGetBlockByIndex(i, out BlockDefinition definition) && definition.prefab != null)
                    RegisterPrefab(definition.prefab, prewarm);
            }
        }

        private Stack<GameObject> GetPool(int prefabId)
        {
            if (!pools.TryGetValue(prefabId, out Stack<GameObject> pool))
            {
                pool = new Stack<GameObject>(8);
                pools.Add(prefabId, pool);
            }

            return pool;
        }

        private GameObject TakeFromPool(Stack<GameObject> pool)
        {
            while (pool.Count > 0)
            {
                GameObject instance = pool.Pop();
                if (instance != null)
                    return instance;
            }

            return null;
        }

        private GameObject CreateInstance(GameObject prefab)
        {
            GameObject instance = Object.Instantiate(prefab, root);
            CachePoolables(instance);
            return instance;
        }

        private void Store(GameObject instance, int prefabId)
        {
            instance.SetActive(false);
            instance.transform.SetParent(root, false);
            GetPool(prefabId).Push(instance);
        }

        private void CachePoolables(GameObject instance)
        {
            poolables[instance.GetInstanceID()] = instance.GetComponentsInChildren<IPoolable>(true);
        }

        private IPoolable[] GetPoolables(GameObject instance)
        {
            int instanceId = instance.GetInstanceID();
            if (!poolables.TryGetValue(instanceId, out IPoolable[] cached) || cached == null)
            {
                CachePoolables(instance);
                cached = poolables[instanceId];
            }

            return cached;
        }

        private void NotifySpawned(GameObject instance)
        {
            foreach (IPoolable poolable in GetPoolables(instance))
                poolable?.OnSpawned();
        }

        private void NotifyDespawned(GameObject instance)
        {
            foreach (IPoolable poolable in GetPoolables(instance))
                poolable?.OnDespawned();
        }

        private void RemoveActive(GameObject instance)
        {
            int index = active.IndexOf(instance);
            if (index < 0)
                return;

            int last = active.Count - 1;
            active[index] = active[last];
            active.RemoveAt(last);
        }
    }
}
