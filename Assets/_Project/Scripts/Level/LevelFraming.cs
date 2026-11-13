using System;
using SmashFest.Gameplay.Cannon;
using UnityEngine;
using Zenject;

namespace SmashFest.Level
{
    [Serializable]
    public class LevelFramingSettings
    {
        [Range(0f, 0.3f)] public float sideMargin = 0.05f;
        [Min(0f)] public float minGap = 2f;
    }

    public class LevelFraming
    {
        private const int SearchSteps = 24;
        private const float MaxDistance = 200f;

        private readonly LevelFramingSettings settings;
        private readonly Camera gameplayCamera;
        private readonly CannonController cannon;

        public LevelFraming(LevelFramingSettings settings, [InjectOptional] Camera gameplayCamera, [InjectOptional] CannonController cannon)
        {
            this.settings = settings;
            this.gameplayCamera = gameplayCamera;
            this.cannon = cannon;
        }

        public void Frame(Bounds level)
        {
            if (cannon == null)
                return;

            float closest = level.min.z - settings.minGap;
            float distance = gameplayCamera != null ? FindDistance(level, closest) : 0f;
            MoveCannon(closest - distance);
            cannon.SetGroundHeight(level.min.y);
            cannon.ResetAim();
        }

        private float FindDistance(Bounds level, float closest)
        {
            MoveCannon(closest - MaxDistance);
            if (!FitsWidth(level))
                return 0f;

            float near = 0f;
            float far = MaxDistance;
            for (int i = 0; i < SearchSteps; i++)
            {
                float distance = (near + far) * 0.5f;
                MoveCannon(closest - distance);
                if (FitsWidth(level))
                    far = distance;
                else
                    near = distance;
            }

            return far;
        }

        private void MoveCannon(float z)
        {
            Vector3 position = cannon.transform.position;
            position.z = z;
            cannon.transform.position = position;
        }

        private bool FitsWidth(Bounds level)
        {
            Vector3 min = level.min;
            Vector3 max = level.max;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
                Vector3 view = gameplayCamera.WorldToViewportPoint(corner);
                if (view.z <= gameplayCamera.nearClipPlane || view.x < settings.sideMargin || view.x > 1f - settings.sideMargin)
                    return false;
            }

            return true;
        }
    }
}
