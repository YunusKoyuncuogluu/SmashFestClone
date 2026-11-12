using System;
using SmashFast.Data;
using SmashFast.Gameplay.Cannon;
using SmashFast.Level;
using SmashFast.Progress;
using SmashFast.UI;
using SmashFast.UI.Panels;
using Zenject;

namespace SmashFast.Scenes
{
    public class GameSceneController : IGameSceneSession, IInitializable, IDisposable, ITickable
    {
        private enum Phase
        {
            Idle,
            Playing,
            Won,
            Lost
        }

        private readonly ILevelSpawner spawner;
        private readonly UIManager ui;
        private readonly IPlayerProgressService progress;
        private readonly LevelDatabaseSO levelDatabase;
        private readonly GameSessionSlot sessionSlot;
        private readonly CannonController cannon;

        private GameHudPanel hud;
        private Phase phase;
        private int targetsAtStart;
        private int shownAmmo = -1;
        private int shownMaxAmmo = -1;

        public GameSceneController(
            ILevelSpawner spawner,
            UIManager ui,
            IPlayerProgressService progress,
            LevelDatabaseSO levelDatabase,
            GameSessionSlot sessionSlot,
            [InjectOptional] CannonController cannon)
        {
            this.spawner = spawner;
            this.ui = ui;
            this.progress = progress;
            this.levelDatabase = levelDatabase;
            this.sessionSlot = sessionSlot;
            this.cannon = cannon;
        }

        public int LevelNumber => progress.SelectedLevelNumber;
        public int RemainingAmmo => cannon != null ? cannon.RemainingAmmo : 0;
        public int MaxAmmo => cannon != null ? cannon.MaxAmmo : 0;

        public void Initialize()
        {
            sessionSlot.Register(this);
            StartLevel(LevelNumber);
        }

        public void Dispose()
        {
            phase = Phase.Idle;
            SetCannonInput(false);
            sessionSlot.Clear(this);
        }

        public void Tick()
        {
            if (phase != Phase.Playing)
                return;

            RefreshHud();

            if (targetsAtStart > 0 && spawner.ActiveDestructibleCount <= 0)
            {
                TriggerWin();
                return;
            }

            if (cannon != null && cannon.RemainingAmmo <= 0 && cannon.ActiveProjectileCount <= 0)
                TriggerFail();
        }

        public void RetryLevel() => StartLevel(LevelNumber);

        public void LoadNextLevel()
        {
            int nextLevel = LevelNumber + 1;
            progress.UnlockThrough(nextLevel);
            progress.SetSelectedLevel(nextLevel);
            StartLevel(nextLevel);
        }

        public void SetGameplayInputEnabled(bool enabled)
        {
            if (phase == Phase.Playing)
                SetCannonInput(enabled);
        }

        public void TriggerWin()
        {
            if (phase != Phase.Playing)
                return;

            progress.UnlockThrough(LevelNumber + 1);
            EndLevel(Phase.Won);
            ui.ShowPanel<SuccessPanel>();
        }

        public void TriggerFail()
        {
            if (phase != Phase.Playing)
                return;

            EndLevel(Phase.Lost);
            ui.ShowPanel<FailPanel>();
        }

        private void EndLevel(Phase result)
        {
            phase = result;
            SetCannonInput(false);
            ui.HidePopup<SettingsPopup>();
        }

        private void StartLevel(int level)
        {
            LevelData levelData = levelDatabase.GetLevelDataForInfiniteIndex(level);
            if (levelData == null)
                return;

            phase = Phase.Idle;
            SetCannonInput(false);

            spawner.SpawnLevel(levelData, level);
            targetsAtStart = spawner.ActiveDestructibleCount;

            int ammo = spawner.ResolvedMaxAmmo;
            if (cannon != null)
            {
                cannon.LoadAmmo(ammo);
                ammo = cannon.RemainingAmmo;
            }

            shownAmmo = ammo;
            shownMaxAmmo = ammo;

            hud = ui.ShowPanel<GameHudPanel>();
            if (hud != null)
            {
                hud.SetLevelNumber(level);
                hud.SetAmmo(shownAmmo, shownMaxAmmo);
            }

            phase = Phase.Playing;
            SetCannonInput(true);
        }

        private void RefreshHud()
        {
            if (cannon == null || hud == null)
                return;

            if (cannon.RemainingAmmo == shownAmmo && cannon.MaxAmmo == shownMaxAmmo)
                return;

            shownAmmo = cannon.RemainingAmmo;
            shownMaxAmmo = cannon.MaxAmmo;
            hud.SetAmmo(shownAmmo, shownMaxAmmo);
        }

        private void SetCannonInput(bool enabled)
        {
            if (cannon != null)
                cannon.SetInputEnabled(enabled);
        }
    }
}
