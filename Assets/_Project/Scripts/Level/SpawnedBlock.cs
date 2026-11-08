using SmashFast.Core.Pooling;
using SmashFast.Data;
using SmashFast.Gameplay.Cannon;
using UnityEngine;

namespace SmashFast.Level
{
    public class SpawnedBlock : MonoBehaviour, IPoolable
    {
        private Rigidbody body;
        private Renderer[] renderers;
        private MaterialPropertyBlock propertyBlock;
        private readonly int baseColorId = Shader.PropertyToID("_BaseColor");
        private readonly int colorId = Shader.PropertyToID("_Color");
        private IDestructibleTracker tracker;
        private IUniversalObjectPool objectPool;
        private IBlockFractureReceiver fractureReceiver;
        private float bodyMass = 1f;
        private float impactBreakThreshold = 3.5f;
        private float fallEliminationY = -1.5f;
        private int groundLayer = -1;
        private BlockMaterialType materialType;
        private bool kinematicBase;
        private bool countsAsDestructible;
        private bool isTargetBlock;
        private bool isBroken;
        private bool lifetimeNotified;

        public bool IsTargetBlock
        {
            get
            {
                return isTargetBlock;
            }
        }

        public bool IsBroken
        {
            get
            {
                return isBroken;
            }
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody>();
            }

            renderers = GetComponentsInChildren<Renderer>(true);
            propertyBlock = new MaterialPropertyBlock();
            groundLayer = LayerMask.NameToLayer("Ground");
            fractureReceiver = GetComponent<IBlockFractureReceiver>();
            if (fractureReceiver == null)
            {
                fractureReceiver = GetComponentInChildren<IBlockFractureReceiver>(true);
            }

            ApplyBodySettings();
            ResetBody();
        }

        public void InitializeBlock(BlockDefinition definition, Vector3 targetScale)
        {
            if (targetScale == Vector3.zero)
            {
                targetScale = Vector3.one;
            }

            transform.localScale = targetScale;
            bodyMass = definition.mass > 0f ? definition.mass : 1f;
            kinematicBase = definition.isKinematicBase;
            materialType = definition.materialType;
            impactBreakThreshold = definition.impactBreakThreshold > 0f ? definition.impactBreakThreshold : 3.5f;
            isTargetBlock = definition.IsTarget();
            isBroken = false;
            ApplyBodySettings();
            ResetBody();
            ClearPropertyOverrides();
            ApplyEditorColor(definition.editorColor);
        }

        public void ConfigureLifetime(IDestructibleTracker lifetimeTracker, IUniversalObjectPool pool, float eliminationY)
        {
            tracker = lifetimeTracker;
            objectPool = pool;
            fallEliminationY = eliminationY;
            countsAsDestructible = isTargetBlock;
            lifetimeNotified = false;
            isBroken = false;
        }

        public void OnImpactReceived(Vector3 hitPoint, Vector3 hitVelocity, float impactForce)
        {
            if (isBroken || !isTargetBlock)
            {
                return;
            }

            if (fractureReceiver == null)
            {
                return;
            }

            fractureReceiver.OnImpactReceived(hitPoint, hitVelocity, impactForce);
        }

