using SmashFest.Data;
using UnityEngine;

namespace SmashFest.Level
{
    public interface ILevelSpawner
    {
        int ActiveDestructibleCount { get; }

        int ResolvedMaxAmmo { get; }

        Bounds LevelBounds { get; }

        void SpawnLevel(LevelData levelData, int displayLevelNumber);
    }

    public interface IDestructibleTracker
    {
        void UnregisterTarget();
    }
}
