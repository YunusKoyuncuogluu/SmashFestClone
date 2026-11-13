using System;

namespace SmashFest.Progress
{
    [Serializable]
    public class PlayerData
    {
        public int version = 1;
        public int highestUnlockedLevel = 1;
        public int selectedLevel = 1;
        public bool soundEnabled = true;
        public bool musicEnabled = true;
        public bool hapticsEnabled = true;
    }
}
