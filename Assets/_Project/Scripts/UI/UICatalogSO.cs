using SmashFest.UI.Panels;
using UnityEngine;

namespace SmashFest.UI
{
    [CreateAssetMenu(fileName = "UICatalog", menuName = "SmashFest/UI Catalog", order = 3)]
    public class UICatalogSO : ScriptableObject
    {
        [SerializeField] private GameObject canvasPrefab;
        [SerializeField] private MainMenuPanel mainMenuPanelPrefab;
        [SerializeField] private GameHudPanel gameHudPanelPrefab;
        [SerializeField] private SuccessPanel successPanelPrefab;
        [SerializeField] private FailPanel failPanelPrefab;
        [SerializeField] private SettingsPopup settingsPopupPrefab;
        [SerializeField] private ShopPopup shopPopupPrefab;
        [SerializeField] private SplashPanel splashPanelPrefab;

        public GameObject CanvasPrefab
        {
            get
            {
                return canvasPrefab;
            }
        }

        public MainMenuPanel MainMenuPanelPrefab
        {
            get
            {
                return mainMenuPanelPrefab;
            }
        }

        public GameHudPanel GameHudPanelPrefab
        {
            get
            {
                return gameHudPanelPrefab;
            }
        }

        public SuccessPanel SuccessPanelPrefab
        {
            get
            {
                return successPanelPrefab;
            }
        }

        public FailPanel FailPanelPrefab
        {
            get
            {
                return failPanelPrefab;
            }
        }

        public ShopPopup ShopPopupPrefab => shopPopupPrefab;

        public SplashPanel SplashPanelPrefab => splashPanelPrefab;

        public SettingsPopup SettingsPopupPrefab
        {
            get
            {
                return settingsPopupPrefab;
            }
        }
    }
}
