using System.Collections.Generic;
using UnityEngine;

namespace SmashFest.Shop
{
    [CreateAssetMenu(fileName = "BallSkinCatalog", menuName = "SmashFest/Ball Skin Catalog", order = 4)]
    public class BallSkinCatalogSO : ScriptableObject
    {
        [SerializeField] private BallSkin[] skins;

        public IReadOnlyList<BallSkin> Skins => skins;

        public BallSkin Default => skins.Length > 0 ? skins[0] : null;

        public BallSkin Find(string id)
        {
            for (int i = 0; i < skins.Length; i++)
            {
                if (skins[i].Id == id)
                    return skins[i];
            }

            return Default;
        }
    }
}
