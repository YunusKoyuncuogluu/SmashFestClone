using Cysharp.Threading.Tasks;
using SmashFast.Core.Pooling;
using SmashFast.Progress;
using SmashFast.UI;
using SmashFast.UI.Panels;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SmashFast.Flow
{
    public class SceneFlowManager : ISceneFlowManager
    {
        private readonly IUniversalObjectPool pool;
        private readonly UIManager ui;
        private readonly IPlayerProgressService progress;
        private bool isLoading;

        public SceneFlowManager(IUniversalObjectPool pool, UIManager ui, IPlayerProgressService progress)
        {
            this.pool = pool;
            this.ui = ui;
            this.progress = progress;
        }

        public async UniTask LoadMainMenuAsync()
        {
            if (isLoading)
                return;

            isLoading = true;
            try
            {
                if (await LoadSceneAsync(SceneNames.MainMenuScene))
                    ui.ShowPanel<MainMenuPanel>();
            }
            finally
            {
                isLoading = false;
            }
        }

        public async UniTask LoadGameSceneAsync(int level)
        {
            if (isLoading)
                return;

            isLoading = true;
            try
            {
                progress.SetSelectedLevel(level);
                if (await LoadSceneAsync(SceneNames.GameScene))
                    ui.ShowPanel<GameHudPanel>();
            }
            finally
            {
                isLoading = false;
            }
        }

        private async UniTask<bool> LoadSceneAsync(string sceneName)
        {
            pool.DespawnAllActive();
            ui.HideAllPanels();

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null)
                return false;

            await operation.ToUniTask();
            ui.RemoveDuplicateEventSystems();
            return true;
        }
    }
}
