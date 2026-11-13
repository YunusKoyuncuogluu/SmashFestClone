using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace SmashFest.Gameplay.Cannon
{
    public class PointerShotInput : ICannonInput
    {
        public bool TryGetShot(out Vector2 screenPosition)
        {
            if (!TryReadPress(out screenPosition, out int pointerId))
                return false;

            EventSystem eventSystem = EventSystem.current;
            return eventSystem == null || !eventSystem.IsPointerOverGameObject(pointerId);
        }

        private bool TryReadPress(out Vector2 screenPosition, out int pointerId)
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                screenPosition = mouse.position.ReadValue();
                pointerId = -1;
                return true;
            }

            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame)
            {
                screenPosition = touchscreen.primaryTouch.position.ReadValue();
                pointerId = touchscreen.primaryTouch.touchId.ReadValue();
                return true;
            }

            screenPosition = Vector2.zero;
            pointerId = -1;
            return false;
        }
    }
}
