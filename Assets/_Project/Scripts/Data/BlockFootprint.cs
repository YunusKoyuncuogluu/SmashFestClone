using UnityEngine;

namespace SmashFast.Data
{
    public class BlockFootprint
    {
        public Vector3Int ClampSize(Vector3Int size)
        {
            return new Vector3Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y), Mathf.Max(1, size.z));
        }

        public Vector3Int GetEffectiveSize(Vector3Int sizeInCells, Vector3 rotationEuler)
        {
            Vector3Int size = ClampSize(sizeInCells);

            if (IsSideways(rotationEuler.z))
                (size.x, size.y) = (size.y, size.x);

            if (IsSideways(rotationEuler.x))
                (size.y, size.z) = (size.z, size.y);

            if (IsSideways(rotationEuler.y))
                (size.x, size.z) = (size.z, size.x);

            return size;
        }

        public Vector3 ResolveLengthAxisEuler(Vector3Int sizeInCells, int axis)
        {
            Vector3Int size = ClampSize(sizeInCells);
            int lengthAxis = 0;
            if (size.y >= size.x && size.y >= size.z)
                lengthAxis = 1;
            else if (size.z >= size.x && size.z >= size.y)
                lengthAxis = 2;

            axis = Mathf.Clamp(axis, 0, 2);
            if (lengthAxis == axis)
                return Vector3.zero;

            return (lengthAxis, axis) switch
            {
                (0, 1) => new Vector3(0f, 0f, -90f),
                (0, 2) => new Vector3(0f, 90f, 0f),
                (1, 0) => new Vector3(0f, 0f, 90f),
                (1, 2) => new Vector3(90f, 0f, 0f),
                (2, 0) => new Vector3(0f, -90f, 0f),
                _ => new Vector3(-90f, 0f, 0f)
            };
        }

        public bool ContainsCell(Vector3Int anchor, Vector3Int effectiveSize, Vector3Int cell)
        {
            Vector3Int size = ClampSize(effectiveSize);
            return cell.x >= anchor.x && cell.x < anchor.x + size.x
                && cell.y >= anchor.y && cell.y < anchor.y + size.y
                && cell.z >= anchor.z && cell.z < anchor.z + size.z;
        }

        public bool ContainsXZ(Vector3Int anchor, Vector3Int effectiveSize, int x, int z)
        {
            Vector3Int size = ClampSize(effectiveSize);
            return x >= anchor.x && x < anchor.x + size.x
                && z >= anchor.z && z < anchor.z + size.z;
        }

        public bool Overlaps(Vector3Int anchorA, Vector3Int sizeA, Vector3Int anchorB, Vector3Int sizeB)
        {
            sizeA = ClampSize(sizeA);
            sizeB = ClampSize(sizeB);
            return anchorA.x < anchorB.x + sizeB.x && anchorB.x < anchorA.x + sizeA.x
                && anchorA.y < anchorB.y + sizeB.y && anchorB.y < anchorA.y + sizeA.y
                && anchorA.z < anchorB.z + sizeB.z && anchorB.z < anchorA.z + sizeA.z;
        }

        private bool IsSideways(float degrees)
        {
            int angle = Mathf.RoundToInt(Mathf.Repeat(degrees, 360f)) % 360;
            return angle == 90 || angle == 270;
        }
    }
}