        public void BreakBlock(Vector3 breakPoint, Vector3 breakVelocity)
        {
            if (isBroken || !isTargetBlock)
            {
                return;
            }

            isBroken = true;
            isTargetBlock = false;
            if (fractureReceiver != null)
            {
                fractureReceiver.OnBlockBroken(breakPoint, breakVelocity);
            }

            if (objectPool != null)
            {
                objectPool.Despawn(gameObject);
            }

            if (countsAsDestructible && !lifetimeNotified && tracker != null)
            {
                lifetimeNotified = true;
                tracker.NotifyDestructibleDespawned();
            }

            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        public void OnSpawned()
        {
            isBroken = false;
            isTargetBlock = false;
            countsAsDestructible = false;
            lifetimeNotified = false;
            materialType = BlockMaterialType.TableFoundation;
            ApplyBodySettings();
            ResetBody();
            ClearPropertyOverrides();
        }

        public void OnDespawned()
        {
            if (countsAsDestructible && !lifetimeNotified && tracker != null)
            {
                lifetimeNotified = true;
                tracker.NotifyDestructibleDespawned();
            }

            countsAsDestructible = false;
            isTargetBlock = false;
            tracker = null;
            objectPool = null;
            ResetBody();
            ClearPropertyOverrides();
        }

        private void FixedUpdate()
        {
            if (!isTargetBlock || isBroken)
            {
                return;
            }

            if (!EliminatesOnFall(materialType))
            {
                return;
            }

            if (transform.position.y >= fallEliminationY)
            {
                return;
            }

            Vector3 velocity = Vector3.zero;
            if (body != null && !body.isKinematic)
            {
                velocity = body.linearVelocity;
            }

            BreakBlock(transform.position, velocity);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!isTargetBlock || isBroken || collision == null)
            {
                return;
            }

            if (collision.contactCount <= 0)
            {
                return;
            }

            Vector3 hitPoint = collision.GetContact(0).point;
            Vector3 hitVelocity = collision.relativeVelocity;
            float impactSpeed = hitVelocity.magnitude;
            float impactForce = impactSpeed;
            if (body != null)
            {
                impactForce = impactSpeed * body.mass;
            }

            OnImpactReceived(hitPoint, hitVelocity, impactForce);
            if (isBroken)
            {
                return;
            }

            Collider other = collision.collider;
            if (other != null && groundLayer >= 0 && other.gameObject.layer == groundLayer)
            {
                if (EliminatesOnFall(materialType))
                {
                    BreakBlock(hitPoint, hitVelocity);
                }

                return;
            }

            if (!BreaksOnImpact(materialType))
            {
                return;
            }

            bool projectileHit = false;
            if (other != null)
            {
                CannonProjectile projectile;
                projectileHit = other.TryGetComponent(out projectile);
                if (!projectileHit)
                {
                    projectileHit = other.GetComponentInParent<CannonProjectile>() != null;
                }
            }

            if (projectileHit || impactSpeed >= impactBreakThreshold)
            {
                BreakBlock(hitPoint, hitVelocity);
            }
        }

        private bool EliminatesOnFall(BlockMaterialType material)
        {
            if (material == BlockMaterialType.Wood)
            {
                return true;
            }

            if (material == BlockMaterialType.Stone)
            {
                return true;
            }

            if (material == BlockMaterialType.Glass)
            {
                return true;
            }

            return material == BlockMaterialType.CeramicJar;
        }

        private bool BreaksOnImpact(BlockMaterialType material)
        {
            if (material == BlockMaterialType.Glass)
            {
                return true;
            }

            return material == BlockMaterialType.CeramicJar;
        }

        private void ApplyBodySettings()
        {
            if (body == null)
            {
                return;
            }

            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            body.mass = bodyMass;
            body.isKinematic = kinematicBase;
            body.useGravity = !kinematicBase;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            body.interpolation = RigidbodyInterpolation.None;
            body.constraints = RigidbodyConstraints.None;
            body.maxDepenetrationVelocity = 0.5f;
            body.solverIterations = 10;
            body.solverVelocityIterations = 4;
            body.sleepThreshold = 0.05f;
        }

        public void SettleInPlace()
        {
            if (body == null)
            {
                return;
            }

            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            body.Sleep();
        }

        private void ResetBody()
        {
            SettleInPlace();
        }

        private void ClearPropertyOverrides()
        {
            if (renderers == null || propertyBlock == null)
            {
                return;
            }

            propertyBlock.Clear();
            int count = renderers.Length;
            for (int i = 0; i < count; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.SetPropertyBlock(propertyBlock);
            }
        }

        private void ApplyEditorColor(Color tint)
        {
            if (renderers == null || propertyBlock == null)
            {
                return;
            }

            if (tint.a <= 0.01f)
            {
                tint = Color.white;
            }

            propertyBlock.SetColor(baseColorId, tint);
            propertyBlock.SetColor(colorId, tint);
            int count = renderers.Length;
            for (int i = 0; i < count; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
