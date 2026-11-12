using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace SmashFast.Core.Effects
{
    public class ShrinkFadeEffect
    {
        private const float MinScale = 0.02f;

        private readonly int baseColorId = Shader.PropertyToID("_BaseColor");
        private readonly Transform[] targets;
        private readonly Renderer[] renderers;
        private readonly Vector3[] baseScales;
        private readonly Material[][] sourceMaterials;
        private readonly Material[][] fadeMaterials;
        private readonly Color[][] baseColors;
        private readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();

        private bool isPrepared;
        private bool useFadeMaterials;
        private bool isFading;
        private int playId;

        public ShrinkFadeEffect(Transform[] targets, Renderer[] renderers)
        {
            this.targets = targets;
            this.renderers = renderers;
            baseScales = new Vector3[targets.Length];
            sourceMaterials = new Material[renderers.Length][];
            fadeMaterials = new Material[renderers.Length][];
            baseColors = new Color[renderers.Length][];
        }

        public void Prepare(FadeMaterialLibrary library)
        {
            if (isPrepared)
                return;

            isPrepared = true;
            useFadeMaterials = library != null;
            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] shared = renderers[i].sharedMaterials;
                Material[] fades = new Material[shared.Length];
                Color[] colors = new Color[shared.Length];
                for (int j = 0; j < shared.Length; j++)
                {
                    Material source = shared[j];
                    colors[j] = source != null && source.HasProperty(baseColorId) ? source.GetColor(baseColorId) : Color.white;
                    fades[j] = library != null ? library.Resolve(source) : source;
                }

                sourceMaterials[i] = shared;
                fadeMaterials[i] = fades;
                baseColors[i] = colors;
            }
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
                if (canceled || id != playId || !isFading)
                    return true;

                elapsed += Time.deltaTime;
                Apply(Mathf.Clamp01(elapsed / duration));
            }

            Apply(1f);
            return false;
        }

        public void Restore()
        {
            if (!isFading)
                return;

            isFading = false;
            playId++;
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] != null)
                    targets[i].localScale = baseScales[i];
            }

            if (!isPrepared || !useFadeMaterials)
                return;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer meshRenderer = renderers[i];
                if (meshRenderer == null)
                    continue;

                meshRenderer.sharedMaterials = sourceMaterials[i];
                for (int j = 0; j < sourceMaterials[i].Length; j++)
                    meshRenderer.SetPropertyBlock(null, j);
            }
        }

        private void Begin()
        {
            if (!isPrepared)
                Prepare(null);

            isFading = true;
            playId++;
            for (int i = 0; i < targets.Length; i++)
                baseScales[i] = targets[i].localScale;

            if (!useFadeMaterials)
                return;

            for (int i = 0; i < renderers.Length; i++)
                renderers[i].sharedMaterials = fadeMaterials[i];
        }

        private void Apply(float t)
        {
            float eased = t * t * (3f - 2f * t);
            float remaining = 1f - eased;
            for (int i = 0; i < targets.Length; i++)
                targets[i].localScale = baseScales[i] * Mathf.Max(remaining, MinScale);

            if (!useFadeMaterials)
                return;

            for (int i = 0; i < renderers.Length; i++)
            {
                Color[] colors = baseColors[i];
                for (int j = 0; j < colors.Length; j++)
                {
                    Color color = colors[j];
                    color.a *= remaining;
                    propertyBlock.SetColor(baseColorId, color);
                    renderers[i].SetPropertyBlock(propertyBlock, j);
                }
            }
        }
    }
}
