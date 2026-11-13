using UnityEngine;

namespace SmashFest.Environment
{
    [CreateAssetMenu(fileName = "LevelThemeCatalog", menuName = "SmashFest/Level Theme Catalog", order = 6)]
    public class LevelThemeCatalogSO : ScriptableObject
    {
        [SerializeField] private LevelThemeSO[] themes;
        [SerializeField, Min(1)] private int levelsPerTheme = 50;

        public LevelThemeSO Resolve(int level)
        {
            if (themes == null || themes.Length == 0)
                return null;

            int index = Mathf.Max(0, level - 1) / levelsPerTheme;
            return themes[index % themes.Length];
        }
    }
}
