using SmashFest.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SmashFest.UI.Panels
{
    public class SkinShopItem : MonoBehaviour
    {
        private const string PriceFormat = "{0:0}";
        private const string UseText = "USE";
        private const string InUseText = "IN USE";

        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private Button actionButton;
        [SerializeField] private Image actionBackground;
        [SerializeField] private TMP_Text actionLabel;
        [SerializeField] private GameObject priceIcon;
        [SerializeField] private Image frame;
        [SerializeField] private Sprite buySprite;
        [SerializeField] private Sprite useSprite;
        [SerializeField] private Sprite inUseSprite;
        [SerializeField] private Color lockedIconColor = new Color(0.55f, 0.55f, 0.6f, 1f);
        [SerializeField] private Color selectedFrameColor = new Color(1f, 0.82f, 0.3f, 1f);

        private ShopPopup owner;
        private BallSkin skin;
        private Color frameColor;

        private void Awake()
        {
            if (frame != null)
                frameColor = frame.color;

            actionButton.onClick.AddListener(OnAction);
        }

        public void Configure(ShopPopup owner) => this.owner = owner;

        public void Bind(BallSkin skin, SkinState state, bool canAfford)
        {
            this.skin = skin;
            icon.sprite = skin.Icon;
            icon.color = state == SkinState.Locked ? lockedIconColor : Color.white;
            nameLabel.SetText(skin.DisplayName);
            priceIcon.SetActive(state == SkinState.Locked);

            if (frame != null)
                frame.color = state == SkinState.Selected ? selectedFrameColor : frameColor;

            switch (state)
            {
                case SkinState.Locked:
                    actionLabel.SetText(PriceFormat, skin.Price);
                    actionBackground.sprite = buySprite;
                    actionButton.interactable = canAfford;
                    break;
                case SkinState.Owned:
                    actionLabel.SetText(UseText);
                    actionBackground.sprite = useSprite;
                    actionButton.interactable = true;
                    break;
                default:
                    actionLabel.SetText(InUseText);
                    actionBackground.sprite = inUseSprite;
                    actionButton.interactable = false;
                    break;
            }
        }

        private void OnAction()
        {
            if (skin != null && owner != null)
                owner.HandleAction(skin);
        }
    }
}
