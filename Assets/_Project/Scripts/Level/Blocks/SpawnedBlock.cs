using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using SmashFest.Core.Effects;
using SmashFest.Core.Pooling;
using SmashFest.Data;
using SmashFest.Gameplay.Cannon;
using UnityEngine;

namespace SmashFest.Level
{
    public class SpawnedBlock : MonoBehaviour, IPoolable
    {
        private const float HitSoundSpeed = 1.2f;
        private const float QuietAfterSpawn = 0.6f;
        private const float WakeSpeed = 0.8f;
        private const float ImpactTransfer = 0.5f;
        private const float MaxTransferSpeed = 6f;
        private const float SupportProbeHeight = 0.2f;

        [SerializeField] private GameObject intactVisual;
        [SerializeField] private GameObject fracturedVisual;
        [SerializeField] private Transform tableTop;
        [SerializeField, Min(0f)] private float fragmentHoldSeconds = 1f;
        [SerializeField, Min(0.05f)] private float fragmentFadeSeconds = 1.2f;
        [SerializeField, Min(0f)] private float fragmentBurstSpeed = 2.2f;
        [SerializeField, Min(0f)] private float fragmentPushSpeed = 3.5f;
        [SerializeField, Min(0f)] private float fragmentLiftSpeed = 1.8f;
        [SerializeField, Min(0f)] private float fragmentSpinSpeed = 10f;
        [SerializeField, Min(0f)] private float impactBoost = 0.6f;

        private readonly Collider[] neighbors = new Collider[16];

        private BlockPhysics physics;
        private BlockFracture fracture;
        private BlockScaler scaler;
        private Renderer intactRenderer;
        private IImpactFeedback feedback;
        private IDestructibleTracker tracker;
        private IPrefabPool pool;
        private BlockMaterialType material;
        private float massMultiplier = 1f;
        private float quietUntil;
        private bool isTarget;
        private bool isBroken;
        private int groundLayer = -1;
        private int breakId;

        private bool BreaksOnBallHit => material == BlockMaterialType.Glass || material == BlockMaterialType.CeramicJar;
        private BlockPhysics Body => physics ??= new BlockPhysics(GetComponent<Rigidbody>());
        private BlockFracture Fracture => fracture ??= new BlockFracture(intactVisual, fracturedVisual, transform);
        private BlockScaler Scaler => scaler ??= new BlockScaler(transform, tableTop);

        private FragmentMotion Motion => new FragmentMotion
        {
            Burst = fragmentBurstSpeed,
            Push = fragmentPushSpeed,
            Lift = fragmentLiftSpeed,
            Spin = fragmentSpinSpeed
        };

        private void Awake()
        {
            if (intactVisual != null)
                intactRenderer = intactVisual.GetComponentInChildren<Renderer>(true);

            groundLayer = LayerMask.NameToLayer("Ground");
            Fracture.ShowIntact();
        }

        public void Initialize(BlockDefinition definition, BlockSetup setup)
        {
            breakId++;
            material = definition.materialType;
            isTarget = definition.IsTarget();
            isBroken = false;
            massMultiplier = setup.MassMultiplier;
            feedback = setup.Feedback;
            tracker = setup.Tracker;
            pool = setup.Pool;
            quietUntil = Time.time + QuietAfterSpawn;

            Scaler.Apply(setup.LayoutScale, setup.GlobalScale, setup.TableHeightFactor, setup.SizeInCells);
            Fracture.ShowIntact();
            Body.Configure((definition.mass > 0f ? definition.mass : 1f) * massMultiplier, definition.isKinematicBase);
            if (isTarget && intactRenderer != null)
                feedback?.Prewarm(intactRenderer.sharedMaterial);
        }

        public void ApplyEditorPreviewVisuals(Vector3 layoutScale, float globalScale, float tableHeightFactor, Vector3Int sizeInCells)
        {
            Scaler.Apply(layoutScale, globalScale, tableHeightFactor, sizeInCells);
            Fracture.ShowIntact();
        }

        public void ApplyEditorPreviewVisuals() => Fracture.ShowIntact();

        public void WakeUp()
        {
            if (!isBroken && Body.Wake())
                WakeBlocksAbove();
        }

        public void OnSpawned()
        {
            breakId++;
            isBroken = false;
            isTarget = false;
            tracker = null;
            pool = null;
            material = BlockMaterialType.TableFoundation;
            Fracture.ShowIntact();
        }

        public void OnDespawned()
        {
            breakId++;
            UnregisterTarget();
            tracker = null;
            pool = null;
            Fracture.ShowIntact();
        }

