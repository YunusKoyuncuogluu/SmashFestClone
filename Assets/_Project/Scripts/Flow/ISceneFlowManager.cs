using Cysharp.Threading.Tasks;

namespace SmashFest.Flow
{
    public interface ISceneFlowManager
    {
        UniTask LoadMainMenuAsync();

        UniTask LoadGameSceneAsync(int levelNumber);
    }
}
