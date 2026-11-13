using System;
using UnityEngine;

namespace SmashFest.Core.Pooling
{
    [Serializable]
    public struct PoolItemConfig
    {
        public GameObject prefab;

        public int initialSize;
    }
}
