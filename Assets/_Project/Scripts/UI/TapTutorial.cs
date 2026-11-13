using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SmashFest.UI
{
    public class TapTutorial
    {
        private const float CycleDuration = 1.3f;
        private const float RippleMinScale = 0.35f;
        private const float RippleMaxScale = 1.4f;
        private const float RippleAlpha = 0.75f;
        private const float LabelPulse = 0.06f;

        private readonly GameObject root;
        private readonly Image[] ripples;
        private readonly TMP_Text label;
        private readonly ProgressTween timeline;

        public TapTutorial(GameObject root, Image[] ripples, TMP_Text label)
        {
            this.root = root;
            this.ripples = ripples;
            this.label = label;
            timeline = new ProgressTween(CycleDuration, Apply, root, true, true);
            root.SetActive(false);
        }

        public void Show(Vector2 localPoint)
        {
            root.transform.localPosition = localPoint;
            root.SetActive(true);
            timeline.Restart();
        }

        public void Hide()
        {
            if (!root.activeSelf)
                return;

            timeline.Stop();
            root.SetActive(false);
        }

        private void Apply(float progress)
        {
            for (int i = 0; i < ripples.Length; i++)
            {
                float phase = Mathf.Repeat(progress + (float)i / ripples.Length, 1f);
                float scale = Mathf.Lerp(RippleMinScale, RippleMaxScale, phase);
                ripples[i].rectTransform.localScale = new Vector3(scale, scale, 1f);
                Color color = ripples[i].color;
                color.a = RippleAlpha * (1f - phase);
                ripples[i].color = color;
            }

            float pulse = 1f + LabelPulse * Mathf.Sin(progress * Mathf.PI * 2f);
            label.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
        }
    }
}
