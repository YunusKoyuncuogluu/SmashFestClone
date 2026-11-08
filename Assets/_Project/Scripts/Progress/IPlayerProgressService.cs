namespace SmashFast.Progress
{
    public interface IPlayerProgressService
    {
        int HighestUnlockedLevel { get; }

        int SelectedLevelNumber { get; }

        bool SoundEnabled { get; }

        bool HapticsEnabled { get; }

        bool IsLevelUnlocked(int levelNumber);

        void SetSelectedLevel(int levelNumber);

        void UnlockThrough(int levelNumber);

        void SetSoundEnabled(bool enabled);

        void SetHapticsEnabled(bool enabled);
    }
}
