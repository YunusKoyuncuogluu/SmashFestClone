using Cysharp.Threading.Tasks;
using SmashFest.Core.Effects;
using SmashFest.Core.Pooling;
using SmashFest.Level;
using UnityEngine;

namespace SmashFest.Gameplay.Cannon
{
    public interface IProjectileLifecycle
    {
        void OnProjectileSpent();
    }

    [RequireComponent(typeof(Rigidbody))]
    public class CannonProjectile : MonoBehaviour, IPoolable
    {
        [SerializeField, Min(0.1f)] private float lifetimeSeconds = 4f;
        [SerializeField] private float despawnBelowY = -10f;
        [SerializeField, Min(0f)] private float postHitHoldSeconds = 0.8f;
        [SerializeField, Min(0.05f)] private float fadeSeconds = 0.6f;
        [SerializeField, Min(0f)] private float spentAfterHitSeconds = 0.35f;
        [SerializeField, Min(1f)] private float missDistance = 18f;
        [SerializeField] private Renderer ballRenderer;
        [SerializeField] private Material fadeMaterial;

        private Rigidbody body;
        private VanishEffect fade;
        private Material solidMaterial;
        private Material activeFadeMaterial;
        private IPrefabPool pool;
        private IImpactFeedback feedback;
        private IProjectileLifecycle owner;
        private Vector3 launchPosition;
        private float releaseTime;
        private float spentTime;
        private bool isFlying;
        private bool isSpent = true;
        private bool hasHit;
        private int lifeId;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.useGravity = true;

            solidMaterial = ballRenderer.sharedMaterial;
            activeFadeMaterial = fadeMaterial;
            fade = new VanishEffect(new[] { transform }, new[] { ballRenderer });
        }

        public void AssignDependencies(IPrefabPool pool, IImpactFeedback feedback)
        {
            this.pool = pool;
            this.feedback = feedback;
        }

        public void ApplySkin(Material material, Material skinFadeMaterial)
        {
            if (material == null)
                return;

            solidMaterial = material;
            activeFadeMaterial = skinFadeMaterial != null ? skinFadeMaterial : fadeMaterial;
            if (ballRenderer.sharedMaterial != material)
                ballRenderer.sharedMaterial = material;
        }

        public void Launch(Vector3 velocity, float mass, IProjectileLifecycle owner)
        {
            body.isKinematic = false;
            body.useGravity = true;
            body.linearDamping = 0f;
            body.mass = mass > 0f ? mass : 1f;
            body.linearVelocity = velocity;
            body.angularVelocity = Vector3.zero;
            body.WakeUp();

            this.owner = owner;
            launchPosition = transform.position;
            releaseTime = Time.time + lifetimeSeconds;
            spentTime = releaseTime;
            hasHit = false;
            isSpent = false;
            isFlying = true;
            lifeId++;
        }

        private void Update()
        {
            if (!isSpent && IsSpentNow())
                MarkSpent();

            if (!isFlying)
                return;

            if (Time.time < releaseTime && transform.position.y >= despawnBelowY)
                return;

            isFlying = false;
            FadeOutAndDespawn(lifeId).Forget();
        }

        private bool IsSpentNow()
        {
            if (Time.time >= spentTime || transform.position.y < despawnBelowY)
                return true;

            return !hasHit && (transform.position - launchPosition).sqrMagnitude > missDistance * missDistance;
        }

        private void MarkSpent()
        {
            isSpent = true;
            IProjectileLifecycle spentOwner = owner;
            owner = null;
            spentOwner?.OnProjectileSpent();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (body.isKinematic)
                return;

            if (hasHit)
                return;

            hasHit = true;
            Rigidbody target = collision.rigidbody;
            if (target == null || !target.TryGetComponent(out SpawnedBlock _))
                feedback?.OnBallThud(collision.relativeVelocity.magnitude);

            releaseTime = Mathf.Min(releaseTime, Time.time + postHitHoldSeconds);
            spentTime = Mathf.Min(spentTime, Time.time + spentAfterHitSeconds);
        }

        public void OnSpawned()
        {
            ResetState();
            owner = null;
            isSpent = true;
        }

        public void OnDespawned()
        {
            ResetState();
            if (!isSpent)
                MarkSpent();
        }

        private async UniTaskVoid FadeOutAndDespawn(int id)
        {
            if (activeFadeMaterial != null)
                ballRenderer.sharedMaterial = activeFadeMaterial;

            fade.RefreshColors();
            bool canceled = await fade.PlayAsync(fadeSeconds, this.GetCancellationTokenOnDestroy());
            if (!canceled && id == lifeId)
                pool.Despawn(gameObject);
        }

        private void ResetState()
        {
            lifeId++;
            isFlying = false;
            fade.Restore();
            if (ballRenderer.sharedMaterial != solidMaterial)
                ballRenderer.sharedMaterial = solidMaterial;

            if (body.isKinematic)
                return;

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.Sleep();
        }
    }
}
