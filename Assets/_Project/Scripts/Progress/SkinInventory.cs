namespace SmashFest.Progress
{
    public class SkinInventory : ISkinInventory
    {
        private readonly IPlayerDataProvider provider;

        public SkinInventory(IPlayerDataProvider provider) => this.provider = provider;

        public string SelectedSkinId => provider.Data.selectedSkin;

        public bool IsOwned(string skinId) => provider.Data.ownedSkins.Contains(skinId);

        public void Unlock(string skinId)
        {
            if (string.IsNullOrEmpty(skinId) || IsOwned(skinId))
                return;

            provider.Data.ownedSkins.Add(skinId);
            provider.Save();
        }

        public void Select(string skinId)
        {
            if (provider.Data.selectedSkin == skinId)
                return;

            provider.Data.selectedSkin = skinId;
            provider.Save();
        }
    }
}
