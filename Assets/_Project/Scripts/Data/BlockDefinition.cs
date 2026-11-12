using System;
using UnityEngine;

namespace SmashFast.Data
{
    public enum BlockMaterialType
    {
        TableFoundation = 0,
        Wood = 1,
        Stone = 2,
        Glass = 3,
        CeramicJar = 4
    }

    public enum BlockShapeRole
    {
        FoundationTable = 0,
        StandardBlock = 1,
        HorizontalPlank = 2,
        FragileProp = 3,
        TopOrnament = 4,
        VerticalPillar = 5
    }

    [Serializable]
    public struct BlockDefinition
    {
        public string blockName;

        public int blockId;

        public GameObject prefab;

        public Mesh previewMesh;

        public Vector3 baseScale;

        public Vector3 meshEuler;

        public Vector3Int sizeInCells;

        public Color editorColor;

        public bool isKinematicBase;

        public float mass;

        public BlockMaterialType materialType;

        public BlockShapeRole shapeRole;

        public bool canSupportBlocksAbove;

        public float impactBreakThreshold;

        [NonSerialized] public int familyKey;

        public bool IsTarget()
        {
            if (materialType == BlockMaterialType.TableFoundation)
            {
                return false;
            }

            if (shapeRole == BlockShapeRole.FoundationTable)
            {
                return false;
            }

            if (isKinematicBase)
            {
                return false;
            }

            return true;
        }
    }
}
