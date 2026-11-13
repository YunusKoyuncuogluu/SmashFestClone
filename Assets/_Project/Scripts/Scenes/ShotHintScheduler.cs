using SmashFest.Level;
using SmashFest.UI.Panels;
using UnityEngine;

namespace SmashFest.Scenes
{
    public class ShotHintScheduler
    {
        private const int TutorialLevel = 1;
        private const float IdleDelay = 5f;

        private readonly BreakHintPicker picker;
        private readonly Camera camera;
        private float showAt = float.MaxValue;
        private bool isShown;

        public ShotHintScheduler(BreakHintPicker picker, Camera camera)
        {
            this.picker = picker;
            this.camera = camera;
        }

        public void Begin(int level)
        {
            isShown = false;
            showAt = level == TutorialLevel ? Time.time : Time.time + IdleDelay;
        }

        public void NotifyShot(GameHudPanel hud)
        {
            showAt = Time.time + IdleDelay;
            Hide(hud);
        }

        public void Stop(GameHudPanel hud)
        {
            showAt = float.MaxValue;
            Hide(hud);
        }

        public void Tick(GameHudPanel hud)
        {
            if (isShown || hud == null || Time.time < showAt)
                return;

            showAt = Time.time + IdleDelay;
            if (!picker.TryPick(out Vector3 hintPoint))
                return;

            Vector3 screenPoint = camera.WorldToScreenPoint(hintPoint);
            if (screenPoint.z <= 0f)
                return;

            isShown = true;
            hud.ShowTutorial(screenPoint);
        }

        private void Hide(GameHudPanel hud)
        {
            isShown = false;
            if (hud != null)
                hud.HideTutorial();
        }
    }
}
