using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SmashFest.UI
{
    public class RecordingCursor : MonoBehaviour
    {
        private const float RippleDuration = 0.45f;
        private const float RippleStartScale = 0.3f;
        private const float RippleEndScale = 1.25f;
        private const float PressedScale = 0.92f;

        [SerializeField] private bool recordingMode;
        [SerializeField] private Canvas canvas;
        [SerializeField] private RectTransform hand;
        [SerializeField] private Image handImage;
        [SerializeField] private Sprite idleSprite;
        [SerializeField] private Sprite pressedSprite;
        [SerializeField] private RectTransform ripple;
        [SerializeField] private Image rippleImage;

        private ProgressTween rippleTween;
        private bool isActive;
        private bool wasPressed;

        private void Awake()
        {
            rippleTween = new ProgressTween(RippleDuration, ApplyRipple, gameObject, true, false);
            SetActive(false);
        }

        private void Update()
        {
            if (recordingMode != isActive)
                SetActive(recordingMode);

            if (!isActive)
                return;

            if (Cursor.visible)
                Cursor.visible = false;

            if (!TryReadPointer(out Vector2 position, out bool isPressed))
                return;

            hand.position = position;
            if (isPressed && !wasPressed)
                Press(position);
            else if (!isPressed && wasPressed)
                Release();

            wasPressed = isPressed;
        }

        private void OnDisable() => Cursor.visible = true;

        private void SetActive(bool active)
        {
            isActive = active;
            wasPressed = false;
            canvas.enabled = active;
            Cursor.visible = !active;
            Release();
            rippleTween.Stop();
            ApplyRipple(1f);
        }

        private void Press(Vector2 position)
        {
            ApplySprite(pressedSprite);
            hand.localScale = new Vector3(PressedScale, PressedScale, 1f);
            ripple.position = position;
            rippleTween.Restart();
        }

        private void Release()
        {
            ApplySprite(idleSprite);
            hand.localScale = Vector3.one;
        }

        private void ApplySprite(Sprite sprite)
        {
            handImage.sprite = sprite;
            Vector2 size = sprite.rect.size;
            hand.pivot = new Vector2(sprite.pivot.x / size.x, sprite.pivot.y / size.y);
        }

        private void ApplyRipple(float progress)
        {
            float eased = DOVirtual.EasedValue(0f, 1f, progress, Ease.OutCubic);
            float scale = Mathf.Lerp(RippleStartScale, RippleEndScale, eased);
            ripple.localScale = new Vector3(scale, scale, 1f);
            Color color = rippleImage.color;
            color.a = 1f - progress;
            rippleImage.color = color;
        }

        private bool TryReadPointer(out Vector2 position, out bool isPressed)
        {
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                position = mouse.position.ReadValue();
                isPressed = mouse.leftButton.isPressed;
                return true;
            }

            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                position = touchscreen.primaryTouch.position.ReadValue();
                isPressed = touchscreen.primaryTouch.press.isPressed;
                return true;
            }

            position = Vector2.zero;
            isPressed = false;
            return false;
        }
    }
}
