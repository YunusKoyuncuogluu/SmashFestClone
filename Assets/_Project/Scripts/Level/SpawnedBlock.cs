using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using SmashFast.Core.Effects;
using SmashFast.Core.Pooling;
using SmashFast.Data;
using SmashFast.Gameplay.Cannon;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SmashFast.Level
{
    public class SpawnedBlock : MonoBehaviour, IPoolable
    {
        private const int HoldTicks = 10;
        private const float LinearDamping = 0.05f;
        private const float AngularDamping = 0.8f;
        private const float MaxDepenetrationVelocity = 3f;
        private const float SleepThreshold = 0.1f;

        [SerializeField] private GameObject intactVisual;
        [SerializeField] private GameObject fracturedVisual;
        [SerializeField] private Transform tableTop;
        [SerializeField, Min(0f)] private float fragmentHoldSeconds = 1f;
        [SerializeField, Min(0.05f)] private float fragmentFadeSeconds = 1.2f;
        [SerializeField, Min(0f)] private float fragmentBurstSpeed = 2.2f;
        [SerializeField, Min(0f)] private float fragmentPushSpeed = 3.5f;
        [SerializeField, Min(0f)] private float fragmentLiftSpeed = 1.8f;
        [SerializeField, Min(0f)] private float fragmentSpinSpeed = 10f;

        private Rigidbody body;
        private Rigidbody[] pieces;
        private float[] pieceMasses;
        private Vector3[] piecePositions;
        private Quaternion[] pieceRotations;
        private Renderer[] tableTopRenderers;
        private ShrinkFadeEffect fade;
        private IDestructibleTracker tracker;
        private IUniversalObjectPool pool;
        private BlockMaterialType material;
        private float massMultiplier = 1f;
        private bool isKinematicBase;
        private bool isTarget;
        private bool isBroken;
        private bool isHolding;
        private int holdTicksLeft;
        private int groundLayer = -1;
        private int breakId;

        private bool IsFragile => material == BlockMaterialType.Glass || material == BlockMaterialType.CeramicJar;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            groundLayer = LayerMask.NameToLayer("Ground");
            CachePieces();
            ShowIntact();
        }

        public void Initialize(
            BlockDefinition definition,
            Vector3 layoutScale,
            float globalScale,
            float tableHeightFactor,
            Vector3Int sizeInCells,
            float massMultiplier,
            IDestructibleTracker tracker,
            IUniversalObjectPool pool,
            FadeMaterialLibrary fadeLibrary)
        {
            breakId++;
            isKinematicBase = definition.isKinematicBase;
            material = definition.materialType;
            isTarget = definition.IsTarget();
            isBroken = false;
            isHolding = false;
            this.massMultiplier = massMultiplier;
            this.tracker = tracker;
            this.pool = pool;

            if (fade != null)
                fade.Prepare(fadeLibrary);

            ApplyScale(layoutScale, globalScale, tableHeightFactor, sizeInCells);
            ShowIntact();
            ConfigureBody((definition.mass > 0f ? definition.mass : 1f) * massMultiplier);
        }

        public void ApplyEditorPreviewVisuals(Vector3 layoutScale, float globalScale, float tableHeightFactor, Vector3Int sizeInCells)
        {
            ApplyScale(layoutScale, globalScale, tableHeightFactor, sizeInCells);
            ShowIntact();
        }

        public void ApplyEditorPreviewVisuals() => ShowIntact();

        public void Hold()
        {
            if (body == null || body.isKinematic)
                return;

            isHolding = true;
            holdTicksLeft = HoldTicks;
            body.Sleep();
        }

        private void FixedUpdate()
        {
            if (!isHolding)
                return;

            if (holdTicksLeft > 0)
            {
                holdTicksLeft--;
                body.Sleep();
                return;
            }

            if (!body.IsSleeping())
                isHolding = false;
        }

        public void OnSpawned()
        {
            breakId++;
            isBroken = false;
            isTarget = false;
            tracker = null;
            pool = null;
            material = BlockMaterialType.TableFoundation;
            ShowIntact();
        }

        public void OnDespawned()
        {
            breakId++;
            UnregisterTarget();
            tracker = null;
            pool = null;
            ShowIntact();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!isTarget || isBroken)
                return;

            Collider other = collision.collider;
            if (other == null)
                return;

            if (groundLayer >= 0 && other.gameObject.layer == groundLayer)
            {
                Break(Vector3.zero);
                return;
            }

            if (!IsFragile || other.GetComponentInParent<CannonProjectile>() == null)
                return;

            Vector3 push = transform.position - other.transform.position;
            push.y = 0f;
            Break(push.sqrMagnitude > 0.0001f ? push.normalized : Vector3.zero);
        }

        private void Break(Vector3 pushDirection)
        {
            isBroken = true;
            UnregisterTarget();

            if (fracturedVisual == null)
            {
                ReturnToPool();
                return;
            }

            ShowFractured(pushDirection);
            DespawnAfterBreak(++breakId).Forget();
        }

        private void CachePieces()
        {
            if (fracturedVisual == null)
                return;

            pieces = fracturedVisual.GetComponentsInChildren<Rigidbody>(true);
            int count = pieces.Length;
            pieceMasses = new float[count];
            piecePositions = new Vector3[count];
            pieceRotations = new Quaternion[count];
            Transform[] pieceTransforms = new Transform[count];

            for (int i = 0; i < count; i++)
            {
                Transform piece = pieces[i].transform;
                pieceTransforms[i] = piece;
                pieceMasses[i] = pieces[i].mass;
                piecePositions[i] = piece.localPosition;
                pieceRotations[i] = piece.localRotation;
            }

            fade = new ShrinkFadeEffect(pieceTransforms, fracturedVisual.GetComponentsInChildren<Renderer>(true));
        }

        private void LaunchPieces(Vector3 linearVelocity, Vector3 angularVelocity, Vector3 pushDirection)
        {
            if (pieces == null)
                return;

            fade.Restore();
            for (int i = 0; i < pieces.Length; i++)
            {
                Rigidbody piece = pieces[i];
                piece.transform.SetLocalPositionAndRotation(piecePositions[i], pieceRotations[i]);
                piece.mass = pieceMasses[i] * massMultiplier;
                if (piece.isKinematic)
                    continue;

                Vector3 outward = piece.transform.position - transform.position;
                outward.y = 0f;
                outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : Random.onUnitSphere;
                outward.y = 0f;

                piece.linearVelocity = linearVelocity
                    + outward * (fragmentBurstSpeed * Random.Range(0.5f, 1.2f))
                    + pushDirection * (fragmentPushSpeed * Random.Range(0.6f, 1.1f))
                    + Vector3.up * (fragmentLiftSpeed * Random.Range(0.2f, 1f));
                piece.angularVelocity = angularVelocity + Random.insideUnitSphere * fragmentSpinSpeed;
            }
        }

        private void ApplyScale(Vector3 layoutScale, float globalScale, float tableHeightFactor, Vector3Int sizeInCells)
        {
            if (tableTop == null)
            {
                Vector3 blockScale = layoutScale == Vector3.zero ? Vector3.one : layoutScale;
                transform.localScale = blockScale * globalScale;
                return;
            }

            float heightFactor = Mathf.Max(1f, tableHeightFactor);
            transform.localScale = new Vector3(globalScale, globalScale * heightFactor, globalScale);

            float topScaleX = Mathf.Max(1, sizeInCells.x);
            float topScaleZ = Mathf.Max(1, sizeInCells.z);
            Vector3 oldScale = tableTop.localScale;
            Vector3 oldPosition = tableTop.localPosition;
            tableTop.localScale = new Vector3(topScaleX, 1f / heightFactor, topScaleZ);
            tableTop.localPosition = new Vector3(
                oldPosition.x / oldScale.x * topScaleX,
                oldPosition.y,
                oldPosition.z / oldScale.z * topScaleZ);

            AlignTableTop(globalScale * heightFactor * 0.5f);
        }

        private void AlignTableTop(float heightAboveCenter)
        {
            if (tableTopRenderers == null || tableTopRenderers.Length == 0)
                tableTopRenderers = tableTop.GetComponentsInChildren<Renderer>(true);

            if (tableTopRenderers.Length == 0)
                return;

            float currentTop = float.MinValue;
            foreach (Renderer topRenderer in tableTopRenderers)
                currentTop = Mathf.Max(currentTop, topRenderer.bounds.max.y);

            float offset = transform.position.y + heightAboveCenter - currentTop;
            tableTop.position += Vector3.up * offset;
        }

        private void ShowIntact()
        {
            if (fade != null)
                fade.Restore();

            if (intactVisual != null)
                intactVisual.SetActive(true);

            if (fracturedVisual != null)
                fracturedVisual.SetActive(false);
        }

        private void ShowFractured(Vector3 pushDirection)
        {
            if (intactVisual != null)
                intactVisual.SetActive(false);

            Vector3 linearVelocity = Vector3.zero;
            Vector3 angularVelocity = Vector3.zero;
            if (body != null && !body.isKinematic)
            {
                linearVelocity = body.linearVelocity;
                angularVelocity = body.angularVelocity;
            }

            fracturedVisual.SetActive(true);
            LaunchPieces(linearVelocity, angularVelocity, pushDirection);

            if (body == null)
                return;

            isHolding = false;
            body.detectCollisions = false;
            body.isKinematic = true;
        }

        private void ConfigureBody(float mass)
        {
            if (body == null)
                return;

            body.mass = mass;
            body.isKinematic = isKinematicBase;
            body.useGravity = !isKinematicBase;
            body.detectCollisions = true;
            body.linearDamping = LinearDamping;
            body.angularDamping = AngularDamping;
            body.maxDepenetrationVelocity = MaxDepenetrationVelocity;
            body.sleepThreshold = SleepThreshold;

            if (body.isKinematic)
                return;

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.Sleep();
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

            canceled = await fade.PlayAsync(fragmentFadeSeconds, token);
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
