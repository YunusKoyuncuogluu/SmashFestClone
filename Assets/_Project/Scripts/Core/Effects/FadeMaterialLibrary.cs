using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace SmashFast.Core.Effects
{
    public class FadeMaterialLibrary : IDisposable
    {
        private readonly Dictionary<Material, Material> fadeMaterials = new Dictionary<Material, Material>(64);

        public Material Resolve(Material source)
        {
            if (source == null)
            {
                return null;
            }

            Material fade;
            if (fadeMaterials.TryGetValue(source, out fade))
            {
                return fade;
            }

            fade = new Material(source);
            fade.SetFloat("_Surface", 1f);
            fade.SetFloat("_Blend", 0f);
            fade.SetFloat("_AlphaClip", 0f);
            fade.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            fade.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            fade.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            fade.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            fade.SetFloat("_ZWrite", 0f);
            fade.SetOverrideTag("RenderType", "Transparent");
            fade.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            fade.DisableKeyword("_ALPHATEST_ON");
            fade.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            fade.renderQueue = (int)RenderQueue.Transparent;
            fadeMaterials.Add(source, fade);
            return fade;
        }

        public void Dispose()
        {
            foreach (KeyValuePair<Material, Material> pair in fadeMaterials)
            {
                if (pair.Value != null)
                {
                    Object.Destroy(pair.Value);
                }
            }

            fadeMaterials.Clear();
        }
    }
}
