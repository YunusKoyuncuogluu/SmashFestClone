using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using SmashFast.Core.Pooling;
using UnityEngine;

namespace SmashFast.Gameplay.Cannon
{
    public interface IProjectileLifecycle
    {
        void OnProjectileReleased();
    }

    public class CannonProjectile : MonoBehaviour, IPoolable
    {
        [SerializeField, Min(0.1f)] private float lifetimeSeconds = 4f;
        [SerializeField] private float despawnBelowY = -10f;

        private Rigidbody body;
        private IUniversalObjectPool objectPool;
        private IProjectileLifecycle projectileLifecycle;
        private CancellationTokenSource destroySource;
        private int lifeId;
        private bool isReturning;
        private bool hasBody;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody>();
            }

            hasBody = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.useGravity = true;
            destroySource = new CancellationTokenSource();
        }

        private void OnDestroy()
        {
            if (destroySource == null)
            {
                return;
            }

            destroySource.Cancel();
            destroySource.Dispose();
            destroySource = null;
        }

        public void AssignPool(IUniversalObjectPool pool)
        {
            objectPool = pool;
        }

        public bool Launch(Vector3 direction, float speed, float mass, IProjectileLifecycle lifecycle)
        {
            if (!hasBody)
            {
                return false;
            }

            Vector3 launchDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            float launchSpeed = speed > 0f ? speed : 0f;
            float launchMass = mass > 0f ? mass : 1f;
            body.isKinematic = false;
            body.useGravity = true;
            body.mass = launchMass;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = launchDirection * launchSpeed;
            body.angularVelocity = Vector3.zero;
            body.WakeUp();
            isReturning = false;
            projectileLifecycle = lifecycle;
            lifeId++;
            if (destroySource == null)
            {
                return true;
            }

            WatchLifetime(lifeId, destroySource.Token).Forget();
            return true;
        }

        public void OnSpawned()
        {
            lifeId++;
            isReturning = false;
            projectileLifecycle = null;
            ResetMotion();
        }

        public void OnDespawned()
        {
            lifeId++;
            isReturning = false;
            IProjectileLifecycle lifecycle = projectileLifecycle;
            projectileLifecycle = null;
            ResetMotion();
            if (lifecycle != null)
            {
                lifecycle.OnProjectileReleased();
            }
        }

        private async UniTaskVoid WatchLifetime(int id, CancellationToken token)
        {
            float endTime = Time.time + lifetimeSeconds;
            try
            {
                while (Time.time < endTime)
                {
                    await UniTask.Delay(100, cancellationToken: token);
                    if (id != lifeId)
                    {
                        return;
                    }

                    if (transform.position.y < despawnBelowY)
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (id != lifeId)
            {
                return;
            }

            RequestDespawn();
        }

        private void RequestDespawn()
        {
            if (isReturning || objectPool == null)
            {
                return;
            }

            isReturning = true;
            objectPool.Despawn(gameObject);
        }

        private void ResetMotion()
        {
            if (!hasBody || body == null || body.isKinematic)
            {
                return;
            }

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.Sleep();
        }
    }
}
