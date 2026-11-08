using SmashFast.Scenes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace SmashFast.UI.Panels
{
    public class GameHudPanel : UIPanelBase
    {
        private const string LevelFormat = "LEVEL {0:0}";
        private const string AmmoFormat = "Ammo: {0:0}/{1:0}";

        [SerializeField] private TMP_Text levelLabel;
        [SerializeField] private TMP_Text ammoLabel;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button restartButton;

        private UIManager uiManager;
        private GameSessionSlot sessionSlot;
        private int shownLevel = -1;
        private int shownRemaining = -1;
        private int shownMax = -1;

        [Inject]
        public void Construct(UIManager uiManager, GameSessionSlot sessionSlot)
        {
            this.uiManager = uiManager;
            this.sessionSlot = sessionSlot;
        }

        public void SetLevelNumber(int levelNumber)
        {
            if (levelNumber == shownLevel)
            {
                return;
            }

            shownLevel = levelNumber;
            if (levelLabel == null)
            {
                return;
            }

            levelLabel.SetText(LevelFormat, levelNumber);
        }

        public void SetAmmo(int remaining, int max)
        {
            if (remaining == shownRemaining && max == shownMax)
            {
                return;
            }

            shownRemaining = remaining;
            shownMax = max;
            if (ammoLabel == null)
            {
                return;
            }

            ammoLabel.SetText(AmmoFormat, remaining, max);
        }

        protected override void OnPanelOpened()
        {
            RefreshFromSession();
        }

        private void Awake()
        {
            if (settingsButton != null)
            {
                settingsButton.onClick.AddListener(HandleSettings);
            }

            if (restartButton != null)
            {
                restartButton.onClick.AddListener(HandleRestart);
            }
        }

        private void RefreshFromSession()
        {
            if (sessionSlot == null || sessionSlot.Current == null)
            {
                return;
            }

            IGameSceneSession session = sessionSlot.Current;
            SetLevelNumber(session.LevelNumber);
            SetAmmo(session.RemainingAmmo, session.MaxAmmo);
        }

        private void HandleSettings()
        {
            if (uiManager == null)
            {
                return;
            }

            uiManager.ShowPopup<SettingsPopup>();
        }

        private void HandleRestart()
        {
            if (sessionSlot == null || sessionSlot.Current == null)
            {
                return;
            }

            sessionSlot.Current.RetryLevel();
        }
    }
}
