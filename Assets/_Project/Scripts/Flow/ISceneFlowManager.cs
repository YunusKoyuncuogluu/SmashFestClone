using Cysharp.Threading.Tasks;

namespace SmashFast.Flow
{
    public interface ISceneFlowManager
    {
        UniTask LoadMainMenuAsync();

        UniTask LoadGameSceneAsync(int levelNumber);
    }
}
