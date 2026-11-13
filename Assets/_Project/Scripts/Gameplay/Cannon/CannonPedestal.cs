using UnityEngine;

namespace SmashFest.Gameplay.Cannon
{
    public class CannonPedestal
    {
        private const float MinHeight = 0.01f;

        private readonly Transform pedestal;
        private readonly float width;

        public CannonPedestal(Transform pedestal, float width)
        {
            this.pedestal = pedestal;
            this.width = width;
        }

        public void Fit(float cannonHeight, float groundHeight)
        {
            if (pedestal == null)
                return;

            float height = cannonHeight - groundHeight;
            bool isRaised = height > MinHeight;
            pedestal.gameObject.SetActive(isRaised);
            if (!isRaised)
                return;

            pedestal.localPosition = new Vector3(0f, -height * 0.5f, 0f);
            pedestal.localScale = new Vector3(width, height * 0.5f, width);
        }
    }
}
