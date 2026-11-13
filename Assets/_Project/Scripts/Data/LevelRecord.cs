using System;
using System.Collections.Generic;
using UnityEngine;

namespace SmashFest.Data
{
    [Serializable]
    public class LevelRecord
    {
        public int levelIndex = 1;
        public int maxAmmo = 10;
        public int parAmmo;
        public Vector3Int gridDimensions = new Vector3Int(16, 14, 6);
        public Vector3 cellSize = Vector3.one;
        public Vector3 originOffset = Vector3.zero;
        public float fallEliminationY = -1.5f;
        public List<BlockPlacementData> placements = new List<BlockPlacementData>(32);
    }

    [Serializable]
    public class LevelCollectionData
    {
        public List<LevelRecord> levels = new List<LevelRecord>(1000);
    }
}