        private void OnCollisionEnter(Collision collision)
        {
            Collider other = collision.collider;
            if (isBroken || other == null)
                return;

            float speed = collision.relativeVelocity.magnitude;
            Rigidbody otherBody = other.attachedRigidbody;
            bool isBallHit = otherBody != null && otherBody.TryGetComponent(out CannonProjectile _);

            if (Body.IsDormant)
            {
                if (!isBallHit && speed < WakeSpeed)
                    return;

                WakeUp();
                TransferImpact(collision, otherBody, speed);
            }

            PlayHitSound(isBallHit, speed);
            if (isBallHit)
                Shove(otherBody, collision);

            if (!isTarget)
                return;

            if (IsGround(other))
                Break(Vector3.zero);
            else if (isBallHit && BreaksOnBallHit)
                Break(FlatDirection(transform.position - other.transform.position));
        }

        private bool IsGround(Collider other) => groundLayer >= 0 && other.gameObject.layer == groundLayer;

        private void PlayHitSound(bool isBallHit, float speed)
        {
            if (isBallHit)
                feedback?.OnBlockHit(material, speed);
            else if (speed > HitSoundSpeed && Time.time > quietUntil)
                feedback?.OnBlockHit(material, speed * 0.6f);
        }

        private void TransferImpact(Collision collision, Rigidbody otherBody, float speed)
        {
            if (otherBody == null || collision.contactCount == 0)
                return;

            Vector3 direction = collision.relativeVelocity;
            if (Vector3.Dot(direction, transform.position - otherBody.position) < 0f)
                direction = -direction;

            float impulse = Mathf.Min(otherBody.mass * speed * ImpactTransfer, Body.Mass * MaxTransferSpeed);
            Body.Push(direction.normalized * impulse, collision.GetContact(0).point);
        }

        private void Shove(Rigidbody ball, Collision collision)
        {
            if (ball == null || collision.contactCount == 0)
                return;

            Vector3 direction = ball.linearVelocity.sqrMagnitude > 0.01f ? ball.linearVelocity.normalized : -collision.GetContact(0).normal;
            Body.Push(direction * impactBoost * Body.Mass, collision.GetContact(0).point);
        }

        private void WakeBlocksAbove()
        {
            if (intactRenderer == null)
                return;

            Bounds bounds = intactRenderer.bounds;
            Vector3 center = new Vector3(bounds.center.x, bounds.max.y + SupportProbeHeight * 0.5f, bounds.center.z);
            Vector3 halfSize = new Vector3(bounds.extents.x * 0.9f, SupportProbeHeight * 0.5f, bounds.extents.z * 0.9f);
            int count = Physics.OverlapBoxNonAlloc(center, halfSize, neighbors, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Rigidbody neighbor = neighbors[i].attachedRigidbody;
                if (neighbor != null && neighbor.TryGetComponent(out SpawnedBlock block) && block != this)
                    block.WakeUp();
            }
        }

        private Vector3 FlatDirection(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
        }

        private void Break(Vector3 pushDirection)
        {
            isBroken = true;
            UnregisterTarget();
            WakeBlocksAbove();

            if (intactRenderer != null)
                feedback?.OnBlockBreak(material, intactRenderer.bounds, intactRenderer.sharedMaterial, pushDirection);

            if (!Fracture.HasPieces)
            {
                ReturnToPool();
                return;
            }

            Body.GetVelocity(out Vector3 linear, out Vector3 angular);
            Fracture.Shatter(linear, angular, pushDirection, massMultiplier, Motion);
            Body.Freeze();
            DespawnAfterBreak(++breakId).Forget();
        }

        private void UnregisterTarget()
        {
            if (!isTarget)
                return;

            isTarget = false;
            tracker?.UnregisterTarget();
        }

        private async UniTaskVoid DespawnAfterBreak(int id)
        {
            CancellationToken token = this.GetCancellationTokenOnDestroy();
            bool canceled = await UniTask.Delay(TimeSpan.FromSeconds(fragmentHoldSeconds), DelayType.DeltaTime, PlayerLoopTiming.Update, token)
                .SuppressCancellationThrow();
            if (canceled || id != breakId)
                return;

            canceled = await Fracture.FadeOutAsync(fragmentFadeSeconds, token);
            if (canceled || id != breakId)
                return;

            ReturnToPool();
        }

        private void ReturnToPool()
        {
            if (pool != null)
                pool.Despawn(gameObject);
            else
                gameObject.SetActive(false);
        }
    }
}
