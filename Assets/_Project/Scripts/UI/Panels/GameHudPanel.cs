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
        private const string UnlimitedAmmoText = "FREE";
        private const float ScalePerCombo = 0.04f;
        private const float MaxComboScale = 1.2f;

        private readonly string[] comboFormats = { "Nice! x{0:0}", "Great! x{0:0}", "Awesome! x{0:0}", "Perfect! x{0:0}" };
        private readonly Color[] comboColors =
        {
            new Color(1f, 0.95f, 0.55f),
            new Color(1f, 0.7f, 0.2f),
            new Color(1f, 0.42f, 0.55f),
            new Color(0.45f, 0.95f, 1f)
        };

        [SerializeField] private TMP_Text levelLabel;
        [SerializeField] private TMP_Text ammoLabel;
        [SerializeField] private TMP_Text comboLabel;
        [SerializeField] private TMP_Text introLabel;
        [SerializeField] private GameObject tutorialRoot;
        [SerializeField] private Image[] tutorialRipples;
        [SerializeField] private TMP_Text tutorialLabel;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button restartButton;

        private IUIManager ui;
        private GameSessionSlot session;
        private ComboPopup comboPopup;
        private LevelIntroBanner introBanner;
        private TapTutorial tapTutorial;
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

            if (comboLabel != null)
                comboPopup = new ComboPopup(comboLabel);

            if (introLabel != null)
                introBanner = new LevelIntroBanner(introLabel);

            if (tutorialRoot != null)
                tapTutorial = new TapTutorial(tutorialRoot, tutorialRipples, tutorialLabel);
        }

        public void SetLevelNumber(int level)
        {
            if (level == shownLevel)
                return;

            shownLevel = level;
            if (levelLabel != null)
                levelLabel.SetText(LevelFormat, level);
        }

        public void SetUnlimitedAmmo()
        {
            shownAmmo = -1;
            shownMaxAmmo = -1;
            if (ammoLabel != null)
                ammoLabel.SetText(UnlimitedAmmoText);
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

        public void ShowCombo(int count)
        {
            if (comboPopup == null)
                return;

            int tier = Mathf.Clamp(count - 2, 0, comboFormats.Length - 1);
            float scale = Mathf.Min(1f + (count - 2) * ScalePerCombo, MaxComboScale);
            comboPopup.Play(comboFormats[tier], count, comboColors[tier], scale);
        }

        public void PlayIntro(int level, bool isBonus) => introBanner?.Play(level, isBonus);

        public void ShowTutorial(Vector2 screenPoint)
        {
            if (tapTutorial == null)
                return;

            RectTransform area = (RectTransform)tutorialRoot.transform.parent;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screenPoint, null, out Vector2 localPoint))
                tapTutorial.Show(localPoint);
        }

        public void HideTutorial() => tapTutorial?.Hide();

        public void HideCombo() => comboPopup?.Hide();

        protected override void OnPanelOpened()
        {
            HideCombo();
            IGameSceneSession current = session.Current;
            if (current == null)
                return;

            SetLevelNumber(current.LevelNumber);
            if (current.HasUnlimitedAmmo)
                SetUnlimitedAmmo();
            else
                SetAmmo(current.RemainingAmmo, current.MaxAmmo);
        }


        private void OpenSettings() => ui.ShowPopup<SettingsPopup>();

        private void Restart() => session.Current?.RetryLevel();
    }
}
