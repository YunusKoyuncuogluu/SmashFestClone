using UnityEngine;

namespace SmashFest.Core.Pooling
{
    public interface IPrefabPool
    {
        void RegisterPrefab(GameObject prefab, int initialSize);

        GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null);

        T Spawn<T>(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null) where T : Component;

        void Despawn(GameObject instance);

        void DespawnAllActive();

        void NotifyInstanceComponentsChanged(GameObject instance);
    }
}
