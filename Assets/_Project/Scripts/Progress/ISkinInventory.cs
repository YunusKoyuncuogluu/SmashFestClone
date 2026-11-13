namespace SmashFest.Progress
{
    public interface ISkinInventory
    {
        string SelectedSkinId { get; }

        bool IsOwned(string skinId);

        void Unlock(string skinId);

        void Select(string skinId);
    }
}
