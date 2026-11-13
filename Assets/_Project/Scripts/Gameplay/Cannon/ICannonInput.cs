using UnityEngine;

namespace SmashFest.Gameplay.Cannon
{
    public interface ICannonInput
    {
        bool TryGetShot(out Vector2 screenPosition);
    }
}
