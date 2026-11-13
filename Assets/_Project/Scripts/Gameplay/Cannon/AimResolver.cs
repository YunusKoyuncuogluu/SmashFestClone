using UnityEngine;

namespace SmashFest.Gameplay.Cannon
{
    public class AimResolver
    {
        private readonly RaycastHit[] hits = new RaycastHit[16];
        private readonly Transform ignoredRoot;
        private readonly LayerMask mask;
        private readonly float maxDistance;
        private readonly float fallbackDistance;
        private readonly Vector3? levelCenter;

        public AimResolver(Transform ignoredRoot, LayerMask mask, float maxDistance, float fallbackDistance, Vector3? levelCenter)
        {
            this.ignoredRoot = ignoredRoot;
            this.mask = mask.value != 0 ? mask : (LayerMask)Physics.DefaultRaycastLayers;
            this.maxDistance = maxDistance;
            this.fallbackDistance = fallbackDistance;
            this.levelCenter = levelCenter;
        }

        public Vector3 Resolve(Camera camera, Ray ray)
        {
            int hitCount = Physics.RaycastNonAlloc(ray, hits, maxDistance, mask, QueryTriggerInteraction.Ignore);
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

            return found ? aimPoint : ProjectOnLevelPlane(camera.transform, ray);
        }

        private Vector3 ProjectOnLevelPlane(Transform view, Ray ray)
        {
            Vector3 planePoint = levelCenter ?? view.position + view.forward * fallbackDistance;
            Plane plane = new Plane(-view.forward, planePoint);
            return plane.Raycast(ray, out float enter) ? ray.GetPoint(enter) : ray.GetPoint(fallbackDistance);
        }

        private bool IsIgnored(Collider other)
        {
            if (other == null)
                return true;

            return other.transform.IsChildOf(ignoredRoot) || other.GetComponentInParent<CannonProjectile>() != null;
        }
    }
}
