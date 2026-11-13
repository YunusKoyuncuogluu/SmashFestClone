using System;
using DG.Tweening;
using UnityEngine;

namespace SmashFest.UI
{
    public class ProgressTween
    {
        private readonly Action<float> apply;
        private readonly Tween tween;

        public ProgressTween(float duration, Action<float> apply, GameObject link, bool ignoreTimeScale, bool loop)
        {
            this.apply = apply;
            tween = DOTween.To(GetProgress, SetProgress, 1f, duration)
                .SetEase(Ease.Linear)
                .SetLoops(loop ? -1 : 1, LoopType.Restart)
                .SetAutoKill(false)
                .SetUpdate(UpdateType.Normal, ignoreTimeScale)
                .SetLink(link)
                .Pause();
        }

        public float Progress { get; private set; }

        public bool IsPlaying => tween.IsActive() && tween.IsPlaying();

        public void Restart()
        {
            SetProgress(0f);
            tween.Restart();
        }

        public void Stop() => tween.Pause();

        private float GetProgress() => Progress;

        private void SetProgress(float value)
        {
            Progress = value;
            apply(value);
        }
    }
}
