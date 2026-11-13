using System.Collections.Generic;
using UnityEngine;

namespace SmashFest.Core.Pooling
{
    [CreateAssetMenu(fileName = "PoolConfig", menuName = "SmashFest/Pool Config", order = 2)]
    public class PoolConfigSO : ScriptableObject
    {
        [SerializeField] private List<PoolItemConfig> customPools = new List<PoolItemConfig>(8);

        [SerializeField, Min(0)] private int catalogPrewarmCount = 4;

        public List<PoolItemConfig> CustomPools
        {
            get
            {
                return customPools;
            }
        }

        public int CatalogPrewarmCount
        {
            get
            {
                if (catalogPrewarmCount < 0)
                    return 0;

                return catalogPrewarmCount;
            }
        }
    }
}
