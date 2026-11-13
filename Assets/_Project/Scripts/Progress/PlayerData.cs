using System;
using System.Collections.Generic;

namespace SmashFest.Progress
{
    [Serializable]
    public class PlayerData
    {
        public int highestUnlockedLevel = 1;
        public int selectedLevel = 1;
        public bool soundEnabled = true;
        public bool musicEnabled = true;
        public bool hapticsEnabled = true;
        public int coins;
        public List<int> levelStars = new List<int>();
        public List<string> ownedSkins = new List<string>();
        public string selectedSkin = string.Empty;
    }
}
