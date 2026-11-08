using SmashFast.Data;

namespace SmashFast.Level
{
    public interface ILevelSpawner
    {
        int ActiveDestructibleCount { get; }

        int ResolvedMaxAmmo { get; }

        void SpawnLevel(LevelData levelData, int displayLevelNumber);
    }

    public interface IDestructibleTracker
    {
        void NotifyDestructibleDespawned();
    }
}
