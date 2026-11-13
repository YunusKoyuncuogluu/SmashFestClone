namespace SmashFest.Core.Feel
{
    public enum HapticPulse
    {
        Light,
        Medium,
        Heavy,
        Success,
        Failure,
        Soft,
        Rigid
    }

    public interface IHapticService
    {
        void Play(HapticPulse pulse);
    }
}
