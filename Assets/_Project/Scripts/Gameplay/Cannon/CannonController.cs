using SmashFest.Audio;
using SmashFest.Core.Effects;
using SmashFest.Core.Pooling;
using SmashFest.Level;
using SmashFest.Shop;
using UnityEngine;
using Zenject;

namespace SmashFest.Gameplay.Cannon
{
    public class CannonController : MonoBehaviour, IProjectileLifecycle
    {
        [SerializeField] private Transform barrel;
        [SerializeField] private Transform muzzle;
        [SerializeField] private Animator animator;
        [SerializeField] private string fireState = "Fire";
        [SerializeField, Min(0f)] private float fireDelay = 0.06f;
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField, Min(0.1f)] private float projectileSpeed = 14f;
        [SerializeField, Min(0.01f)] private float projectileMass = 1f;
        [SerializeField, Min(0)] private int prewarmCount = 12;
        [SerializeField, Min(0)] private int startingAmmo = 10;
        [SerializeField, Min(0f)] private float muzzleOffset = 0.6f;
        [SerializeField, Min(0f)] private float recoilDistance = 0.18f;
        [SerializeField, Min(0.01f)] private float recoilReturnSpeed = 2.5f;
        [SerializeField] private Vector2 pitchLimits = new Vector2(-15f, 65f);
        [SerializeField, Min(1f)] private float aimDistance = 80f;
        [SerializeField, Min(1f)] private float emptyAimDistance = 18f;
        [SerializeField] private LayerMask aimMask = Physics.DefaultRaycastLayers;
        [SerializeField] private Transform pedestal;
        [SerializeField, Min(0.1f)] private float pedestalWidth = 0.85f;

        private IPrefabPool pool;
        private IImpactFeedback feedback;
        private IAudioService audioService;
        private IBallSkinShop skinShop;
        private ICannonInput input;
        private Camera aimCamera;
        private Vector3? levelCenter;
        private AimResolver aimResolver;
        private readonly BallisticSolver ballistics = new BallisticSolver();
        private Vector3 pendingTarget;
        private CannonBarrel cannonBarrel;
        private CannonPedestal cannonPedestal;
        private bool hasPendingShot;
        private float pendingShotTime;
        private bool inputEnabled = true;
        private int fireStateHash;

        public int RemainingAmmo { get; private set; }
        public int MaxAmmo { get; private set; }
        public int ActiveProjectileCount { get; private set; }
        public int ShotsFired { get; private set; }
        public bool HasUnlimitedAmmo { get; private set; }

        [Inject]
        public void Construct(
            IPrefabPool pool,
            Camera gameplayCamera,
            ICannonInput input,
            [InjectOptional(Id = LevelSpawner.GroundBindingId)] Collider ground,
            [InjectOptional] IImpactFeedback feedback,
            [InjectOptional] IAudioService audio,
            [InjectOptional] IBallSkinShop skinShop)
        {
            this.input = input;
            this.pool = pool;
            this.feedback = feedback;
            audioService = audio;
            this.skinShop = skinShop;
            aimCamera = gameplayCamera;

            if (ground != null)
                levelCenter = new LevelGroundAnchor().Resolve(ground);
        }

        private void Awake()
        {
            cannonBarrel = new CannonBarrel(barrel != null ? barrel : transform, muzzle, pitchLimits, muzzleOffset, recoilDistance, recoilReturnSpeed);
            cannonPedestal = new CannonPedestal(pedestal, pedestalWidth);
            fireStateHash = Animator.StringToHash(fireState);
            LoadAmmo(startingAmmo, false);
        }

        private void Start()
        {
            aimResolver = new AimResolver(transform, aimMask, aimDistance, emptyAimDistance, levelCenter);
            if (projectilePrefab != null)
                pool.RegisterPrefab(projectilePrefab, prewarmCount);
        }

        private void Update()
        {
            if (animator == null)
                cannonBarrel.UpdateRecoil(Time.deltaTime);

            UpdatePendingShot();
            TryFire();
        }

        private void LateUpdate() => cannonBarrel.ApplyAim();

        public void LoadAmmo(int ammo, bool unlimited)
        {
            HasUnlimitedAmmo = unlimited;
            ShotsFired = 0;
            MaxAmmo = Mathf.Max(0, ammo);
            RemainingAmmo = MaxAmmo;
            CancelPendingShot();
        }

        public void SetInputEnabled(bool enabled) => inputEnabled = enabled;

        public void SetGroundHeight(float groundY) => cannonPedestal.Fit(transform.position.y, groundY);

        public void ResetAim()
        {
            cannonBarrel.ResetAim();
            CancelPendingShot();
        }

        public void OnProjectileSpent()
        {
            if (ActiveProjectileCount > 0)
                ActiveProjectileCount--;
        }

        private void CancelPendingShot()
        {
            if (!hasPendingShot)
                return;

            hasPendingShot = false;
            OnProjectileSpent();
        }

        private void TryFire()
        {
            if (!inputEnabled || hasPendingShot || RemainingAmmo <= 0 || projectilePrefab == null)
                return;

            if (!input.TryGetShot(out Vector2 screenPosition))
                return;

            Ray ray = aimCamera.ScreenPointToRay(screenPosition);
            Vector3 target = aimResolver.Resolve(aimCamera, ray);
            if ((target - cannonBarrel.Position).sqrMagnitude < 0.0001f)
                return;

            pendingTarget = target;
            cannonBarrel.AimAt(ballistics.VelocityToHit(cannonBarrel.Position, target, projectileSpeed), transform.forward);
            ShotsFired++;
            if (!HasUnlimitedAmmo)
                RemainingAmmo--;

            ActiveProjectileCount++;
            hasPendingShot = true;
            pendingShotTime = Time.time + (animator != null ? fireDelay : 0f);

            if (animator != null)
                animator.Play(fireStateHash, 0, 0f);
            else
                cannonBarrel.Kick();
        }

        private void UpdatePendingShot()
        {
            if (!hasPendingShot || Time.time < pendingShotTime)
                return;

            hasPendingShot = false;
            Vector3 muzzle = cannonBarrel.MuzzlePosition;
            Vector3 velocity = ballistics.VelocityToHit(muzzle, pendingTarget, projectileSpeed);
            CannonProjectile projectile = pool.Spawn<CannonProjectile>(projectilePrefab, muzzle, Quaternion.LookRotation(velocity, Vector3.up));
            if (projectile == null)
            {
                OnProjectileSpent();
                return;
            }

            projectile.AssignDependencies(pool, feedback);
            if (skinShop != null)
                projectile.ApplySkin(skinShop.Selected.Material, skinShop.Selected.FadeMaterial);

            projectile.Launch(velocity, projectileMass, this);
            audioService?.PlayCannonFire();
            feedback?.OnCannonFire();
        }
    }
}
