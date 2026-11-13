using SmashFest.Progress;
using SmashFest.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace SmashFest.UI.Panels
{
    public class ShopPopup : UIPanelBase
    {
        private const string CoinFormat = "{0:0}";

        [SerializeField] private SkinShopItem[] items;
        [SerializeField] private TMP_Text coinLabel;
        [SerializeField] private Button closeButton;

        private IUIManager ui;
        private IBallSkinShop shop;
        private ICoinWallet wallet;

        [Inject]
        public void Construct(IUIManager ui, IBallSkinShop shop, ICoinWallet wallet)
        {
            this.ui = ui;
            this.shop = shop;
            this.wallet = wallet;
        }

        private void Awake()
        {
            for (int i = 0; i < items.Length; i++)
                items[i].Configure(this);

            if (closeButton != null)
                closeButton.onClick.AddListener(Close);
        }

        public void HandleAction(BallSkin skin)
        {
            if (shop.GetState(skin) == SkinState.Locked)
                shop.TryBuy(skin);
            else
                shop.Select(skin);

            Refresh();
        }

        protected override void OnPanelOpened() => Refresh();

        private void Refresh()
        {
            if (coinLabel != null)
                coinLabel.SetText(CoinFormat, wallet.Coins);

            int count = Mathf.Min(items.Length, shop.Skins.Count);
            for (int i = 0; i < items.Length; i++)
            {
                bool hasSkin = i < count;
                items[i].gameObject.SetActive(hasSkin);
                if (!hasSkin)
                    continue;

                BallSkin skin = shop.Skins[i];
                items[i].Bind(skin, shop.GetState(skin), shop.CanAfford(skin));
            }
        }

        private void Close() => ui.HidePopup<ShopPopup>();
    }
}
