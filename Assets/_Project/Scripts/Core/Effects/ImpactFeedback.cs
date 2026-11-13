using SmashFest.Audio;
using SmashFest.Core.Feel;
using SmashFest.Data;
using UnityEngine;
using Zenject;

namespace SmashFest.Core.Effects
{
    public class ImpactFeedback : IImpactFeedback
    {
        private const float StrongImpactSpeed = 12f;

        private readonly BreakParticles particles;
        private readonly MaterialColorSampler colors = new MaterialColorSampler();
        private readonly ComboCounter combo;
        private readonly IAudioService audio;
        private readonly IHapticService haptics;

        public ImpactFeedback(
            BreakParticles particles,
            ComboCounter combo,
            [InjectOptional] IAudioService audio,
            [InjectOptional] IHapticService haptics)
        {
            this.particles = particles;
            this.combo = combo;
            this.audio = audio;
            this.haptics = haptics;
        }

        public void Prewarm(Material visual) => colors.GetColor(visual);

        public void OnBlockHit(BlockMaterialType material, float speed) => audio?.PlayHit(material, Strength(speed));

        public void OnBallHitBlock(BlockMaterialType material, float speed)
        {
            audio?.PlayHit(material, Strength(speed));
            haptics?.Play(HapticPulse.Medium);
        }

        public void OnCannonFire() => haptics?.Play(HapticPulse.Rigid);

        public void OnBallThud(float speed) => audio?.PlayBallThud(Strength(speed));

        public void OnBlockBreak(BlockMaterialType material, Bounds bounds, Material visual, Vector3 push)
        {
            audio?.PlayBreak(material);
            particles.PlayBreak(material, bounds, colors.GetColor(visual), push);
            haptics?.Play(HapticPulse.Soft);
            combo.RegisterBreak();
        }

        private float Strength(float speed) => Mathf.Clamp01(speed / StrongImpactSpeed);
    }
}
