using System;
using UnityEngine;

namespace SmashFast.Core.Pooling
{
    [Serializable]
    public struct PoolItemConfig
    {
        [Tooltip("Any prefab. Blocks, projectiles, debris, and VFX all use the same pool.")]
        public GameObject prefab;

        [Tooltip("How many instances to create before gameplay starts.")]
        public int initialSize;
    }
}
