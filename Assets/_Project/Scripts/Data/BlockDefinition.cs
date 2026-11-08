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
        TopOrnament = 4
    }

    [Serializable]
    public struct BlockDefinition
    {
        [Tooltip("Display name used by the blueprint editor and the catalog list.")]
        public string blockName;

        [Tooltip("Unique id referenced by level placements.")]
        public int blockId;

        [Tooltip("Physical object spawned from the pool at runtime.")]
        public GameObject prefab;

        [Tooltip("Mesh drawn in the Scene view blueprint outside Play Mode.")]
        public Mesh previewMesh;

        [Tooltip("Local scale multiplied by the level cell size and by sizeInCells.")]
        public Vector3 baseScale;

        [Tooltip("Footprint in grid cells. X is width, Y is height, Z is depth. Minimum is 1,1,1. A yaw of 90 or 270 swaps X and Z.")]
        public Vector3Int sizeInCells;

        [Tooltip("Tint used by the 2D matrix and the scene blueprint.")]
        public Color editorColor;

        [Tooltip("Fixed table or plinth. Runtime keeps the body kinematic so it does not tip over.")]
        public bool isKinematicBase;

        [Tooltip("Rigidbody mass used when the block is spawned.")]
        public float mass;

        [Tooltip("Smash Fast material. Tables are not targets. Glass breaks on impact. Wood and stone break when they leave the table.")]
        public BlockMaterialType materialType;

        [Tooltip("Architectural role used by presets and stacking.")]
        public BlockShapeRole shapeRole;

        [Tooltip("Top ornaments cannot hold another block. Every other role can.")]
        public bool canSupportBlocksAbove;

        [Tooltip("Relative speed required for glass and ceramic to shatter on a non-projectile hit.")]
        public float impactBreakThreshold;

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
