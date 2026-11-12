using UnityEngine;

namespace SmashFast.Level
{
    public class LevelGroundAnchor
    {
        public Vector3 Resolve(Collider groundCollider)
        {
            if (groundCollider == null)
            {
                return Vector3.zero;
            }

            BoxCollider box = groundCollider as BoxCollider;
            if (box == null)
            {
                Bounds bounds = groundCollider.bounds;
                return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            }

            Transform boxTransform = box.transform;
            Vector3 center = boxTransform.TransformPoint(box.center);
            Vector3 scale = boxTransform.lossyScale;
            float top = center.y + (box.size.y * Mathf.Abs(scale.y) * 0.5f);
            return new Vector3(center.x, top, center.z);
        }
    }
}
