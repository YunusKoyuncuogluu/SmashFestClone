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
        private ShrinkFadeEffect fade;
        private IUniversalObjectPool pool;
        private IProjectileLifecycle owner;
        private float releaseTime;
        private bool isFlying;
        private bool hasHit;
        private int lifeId;

        private void Awake()
        {
            if (!TryGetComponent(out body))
                body = gameObject.AddComponent<Rigidbody>();

            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.useGravity = false;

            fade = new ShrinkFadeEffect(new[] { transform }, GetComponentsInChildren<Renderer>(true));
        }

        public void AssignDependencies(IUniversalObjectPool pool, FadeMaterialLibrary fadeLibrary)
        {
            this.pool = pool;
            fade.Prepare(fadeLibrary);
        }

        public void Launch(Vector3 direction, float speed, float mass, IProjectileLifecycle owner)
        {
            body.isKinematic = false;
            body.useGravity = false;
            body.mass = mass > 0f ? mass : 1f;
            body.linearVelocity = direction.normalized * Mathf.Max(0f, speed);
            body.angularVelocity = Vector3.zero;
            body.WakeUp();

            this.owner = owner;
            releaseTime = Time.time + lifetimeSeconds;
            hasHit = false;
            isFlying = true;
            lifeId++;
        }

        private void Update()
        {
            if (!isFlying)
                return;

            if (Time.time < releaseTime && transform.position.y >= despawnBelowY)
                return;

            isFlying = false;
            FadeOutAndDespawn(lifeId).Forget();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (body.isKinematic)
                return;

            body.useGravity = true;
            if (hasHit)
                return;

            hasHit = true;
            releaseTime = Mathf.Min(releaseTime, Time.time + postHitHoldSeconds);
        }

        public void OnSpawned()
        {
            ResetState();
            owner = null;
        }

        public void OnDespawned()
        {
            ResetState();
            IProjectileLifecycle releasedOwner = owner;
            owner = null;
            releasedOwner?.OnProjectileReleased();
        }

        private async UniTaskVoid FadeOutAndDespawn(int id)
        {
            bool canceled = await fade.PlayAsync(fadeSeconds, this.GetCancellationTokenOnDestroy());
            if (!canceled && id == lifeId)
                pool.Despawn(gameObject);
        }

        private void ResetState()
        {
            lifeId++;
            isFlying = false;
            fade.Restore();

            if (body.isKinematic)
                return;

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.Sleep();
        }
    }
}
