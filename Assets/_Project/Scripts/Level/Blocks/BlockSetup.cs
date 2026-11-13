using SmashFest.Core.Effects;
using SmashFest.Core.Pooling;
using UnityEngine;

namespace SmashFest.Level
{
    public struct BlockSetup
    {
        public Vector3 LayoutScale;
        public float GlobalScale;
        public float TableHeightFactor;
        public Vector3Int SizeInCells;
        public float MassMultiplier;
        public IDestructibleTracker Tracker;
        public IPrefabPool Pool;
        public IImpactFeedback Feedback;
    }
}
