using System;
using UnityEngine;

namespace SmashFest.Data
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

        [NonSerialized] public int familyKey;

        public bool IsTable() => shapeRole == BlockShapeRole.FoundationTable || materialType == BlockMaterialType.TableFoundation;

        public bool IsTarget() => !IsTable() && !isKinematicBase;
    }
}
