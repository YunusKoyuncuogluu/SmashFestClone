using UnityEngine;

namespace SmashFest.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UIPanelBase : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField, Min(0.01f)] private float fadeDuration = 0.18f;
        [SerializeField] private RectTransform popTarget;

        private readonly PanelAnimator animator = new PanelAnimator();
        private bool isPrepared;

        public void Show()
        {
            Prepare();
            if (gameObject.activeSelf && canvasGroup.alpha >= 0.999f && !animator.IsRunning)
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
                RaiseOpened();
                return;
            }

            animator.Show();
        }

        public void Hide()
        {
            Prepare();
            if (gameObject.activeSelf)
                animator.Hide();
        }

        public void HideImmediate()
        {
            Prepare();
            if (gameObject.activeSelf)
                animator.HideImmediate();
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
            if (isPrepared)
                return;

            isPrepared = true;
            animator.Setup(this, canvasGroup, popTarget, fadeDuration);
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            gameObject.SetActive(false);
        }
    }
}
