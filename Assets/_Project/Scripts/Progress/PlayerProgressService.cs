using System;
using UnityEngine;
using Zenject;

namespace SmashFest.Progress
{
    public class PlayerProgressService : IPlayerProgressService, IInitializable
    {
        private readonly PlayerDataStore store;
        private PlayerData data = new PlayerData();

        public PlayerProgressService(PlayerDataStore store)
        {
            this.store = store;
        }

        public event Action SettingsChanged;

        public int HighestUnlockedLevel => data.highestUnlockedLevel;
        public int SelectedLevelNumber => data.selectedLevel;
        public bool SoundEnabled => data.soundEnabled;
        public bool MusicEnabled => data.musicEnabled;
        public bool HapticsEnabled => data.hapticsEnabled;

        public void Initialize()
        {
            data = store.Load();
            data.highestUnlockedLevel = Mathf.Max(1, data.highestUnlockedLevel);
            data.selectedLevel = Mathf.Clamp(data.selectedLevel, 1, data.highestUnlockedLevel);
        }

        public bool IsLevelUnlocked(int level) => level >= 1 && level <= data.highestUnlockedLevel;

        public void SetSelectedLevel(int level)
        {
            level = Mathf.Clamp(level, 1, data.highestUnlockedLevel);
            if (level == data.selectedLevel)
                return;

            data.selectedLevel = level;
            store.Save(data);
        }

        public void UnlockThrough(int level)
        {
            if (level <= data.highestUnlockedLevel)
                return;

            data.highestUnlockedLevel = level;
            store.Save(data);
        }

        public void SetSoundEnabled(bool enabled)
        {
            if (data.soundEnabled == enabled)
                return;

            data.soundEnabled = enabled;
            SaveSettings();
        }

        public void SetMusicEnabled(bool enabled)
        {
            if (data.musicEnabled == enabled)
                return;

            data.musicEnabled = enabled;
            SaveSettings();
        }

        public void SetHapticsEnabled(bool enabled)
        {
            if (data.hapticsEnabled == enabled)
                return;

            data.hapticsEnabled = enabled;
            SaveSettings();
        }

        private void SaveSettings()
        {
            store.Save(data);
            SettingsChanged?.Invoke();
        }
    }
}
