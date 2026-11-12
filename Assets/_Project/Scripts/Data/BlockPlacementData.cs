using System;
using UnityEngine;

namespace SmashFast.Data
{
    [Serializable]
    public struct BlockPlacementData
    {
        public int blockId;

        public Vector3Int gridPosition;

        public Vector3 rotationEuler;

        public Vector3Int customSize;
    }
}
