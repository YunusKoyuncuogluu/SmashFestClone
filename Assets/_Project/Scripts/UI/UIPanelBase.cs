using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace SmashFast.UI
{
    public abstract class UIPanelBase : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField, Min(0.01f)] private float fadeDuration = 0.16f;

        private PanelFadeRunner fadeRunner;
        private bool visualsPrepared;

        public UniTask ShowAsync()
        {
            EnsureVisuals();
            if (gameObject.activeSelf && canvasGroup.alpha >= 0.999f)
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
                RaiseOpened();
                return UniTask.CompletedTask;
            }

            return fadeRunner.Play(this, canvasGroup, 1f, fadeDuration, true, destroyCancellationToken);
        }

        public UniTask HideAsync()
        {
            EnsureVisuals();
            if (!gameObject.activeSelf)
            {
                return UniTask.CompletedTask;
            }

            return fadeRunner.Play(this, canvasGroup, 0f, fadeDuration, false, destroyCancellationToken);
        }

        internal void RaiseOpened()
        {
            OnPanelOpened();
        }

        internal void RaiseClosed()
        {
            OnPanelClosed();
        }

        protected virtual void OnPanelOpened()
        {
        }

        protected virtual void OnPanelClosed()
        {
        }

        private void EnsureVisuals()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            if (fadeRunner == null)
            {
                fadeRunner = new PanelFadeRunner();
            }

            if (visualsPrepared || fadeRunner.IsRunning)
            {
                return;
            }

            visualsPrepared = true;
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            gameObject.SetActive(false);
        }
    }

    internal class PanelFadeRunner : IPlayerLoopItem
    {
        private UIPanelBase owner;
        private CanvasGroup group;
        private CancellationToken token;
        private AutoResetUniTaskCompletionSource source;
        private float fromAlpha;
        private float toAlpha;
        private float duration;
        private float elapsed;
        private bool opening;
        private bool running;
        private bool queued;

        internal bool IsRunning
        {
            get
            {
                return running;
            }
        }

        public UniTask Play(
            UIPanelBase panel,
            CanvasGroup canvasGroup,
            float targetAlpha,
            float durationSeconds,
            bool open,
            CancellationToken cancellationToken)
        {
            ReleaseSource();
            owner = panel;
            group = canvasGroup;
            token = cancellationToken;
            opening = open;
            fromAlpha = canvasGroup != null ? canvasGroup.alpha : 0f;
            toAlpha = targetAlpha;
            duration = durationSeconds < 0.01f ? 0.01f : durationSeconds;
            elapsed = 0f;
            source = AutoResetUniTaskCompletionSource.Create();
            running = true;
            if (opening)
            {
                owner.gameObject.SetActive(true);
                group.blocksRaycasts = true;
                group.interactable = true;
                owner.RaiseOpened();
            }
            else
            {
                group.interactable = false;
                group.blocksRaycasts = false;
            }

            if (!queued)
            {
                queued = true;
                PlayerLoopHelper.AddAction(PlayerLoopTiming.Update, this);
            }

            return source.Task;
        }

        public bool MoveNext()
        {
            if (!running || group == null || owner == null)
            {
                FinishCanceled();
                return false;
            }

            if (token.IsCancellationRequested)
            {
                FinishCanceled();
                return false;
            }

            elapsed += Time.unscaledDeltaTime;
            float progress = elapsed / duration;
            if (progress > 1f)
            {
                progress = 1f;
            }

            group.alpha = Mathf.Lerp(fromAlpha, toAlpha, progress);
            if (progress < 1f)
            {
                return true;
            }

            group.alpha = toAlpha;
            if (opening)
            {
                group.interactable = true;
                group.blocksRaycasts = true;
            }
            else
            {
                group.interactable = false;
                group.blocksRaycasts = false;
                owner.RaiseClosed();
                owner.gameObject.SetActive(false);
            }

            FinishSuccess();
            return false;
        }

        private void ReleaseSource()
        {
            AutoResetUniTaskCompletionSource previous = source;
            source = null;
            if (previous != null)
            {
                previous.TrySetResult();
            }
        }

        private void FinishSuccess()
        {
            running = false;
            queued = false;
            ReleaseSource();
        }

        private void FinishCanceled()
        {
            running = false;
            queued = false;
            AutoResetUniTaskCompletionSource previous = source;
            source = null;
            if (previous != null)
            {
                previous.TrySetCanceled(token);
            }
        }
    }
}
