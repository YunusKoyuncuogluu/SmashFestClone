using System;
using UnityEngine;

namespace SmashFast.Core.Pooling
{
    [Serializable]
    public struct PoolItemConfig
    {
        public GameObject prefab;

        public int initialSize;
    }
}
