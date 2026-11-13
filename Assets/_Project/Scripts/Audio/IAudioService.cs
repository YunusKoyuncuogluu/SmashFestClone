using SmashFest.Data;

namespace SmashFest.Audio
{
    public interface IAudioService
    {
        void PlayClick();

        void PlayCannonFire();

        void PlayBallThud(float strength);

        void PlayHit(BlockMaterialType material, float strength);

        void PlayBreak(BlockMaterialType material);

        void PlayLevelWin();

        void PlayLevelFail();
    }
}
