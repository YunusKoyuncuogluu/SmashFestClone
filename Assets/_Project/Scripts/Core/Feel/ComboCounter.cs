using System;
using UnityEngine;

namespace SmashFest.Core.Feel
{
    public class ComboCounter
    {
        private const float ComboWindow = 1.2f;
        private const int MinCombo = 2;

        private float lastBreakTime = float.MinValue;
        private int count;

        public event Action<int> ComboReached;

        public void RegisterBreak()
        {
            float now = Time.time;
            count = now - lastBreakTime <= ComboWindow ? count + 1 : 1;
            lastBreakTime = now;
            if (count >= MinCombo)
                ComboReached?.Invoke(count);
        }

        public void Reset()
        {
            count = 0;
            lastBreakTime = float.MinValue;
        }
    }
}
