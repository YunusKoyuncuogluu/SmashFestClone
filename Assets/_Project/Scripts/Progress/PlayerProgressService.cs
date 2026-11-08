using UnityEngine;
using Zenject;

namespace SmashFast.Progress
{
    public class PlayerProgressService : IPlayerProgressService, IInitializable
    {
        private const string HighestUnlockedKey = "SmashFast.HighestUnlockedLevel";
        private const string SelectedLevelKey = "SmashFast.SelectedLevelNumber";
        private const string SoundEnabledKey = "SmashFast.SoundEnabled";
        private const string HapticsEnabledKey = "SmashFast.HapticsEnabled";

        private int highestUnlockedLevel;
        private int selectedLevelNumber;
        private bool soundEnabled;
        private bool hapticsEnabled;

        public int HighestUnlockedLevel
        {
            get
            {
                return highestUnlockedLevel;
            }
        }

        public int SelectedLevelNumber
        {
            get
            {
                return selectedLevelNumber;
            }
        }

        public bool SoundEnabled
        {
            get
            {
                return soundEnabled;
            }
        }

        public bool HapticsEnabled
        {
            get
            {
                return hapticsEnabled;
            }
        }

        public void Initialize()
        {
            highestUnlockedLevel = PlayerPrefs.GetInt(HighestUnlockedKey, 1);
            selectedLevelNumber = PlayerPrefs.GetInt(SelectedLevelKey, 1);
            soundEnabled = PlayerPrefs.GetInt(SoundEnabledKey, 1) != 0;
            hapticsEnabled = PlayerPrefs.GetInt(HapticsEnabledKey, 1) != 0;
            if (highestUnlockedLevel < 1)
            {
                highestUnlockedLevel = 1;
            }

            if (selectedLevelNumber < 1)
            {
                selectedLevelNumber = 1;
            }

            if (selectedLevelNumber > highestUnlockedLevel)
            {
                selectedLevelNumber = highestUnlockedLevel;
            }

            ApplySound();
        }

        public bool IsLevelUnlocked(int levelNumber)
        {
            return levelNumber >= 1 && levelNumber <= highestUnlockedLevel;
        }

        public void SetSelectedLevel(int levelNumber)
        {
            int safeNumber = levelNumber < 1 ? 1 : levelNumber;
            if (safeNumber > highestUnlockedLevel)
            {
                safeNumber = highestUnlockedLevel;
            }

            if (safeNumber == selectedLevelNumber)
            {
                return;
            }

            selectedLevelNumber = safeNumber;
            PlayerPrefs.SetInt(SelectedLevelKey, selectedLevelNumber);
            PlayerPrefs.Save();
        }

        public void UnlockThrough(int levelNumber)
        {
            if (levelNumber <= highestUnlockedLevel)
            {
                return;
            }

            highestUnlockedLevel = levelNumber;
            PlayerPrefs.SetInt(HighestUnlockedKey, highestUnlockedLevel);
            PlayerPrefs.Save();
        }

        public void SetSoundEnabled(bool enabled)
        {
            if (soundEnabled == enabled)
            {
                return;
            }

            soundEnabled = enabled;
            PlayerPrefs.SetInt(SoundEnabledKey, soundEnabled ? 1 : 0);
            PlayerPrefs.Save();
            ApplySound();
        }

        public void SetHapticsEnabled(bool enabled)
        {
            if (hapticsEnabled == enabled)
            {
                return;
            }

            hapticsEnabled = enabled;
            PlayerPrefs.SetInt(HapticsEnabledKey, hapticsEnabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void ApplySound()
        {
            AudioListener.volume = soundEnabled ? 1f : 0f;
        }
    }
}
