using System;
using UnityEngine;

namespace SmashFast.Data
{
    [Serializable]
    public struct BlockPlacementData
    {
        [Tooltip("BlockDefinition.blockId from the active catalog.")]
        public int blockId;

        [Tooltip("Anchor cell at the minimum X, Y, and Z of the footprint.")]
        public Vector3Int gridPosition;

        [Tooltip("Euler rotation in degrees. Yaw supports free angles such as -45, -30, 0, 30, 45, and 90.")]
        public Vector3 rotationEuler;
    }
}
