using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace SmashFest.UI
{
    internal class PanelAnimator
    {
        private const float OpenDelay = 0.1f;
        private const float PopDuration = 0.4f;
        private const float PopStartScale = 0.8f;
        private const float CloseScale = 0.92f;
        private const float SlideDistance = 260f;
        private const float SlideDuration = 0.55f;
        private const float SlideStagger = 0.08f;
        private const float ItemFadeDuration = 0.25f;

        private readonly List<SlideItem> items = new List<SlideItem>(8);

        private UIPanelBase panel;
        private CanvasGroup group;
        private RectTransform popTarget;
        private Tween openTween;
        private Tween closeTween;
        private float fadeDuration;
        private float closeFrom;
        private float time;

        public bool IsRunning => openTween.IsPlaying() || closeTween.IsPlaying();

        private struct SlideItem
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public Vector2 Rest;
            public Vector2 Offset;
            public float Delay;
        }

        public void Setup(UIPanelBase owner, CanvasGroup canvasGroup, RectTransform pop, float fade)
        {
            panel = owner;
            group = canvasGroup;
            popTarget = pop;
            fadeDuration = Mathf.Max(0.01f, fade);
            if (popTarget == null)
                CaptureItems((RectTransform)owner.transform);

            openTween = CreateTween(OpenDuration(), ApplyOpen, ApplyOpen);
            closeTween = CreateTween(fadeDuration, ApplyClose, FinishClose);
        }

        public void Show()
        {
            closeTween.Pause();
            group.interactable = true;
            group.blocksRaycasts = true;
            panel.gameObject.SetActive(true);
            panel.RaiseOpened();

            time = 0f;
            ApplyOpen();
            openTween.Restart();
        }

        public void Hide()
        {
            openTween.Pause();
            group.interactable = false;
            group.blocksRaycasts = false;
            ResetItems();

            closeFrom = group.alpha;
            time = 0f;
            closeTween.Restart();
        }

        public void HideImmediate()
        {
            openTween.Pause();
            closeTween.Pause();
            group.interactable = false;
            group.blocksRaycasts = false;
            ResetItems();
            FinishClose();
        }

        private Tween CreateTween(float duration, TweenCallback update, TweenCallback complete)
        {
            return DOTween.To(() => time, value => time = value, duration, duration)
                .SetEase(Ease.Linear)
                .SetUpdate(true)
                .SetAutoKill(false)
                .SetLink(panel.gameObject)
                .OnUpdate(update)
                .OnComplete(complete)
                .Pause();
        }

        private float OpenDuration()
        {
            float slides = items.Count > 0 ? (items.Count - 1) * SlideStagger + SlideDuration : 0f;
            float pop = popTarget != null ? PopDuration : 0f;
            return OpenDelay + Mathf.Max(fadeDuration, Mathf.Max(slides, pop));
        }

        private void CaptureItems(RectTransform root)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                RectTransform child = root.GetChild(i) as RectTransform;
                if (child == null || !child.TryGetComponent(out CanvasGroup childGroup))
                    continue;

                float anchorY = (child.anchorMin.y + child.anchorMax.y) * 0.5f;
                items.Add(new SlideItem
                {
                    Rect = child,
                    Group = childGroup,
                    Rest = child.anchoredPosition,
                    Offset = new Vector2(0f, anchorY > 0.55f ? SlideDistance : -SlideDistance),
                    Delay = items.Count * SlideStagger
                });
            }
        }

        private void ApplyOpen()
        {
            group.alpha = Mathf.Clamp01(time / fadeDuration);
            float local = time - OpenDelay;

            if (popTarget != null)
            {
                float scale = DOVirtual.EasedValue(PopStartScale, 1f, Mathf.Clamp01(local / PopDuration), Ease.OutBack);
                popTarget.localScale = new Vector3(scale, scale, 1f);
            }

            foreach (SlideItem item in items)
            {
                float itemTime = local - item.Delay;
                float slide = DOVirtual.EasedValue(1f, 0f, Mathf.Clamp01(itemTime / SlideDuration), Ease.OutBack);
                item.Rect.anchoredPosition = item.Rest + item.Offset * slide;
                item.Group.alpha = Mathf.Clamp01(itemTime / ItemFadeDuration);
            }
        }

        private void ApplyClose()
        {
            float t = Mathf.Clamp01(time / fadeDuration);
            group.alpha = Mathf.Lerp(closeFrom, 0f, t);
            if (popTarget == null)
                return;

            float scale = Mathf.Lerp(1f, CloseScale, t);
            popTarget.localScale = new Vector3(scale, scale, 1f);
        }

        private void FinishClose()
        {
            if (popTarget != null)
                popTarget.localScale = Vector3.one;

            group.alpha = 0f;
            panel.RaiseClosed();
            panel.gameObject.SetActive(false);
        }

        private void ResetItems()
        {
            foreach (SlideItem item in items)
            {
                item.Rect.anchoredPosition = item.Rest;
                item.Group.alpha = 1f;
            }

            if (popTarget != null)
                popTarget.localScale = Vector3.one;
        }
    }
}
