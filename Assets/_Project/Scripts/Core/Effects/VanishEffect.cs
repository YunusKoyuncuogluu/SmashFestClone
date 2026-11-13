using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace SmashFest.Core.Effects
{
    public class VanishEffect
    {
        private const float MinScale = 0.02f;
        private const float FadingShrink = 0.5f;

        private readonly int baseColorId = Shader.PropertyToID("_BaseColor");
        private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        private readonly Transform[] targets;
        private readonly Vector3[] baseScales;
        private readonly Renderer[] renderers;
        private readonly Color[][] baseColors;

        private bool isPlaying;
        private int playId;

        public VanishEffect(Transform[] targets, Renderer[] fadeRenderers = null)
        {
            this.targets = targets;
            baseScales = new Vector3[targets.Length];
            renderers = fadeRenderers ?? Array.Empty<Renderer>();
            baseColors = new Color[renderers.Length][];
            for (int i = 0; i < renderers.Length; i++)
                baseColors[i] = ReadColors(renderers[i]);
        }

        public async UniTask<bool> PlayAsync(float duration, CancellationToken token)
        {
            Begin();
            int id = playId;
            if (duration <= 0f)
            {
                Apply(1f);
                return false;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                bool canceled = await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
                if (canceled || id != playId || !isPlaying)
                    return true;

                elapsed += Time.deltaTime;
                Apply(Mathf.Clamp01(elapsed / duration));
            }

            Apply(1f);
            return false;
        }

        public void Restore()
        {
            if (!isPlaying)
                return;

            isPlaying = false;
            playId++;
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] != null)
                    targets[i].localScale = baseScales[i];
            }

            foreach (Renderer target in renderers)
            {
                for (int j = 0; j < target.sharedMaterials.Length; j++)
                    target.SetPropertyBlock(null, j);
            }
        }

        private void Begin()
        {
            isPlaying = true;
            playId++;
            for (int i = 0; i < targets.Length; i++)
                baseScales[i] = targets[i].localScale;
        }

        private void Apply(float t)
        {
            float eased = t * t * (3f - 2f * t);
            float shrink = renderers.Length > 0 ? FadingShrink : 1f;
            float scale = Mathf.Max(1f - eased * shrink, MinScale);
            for (int i = 0; i < targets.Length; i++)
                targets[i].localScale = baseScales[i] * scale;

            float alpha = 1f - t;
            for (int i = 0; i < renderers.Length; i++)
            {
                Color[] colors = baseColors[i];
                for (int j = 0; j < colors.Length; j++)
                {
                    Color color = colors[j];
                    color.a *= alpha;
                    block.SetColor(baseColorId, color);
                    renderers[i].SetPropertyBlock(block, j);
                }
            }
        }

        private Color[] ReadColors(Renderer target)
        {
            Material[] materials = target.sharedMaterials;
            Color[] colors = new Color[materials.Length];
            for (int i = 0; i < materials.Length; i++)
                colors[i] = materials[i] != null && materials[i].HasProperty(baseColorId) ? materials[i].GetColor(baseColorId) : Color.white;

            return colors;
        }
    }
}
