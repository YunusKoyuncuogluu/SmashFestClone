namespace SmashFest.Progress
{
    public interface IPlayerDataProvider
    {
        PlayerData Data { get; }

        void Save();
    }
}
