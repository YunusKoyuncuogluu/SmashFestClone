using UnityEngine;

namespace SmashFest.Environment
{
    public class EnvironmentThemer : MonoBehaviour
    {
        [SerializeField] private Renderer[] renderers;
        [SerializeField] private GameObject snowfall;
        [SerializeField] private GameObject gifts;

        private LevelThemeSO current;

        public void Apply(LevelThemeSO theme)
        {
            if (theme == null || theme == current)
                return;

            current = theme;
            if (theme.SceneryMaterial != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                    renderers[i].sharedMaterial = theme.SceneryMaterial;
            }

            if (theme.Skybox != null)
                RenderSettings.skybox = theme.Skybox;

            RenderSettings.fogColor = theme.FogColor;

            if (snowfall != null)
                snowfall.SetActive(theme.ShowSnowfall);

            if (gifts != null)
                gifts.SetActive(theme.ShowGifts);
        }
    }
}
