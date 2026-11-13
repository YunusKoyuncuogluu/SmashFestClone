using System;
using UnityEngine;

namespace SmashFest.Shop
{
    [Serializable]
    public class BallSkin
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField, Min(0)] private int price;
        [SerializeField] private Material material;
        [SerializeField] private Material fadeMaterial;
        [SerializeField] private Sprite icon;

        public string Id => id;
        public string DisplayName => displayName;
        public int Price => price;
        public Material Material => material;
        public Material FadeMaterial => fadeMaterial;
        public Sprite Icon => icon;
        public bool IsFree => price <= 0;
    }
}
