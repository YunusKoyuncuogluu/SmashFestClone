using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace SmashFast.UI
{
    public abstract class UIPanelBase : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField, Min(0.01f)] private float fadeDuration = 0.16f;

        private readonly PanelFadeRunner fader = new PanelFadeRunner();
        private bool isPrepared;

        public UniTask ShowAsync()
        {
            Prepare();
            if (gameObject.activeSelf && canvasGroup.alpha >= 0.999f)
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
                RaiseOpened();
                return UniTask.CompletedTask;
            }

            return fader.Play(this, canvasGroup, 1f, fadeDuration, true, destroyCancellationToken);
        }

        public UniTask HideAsync()
        {
            Prepare();
            if (!gameObject.activeSelf)
                return UniTask.CompletedTask;

            return fader.Play(this, canvasGroup, 0f, fadeDuration, false, destroyCancellationToken);
        }

        internal void RaiseOpened() => OnPanelOpened();

        internal void RaiseClosed() => OnPanelClosed();

        protected virtual void OnPanelOpened()
        {
        }

        protected virtual void OnPanelClosed()
        {
        }

        private void Prepare()
        {
            if (canvasGroup == null && !TryGetComponent(out canvasGroup))
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            if (isPrepared || fader.IsRunning)
                return;

            isPrepared = true;
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            gameObject.SetActive(false);
        }
    }

    internal class PanelFadeRunner : IPlayerLoopItem
    {
        private UIPanelBase panel;
        private CanvasGroup group;
        private CancellationToken token;
        private AutoResetUniTaskCompletionSource source;
        private float fromAlpha;
        private float toAlpha;
        private float duration;
        private float elapsed;
        private bool isOpening;
        private bool isQueued;

        internal bool IsRunning { get; private set; }

        public UniTask Play(UIPanelBase panel, CanvasGroup group, float targetAlpha, float duration, bool open, CancellationToken token)
        {
            Complete();
            this.panel = panel;
            this.group = group;
            this.token = token;
            this.duration = Mathf.Max(0.01f, duration);
            isOpening = open;
            fromAlpha = group != null ? group.alpha : 0f;
            toAlpha = targetAlpha;
            elapsed = 0f;
            source = AutoResetUniTaskCompletionSource.Create();
            IsRunning = true;

            group.interactable = open;
            group.blocksRaycasts = open;
            if (open)
            {
                panel.gameObject.SetActive(true);
                panel.RaiseOpened();
            }

            if (!isQueued)
            {
                isQueued = true;
                PlayerLoopHelper.AddAction(PlayerLoopTiming.Update, this);
            }

            return source.Task;
        }

        public bool MoveNext()
        {
            if (!IsRunning || group == null || panel == null || token.IsCancellationRequested)
            {
                Cancel();
                return false;
            }

            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Min(1f, elapsed / duration);
            group.alpha = Mathf.Lerp(fromAlpha, toAlpha, progress);
            if (progress < 1f)
                return true;

            group.alpha = toAlpha;
            group.interactable = isOpening;
            group.blocksRaycasts = isOpening;
            if (!isOpening)
            {
                panel.RaiseClosed();
                panel.gameObject.SetActive(false);
            }

            Stop();
            Complete();
            return false;
        }

        private void Stop()
        {
            IsRunning = false;
            isQueued = false;
        }

        private void Complete()
        {
            AutoResetUniTaskCompletionSource previous = source;
            source = null;
            previous?.TrySetResult();
        }

        private void Cancel()
        {
            Stop();
            AutoResetUniTaskCompletionSource previous = source;
            source = null;
            previous?.TrySetCanceled(token);
        }
    }
}
