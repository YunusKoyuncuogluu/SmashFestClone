using System;
using SmashFast.Data;
using SmashFast.Gameplay.Cannon;
using SmashFast.Level;
using SmashFast.Progress;
using SmashFast.UI;
using SmashFast.UI.Panels;
using UnityEngine;
using Zenject;

namespace SmashFast.Scenes
{
    public class GameSceneController : IGameSceneSession, IInitializable, IDisposable, ITickable
    {
        private enum SessionPhase
        {
            Idle = 0,
            Playing = 1,
            Won = 2,
            Lost = 3
        }

        private readonly ILevelSpawner levelSpawner;
        private readonly UIManager uiManager;
        private readonly IPlayerProgressService progress;
        private readonly LevelDatabaseSO levelDatabase;
        private readonly GameSessionSlot sessionSlot;
        private readonly CannonController cannonController;
        private GameHudPanel hudPanel;
        private SessionPhase phase;
        private int destructiblesAtSpawn;
        private int shownAmmo = -1;
        private int shownMax = -1;

        public GameSceneController(
            ILevelSpawner levelSpawner,
            UIManager uiManager,
            IPlayerProgressService progress,
            LevelDatabaseSO levelDatabase,
            GameSessionSlot sessionSlot,
            [InjectOptional] CannonController cannonController)
        {
            this.levelSpawner = levelSpawner;
            this.uiManager = uiManager;
            this.progress = progress;
            this.levelDatabase = levelDatabase;
            this.sessionSlot = sessionSlot;
            this.cannonController = cannonController;
        }

        public int LevelNumber
        {
            get
            {
                if (progress == null)
                {
                    return 1;
                }

                return progress.SelectedLevelNumber;
            }
        }

        public int RemainingAmmo
        {
            get
            {
                if (cannonController == null)
                {
                    return 0;
                }

                return cannonController.RemainingAmmo;
            }
        }

        public int MaxAmmo
        {
            get
            {
                if (cannonController == null)
                {
                    return 0;
                }

                return cannonController.MaxAmmo;
            }
        }

        public void Initialize()
        {
            sessionSlot.Register(this);
            BeginLevel(LevelNumber);
        }

        public void Dispose()
        {
            phase = SessionPhase.Idle;
            if (cannonController != null)
            {
                cannonController.SetInputEnabled(false);
            }

            sessionSlot.Clear(this);
        }

        public void Tick()
        {
            if (phase != SessionPhase.Playing || levelSpawner == null)
            {
                return;
            }

            RefreshHud();
            if (destructiblesAtSpawn > 0 && levelSpawner.ActiveDestructibleCount <= 0)
            {
                TriggerWin();
                return;
            }

            if (cannonController == null)
            {
                return;
            }

            if (cannonController.RemainingAmmo > 0 || cannonController.ActiveProjectileCount > 0)
            {
                return;
            }

            TriggerFail();
        }

        public void RetryLevel()
        {
            BeginLevel(LevelNumber);
        }

        public void LoadNextLevel()
        {
            int nextNumber = LevelNumber + 1;
            progress.UnlockThrough(nextNumber);
            progress.SetSelectedLevel(nextNumber);
            BeginLevel(nextNumber);
        }

        public void SetGameplayInputEnabled(bool enabled)
        {
            if (phase != SessionPhase.Playing || cannonController == null)
            {
                return;
            }

            cannonController.SetInputEnabled(enabled);
        }

        public void TriggerWin()
        {
            if (phase != SessionPhase.Playing)
            {
                return;
            }

            phase = SessionPhase.Won;
            if (cannonController != null)
            {
                cannonController.SetInputEnabled(false);
            }

            progress.UnlockThrough(LevelNumber + 1);
            if (uiManager == null)
            {
                return;
            }

            uiManager.HidePopup<SettingsPopup>();
            uiManager.ShowPanel<SuccessPanel>();
        }

        public void TriggerFail()
        {
            if (phase != SessionPhase.Playing)
            {
                return;
            }

            phase = SessionPhase.Lost;
            if (cannonController != null)
            {
                cannonController.SetInputEnabled(false);
            }

            if (uiManager == null)
            {
                return;
            }

            uiManager.HidePopup<SettingsPopup>();
            uiManager.ShowPanel<FailPanel>();
        }

        private void BeginLevel(int levelNumber)
        {
            if (levelDatabase == null || levelSpawner == null || progress == null)
            {
                return;
            }

            LevelData levelData = levelDatabase.GetLevelDataForInfiniteIndex(levelNumber);
            if (levelData == null)
            {
                return;
            }

            phase = SessionPhase.Idle;
            if (cannonController != null)
            {
                cannonController.SetInputEnabled(false);
            }

            levelSpawner.SpawnLevel(levelData, levelNumber);
            int ammo = levelSpawner.ResolvedMaxAmmo;
            if (cannonController != null)
            {
                cannonController.LoadAmmo(ammo);
                shownAmmo = cannonController.RemainingAmmo;
                shownMax = cannonController.MaxAmmo;
            }
            else
            {
                shownAmmo = ammo;
                shownMax = ammo;
            }

            destructiblesAtSpawn = levelSpawner.ActiveDestructibleCount;
            if (uiManager != null)
            {
                hudPanel = uiManager.ShowPanel<GameHudPanel>();
                if (hudPanel != null)
                {
                    hudPanel.SetLevelNumber(levelNumber);
                    hudPanel.SetAmmo(shownAmmo, shownMax);
                }
            }

            phase = SessionPhase.Playing;
            if (cannonController != null)
            {
                cannonController.SetInputEnabled(true);
            }
        }

        private void RefreshHud()
        {
            if (cannonController == null || hudPanel == null)
            {
                return;
            }

            int ammo = cannonController.RemainingAmmo;
            int max = cannonController.MaxAmmo;
            if (ammo == shownAmmo && max == shownMax)
            {
                return;
            }

            shownAmmo = ammo;
            shownMax = max;
            hudPanel.SetAmmo(ammo, max);
        }
    }
}
