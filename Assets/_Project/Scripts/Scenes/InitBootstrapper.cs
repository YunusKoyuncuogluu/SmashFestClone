using Cysharp.Threading.Tasks;
using SmashFast.Flow;
using UnityEngine.SceneManagement;
using Zenject;

namespace SmashFast.Scenes
{
    public class InitBootstrapper : IInitializable
    {
        private readonly ISceneFlowManager sceneFlow;

        public InitBootstrapper(ISceneFlowManager sceneFlow)
        {
            this.sceneFlow = sceneFlow;
        }

        public void Initialize()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.name != SceneNames.InitScene)
            {
                return;
            }

            sceneFlow.LoadMainMenuAsync().Forget();
        }
    }
}
