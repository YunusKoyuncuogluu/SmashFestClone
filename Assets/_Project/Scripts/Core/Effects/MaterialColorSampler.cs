using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace SmashFest.Core.Effects
{
    public class MaterialColorSampler
    {
        private const int SourceSize = 256;
        private const int SampleSize = 4;

        private readonly Dictionary<Material, Color> colorCache = new Dictionary<Material, Color>();

        public Color GetColor(Material material)
        {
            if (material == null)
                return Color.white;

            if (colorCache.TryGetValue(material, out Color cached))
                return cached;

            Color tint = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white;
            Texture texture = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : material.mainTexture;
            colorCache[material] = Tune(tint);
            if (texture != null && SystemInfo.supportsAsyncGPUReadback)
                RequestAverage(material, texture, tint);

            return colorCache[material];
        }

        private void RequestAverage(Material material, Texture texture, Color tint)
        {
            RenderTexture sample = Downsample(texture);
            AsyncGPUReadback.Request(sample, 0, TextureFormat.RGBA32, request =>
            {
                RenderTexture.ReleaseTemporary(sample);
                if (request.hasError || material == null)
                    return;

                NativeArray<Color32> pixels = request.GetData<Color32>();
                Color sum = Color.black;
                foreach (Color32 pixel in pixels)
                    sum += pixel;

                colorCache[material] = Tune(tint * (sum / pixels.Length));
            });
        }

        private RenderTexture Downsample(Texture texture)
        {
            int size = SourceSize;
            RenderTexture current = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(texture, current);
            while (size > SampleSize)
            {
                size /= 2;
                RenderTexture smaller = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(current, smaller);
                RenderTexture.ReleaseTemporary(current);
                current = smaller;
            }

            return current;
        }

        private Color Tune(Color color)
        {
            Color.RGBToHSV(color, out float hue, out float saturation, out float value);
            color = Color.HSVToRGB(hue, Mathf.Clamp01(saturation * 1.25f), Mathf.Clamp(value, 0.35f, 1f));
            color.a = 1f;
            return color;
        }
    }
}
