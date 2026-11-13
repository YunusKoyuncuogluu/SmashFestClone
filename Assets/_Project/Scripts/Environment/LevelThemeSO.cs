using UnityEngine;

namespace SmashFest.Environment
{
    [CreateAssetMenu(fileName = "LevelTheme", menuName = "SmashFest/Level Theme", order = 5)]
    public class LevelThemeSO : ScriptableObject
    {
        [SerializeField] private Material skybox;
        [SerializeField] private Color fogColor = Color.white;
        [SerializeField] private bool showSnowfall = true;
        [SerializeField] private bool showGifts = true;
        [SerializeField] private Material sceneryMaterial;

        public Material Skybox => skybox;
        public Color FogColor => fogColor;
        public bool ShowSnowfall => showSnowfall;
        public bool ShowGifts => showGifts;
        public Material SceneryMaterial => sceneryMaterial;
    }
}
