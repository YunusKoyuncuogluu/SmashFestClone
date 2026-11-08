using System.Collections.Generic;
using SmashFast.Data;
using UnityEngine;
using Zenject;

namespace SmashFast.Core.Pooling
{
    public class UniversalObjectPool : IUniversalObjectPool, IInitializable
    {
        private Transform poolRootTransform;
        private readonly PoolConfigSO poolConfig;
        private readonly BlockCatalogSO blockCatalog;
        private readonly Dictionary<int, Stack<GameObject>> pools;
        private readonly Dictionary<int, int> instanceToPrefabIdMap;
        private readonly Dictionary<int, IPoolable[]> poolablesByInstanceId;
        private readonly List<GameObject> activeInstances;
        private bool isReady;

        public UniversalObjectPool(
            [InjectOptional] PoolConfigSO poolConfig,
            [InjectOptional] BlockCatalogSO blockCatalog)
        {
            this.poolConfig = poolConfig;
            this.blockCatalog = blockCatalog;
            pools = new Dictionary<int, Stack<GameObject>>(32);
            instanceToPrefabIdMap = new Dictionary<int, int>(256);
            poolablesByInstanceId = new Dictionary<int, IPoolable[]>(256);
            activeInstances = new List<GameObject>(256);
        }

        public void Initialize()
        {
            if (isReady)
            {
                return;
            }

            isReady = true;
            EnsureRoot();
            RegisterConfiguredPools();
            RegisterCatalogPools();
        }

        public void RegisterPrefab(GameObject prefab, int initialSize)
        {
            EnsureReady();
            if (prefab == null)
            {
                return;
            }

            int prefabId = prefab.GetInstanceID();
            Stack<GameObject> stack = GetOrCreatePool(prefabId);
            int targetCount = initialSize > 0 ? initialSize : 0;
            int missing = targetCount - stack.Count;
            for (int i = 0; i < missing; i++)
            {
                GameObject instance = CreateInstance(prefab);
                InvokePoolables(instance, false);
                ReturnToPool(instance, prefabId);
            }
        }

        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            EnsureReady();
            if (prefab == null)
            {
                return null;
            }

            int prefabId = prefab.GetInstanceID();
            Stack<GameObject> stack = GetOrCreatePool(prefabId);
            GameObject instance = PopLiveInstance(stack);
            if (instance == null)
            {
                instance = CreateInstance(prefab);
            }

            Transform parentTransform = parent != null ? parent : poolRootTransform;
            Transform instanceTransform = instance.transform;
            instanceTransform.SetParent(parentTransform, false);
            instanceTransform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);

