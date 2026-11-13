using Cysharp.Threading.Tasks;
using SmashFest.Flow;
using SmashFest.UI;
using SmashFest.UI.Panels;
using UnityEngine.SceneManagement;
using Zenject;

namespace SmashFest.Scenes
{
    public class InitBootstrapper : IInitializable
    {
        private readonly ISceneFlowManager sceneFlow;
        private readonly IUIManager ui;

        public InitBootstrapper(ISceneFlowManager sceneFlow, IUIManager ui)
        {
            this.sceneFlow = sceneFlow;
            this.ui = ui;
        }

        public void Initialize()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.name != SceneNames.InitScene)
                return;

            StartAsync().Forget();
        }

        private async UniTaskVoid StartAsync()
        {
            SplashPanel splash = ui.ShowPanel<SplashPanel>();
            if (splash != null)
                await splash.PlayAsync();

            await sceneFlow.LoadMainMenuAsync();
        }
    }
}
