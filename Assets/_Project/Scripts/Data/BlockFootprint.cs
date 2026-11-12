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
            int roll = SnapRightAngle(rotationEuler.z);
            int pitch = SnapRightAngle(rotationEuler.x);
            int yaw = SnapRightAngle(rotationEuler.y);

            if (roll == 90 || roll == 270)
            {
                int width = size.x;
                size.x = size.y;
                size.y = width;
            }

            if (pitch == 90 || pitch == 270)
            {
                int height = size.y;
                size.y = size.z;
                size.z = height;
            }

            if (yaw == 90 || yaw == 270)
            {
                int width = size.x;
                size.x = size.z;
                size.z = width;
            }

            return size;
        }

        public Vector3 ResolveLengthAxisEuler(Vector3Int sizeInCells, int axis)
        {
            Vector3Int size = ClampSize(sizeInCells);
            int lengthAxis = 0;
            if (size.y >= size.x && size.y >= size.z)
            {
                lengthAxis = 1;
            }
            else if (size.z >= size.x && size.z >= size.y)
            {
                lengthAxis = 2;
            }

            if (axis < 0)
            {
                axis = 0;
            }

            if (axis > 2)
            {
                axis = 2;
            }

            if (lengthAxis == axis)
            {
                return Vector3.zero;
            }

            if (lengthAxis == 0 && axis == 1)
            {
                return new Vector3(0f, 0f, -90f);
            }

            if (lengthAxis == 0 && axis == 2)
            {
                return new Vector3(0f, 90f, 0f);
            }

            if (lengthAxis == 1 && axis == 0)
            {
                return new Vector3(0f, 0f, 90f);
            }

            if (lengthAxis == 1 && axis == 2)
            {
                return new Vector3(90f, 0f, 0f);
            }

            if (lengthAxis == 2 && axis == 0)
            {
                return new Vector3(0f, -90f, 0f);
            }

            return new Vector3(-90f, 0f, 0f);
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

        private int SnapRightAngle(float degrees)
        {
            int angle = Mathf.RoundToInt(Mathf.Repeat(degrees, 360f));
            if (angle >= 360)
            {
                angle = 0;
            }

            return angle;
        }
    }
}
