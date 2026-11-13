using Lofelt.NiceVibrations;
using SmashFest.Progress;
using UnityEngine;
using Zenject;

namespace SmashFest.Core.Feel
{
    public class HapticService : IHapticService, IInitializable
    {
        private const float MinInterval = 0.1f;

        private readonly HapticPatterns.PresetType[] presets =
        {
            HapticPatterns.PresetType.LightImpact,
            HapticPatterns.PresetType.MediumImpact,
            HapticPatterns.PresetType.HeavyImpact,
            HapticPatterns.PresetType.Success,
            HapticPatterns.PresetType.Failure,
            HapticPatterns.PresetType.SoftImpact,
            HapticPatterns.PresetType.RigidImpact
        };

        private readonly IPlayerProgressService progress;
        private float nextAllowedTime;

        public HapticService(IPlayerProgressService progress) => this.progress = progress;

        public void Initialize() => HapticController.Init();

        public void Play(HapticPulse pulse)
        {
            if (!progress.HapticsEnabled || Time.unscaledTime < nextAllowedTime)
                return;

            nextAllowedTime = Time.unscaledTime + MinInterval;
            HapticPatterns.PlayPreset(presets[(int)pulse]);
        }
    }
}
