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
        [SerializeField, Min(1f)] private float stiffness = 420f;
        [SerializeField, Min(0f)] private float damping = 16f;

        private IAudioService audioService;
        private Selectable selectable;
        private Vector3 baseScale = Vector3.one;
        private float scale = 1f;
        private float velocity;
        private float target = 1f;
        private bool isSettled = true;
        private bool isPressed;

        [Inject]
        public void Construct([InjectOptional] IAudioService audio) => this.audioService = audio;

        private void Awake()
        {
            baseScale = transform.localScale;
            TryGetComponent(out selectable);
        }

        private void OnDisable()
        {
            scale = 1f;
            velocity = 0f;
            target = 1f;
            isSettled = true;
            isPressed = false;
            transform.localScale = baseScale;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            isPressed = IsInteractable();
            if (isPressed)
                SetTarget(pressedScale);
        }

        public void OnPointerUp(PointerEventData eventData) => SetTarget(1f);

        public void OnPointerExit(PointerEventData eventData) => SetTarget(1f);

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!isPressed)
                return;

            isPressed = false;
            audioService?.PlayClick();
        }

        private bool IsInteractable() => selectable == null || selectable.IsInteractable();

        private void SetTarget(float value)
        {
            target = value;
            isSettled = false;
        }

        private void Update()
        {
            if (isSettled)
                return;

            float deltaTime = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            velocity += (target - scale) * stiffness * deltaTime;
            velocity *= Mathf.Exp(-damping * deltaTime);
            scale += velocity * deltaTime;

            if (Mathf.Abs(target - scale) < 0.001f && Mathf.Abs(velocity) < 0.01f)
            {
                scale = target;
                velocity = 0f;
                isSettled = true;
            }

            transform.localScale = baseScale * scale;
        }
    }
}
