using UnityEngine;

namespace SmashFest.Gameplay.Cannon
{
    public class CannonBarrel
    {
        private readonly Transform barrel;
        private readonly Transform muzzle;
        private readonly Vector2 pitchLimits;
        private readonly float muzzleOffset;
        private readonly float recoilDistance;
        private readonly float recoilReturnSpeed;
        private readonly Vector3 restPosition;
        private readonly Quaternion restRotation;
        private readonly Vector3 axis = Vector3.forward;

        private bool hasAim;
        private float recoil;

        public CannonBarrel(Transform barrel, Transform muzzle, Vector2 pitchLimits, float muzzleOffset, float recoilDistance, float recoilReturnSpeed)
        {
            this.barrel = barrel;
            this.muzzle = muzzle;
            this.pitchLimits = pitchLimits;
            this.muzzleOffset = muzzleOffset;
            this.recoilDistance = recoilDistance;
            this.recoilReturnSpeed = recoilReturnSpeed;
            restPosition = barrel.localPosition;
            restRotation = barrel.localRotation;
            if (muzzle != null && muzzle.position != barrel.position)
                axis = barrel.InverseTransformDirection(muzzle.position - barrel.position).normalized;
        }

        public Vector3 Position => barrel.position;
        public Vector3 AimDirection { get; private set; }
        public Vector3 MuzzlePosition => muzzle != null ? muzzle.position : barrel.position + AimDirection * muzzleOffset;

        public void AimAt(Vector3 direction, Vector3 fallbackForward)
        {
            Vector3 flat = new Vector3(direction.x, 0f, direction.z);
            Vector3 heading = flat.sqrMagnitude > 0.0001f ? flat.normalized : fallbackForward;
            Vector3 side = Vector3.Cross(Vector3.up, heading);

            float pitch = Mathf.Atan2(direction.y, flat.magnitude) * Mathf.Rad2Deg;
            pitch = Mathf.Clamp(pitch, pitchLimits.x, pitchLimits.y);
            AimDirection = Quaternion.AngleAxis(-pitch, side) * heading;
            hasAim = true;
        }

        public void ApplyAim()
        {
            if (!hasAim)
                return;

            Vector3 current = barrel.TransformDirection(axis);
            barrel.rotation = Quaternion.FromToRotation(current, AimDirection) * barrel.rotation;
        }

        public void Kick() => recoil = recoilDistance;

        public void UpdateRecoil(float deltaTime)
        {
            if (recoil <= 0f)
                return;

            recoil = Mathf.Max(0f, recoil - recoilReturnSpeed * deltaTime);
            barrel.localPosition = restPosition - barrel.localRotation * axis * recoil;
        }

        public void ResetAim()
        {
            hasAim = false;
            recoil = 0f;
            barrel.localPosition = restPosition;
            barrel.localRotation = restRotation;
        }
    }
}
