using UnityEngine;

namespace SmashFest.UI
{
    public class ConfettiBurst : MonoBehaviour
    {
        [SerializeField] private ParticleSystem[] bursts;

        public void Play()
        {
            for (int i = 0; i < bursts.Length; i++)
            {
                bursts[i].Clear(true);
                bursts[i].Play(true);
            }
        }

        public void Stop()
        {
            for (int i = 0; i < bursts.Length; i++)
                bursts[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
