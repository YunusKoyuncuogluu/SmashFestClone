using System;

namespace SmashFest.Progress
{
    public interface IPlayerProgressService
    {
        event Action SettingsChanged;

        int HighestUnlockedLevel { get; }

        int SelectedLevelNumber { get; }

        bool SoundEnabled { get; }

        bool MusicEnabled { get; }

        bool HapticsEnabled { get; }

        bool IsLevelUnlocked(int levelNumber);

        void SetSelectedLevel(int levelNumber);

        void UnlockThrough(int levelNumber);

        void SetSoundEnabled(bool enabled);

        void SetMusicEnabled(bool enabled);

        void SetHapticsEnabled(bool enabled);
    }
}
