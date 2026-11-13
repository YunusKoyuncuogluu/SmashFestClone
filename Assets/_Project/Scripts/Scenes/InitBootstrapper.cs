using Cysharp.Threading.Tasks;
using SmashFest.Flow;
using UnityEngine.SceneManagement;
using Zenject;

namespace SmashFest.Scenes
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
                return;

            sceneFlow.LoadMainMenuAsync().Forget();
        }
    }
}
