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

        public int HighestUnlockedLevel { get; private set; }
        public int SelectedLevelNumber { get; private set; }
        public bool SoundEnabled { get; private set; }
        public bool HapticsEnabled { get; private set; }

        public void Initialize()
        {
            HighestUnlockedLevel = Mathf.Max(1, PlayerPrefs.GetInt(HighestUnlockedKey, 1));
            SelectedLevelNumber = Mathf.Clamp(PlayerPrefs.GetInt(SelectedLevelKey, 1), 1, HighestUnlockedLevel);
            SoundEnabled = PlayerPrefs.GetInt(SoundEnabledKey, 1) != 0;
            HapticsEnabled = PlayerPrefs.GetInt(HapticsEnabledKey, 1) != 0;
            ApplySound();
        }

        public bool IsLevelUnlocked(int level) => level >= 1 && level <= HighestUnlockedLevel;

        public void SetSelectedLevel(int level)
        {
            level = Mathf.Clamp(level, 1, HighestUnlockedLevel);
            if (level == SelectedLevelNumber)
                return;

            SelectedLevelNumber = level;
            Save(SelectedLevelKey, level);
        }

        public void UnlockThrough(int level)
        {
            if (level <= HighestUnlockedLevel)
                return;

            HighestUnlockedLevel = level;
            Save(HighestUnlockedKey, level);
        }

        public void SetSoundEnabled(bool enabled)
        {
            if (SoundEnabled == enabled)
                return;

            SoundEnabled = enabled;
            Save(SoundEnabledKey, enabled ? 1 : 0);
            ApplySound();
        }

        public void SetHapticsEnabled(bool enabled)
        {
            if (HapticsEnabled == enabled)
                return;

            HapticsEnabled = enabled;
            Save(HapticsEnabledKey, enabled ? 1 : 0);
        }

        private void ApplySound() => AudioListener.volume = SoundEnabled ? 1f : 0f;

        private void Save(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
        }
    }
}
