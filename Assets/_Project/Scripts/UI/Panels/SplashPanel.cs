using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace SmashFest.UI.Panels
{
    public class SplashPanel : UIPanelBase
    {
        private const string LoadingFormat = "LOADING {0:0}%";
        private const float Duration = 1.8f;
        private const float LogoPopDuration = 0.5f;
        private const float LogoStartScale = 0.55f;
        private const float TitleDelay = 0.25f;
        private const float TitleFadeDuration = 0.35f;
        private const float TitleRise = 40f;
        private const float BarDelay = 0.15f;

        [SerializeField] private RectTransform logo;
        [SerializeField] private RectTransform title;
        [SerializeField] private TMP_Text[] titleLabels;
        [SerializeField] private RectTransform barFill;
        [SerializeField] private TMP_Text loadingLabel;

        private ProgressTween timeline;
        private Vector2 titleRest;
        private float minFillWidth;
        private float maxFillWidth;
        private int shownPercent = -1;

        public async UniTask PlayAsync()
        {
            if (timeline == null)
                Build();

            shownPercent = -1;
            timeline.Restart();
            while (timeline.IsPlaying)
                await UniTask.Yield(PlayerLoopTiming.Update, destroyCancellationToken);
        }

        private void Build()
        {
            titleRest = title.anchoredPosition;
            RectTransform track = (RectTransform)barFill.parent;
            float inset = barFill.anchoredPosition.x;
            minFillWidth = track.rect.height - inset * 2f;
            maxFillWidth = track.rect.width - inset * 2f;
            timeline = new ProgressTween(Duration, Apply, gameObject, false, false);
        }

        private void Apply(float progress)
        {
            float time = progress * Duration;
            float pop = DOVirtual.EasedValue(LogoStartScale, 1f, Mathf.Clamp01(time / LogoPopDuration), Ease.OutBack);
            logo.localScale = new Vector3(pop, pop, 1f);

            float reveal = DOVirtual.EasedValue(0f, 1f, Mathf.Clamp01((time - TitleDelay) / TitleFadeDuration), Ease.OutCubic);
            for (int i = 0; i < titleLabels.Length; i++)
                titleLabels[i].alpha = reveal;

            title.anchoredPosition = titleRest + new Vector2(0f, TitleRise * (reveal - 1f));

            float fill = DOVirtual.EasedValue(0f, 1f, Mathf.Clamp01((time - BarDelay) / (Duration - BarDelay)), Ease.InOutSine);
            Vector2 size = barFill.sizeDelta;
            size.x = Mathf.Lerp(minFillWidth, maxFillWidth, fill);
            barFill.sizeDelta = size;

            int percent = Mathf.RoundToInt(fill * 100f);
            if (percent == shownPercent || loadingLabel == null)
                return;

            shownPercent = percent;
            loadingLabel.SetText(LoadingFormat, percent);
        }
    }
}
