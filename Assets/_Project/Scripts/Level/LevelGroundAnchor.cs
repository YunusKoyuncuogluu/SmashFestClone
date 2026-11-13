using UnityEngine;

namespace SmashFest.Level
{
    public class LevelGroundAnchor
    {
        public Vector3 Resolve(Collider ground)
        {
            if (ground == null)
                return Vector3.zero;

            if (ground is BoxCollider box)
            {
                Vector3 center = box.transform.TransformPoint(box.center);
                float top = center.y + box.size.y * Mathf.Abs(box.transform.lossyScale.y) * 0.5f;
                return new Vector3(center.x, top, center.z);
            }

            Bounds bounds = ground.bounds;
            return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        }
    }
}
