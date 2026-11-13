namespace SmashFest.Scenes
{
    public interface IGameSceneSession
    {
        int LevelNumber { get; }

        int RemainingAmmo { get; }

        int MaxAmmo { get; }

        void RetryLevel();

        void LoadNextLevel();

        void SetGameplayInputEnabled(bool enabled);

        void TriggerWin();

        void TriggerFail();
    }

    public class GameSessionSlot
    {
        public IGameSceneSession Current { get; private set; }

        public void Register(IGameSceneSession session)
        {
            Current = session;
        }

        public void Clear(IGameSceneSession session)
        {
            if (Current == session)
                Current = null;
        }
    }
}
