using DG.Tweening;
using TMPro;
using UnityEngine;

namespace SmashFest.UI
{
    public class ComboPopup
    {
        private const float Lifetime = 0.75f;
        private const float PopShare = 0.22f;
        private const float FadeStart = 0.55f;
        private const float StartScale = 0.5f;
        private const float RiseDistance = 60f;
        private const float MaxDrift = 18f;
        private const float MaxTilt = 6f;

        private readonly TMP_Text label;
        private readonly RectTransform rect;
        private readonly ProgressTween timeline;
        private readonly Vector2 origin;
        private float drift;
        private float tilt;
        private float peakScale;

        public ComboPopup(TMP_Text label)
        {
            this.label = label;
            rect = label.rectTransform;
            origin = rect.anchoredPosition;
            label.alpha = 0f;
            timeline = new ProgressTween(Lifetime, Apply, label.gameObject, true, false);
        }

        public void Play(string format, int count, Color color, float scale)
        {
            drift = Random.Range(-MaxDrift, MaxDrift);
            tilt = Random.Range(-MaxTilt, MaxTilt);
            peakScale = scale;
            label.SetText(format, count);
            label.color = color;
            rect.SetAsLastSibling();
            timeline.Restart();
        }

        public void Hide()
        {
            timeline.Stop();
            label.alpha = 0f;
        }

        private void Apply(float progress)
        {
            float pop = DOVirtual.EasedValue(StartScale, peakScale, Mathf.Clamp01(progress / PopShare), Ease.OutBack);
            float rise = DOVirtual.EasedValue(0f, 1f, progress, Ease.OutCubic);
            float fade = Mathf.Clamp01((progress - FadeStart) / (1f - FadeStart));

            rect.anchoredPosition = new Vector2(origin.x + drift * rise, origin.y + RiseDistance * rise);
            rect.localRotation = Quaternion.Euler(0f, 0f, tilt * rise);
            rect.localScale = new Vector3(pop, pop, 1f);
            label.alpha = 1f - fade;
        }
    }
}
