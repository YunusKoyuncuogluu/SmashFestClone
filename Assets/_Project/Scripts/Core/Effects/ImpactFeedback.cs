using SmashFest.Audio;
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
        private readonly IAudioService audio;

        public ImpactFeedback(BreakParticles particles, [InjectOptional] IAudioService audio)
        {
            this.particles = particles;
            this.audio = audio;
        }

        public void Prewarm(Material visual) => colors.GetColor(visual);

        public void OnBlockHit(BlockMaterialType material, float speed) => audio?.PlayHit(material, Strength(speed));

        public void OnBallThud(float speed) => audio?.PlayBallThud(Strength(speed));

        public void OnBlockBreak(BlockMaterialType material, Bounds bounds, Material visual, Vector3 push)
        {
            audio?.PlayBreak(material);
            particles.PlayBreak(material, bounds, colors.GetColor(visual), push);
        }

        private float Strength(float speed) => Mathf.Clamp01(speed / StrongImpactSpeed);
    }
}