using System.Collections.Generic;
using UnityEngine;

namespace SmashFest.Level
{
    public class BreakHintPicker
    {
        private const float FragileScore = 3f;
        private const float VisibleScore = 6f;
        private const float HeightScore = 1f;
        private const float DistancePenalty = 0.02f;

        private readonly ILevelSpawner spawner;
        private readonly Camera camera;

        public BreakHintPicker(ILevelSpawner spawner, Camera camera)
        {
            this.spawner = spawner;
            this.camera = camera;
        }

        public bool TryPick(out Vector3 hintPoint)
        {
            Physics.SyncTransforms();
            IReadOnlyList<SpawnedBlock> blocks = spawner.Blocks;
            Bounds level = spawner.LevelBounds;
            Vector3 eye = camera.transform.position;
            float bestScore = float.MinValue;
            hintPoint = default;

            for (int i = 0; i < blocks.Count; i++)
            {
                SpawnedBlock block = blocks[i];
                if (block == null || !block.IsStandingTarget)
                    continue;

                Bounds bounds = block.VisualBounds;
                float score = Score(block, bounds, level, eye);
                if (score <= bestScore)
                    continue;

                bestScore = score;
                hintPoint = bounds.center;
            }

            return bestScore > float.MinValue;
        }

        private float Score(SpawnedBlock block, Bounds bounds, Bounds level, Vector3 eye)
        {
            float score = block.BreaksOnBallHit ? FragileScore : 0f;
            if (IsVisible(block, bounds.center, eye))
                score += VisibleScore;

            float height = level.size.y > 0.01f ? (bounds.max.y - level.min.y) / level.size.y : 0f;
            score += HeightScore * height;
            score -= DistancePenalty * Vector3.Distance(eye, bounds.center);
            return score;
        }

        private bool IsVisible(SpawnedBlock block, Vector3 point, Vector3 eye)
        {
            Vector3 toPoint = point - eye;
            if (!Physics.Raycast(eye, toPoint, out RaycastHit hit, toPoint.magnitude + 0.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return false;

            return hit.transform == block.transform || hit.transform.IsChildOf(block.transform);
        }
    }
}
