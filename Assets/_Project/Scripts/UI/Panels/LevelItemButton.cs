using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SmashFest.UI.Panels
{
    public class LevelItemButton : MonoBehaviour
    {
        private const string LabelFormat = "{0:0}";

        [SerializeField] private Button button;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Image background;
        [SerializeField] private Image statusIcon;
        [SerializeField] private Sprite unlockedSprite;
        [SerializeField] private Sprite selectedSprite;
        [SerializeField] private Sprite lockedSprite;
        [SerializeField] private Sprite unlockedIcon;
        [SerializeField] private Sprite selectedIcon;
        [SerializeField] private Sprite lockedIcon;
        [SerializeField] private Color lockedLabelColor = new Color(0.49f, 0.48f, 0.66f, 1f);

        private RectTransform rect;
        private MainMenuPanel menu;
        private int level;
        private bool isUnlocked;
        private bool isSelected;
        private bool hasApplied;

        public RectTransform Rect
        {
            get
            {
                if (rect == null)
                    rect = (RectTransform)transform;

                return rect;
            }
        }

        private void Awake()
        {
            if (button == null && !TryGetComponent(out button))
                return;

            button.onClick.AddListener(OnClick);
        }

        public void Configure(MainMenuPanel menu) => this.menu = menu;

        public void Apply(int level, bool isUnlocked, bool isSelected)
        {
            if (hasApplied && level == this.level && isUnlocked == this.isUnlocked && isSelected == this.isSelected)
                return;

            hasApplied = true;
            this.level = level;
            this.isUnlocked = isUnlocked;
            this.isSelected = isSelected;

            if (label != null)
            {
                label.SetText(LabelFormat, level);
                label.color = isUnlocked ? Color.white : lockedLabelColor;
            }

            if (button != null)
                button.interactable = isUnlocked;

            if (background != null)
                background.sprite = !isUnlocked ? lockedSprite : isSelected ? selectedSprite : unlockedSprite;

            if (statusIcon != null)
            {
                statusIcon.sprite = !isUnlocked ? lockedIcon : isSelected ? selectedIcon : unlockedIcon;
                statusIcon.color = isUnlocked ? Color.white : lockedLabelColor;
            }
        }

        private void OnClick()
        {
            if (isUnlocked && menu != null)
                menu.SelectLevel(level);
        }
    }
}
