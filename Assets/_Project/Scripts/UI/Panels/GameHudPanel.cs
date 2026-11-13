using SmashFest.Scenes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace SmashFest.UI.Panels
{
    public class GameHudPanel : UIPanelBase
    {
        private const string LevelFormat = "Level {0:0}";
        private const string AmmoFormat = "{0:0}/{1:0}";

        [SerializeField] private TMP_Text levelLabel;
        [SerializeField] private TMP_Text ammoLabel;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button restartButton;

        private IUIManager ui;
        private GameSessionSlot session;
        private int shownLevel = -1;
        private int shownAmmo = -1;
        private int shownMaxAmmo = -1;

        [Inject]
        public void Construct(IUIManager ui, GameSessionSlot session)
        {
            this.ui = ui;
            this.session = session;
        }

        private void Awake()
        {
            if (settingsButton != null)
                settingsButton.onClick.AddListener(OpenSettings);

            if (restartButton != null)
                restartButton.onClick.AddListener(Restart);
        }

        public void SetLevelNumber(int level)
        {
            if (level == shownLevel)
                return;

            shownLevel = level;
            if (levelLabel != null)
                levelLabel.SetText(LevelFormat, level);
        }

        public void SetAmmo(int ammo, int maxAmmo)
        {
            if (ammo == shownAmmo && maxAmmo == shownMaxAmmo)
                return;

            shownAmmo = ammo;
            shownMaxAmmo = maxAmmo;
            if (ammoLabel != null)
                ammoLabel.SetText(AmmoFormat, ammo, maxAmmo);
        }

        protected override void OnPanelOpened()
        {
            IGameSceneSession current = session.Current;
            if (current == null)
                return;

            SetLevelNumber(current.LevelNumber);
            SetAmmo(current.RemainingAmmo, current.MaxAmmo);
        }

        private void OpenSettings() => ui.ShowPopup<SettingsPopup>();

        private void Restart() => session.Current?.RetryLevel();
    }
}
