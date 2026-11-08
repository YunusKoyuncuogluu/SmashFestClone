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

        private RectTransform rectTransform;
        private MainMenuPanel owner;
        private int levelNumber;
        private bool unlocked;
        private bool selected;

        public RectTransform RectTransform
        {
            get
            {
                if (rectTransform == null)
                {
                    rectTransform = (RectTransform)transform;
                }

                return rectTransform;
            }
        }

        public void Configure(MainMenuPanel menu)
        {
            owner = menu;
        }

        public void Apply(int number, bool isUnlocked, bool isSelected)
        {
            if (number == levelNumber && isUnlocked == unlocked && isSelected == selected)
            {
                return;
            }

            levelNumber = number;
            unlocked = isUnlocked;
            selected = isSelected;
            if (label != null)
            {
                label.SetText(LabelFormat, number);
            }

            if (button != null)
            {
                button.interactable = isUnlocked;
            }

            if (background == null)
            {
                return;
            }

            if (!isUnlocked)
            {
                background.color = lockedColor;
                return;
            }

            if (isSelected)
            {
                background.color = selectedColor;
                return;
            }

            background.color = unlockedColor;
        }

        private void Awake()
        {
            rectTransform = (RectTransform)transform;
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (button == null)
            {
                return;
            }

            button.onClick.AddListener(HandleClick);
        }

        private void HandleClick()
        {
            if (!unlocked || owner == null)
            {
                return;
            }

            owner.SelectLevel(levelNumber);
        }
    }
}
