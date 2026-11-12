using Cysharp.Threading.Tasks;
using SmashFast.Flow;
using SmashFast.Scenes;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace SmashFast.UI.Panels
{
    public class FailPanel : UIPanelBase
    {
        [SerializeField] private Button retryButton;
        [SerializeField] private Button mainMenuButton;

        private ISceneFlowManager sceneFlow;
        private GameSessionSlot session;
        private bool isBusy;

        [Inject]
        public void Construct(ISceneFlowManager sceneFlow, GameSessionSlot session)
        {
            this.sceneFlow = sceneFlow;
            this.session = session;
        }

        private void Awake()
        {
            if (retryButton != null)
                retryButton.onClick.AddListener(Retry);

            if (mainMenuButton != null)
                mainMenuButton.onClick.AddListener(() => GoToMainMenu().Forget());
        }

        protected override void OnPanelOpened() => isBusy = false;

        private void Retry()
        {
            if (!isBusy)
                session.Current?.RetryLevel();
        }

        private async UniTaskVoid GoToMainMenu()
        {
            if (isBusy)
                return;

            isBusy = true;
            await sceneFlow.LoadMainMenuAsync();
            isBusy = false;
        }
    }
}
