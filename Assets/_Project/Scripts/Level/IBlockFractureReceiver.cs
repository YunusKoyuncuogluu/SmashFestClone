using UnityEngine;

namespace SmashFast.Level
{
    public interface IBlockFractureReceiver
    {
        void OnImpactReceived(Vector3 hitPoint, Vector3 hitVelocity, float impactForce);

        void OnBlockBroken(Vector3 breakPoint, Vector3 breakVelocity);
    }
}
