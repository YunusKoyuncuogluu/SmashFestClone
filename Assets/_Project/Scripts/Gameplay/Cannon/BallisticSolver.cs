using UnityEngine;

namespace SmashFest.Gameplay.Cannon
{
    public class BallisticSolver
    {
        private const float MinFlightTime = 0.25f;

        public Vector3 VelocityToHit(Vector3 from, Vector3 to, float speed)
        {
            Vector3 delta = to - from;
            float step = Time.fixedDeltaTime;
            float flightTime = Mathf.Max(MinFlightTime, delta.magnitude / Mathf.Max(0.1f, speed));
            int steps = Mathf.Max(1, Mathf.RoundToInt(flightTime / step));
            Vector3 drop = Physics.gravity * (step * step * steps * (steps + 1) * 0.5f);
            return (delta - drop) / (steps * step);
        }
    }
}
