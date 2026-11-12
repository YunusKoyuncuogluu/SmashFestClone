using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SmashFast.UI.Panels
{
    public class LevelItemButton : MonoBehaviour
    {
        private const string LabelFormat = "{0:0}";

        [SerializeField] private Button button;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Image background;
        [SerializeField] private Color unlockedColor = new Color(0.18f, 0.42f, 0.78f, 1f);
        [SerializeField] private Color lockedColor = new Color(0.22f, 0.22f, 0.24f, 1f);
        [SerializeField] private Color selectedColor = new Color(0.93f, 0.7f, 0.16f, 1f);

        private RectTransform rect;
        private MainMenuPanel menu;
        private int level;
        private bool isUnlocked;
        private bool isSelected;

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
            if (level == this.level && isUnlocked == this.isUnlocked && isSelected == this.isSelected)
                return;

            this.level = level;
            this.isUnlocked = isUnlocked;
            this.isSelected = isSelected;

            if (label != null)
                label.SetText(LabelFormat, level);

            if (button != null)
                button.interactable = isUnlocked;

            if (background != null)
                background.color = !isUnlocked ? lockedColor : isSelected ? selectedColor : unlockedColor;
        }

        private void OnClick()
        {
            if (isUnlocked && menu != null)
                menu.SelectLevel(level);
        }
    }
}
