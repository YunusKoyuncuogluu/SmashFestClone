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
        private readonly IUniversalObjectPool objectPool;
        private readonly UIManager uiManager;
        private readonly IPlayerProgressService progress;
        private bool isLoading;

        public SceneFlowManager(
            IUniversalObjectPool objectPool,
            UIManager uiManager,
            IPlayerProgressService progress)
        {
            this.objectPool = objectPool;
            this.uiManager = uiManager;
            this.progress = progress;
        }

        public async UniTask LoadMainMenuAsync()
        {
            if (isLoading)
            {
                return;
            }

            isLoading = true;
            try
            {
                PrepareForSceneChange();
                bool loaded = await LoadSceneAsync(SceneNames.MainMenuScene);
                if (!loaded)
                {
                    return;
                }

                uiManager.RemoveDuplicateEventSystems();
                uiManager.ShowPanel<MainMenuPanel>();
            }
            finally
            {
                isLoading = false;
            }
        }

        public async UniTask LoadGameSceneAsync(int levelNumber)
        {
            if (isLoading)
            {
                return;
            }

            isLoading = true;
            try
            {
                progress.SetSelectedLevel(levelNumber);
                PrepareForSceneChange();
                bool loaded = await LoadSceneAsync(SceneNames.GameScene);
                if (!loaded)
                {
                    return;
                }

                uiManager.RemoveDuplicateEventSystems();
                uiManager.ShowPanel<GameHudPanel>();
            }
            finally
            {
                isLoading = false;
            }
        }

        private void PrepareForSceneChange()
        {
            if (objectPool != null)
            {
                objectPool.DespawnAllActive();
            }

            if (uiManager != null)
            {
                uiManager.HideAllPanels();
            }
        }

        private async UniTask<bool> LoadSceneAsync(string sceneName)
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                Debug.LogError(sceneName + " is missing from the build settings.");
                return false;
            }

            await operation.ToUniTask();
            return true;
        }
    }
}
