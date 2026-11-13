using System;
using UnityEngine;

namespace SmashFest.Core.Performance
{
    [Serializable]
    public class FrameRateSettings
    {
        public int targetFrameRate = 60;
        public bool adaptiveResolution = true;
        [Range(0.5f, 1f)] public float minRenderScale = 0.6f;
        [Range(0.05f, 0.25f)] public float scaleStep = 0.1f;
        public float sampleSeconds = 2f;
        [Range(0.5f, 1f)] public float dropRatio = 0.9f;
        [Range(0.5f, 1f)] public float recoverRatio = 0.97f;
        public int samplesBeforeRecover = 3;
        public float hitchSeconds = 0.25f;
    }
}
