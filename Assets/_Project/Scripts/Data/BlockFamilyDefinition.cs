using System;
using UnityEngine;

namespace SmashFast.Data
{
    [Serializable]
    public class BlockFamilyDefinition
    {
        public const int MaxLength = 5;

        public string familyName = "SnowLogCylinder";

        public GameObject length1Prefab;

        public GameObject length2Prefab;

        public GameObject length3Prefab;

        public GameObject length4Prefab;

        public GameObject length5Prefab;

        public Color xColor = new Color(0.95f, 0.45f, 0.18f, 1f);
        public Color yColor = new Color(0.25f, 0.7f, 0.95f, 1f);
        public Color zColor = new Color(0.35f, 0.85f, 0.4f, 1f);

        public BlockMaterialType materialType = BlockMaterialType.Wood;
        public float baseMass = 5f;
        public float massPerExtraLength = 2f;
        public float impactBreakThreshold = 3.5f;

        public bool fragileProp;

        public bool foundationTable;

        public bool onlyVerticalAxis;

        public int baseBlockId;

        public void ApplyNamingRules()
        {
            if (!string.IsNullOrEmpty(familyName) && familyName.Contains("Cylinder"))
            {
                onlyVerticalAxis = true;
            }
        }

        public GameObject GetLengthPrefab(int length)
        {
            if (foundationTable)
            {
                return length1Prefab;
            }

            if (length == 1)
            {
                return length1Prefab;
            }

            if (length == 2)
            {
                return length2Prefab;
            }

            if (length == 3)
            {
                return length3Prefab;
            }

            if (length == 4)
            {
                return length4Prefab;
            }

            if (length == 5)
            {
                return length5Prefab;
            }

            return null;
        }

        public void SetLengthPrefab(int length, GameObject prefab)
        {
            if (length == 1)
            {
                length1Prefab = prefab;
            }
            else if (length == 2)
            {
                length2Prefab = prefab;
            }
            else if (length == 3)
            {
                length3Prefab = prefab;
            }
            else if (length == 4)
            {
                length4Prefab = prefab;
            }
            else if (length == 5)
            {
                length5Prefab = prefab;
            }
        }

        public int GetHighestAssignedLength()
        {
            if (foundationTable)
            {
                return length1Prefab != null ? 1 : 0;
            }

            for (int length = MaxLength; length >= 1; length--)
            {
                if (GetLengthPrefab(length) != null)
                {
                    return length;
                }
            }

            return 0;
        }
    }
}
