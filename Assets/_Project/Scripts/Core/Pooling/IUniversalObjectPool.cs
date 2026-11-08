using UnityEngine;

namespace SmashFast.Core.Pooling
{
    public interface IUniversalObjectPool
    {
        void RegisterPrefab(GameObject prefab, int initialSize);

        GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null);

        T Spawn<T>(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null) where T : Component;

        void Despawn(GameObject instance);

        void DespawnAllActive();

        void NotifyInstanceComponentsChanged(GameObject instance);
    }
}
