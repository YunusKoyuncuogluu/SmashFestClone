using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zenject;

namespace SmashFest.Core.Performance
{
    public class FrameRateService : IInitializable, ITickable, IDisposable
    {
        private readonly FrameRateSettings settings;
        private UniversalRenderPipelineAsset pipeline;
        private float defaultScale;
        private float elapsed;
        private float longestFrame;
        private int frames;
        private int stableSamples;

        public FrameRateService(FrameRateSettings settings) => this.settings = settings;

        public void Initialize()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = settings.targetFrameRate;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            if (!settings.adaptiveResolution || Application.isEditor)
                return;

            pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline != null)
                defaultScale = pipeline.renderScale;
        }

        public void Tick()
        {
            if (pipeline == null)
                return;

            float delta = Time.unscaledDeltaTime;
            elapsed += delta;
            longestFrame = Mathf.Max(longestFrame, delta);
            frames++;
            if (elapsed < settings.sampleSeconds)
                return;

            float average = frames / elapsed;
            bool hitched = longestFrame > settings.hitchSeconds;
            elapsed = 0f;
            longestFrame = 0f;
            frames = 0;
            if (!hitched)
                Adjust(average);
        }

        public void Dispose()
        {
            if (pipeline != null)
                pipeline.renderScale = defaultScale;
        }

        private void Adjust(float average)
        {
            float target = settings.targetFrameRate;
            if (average < target * settings.dropRatio)
            {
                stableSamples = 0;
                SetScale(pipeline.renderScale - settings.scaleStep);
                return;
            }

            if (average < target * settings.recoverRatio || pipeline.renderScale >= defaultScale)
            {
                stableSamples = 0;
                return;
            }

            if (++stableSamples < settings.samplesBeforeRecover)
                return;

            stableSamples = 0;
            SetScale(pipeline.renderScale + settings.scaleStep * 0.5f);
        }

        private void SetScale(float scale) => pipeline.renderScale = Mathf.Clamp(scale, settings.minRenderScale, defaultScale);
    }
}
