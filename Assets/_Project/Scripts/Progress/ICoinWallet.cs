using System;

namespace SmashFest.Progress
{
    public interface ICoinWallet
    {
        event Action<int> CoinsChanged;

        int Coins { get; }

        void Add(int amount);

        bool TrySpend(int amount);
    }
}
