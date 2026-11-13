using SmashFest.Data;
using UnityEngine;

namespace SmashFest.Core.Effects
{
    public interface IImpactFeedback
    {
        void Prewarm(Material visual);

        void OnBlockHit(BlockMaterialType material, float speed);

        void OnBlockBreak(BlockMaterialType material, Bounds bounds, Material visual, Vector3 push);

        void OnBallThud(float speed);
    }
}
