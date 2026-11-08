using UnityEngine;

namespace SmashFast.Data
{
    public class BlockFootprint
    {
        public Vector3Int ClampSize(Vector3Int sizeInCells)
        {
            return new Vector3Int(
                Mathf.Max(1, sizeInCells.x),
                Mathf.Max(1, sizeInCells.y),
                Mathf.Max(1, sizeInCells.z));
        }

        public Vector3Int GetEffectiveSize(Vector3Int sizeInCells, Vector3 rotationEuler)
        {
            Vector3Int size = ClampSize(sizeInCells);
            int yaw = Mathf.RoundToInt(Mathf.Repeat(rotationEuler.y, 360f));
            if (yaw >= 360)
            {
                yaw = 0;
            }

            if (yaw == 90 || yaw == 270)
            {
                int width = size.x;
                size.x = size.z;
                size.z = width;
            }

            return size;
        }

        public bool ContainsCell(Vector3Int anchor, Vector3Int effectiveSize, Vector3Int cell)
        {
            Vector3Int size = ClampSize(effectiveSize);
            if (cell.x < anchor.x || cell.y < anchor.y || cell.z < anchor.z)
            {
                return false;
            }

            if (cell.x >= anchor.x + size.x || cell.y >= anchor.y + size.y || cell.z >= anchor.z + size.z)
            {
                return false;
            }

            return true;
        }

        public bool Overlaps(Vector3Int anchorA, Vector3Int sizeA, Vector3Int anchorB, Vector3Int sizeB)
        {
            Vector3Int clampedA = ClampSize(sizeA);
            Vector3Int clampedB = ClampSize(sizeB);
            if (anchorA.x + clampedA.x <= anchorB.x || anchorB.x + clampedB.x <= anchorA.x)
            {
                return false;
            }

            if (anchorA.y + clampedA.y <= anchorB.y || anchorB.y + clampedB.y <= anchorA.y)
            {
                return false;
            }

            if (anchorA.z + clampedA.z <= anchorB.z || anchorB.z + clampedB.z <= anchorA.z)
            {
                return false;
            }

            return true;
        }

        public bool ContainsXZ(Vector3Int anchor, Vector3Int effectiveSize, int cellX, int cellZ)
        {
            Vector3Int size = ClampSize(effectiveSize);
            if (cellX < anchor.x || cellZ < anchor.z)
            {
                return false;
            }

            if (cellX >= anchor.x + size.x || cellZ >= anchor.z + size.z)
            {
                return false;
            }

            return true;
        }
    }
}
