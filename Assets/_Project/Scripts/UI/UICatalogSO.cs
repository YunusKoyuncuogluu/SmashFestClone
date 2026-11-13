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

        public SettingsPopup SettingsPopupPrefab
        {
            get
            {
                return settingsPopupPrefab;
            }
        }
    }
}
