using System;
using System.Collections.Generic;

namespace SmashFest.Shop
{
    public enum SkinState
    {
        Locked,
        Owned,
        Selected
    }

    public interface IBallSkinShop
    {
        event Action Changed;

        IReadOnlyList<BallSkin> Skins { get; }

        BallSkin Selected { get; }

        SkinState GetState(BallSkin skin);

        bool CanAfford(BallSkin skin);

        bool TryBuy(BallSkin skin);

        void Select(BallSkin skin);
    }
}
