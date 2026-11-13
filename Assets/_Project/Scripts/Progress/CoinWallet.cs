using System;

namespace SmashFest.Progress
{
    public class CoinWallet : ICoinWallet
    {
        private readonly IPlayerDataProvider provider;

        public CoinWallet(IPlayerDataProvider provider) => this.provider = provider;

        public event Action<int> CoinsChanged;

        public int Coins => provider.Data.coins;

        public void Add(int amount)
        {
            if (amount <= 0)
                return;

            SetCoins(Coins + amount);
        }

        public bool TrySpend(int amount)
        {
            if (amount < 0 || Coins < amount)
                return false;

            SetCoins(Coins - amount);
            return true;
        }

        private void SetCoins(int coins)
        {
            provider.Data.coins = coins;
            provider.Save();
            CoinsChanged?.Invoke(coins);
        }
    }
}
