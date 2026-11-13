using Cysharp.Threading.Tasks;
using SmashFest.Core.Pooling;
using SmashFest.Progress;
using SmashFest.UI;
using SmashFest.UI.Panels;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SmashFest.Flow
{
    public class SceneFlowManager : ISceneFlowManager
    {
        private readonly IPrefabPool pool;
        private readonly IUIManager ui;
        private readonly IPlayerProgressService progress;
        private bool isLoading;

        public SceneFlowManager(IPrefabPool pool, IUIManager ui, IPlayerProgressService progress)
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
            ui.HidePopups();

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null)
                return false;

            await operation.ToUniTask();
            return true;
        }
    }
}
