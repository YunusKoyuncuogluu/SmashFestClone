using DG.Tweening;
using SmashFest.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zenject;

namespace SmashFest.UI
{
    public class ButtonPop : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField, Range(0.7f, 1f)] private float pressedScale = 0.9f;
        [SerializeField, Min(0.01f)] private float pressDuration = 0.08f;
        [SerializeField, Min(0.01f)] private float releaseDuration = 0.25f;

        private IAudioService audioService;
        private Selectable selectable;
        private Vector3 baseScale = Vector3.one;
        private bool isPressed;

        [Inject]
        public void Construct([InjectOptional] IAudioService audio) => audioService = audio;

        private void Awake()
        {
            baseScale = transform.localScale;
            TryGetComponent(out selectable);
        }

        private void OnDisable()
        {
            transform.DOKill();
            transform.localScale = baseScale;
            isPressed = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            isPressed = IsInteractable();
            if (isPressed)
                ScaleTo(pressedScale, pressDuration, Ease.OutQuad);
        }

        public void OnPointerUp(PointerEventData eventData) => ScaleTo(1f, releaseDuration, Ease.OutBack);

        public void OnPointerExit(PointerEventData eventData) => ScaleTo(1f, releaseDuration, Ease.OutBack);

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!isPressed)
                return;

            isPressed = false;
            audioService?.PlayClick();
        }

        private bool IsInteractable() => selectable == null || selectable.IsInteractable();

        private void ScaleTo(float scale, float duration, Ease ease)
        {
            transform.DOKill();
            transform.DOScale(baseScale * scale, duration)
                .SetEase(ease)
                .SetUpdate(true)
                .SetRecyclable(true)
                .SetLink(gameObject);
        }
    }
}
