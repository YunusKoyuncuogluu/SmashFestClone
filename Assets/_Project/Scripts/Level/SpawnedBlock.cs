using System.Threading;
using Cysharp.Threading.Tasks;
using SmashFast.Core.Effects;
using SmashFast.Core.Pooling;
using SmashFast.Data;
using SmashFast.Gameplay.Cannon;
using UnityEngine;

namespace SmashFast.Level
{
    public class SpawnedBlock : MonoBehaviour, IPoolable
    {
        private const int HoldGraceTicks = 10;
        private const float BodyLinearDamping = 0.05f;
        private const float BodyAngularDamping = 0.8f;
        private const float BodyMaxDepenetrationVelocity = 3f;
        private const float BodySleepThreshold = 0.1f;

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
        private Rigidbody[] fracturePieces;
        private float[] fracturePieceMasses;
        private Vector3[] fracturePiecePositions;
        private Quaternion[] fracturePieceRotations;
        private Renderer[] tableTopRenderers;
        private ShrinkFadeEffect fadeEffect;
        private IDestructibleTracker destructibleTracker;
        private IUniversalObjectPool objectPool;
        private BlockMaterialType materialType;
        private bool isKinematicBase;
        private bool isTarget;
        private bool isBroken;
        private float massMultiplier = 1f;
        private bool isHolding;
        private int holdTicksRemaining;
        private int groundLayer = -1;
        private CancellationTokenSource returnPoolCts;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            groundLayer = LayerMask.NameToLayer("Ground");
            CacheFracturePieces();
            ShowIntact();
        }

        private void CacheFracturePieces()
        {
            if (fracturedVisual == null)
            {
                return;
            }

            fracturePieces = fracturedVisual.GetComponentsInChildren<Rigidbody>(true);
            int count = fracturePieces.Length;
            fracturePieceMasses = new float[count];
            fracturePiecePositions = new Vector3[count];
            fracturePieceRotations = new Quaternion[count];
            Transform[] pieceTransforms = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                Transform pieceTransform = fracturePieces[i].transform;
                pieceTransforms[i] = pieceTransform;
                fracturePieceMasses[i] = fracturePieces[i].mass;
                fracturePiecePositions[i] = pieceTransform.localPosition;
                fracturePieceRotations[i] = pieceTransform.localRotation;
            }

