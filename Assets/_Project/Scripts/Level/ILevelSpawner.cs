using System.Collections.Generic;
using SmashFest.Data;
using UnityEngine;

namespace SmashFest.Level
{
    public interface ILevelSpawner
    {
        int ActiveDestructibleCount { get; }

        int ResolvedMaxAmmo { get; }

        Bounds LevelBounds { get; }

        bool IsBonusLevel { get; }

        IReadOnlyList<SpawnedBlock> Blocks { get; }

        void EliminateFallenTargets();

        void SpawnLevel(LevelData levelData, int displayLevelNumber);
    }

    public interface IDestructibleTracker
    {
        void UnregisterTarget();
    }
}
