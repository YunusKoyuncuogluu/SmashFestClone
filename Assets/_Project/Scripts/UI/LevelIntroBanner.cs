using DG.Tweening;
using TMPro;
using UnityEngine;

namespace SmashFest.UI
{
    public class LevelIntroBanner
    {
        private const string Format = "Level {0:0}";
        private const string BonusText = "BONUS LEVEL!";
        private const float Lifetime = 1.5f;
        private const float PopShare = 0.2f;
        private const float FadeStart = 0.72f;
        private const float StartScale = 0.6f;
        private const float ExitRise = 90f;

        private readonly TMP_Text label;
        private readonly RectTransform rect;
        private readonly Vector3 restPosition;
        private readonly ProgressTween timeline;

        public LevelIntroBanner(TMP_Text label)
        {
            this.label = label;
            rect = label.rectTransform;
            restPosition = rect.localPosition;
            label.alpha = 0f;
            timeline = new ProgressTween(Lifetime, Apply, label.gameObject, true, false);
        }

        public void Play(int level, bool isBonus)
        {
            if (isBonus)
                label.SetText(BonusText);
            else
                label.SetText(Format, level);

            timeline.Restart();
        }

        private void Apply(float progress)
        {
            float pop = Mathf.Clamp01(progress / PopShare);
            float exit = Mathf.Clamp01((progress - FadeStart) / (1f - FadeStart));
            float scale = DOVirtual.EasedValue(StartScale, 1f, pop, Ease.OutBack);

            rect.localScale = new Vector3(scale, scale, 1f);
            rect.localPosition = restPosition + new Vector3(0f, ExitRise * DOVirtual.EasedValue(0f, 1f, exit, Ease.InQuad), 0f);
            label.alpha = Mathf.Min(pop * 2f, 1f - exit);
        }
    }
}