            int instanceId = instance.GetInstanceID();
            instanceToPrefabIdMap[instanceId] = prefabId;
            activeInstances.Add(instance);
            InvokePoolables(instance, true);
            return instance;
        }

        public T Spawn<T>(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null) where T : Component
        {
            GameObject instance = Spawn(prefab, position, rotation, parent);
            if (instance == null)
            {
                return null;
            }

            T component = instance.GetComponent<T>();
            if (component == null)
            {
                component = instance.GetComponentInChildren<T>(true);
            }

            if (component != null)
            {
                return component;
            }

            Despawn(instance);
            return null;
        }

        public void Despawn(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            int instanceId = instance.GetInstanceID();
            int prefabId;
            if (!instanceToPrefabIdMap.TryGetValue(instanceId, out prefabId))
            {
                return;
            }

            instanceToPrefabIdMap.Remove(instanceId);
            RemoveActive(instance);
            InvokePoolables(instance, false);
            ReturnToPool(instance, prefabId);
        }

        public void DespawnAllActive()
        {
            while (activeInstances.Count > 0)
            {
                int lastIndex = activeInstances.Count - 1;
                int countBefore = activeInstances.Count;
                GameObject instance = activeInstances[lastIndex];
                if (instance == null)
                {
                    activeInstances.RemoveAt(lastIndex);
                    continue;
                }

                Despawn(instance);
                if (activeInstances.Count == countBefore)
                {
                    activeInstances.RemoveAt(activeInstances.Count - 1);
                }
            }
        }

        public void NotifyInstanceComponentsChanged(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            CachePoolables(instance);
        }

        private void EnsureRoot()
        {
            if (poolRootTransform != null)
            {
                return;
            }

            GameObject rootObject = new GameObject("UniversalPool");
            poolRootTransform = rootObject.transform;
            if (Application.isPlaying)
            {
                Object.DontDestroyOnLoad(rootObject);
            }
        }

        private void EnsureReady()
        {
            if (isReady)
            {
                return;
            }

            Initialize();
        }

        private void RegisterConfiguredPools()
        {
            if (poolConfig == null || poolConfig.CustomPools == null)
            {
                return;
            }

            List<PoolItemConfig> configs = poolConfig.CustomPools;
            int count = configs.Count;
            for (int i = 0; i < count; i++)
            {
                PoolItemConfig config = configs[i];
                RegisterPrefab(config.prefab, config.initialSize);
            }
        }

        private void RegisterCatalogPools()
        {
            if (blockCatalog == null)
            {
                return;
            }

            int prewarmCount = 4;
            if (poolConfig != null)
            {
                prewarmCount = poolConfig.CatalogPrewarmCount;
            }

            int count = blockCatalog.BlockCount;
            for (int i = 0; i < count; i++)
            {
                BlockDefinition definition;
                if (!blockCatalog.TryGetBlockByIndex(i, out definition))
                {
                    continue;
                }

                if (definition.prefab == null)
                {
                    continue;
                }

                RegisterPrefab(definition.prefab, prewarmCount);
            }
        }

        private Stack<GameObject> GetOrCreatePool(int prefabId)
        {
            Stack<GameObject> stack;
            if (pools.TryGetValue(prefabId, out stack))
            {
                return stack;
            }

            stack = new Stack<GameObject>(8);
            pools.Add(prefabId, stack);
            return stack;
        }

        private GameObject PopLiveInstance(Stack<GameObject> stack)
        {
            while (stack.Count > 0)
            {
                GameObject instance = stack.Pop();
                if (instance != null)
                {
                    return instance;
                }
            }

            return null;
        }

        private GameObject CreateInstance(GameObject prefab)
        {
            GameObject instance = Object.Instantiate(prefab, poolRootTransform);
            CachePoolables(instance);
            return instance;
        }

        private void ReturnToPool(GameObject instance, int prefabId)
        {
            if (instance == null)
            {
                return;
            }

            instance.SetActive(false);
            instance.transform.SetParent(poolRootTransform, false);
            GetOrCreatePool(prefabId).Push(instance);
        }

        private void CachePoolables(GameObject instance)
        {
            poolablesByInstanceId[instance.GetInstanceID()] = instance.GetComponentsInChildren<IPoolable>(true);
        }

        private void InvokePoolables(GameObject instance, bool spawned)
        {
            int instanceId = instance.GetInstanceID();
            IPoolable[] poolables;
            if (!poolablesByInstanceId.TryGetValue(instanceId, out poolables) || poolables == null)
            {
                CachePoolables(instance);
                poolables = poolablesByInstanceId[instanceId];
            }

            int count = poolables.Length;
            for (int i = 0; i < count; i++)
            {
                IPoolable poolable = poolables[i];
                if (poolable == null)
                {
                    continue;
                }

                if (spawned)
                {
                    poolable.OnSpawned();
                }
                else
                {
                    poolable.OnDespawned();
                }
            }
        }

        private void RemoveActive(GameObject instance)
        {
            int count = activeInstances.Count;
            for (int i = 0; i < count; i++)
            {
                if (activeInstances[i] != instance)
                {
                    continue;
                }

                int last = count - 1;
                activeInstances[i] = activeInstances[last];
                activeInstances.RemoveAt(last);
                return;
            }
        }
    }
}
