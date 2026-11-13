using System;
using UnityEngine;

namespace SmashFest.Data
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

        public bool fragileProp;
        public bool foundationTable;
        public bool onlyVerticalAxis;

        public int baseBlockId;

        public void ApplyNamingRules()
        {
            if (!string.IsNullOrEmpty(familyName) && familyName.Contains("Cylinder"))
                onlyVerticalAxis = true;
        }

        public GameObject GetLengthPrefab(int length)
        {
            if (foundationTable)
                return length1Prefab;

            return length switch
            {
                1 => length1Prefab,
                2 => length2Prefab,
                3 => length3Prefab,
                4 => length4Prefab,
                5 => length5Prefab,
                _ => null
            };
        }

        public int GetHighestAssignedLength()
        {
            if (foundationTable)
                return length1Prefab != null ? 1 : 0;

            for (int length = MaxLength; length >= 1; length--)
            {
                if (GetLengthPrefab(length) != null)
                    return length;
            }

            return 0;
        }
    }
}
