using System;
using SmashFest.Audio;
using SmashFest.Core.Feel;
using SmashFest.Data;
using SmashFest.Environment;
using SmashFest.Gameplay.Cannon;
using SmashFest.Level;
using SmashFest.Progress;
using SmashFest.UI;
using SmashFest.UI.Panels;
using UnityEngine;
using Zenject;

namespace SmashFest.Scenes
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
        private readonly IUIManager ui;
        private readonly IPlayerProgressService progress;
        private readonly LevelDatabaseSO levelDatabase;
        private readonly GameSessionSlot sessionSlot;
        private readonly CannonController cannon;
        private readonly LevelFraming framing;
        private readonly IAudioService audio;
        private readonly ComboCounter combo;
        private readonly IHapticService haptics;
        private readonly ShotHintScheduler hints;
        private readonly ICoinWallet wallet;
        private readonly EnvironmentThemer themer;
        private readonly LevelThemeCatalogSO themes;
        private readonly LevelRewardRules rewardRules = new LevelRewardRules();

        private const float FailDelay = 0.75f;
        private const float WinDelay = 0.5f;
        private const float FallCheckInterval = 0.2f;

        private GameHudPanel hud;
        private Phase phase;
        private float outOfBallsTime = -1f;
        private float winAt = -1f;
        private float nextFallCheck;
        private int targetsAtStart;
        private int shownAmmo = -1;
        private int shownMaxAmmo = -1;
        private int shownShots;
        private bool isBonus;

        public GameSceneController(
            ILevelSpawner spawner,
            IUIManager ui,
            IPlayerProgressService progress,
            LevelDatabaseSO levelDatabase,
            GameSessionSlot sessionSlot,
            ComboCounter combo,
            ShotHintScheduler hints,
            ICoinWallet wallet,
            [InjectOptional] CannonController cannon,
            [InjectOptional] LevelFraming framing,
            [InjectOptional] IAudioService audio,
            [InjectOptional] IHapticService haptics,
            [InjectOptional] EnvironmentThemer themer,
            [InjectOptional] LevelThemeCatalogSO themes)
        {
            this.audio = audio;
            this.combo = combo;
            this.hints = hints;
            this.wallet = wallet;
            this.themer = themer;
            this.themes = themes;
            this.haptics = haptics;
            this.framing = framing;
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
        public bool HasUnlimitedAmmo => cannon != null && cannon.HasUnlimitedAmmo;

        public void Initialize()
        {
            sessionSlot.Register(this);
            combo.ComboReached += ShowCombo;
            StartLevel(LevelNumber);
        }

        public void Dispose()
        {
            phase = Phase.Idle;
            SetCannonInput(false);
            combo.ComboReached -= ShowCombo;
            sessionSlot.Clear(this);
        }

        public void Tick()
        {
            if (phase != Phase.Playing)
                return;

            RefreshHud();
            EliminateFallenTargets();
            if (TickPendingWin())
                return;

            if (RemainingAmmo > 0)
                hints.Tick(hud);

            if (cannon == null || cannon.RemainingAmmo > 0 || cannon.ActiveProjectileCount > 0)
            {
                outOfBallsTime = -1f;
                return;
            }

            if (outOfBallsTime < 0f)
                outOfBallsTime = Time.time;
            else if (Time.time - outOfBallsTime >= FailDelay)
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
            if (phase == Phase.Playing && winAt < 0f)
                SetCannonInput(enabled);
        }

        public void TriggerWin()
        {
            if (phase != Phase.Playing)
                return;

            LevelResult result = rewardRules.Evaluate(LevelNumber, RemainingAmmo, MaxAmmo, isBonus);
            progress.UnlockThrough(LevelNumber + 1);
            progress.RecordStars(result.Level, result.Stars);
            wallet.Add(result.TotalReward);
            EndLevel(Phase.Won);
            audio?.PlayLevelWin();
            haptics?.Play(HapticPulse.Success);
            SuccessPanel panel = ui.ShowPanel<SuccessPanel>();
            if (panel != null)
                panel.ShowResult(result);
        }

        public void TriggerFail()
        {
            if (phase != Phase.Playing)
                return;

            EndLevel(Phase.Lost);
            audio?.PlayLevelFail();
            haptics?.Play(HapticPulse.Failure);
            ui.ShowPanel<FailPanel>();
        }

        private void EndLevel(Phase result)
        {
            phase = result;
            hints.Stop(hud);
            SetCannonInput(false);
            ui.HidePopup<SettingsPopup>();
        }

        private void StartLevel(int level)
        {
            LevelData levelData = levelDatabase.GetLevelDataForInfiniteIndex(level);
            if (levelData == null)
                return;

            phase = Phase.Idle;
            outOfBallsTime = -1f;
            SetCannonInput(false);
            combo.Reset();
            winAt = -1f;

            if (themer != null)
                themer.Apply(themes.Resolve(level));

            spawner.SpawnLevel(levelData, level);
            framing?.Frame(spawner.LevelBounds);
            targetsAtStart = spawner.ActiveDestructibleCount;
            isBonus = spawner.IsBonusLevel;
            shownShots = 0;

            int ammo = spawner.ResolvedMaxAmmo;
            if (cannon != null)
            {
                cannon.LoadAmmo(ammo, isBonus);
                ammo = cannon.RemainingAmmo;
            }

            shownAmmo = ammo;
            shownMaxAmmo = ammo;

            hud = ui.ShowPanel<GameHudPanel>();
            if (hud != null)
            {
                hud.SetLevelNumber(level);
                if (isBonus)
                    hud.SetUnlimitedAmmo();
                else
                    hud.SetAmmo(shownAmmo, shownMaxAmmo);

                hud.PlayIntro(level, isBonus);
            }

            hints.Stop(hud);
            hints.Begin(level);
            phase = Phase.Playing;
            SetCannonInput(true);
        }

        private void RefreshHud()
        {
            if (cannon == null || hud == null)
                return;

            if (cannon.ShotsFired != shownShots)
            {
                shownShots = cannon.ShotsFired;
                hints.NotifyShot(hud);
            }

            if (isBonus || (cannon.RemainingAmmo == shownAmmo && cannon.MaxAmmo == shownMaxAmmo))
                return;

            shownAmmo = cannon.RemainingAmmo;
            shownMaxAmmo = cannon.MaxAmmo;
            hud.SetAmmo(shownAmmo, shownMaxAmmo);
        }

        private void EliminateFallenTargets()
        {
            if (Time.time < nextFallCheck)
                return;

            nextFallCheck = Time.time + FallCheckInterval;
            spawner.EliminateFallenTargets();
        }

        private bool TickPendingWin()
        {
            if (targetsAtStart <= 0 || spawner.ActiveDestructibleCount > 0)
                return false;

            if (winAt < 0f)
            {
                winAt = Time.time + WinDelay;
                hints.Stop(hud);
                SetCannonInput(false);
            }
            else if (Time.time >= winAt)
            {
                TriggerWin();
            }

            return true;
        }

        private void ShowCombo(int count)
        {
            if (phase == Phase.Playing && hud != null)
                hud.ShowCombo(count);
        }

        private void SetCannonInput(bool enabled)
        {
            if (cannon != null)
                cannon.SetInputEnabled(enabled);
        }
    }
}
