using SmashFast.Core.Effects;
using SmashFast.Core.Pooling;
using SmashFast.Level;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Zenject;

namespace SmashFast.Gameplay.Cannon
{
    public class CannonController : MonoBehaviour, IProjectileLifecycle
    {
        [SerializeField] private Transform barrel;
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField, Min(0.1f)] private float projectileSpeed = 14f;
        [SerializeField, Min(0.01f)] private float projectileMass = 1f;
        [SerializeField, Min(0)] private int prewarmCount = 12;
        [SerializeField, Min(0)] private int startingAmmo = 10;
        [SerializeField, Min(0f)] private float muzzleOffset = 0.6f;
        [SerializeField, Min(0f)] private float recoilDistance = 0.18f;
        [SerializeField, Min(0.01f)] private float recoilReturnSpeed = 2.5f;
        [SerializeField, Min(1f)] private float aimDistance = 80f;
        [SerializeField, Min(1f)] private float emptyAimDistance = 18f;
        [SerializeField] private LayerMask aimMask = Physics.DefaultRaycastLayers;

        private readonly RaycastHit[] hits = new RaycastHit[16];

        private IUniversalObjectPool pool;
        private FadeMaterialLibrary fadeLibrary;
        private Camera aimCamera;
        private Vector3 barrelRestPosition;
        private Vector3 levelCenter;
        private bool hasLevelCenter;
        private float recoil;
        private bool inputEnabled = true;

        public int RemainingAmmo { get; private set; }
        public int MaxAmmo { get; private set; }
        public int ActiveProjectileCount { get; private set; }

        [Inject]
        public void Construct(
            IUniversalObjectPool pool,
            FadeMaterialLibrary fadeLibrary,
            [InjectOptional] Camera gameplayCamera,
            [InjectOptional(Id = LevelSpawner.GroundBindingId)] Collider ground)
        {
            this.pool = pool;
            this.fadeLibrary = fadeLibrary;
            if (gameplayCamera != null)
                aimCamera = gameplayCamera;

            if (ground != null)
            {
                levelCenter = new LevelGroundAnchor().Resolve(ground);
                hasLevelCenter = true;
            }
        }

        private void Awake()
        {
            if (barrel == null)
                barrel = transform;

            if (aimMask.value == 0)
                aimMask = Physics.DefaultRaycastLayers;

            barrelRestPosition = barrel.localPosition;
            LoadAmmo(startingAmmo);
        }

        private void Start()
        {
            if (aimCamera == null)
                aimCamera = Camera.main;

            if (projectilePrefab != null)
                pool.RegisterPrefab(projectilePrefab, prewarmCount);
        }

        private void Update()
        {
            UpdateRecoil();
            TryFire();
        }

        public void LoadAmmo(int ammo)
        {
            MaxAmmo = Mathf.Max(0, ammo);
            RemainingAmmo = MaxAmmo;
        }

        public void SetInputEnabled(bool enabled) => inputEnabled = enabled;

        public void OnProjectileReleased()
        {
            if (ActiveProjectileCount > 0)
                ActiveProjectileCount--;
        }

        private void UpdateRecoil()
        {
            if (recoil <= 0f)
                return;

            recoil = Mathf.Max(0f, recoil - recoilReturnSpeed * Time.deltaTime);
            barrel.localPosition = barrelRestPosition - Vector3.forward * recoil;
        }

        private void TryFire()
        {
            if (!inputEnabled || RemainingAmmo <= 0 || projectilePrefab == null || aimCamera == null)
                return;

            if (!TryReadPress(out Vector2 screenPosition, out int pointerId) || IsPointerOverUi(pointerId))
                return;

            Ray ray = aimCamera.ScreenPointToRay(screenPosition);
            Vector3 direction = GetAimPoint(ray) - barrel.position;
            direction = direction.sqrMagnitude < 0.0001f ? barrel.forward : direction.normalized;

            barrel.rotation = Quaternion.LookRotation(direction, Vector3.up);
            recoil = recoilDistance;

            Vector3 spawnPosition = barrel.position + barrel.forward * muzzleOffset;
            CannonProjectile projectile = pool.Spawn<CannonProjectile>(projectilePrefab, spawnPosition, barrel.rotation);
            if (projectile == null)
                return;

            projectile.AssignDependencies(pool, fadeLibrary);
            projectile.Launch(barrel.forward, projectileSpeed, projectileMass, this);
            ActiveProjectileCount++;
            RemainingAmmo--;
        }

        private bool TryReadPress(out Vector2 screenPosition, out int pointerId)
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                screenPosition = mouse.position.ReadValue();
                pointerId = -1;
                return true;
            }

            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame)
            {
                screenPosition = touchscreen.primaryTouch.position.ReadValue();
                pointerId = touchscreen.primaryTouch.touchId.ReadValue();
                return true;
            }

            screenPosition = Vector2.zero;
            pointerId = -1;
            return false;
        }

        private bool IsPointerOverUi(int pointerId)
        {
            EventSystem eventSystem = EventSystem.current;
            return eventSystem != null && eventSystem.IsPointerOverGameObject(pointerId);
        }

        private Vector3 GetAimPoint(Ray ray)
        {
            int hitCount = Physics.RaycastNonAlloc(ray, hits, aimDistance, aimMask, QueryTriggerInteraction.Ignore);
            float closest = float.MaxValue;
            bool found = false;
            Vector3 aimPoint = Vector3.zero;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.distance >= closest || IsIgnored(hit.collider))
                    continue;

                closest = hit.distance;
                aimPoint = hit.point;
                found = true;
            }

            if (found)
                return aimPoint;

            Transform cam = aimCamera.transform;
            Vector3 planePoint = hasLevelCenter ? levelCenter : cam.position + cam.forward * emptyAimDistance;
            Plane aimPlane = new Plane(-cam.forward, planePoint);
            return aimPlane.Raycast(ray, out float enter) ? ray.GetPoint(enter) : ray.GetPoint(emptyAimDistance);
        }

        private bool IsIgnored(Collider other)
        {
            if (other == null)
                return true;

            return other.transform.IsChildOf(transform) || other.GetComponentInParent<CannonProjectile>() != null;
        }
    }
}