            fadeEffect = new ShrinkFadeEffect(pieceTransforms, fracturedVisual.GetComponentsInChildren<Renderer>(true));
        }

        private void ResetFracturePieces(Vector3 linearVelocity, Vector3 angularVelocity, Vector3 pushDirection)
        {
            if (fracturePieces == null)
            {
                return;
            }

            fadeEffect.Restore();
            int count = fracturePieces.Length;
            for (int i = 0; i < count; i++)
            {
                Rigidbody piece = fracturePieces[i];
                Transform pieceTransform = piece.transform;
                pieceTransform.SetLocalPositionAndRotation(fracturePiecePositions[i], fracturePieceRotations[i]);
                piece.mass = fracturePieceMasses[i] * massMultiplier;
                if (piece.isKinematic)
                {
                    continue;
                }

                Vector3 outward = pieceTransform.position - transform.position;
                outward.y = 0f;
                outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : Random.onUnitSphere;
                outward.y = 0f;
                piece.linearVelocity = linearVelocity
                    + (outward * (fragmentBurstSpeed * Random.Range(0.5f, 1.2f)))
                    + (pushDirection * (fragmentPushSpeed * Random.Range(0.6f, 1.1f)))
                    + (Vector3.up * (fragmentLiftSpeed * Random.Range(0.2f, 1f)));
                piece.angularVelocity = angularVelocity + (Random.insideUnitSphere * fragmentSpinSpeed);
            }
        }

        public void Initialize(
            BlockDefinition definition,
            Vector3 layoutScale,
            float globalBlockScale,
            float tableHeightFactor,
            Vector3Int sizeInCells,
            float blockMassMultiplier,
            IDestructibleTracker tracker,
            IUniversalObjectPool pool,
            FadeMaterialLibrary fadeLibrary)
        {
            CancelReturnPool();

            isKinematicBase = definition.isKinematicBase;
            materialType = definition.materialType;
            isTarget = definition.IsTarget();
            isBroken = false;
            isHolding = false;
            massMultiplier = blockMassMultiplier;
            destructibleTracker = tracker;
            objectPool = pool;
            if (fadeEffect != null)
            {
                fadeEffect.Prepare(fadeLibrary);
            }

            ApplyScale(layoutScale, globalBlockScale, tableHeightFactor, sizeInCells);
            ShowIntact();
            ConfigureBody((definition.mass > 0f ? definition.mass : 1f) * massMultiplier);
        }

        public void ApplyEditorPreviewVisuals(Vector3 layoutScale, float globalBlockScale, float tableHeightFactor, Vector3Int sizeInCells)
        {
            ApplyScale(layoutScale, globalBlockScale, tableHeightFactor, sizeInCells);
            ShowIntact();
        }

        public void ApplyEditorPreviewVisuals()
        {
            ShowIntact();
        }

        public void Hold()
        {
            if (body == null || body.isKinematic)
            {
                return;
            }

            isHolding = true;
            holdTicksRemaining = HoldGraceTicks;
            body.Sleep();
        }

        private void FixedUpdate()
        {
            if (!isHolding)
            {
                return;
            }

            if (holdTicksRemaining > 0)
            {
                holdTicksRemaining--;
                body.Sleep();
                return;
            }

            if (!body.IsSleeping())
            {
                isHolding = false;
            }
        }

        public void OnSpawned()
        {
            CancelReturnPool();
            isBroken = false;
            isTarget = false;
            destructibleTracker = null;
            objectPool = null;
            materialType = BlockMaterialType.TableFoundation;
            ShowIntact();
        }

        public void OnDespawned()
        {
            CancelReturnPool();
            UnregisterTarget();
            destructibleTracker = null;
            objectPool = null;
            ShowIntact();
        }

        private void ApplyScale(Vector3 layoutScale, float globalBlockScale, float tableHeightFactor, Vector3Int sizeInCells)
        {
            Vector3 globalScale = Vector3.one * globalBlockScale;
            if (tableTop == null)
            {
                Vector3 blockScale = layoutScale == Vector3.zero ? Vector3.one : layoutScale;
                transform.localScale = Vector3.Scale(blockScale, globalScale);
                return;
            }

            float heightFactor = Mathf.Max(1f, tableHeightFactor);
            transform.localScale = new Vector3(globalBlockScale, globalBlockScale * heightFactor, globalBlockScale);
            float topScaleX = Mathf.Max(1, sizeInCells.x);
            float topScaleZ = Mathf.Max(1, sizeInCells.z);
            Vector3 previousScale = tableTop.localScale;
            Vector3 previousPosition = tableTop.localPosition;
            tableTop.localScale = new Vector3(topScaleX, 1f / heightFactor, topScaleZ);
            tableTop.localPosition = new Vector3(
                previousPosition.x / previousScale.x * topScaleX,
                previousPosition.y,
                previousPosition.z / previousScale.z * topScaleZ);
            AlignTableTop(globalBlockScale * heightFactor * 0.5f);
        }

        private void AlignTableTop(float topHeightAboveCenter)
        {
            if (tableTopRenderers == null || tableTopRenderers.Length == 0)
            {
                tableTopRenderers = tableTop.GetComponentsInChildren<Renderer>(true);
            }

            if (tableTopRenderers.Length == 0)
            {
                return;
            }

            float currentTop = float.MinValue;
            for (int i = 0; i < tableTopRenderers.Length; i++)
            {
                float rendererTop = tableTopRenderers[i].bounds.max.y;
                if (rendererTop > currentTop)
                {
                    currentTop = rendererTop;
                }
            }

            float delta = (transform.position.y + topHeightAboveCenter) - currentTop;
            tableTop.position += Vector3.up * delta;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!isTarget || isBroken || collision == null)
            {
                return;
            }

            Collider other = collision.collider;
            if (other == null)
            {
                return;
            }

            if (groundLayer >= 0 && other.gameObject.layer == groundLayer)
            {
                Break(Vector3.zero);
                return;
            }

            if (materialType != BlockMaterialType.Glass && materialType != BlockMaterialType.CeramicJar)
            {
                return;
            }

            CannonProjectile projectile;
            bool hitByCannonBall = other.TryGetComponent(out projectile)
                || other.GetComponentInParent<CannonProjectile>() != null;

            if (!hitByCannonBall)
            {
                return;
            }

            Vector3 push = transform.position - other.transform.position;
            push.y = 0f;
            Break(push.sqrMagnitude > 0.0001f ? push.normalized : Vector3.zero);
        }

        private void Break(Vector3 pushDirection)
        {
            if (isBroken || !isTarget)
            {
                return;
            }

            isBroken = true;
            UnregisterTarget();

            if (fracturedVisual == null)
            {
                ReturnPool();
                return;
            }

            ShowFractured(pushDirection);
            ScheduleReturnPool().Forget();
        }

        private void ShowIntact()
        {
            if (fadeEffect != null)
            {
                fadeEffect.Restore();
            }

            if (intactVisual != null)
            {
                intactVisual.SetActive(true);
            }

            if (fracturedVisual != null)
            {
                fracturedVisual.SetActive(false);
            }
        }

        private void ShowFractured(Vector3 pushDirection)
        {
            if (intactVisual != null)
            {
                intactVisual.SetActive(false);
            }

            if (fracturedVisual == null)
            {
                return;
            }

            Vector3 inheritedLinear = Vector3.zero;
            Vector3 inheritedAngular = Vector3.zero;
            if (body != null && !body.isKinematic)
            {
                inheritedLinear = body.linearVelocity;
                inheritedAngular = body.angularVelocity;
            }

            fracturedVisual.SetActive(true);
            ResetFracturePieces(inheritedLinear, inheritedAngular, pushDirection);

            if (body == null)
            {
                return;
            }

            isHolding = false;
            body.detectCollisions = false;
            body.isKinematic = true;
        }

        private void ConfigureBody(float mass)
        {
            if (body == null)
            {
                return;
            }

            body.mass = mass;
            body.isKinematic = isKinematicBase;
            body.useGravity = !isKinematicBase;
            body.detectCollisions = true;
            body.linearDamping = BodyLinearDamping;
            body.angularDamping = BodyAngularDamping;
            body.maxDepenetrationVelocity = BodyMaxDepenetrationVelocity;
            body.sleepThreshold = BodySleepThreshold;

            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.Sleep();
            }
        }

        private void UnregisterTarget()
        {
            if (!isTarget)
            {
                return;
            }

            isTarget = false;
            if (destructibleTracker != null)
            {
                destructibleTracker.UnregisterTarget();
            }
        }

        private async UniTaskVoid ScheduleReturnPool()
        {
            CancelReturnPool();
            returnPoolCts = new CancellationTokenSource();
            CancellationToken token = returnPoolCts.Token;

            bool canceled = await UniTask.Delay(
                System.TimeSpan.FromSeconds(fragmentHoldSeconds),
                DelayType.DeltaTime,
                PlayerLoopTiming.Update,
                token).SuppressCancellationThrow();

            if (canceled || token.IsCancellationRequested)
            {
                return;
            }

            canceled = await fadeEffect.PlayAsync(fragmentFadeSeconds, token);
            if (canceled || token.IsCancellationRequested)
            {
                return;
            }

            ReturnPool();
        }

        private void CancelReturnPool()
        {
            if (returnPoolCts == null)
            {
                return;
            }

            returnPoolCts.Cancel();
            returnPoolCts.Dispose();
            returnPoolCts = null;
        }

        private void ReturnPool()
        {
            if (objectPool != null)
            {
                objectPool.Despawn(gameObject);
                return;
            }

            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            CancelReturnPool();
        }
    }
}
