using System;
using UnityEngine;
using UnityEngine.Pool;
using Zenject;

namespace SmashFest.Audio
{
    public class SoundEmitterPool : IDisposable
    {
        private const int Capacity = 8;
        private const int MaxSize = 16;

        private readonly DiContainer container;
        private readonly SoundEmitter prefab;
        private readonly Transform root;
        private readonly ObjectPool<SoundEmitter> pool;

        public SoundEmitterPool(DiContainer container, SoundEmitter prefab, Transform root)
        {
            this.container = container;
            this.prefab = prefab;
            this.root = root;
            pool = new ObjectPool<SoundEmitter>(Create, OnGet, OnRelease, OnDestroy, false, Capacity, MaxSize);
        }

        public SoundEmitter Get() => pool.Get();

        public void Release(SoundEmitter emitter)
        {
            if (emitter != null && emitter.gameObject.activeSelf)
                pool.Release(emitter);
        }

        public void Dispose() => pool.Clear();

        private SoundEmitter Create() => container.InstantiatePrefabForComponent<SoundEmitter>(prefab, root);

        private void OnGet(SoundEmitter emitter) => emitter.gameObject.SetActive(true);

        private void OnRelease(SoundEmitter emitter)
        {
            emitter.Stop();
            emitter.gameObject.SetActive(false);
        }

        private void OnDestroy(SoundEmitter emitter)
        {
            if (emitter != null)
                UnityEngine.Object.Destroy(emitter.gameObject);
        }
    }
}
