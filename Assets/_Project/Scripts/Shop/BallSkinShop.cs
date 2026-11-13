using System;
using System.Collections.Generic;
using SmashFest.Progress;

namespace SmashFest.Shop
{
    public class BallSkinShop : IBallSkinShop
    {
        private readonly BallSkinCatalogSO catalog;
        private readonly ISkinInventory inventory;
        private readonly ICoinWallet wallet;

        public BallSkinShop(BallSkinCatalogSO catalog, ISkinInventory inventory, ICoinWallet wallet)
        {
            this.catalog = catalog;
            this.inventory = inventory;
            this.wallet = wallet;
        }

        public event Action Changed;

        public IReadOnlyList<BallSkin> Skins => catalog.Skins;

        public BallSkin Selected
        {
            get
            {
                BallSkin skin = catalog.Find(inventory.SelectedSkinId);
                return skin != null && IsOwned(skin) ? skin : catalog.Default;
            }
        }

        public SkinState GetState(BallSkin skin)
        {
            if (skin == Selected)
                return SkinState.Selected;

            return IsOwned(skin) ? SkinState.Owned : SkinState.Locked;
        }

        public bool CanAfford(BallSkin skin) => wallet.Coins >= skin.Price;

        public bool TryBuy(BallSkin skin)
        {
            if (IsOwned(skin) || !wallet.TrySpend(skin.Price))
                return false;

            inventory.Unlock(skin.Id);
            Select(skin);
            return true;
        }

        public void Select(BallSkin skin)
        {
            if (!IsOwned(skin) || skin == Selected)
                return;

            inventory.Select(skin.Id);
            Changed?.Invoke();
        }

        private bool IsOwned(BallSkin skin) => skin.IsFree || inventory.IsOwned(skin.Id);
    }
}
