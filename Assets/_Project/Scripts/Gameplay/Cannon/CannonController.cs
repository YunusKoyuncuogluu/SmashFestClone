using SmashFast.Core.Pooling;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Zenject;

namespace SmashFast.Gameplay.Cannon
{
    public class CannonController : MonoBehaviour, IProjectileLifecycle
    {
        [SerializeField] private Transform cannonBarrelTransform;
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField, Min(0.1f)] private float projectileSpeed = 22f;
        [SerializeField, Min(0.01f)] private float projectileMass = 2f;
        [SerializeField, Min(0)] private int prewarmCount = 12;
        [SerializeField, Min(0)] private int startingAmmo = 10;
        [SerializeField, Min(0f)] private float muzzleForwardOffset = 0.6f;
        [SerializeField, Min(0f)] private float recoilDistance = 0.18f;
        [SerializeField, Min(0.01f)] private float recoilReturnSpeed = 2.5f;
        [SerializeField, Min(1f)] private float aimDistance = 80f;
        [SerializeField, Min(1f)] private float emptyAimDistance = 18f;
        [SerializeField] private LayerMask aimMask = Physics.DefaultRaycastLayers;

        private readonly RaycastHit[] raycastHits = new RaycastHit[16];

        private IUniversalObjectPool objectPool;
        private Camera aimCamera;
        private Vector3 baseLocalPosition;
        private float recoilOffset;
        private int remainingAmmo;
        private int loadedAmmo;
        private int activeProjectileCount;
        private bool inputEnabled;
        private bool barrelReady;

        public int RemainingAmmo
        {
            get
            {
                return remainingAmmo;
            }
        }

        public int MaxAmmo
        {
            get
            {
                return loadedAmmo;
            }
        }

        public int ActiveProjectileCount
        {
            get
            {
                return activeProjectileCount;
            }
        }

        [Inject]
        public void Construct(IUniversalObjectPool objectPool, [InjectOptional] Camera gameplayCamera)
        {
            this.objectPool = objectPool;
            if (gameplayCamera != null)
            {
                aimCamera = gameplayCamera;
            }
        }

        private void Awake()
        {
            if (cannonBarrelTransform == null)
            {
                cannonBarrelTransform = transform;
            }

            baseLocalPosition = cannonBarrelTransform.localPosition;
            barrelReady = true;
            inputEnabled = true;
            loadedAmmo = startingAmmo;
            remainingAmmo = startingAmmo;
            if (aimMask.value == 0)
            {
                aimMask = Physics.DefaultRaycastLayers;
            }
        }

        private void Start()
        {
            if (aimCamera == null)
            {
                aimCamera = Camera.main;
            }

            if (objectPool == null || projectilePrefab == null)
            {
                return;
            }

            objectPool.RegisterPrefab(projectilePrefab, prewarmCount);
        }

        private void Update()
        {
            UpdateRecoil(Time.deltaTime);
            TryFire();
        }

        public void LoadAmmo(int ammo)
        {
            int safeAmmo = ammo < 0 ? 0 : ammo;
            loadedAmmo = safeAmmo;
            remainingAmmo = safeAmmo;
        }

        public void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
        }

        public void OnProjectileReleased()
        {
            if (activeProjectileCount <= 0)
            {
                return;
            }

            activeProjectileCount--;
        }

        private void UpdateRecoil(float deltaTime)
        {
            if (!barrelReady || recoilOffset <= 0f)
            {
                return;
            }

            recoilOffset -= recoilReturnSpeed * deltaTime;
            if (recoilOffset < 0f)
            {
                recoilOffset = 0f;
            }

            cannonBarrelTransform.localPosition = baseLocalPosition - (Vector3.forward * recoilOffset);
        }

        private void TryFire()
        {
            if (!inputEnabled || remainingAmmo <= 0 || objectPool == null || projectilePrefab == null || aimCamera == null || !barrelReady)
            {
                return;
            }

            Vector2 screenPosition;
            int pointerId;
            if (!TryReadPress(out screenPosition, out pointerId))
            {
                return;
            }

            if (IsBlockedByUi(pointerId))
            {
                return;
            }

            Ray ray = aimCamera.ScreenPointToRay(screenPosition);
            Vector3 aimPoint;
            if (!TryGetAimPoint(ray, out aimPoint))
            {
                return;
            }

            Vector3 direction = aimPoint - cannonBarrelTransform.position;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = cannonBarrelTransform.forward;
            }
            else
            {
                direction.Normalize();
            }

            cannonBarrelTransform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            recoilOffset = recoilDistance;
            Vector3 spawnPosition = cannonBarrelTransform.position + (cannonBarrelTransform.forward * muzzleForwardOffset);
            CannonProjectile projectile = objectPool.Spawn<CannonProjectile>(projectilePrefab, spawnPosition, cannonBarrelTransform.rotation, null);
            if (projectile == null)
            {
                return;
            }

            projectile.AssignPool(objectPool);
            if (!projectile.Launch(cannonBarrelTransform.forward, projectileSpeed, projectileMass, this))
            {
                objectPool.Despawn(projectile.gameObject);
                return;
            }

            activeProjectileCount++;
            remainingAmmo--;
        }

        private bool TryReadPress(out Vector2 screenPosition, out int pointerId)
        {
            screenPosition = Vector2.zero;
            pointerId = -1;
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                screenPosition = mouse.position.ReadValue();
                pointerId = -1;
                return true;
            }

            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen == null)
            {
                return false;
            }

            UnityEngine.InputSystem.Controls.TouchControl touch = touchscreen.primaryTouch;
            if (!touch.press.wasPressedThisFrame)
            {
                return false;
            }

            screenPosition = touch.position.ReadValue();
            pointerId = touch.touchId.ReadValue();
            return true;
        }

        private bool IsBlockedByUi(int pointerId)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            return eventSystem.IsPointerOverGameObject(pointerId);
        }

        private bool TryGetAimPoint(Ray ray, out Vector3 aimPoint)
        {
            aimPoint = Vector3.zero;
            float closestDistance = float.MaxValue;
            bool foundHit = false;
            int hitCount = Physics.RaycastNonAlloc(ray, raycastHits, aimDistance, aimMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = raycastHits[i];
                if (hit.collider == null || IsCannonCollider(hit.collider.transform))
                {
                    continue;
                }

                if (hit.distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = hit.distance;
                aimPoint = hit.point;
                foundHit = true;
            }

            if (foundHit)
            {
                return true;
            }

            Transform cameraTransform = aimCamera.transform;
            Plane aimPlane = new Plane(-cameraTransform.forward, cameraTransform.position + (cameraTransform.forward * emptyAimDistance));
            float enter;
            if (!aimPlane.Raycast(ray, out enter))
            {
                aimPoint = ray.GetPoint(emptyAimDistance);
                return true;
            }

            aimPoint = ray.GetPoint(enter);
            return true;
        }

        private bool IsCannonCollider(Transform hitTransform)
        {
            if (hitTransform == null)
            {
                return false;
            }

            if (hitTransform == transform)
            {
                return true;
            }

            return hitTransform.IsChildOf(transform);
        }
    }
}
