using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using SmashFast.Core.Effects;
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
        [SerializeField, Min(0f)] private float postHitHoldSeconds = 0.8f;
        [SerializeField, Min(0.05f)] private float fadeSeconds = 0.6f;

        private Rigidbody body;
        private ShrinkFadeEffect fadeEffect;
        private IUniversalObjectPool objectPool;
        private IProjectileLifecycle projectileLifecycle;
        private CancellationTokenSource destroySource;
        private int lifeId;
        private bool isReturning;
        private bool hasBody;
        private bool hasHit;
        private float releaseTime;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody>();
            }

            fadeEffect = new ShrinkFadeEffect(new Transform[] { transform }, GetComponentsInChildren<Renderer>(true));
            hasBody = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.useGravity = false;
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

        public void AssignDependencies(IUniversalObjectPool pool, FadeMaterialLibrary fadeLibrary)
        {
            objectPool = pool;
            fadeEffect.Prepare(fadeLibrary);
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
            body.useGravity = false;
            body.mass = launchMass;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = launchDirection * launchSpeed;
            body.angularVelocity = Vector3.zero;
            body.WakeUp();
            isReturning = false;
            hasHit = false;
            releaseTime = Time.time + lifetimeSeconds;
            projectileLifecycle = lifecycle;
            lifeId++;
            if (destroySource == null)
            {
                return true;
            }

            WatchLifetime(lifeId, destroySource.Token).Forget();
            return true;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!hasBody || body == null || body.isKinematic)
            {
                return;
            }

            body.useGravity = true;
            if (hasHit)
            {
                return;
            }

            hasHit = true;
            releaseTime = Mathf.Min(releaseTime, Time.time + postHitHoldSeconds);
        }

        public void OnSpawned()
        {
            lifeId++;
            isReturning = false;
            projectileLifecycle = null;
            RestoreAfterFade();
            ResetMotion();
        }

        public void OnDespawned()
        {
            lifeId++;
            isReturning = false;
            IProjectileLifecycle lifecycle = projectileLifecycle;
            projectileLifecycle = null;
            RestoreAfterFade();
            ResetMotion();
            if (lifecycle != null)
            {
                lifecycle.OnProjectileReleased();
            }
        }

        private async UniTaskVoid WatchLifetime(int id, CancellationToken token)
        {
            try
            {
                while (Time.time < releaseTime)
                {
                    await UniTask.Delay(50, cancellationToken: token);
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

            bool canceled = await fadeEffect.PlayAsync(fadeSeconds, token);
            if (canceled || id != lifeId)
            {
                return;
            }

            RequestDespawn();
        }

        private void RestoreAfterFade()
        {
            if (!hasBody)
            {
                return;
            }

            fadeEffect.Restore();
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
