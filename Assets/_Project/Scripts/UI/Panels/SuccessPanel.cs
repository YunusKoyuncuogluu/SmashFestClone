using Cysharp.Threading.Tasks;
using SmashFast.Flow;
using SmashFast.Scenes;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace SmashFast.UI.Panels
{
    public class SuccessPanel : UIPanelBase
    {
        [SerializeField] private Button nextLevelButton;
        [SerializeField] private Button mainMenuButton;

        private ISceneFlowManager sceneFlow;
        private GameSessionSlot sessionSlot;
        private bool isBusy;

        [Inject]
        public void Construct(ISceneFlowManager sceneFlow, GameSessionSlot sessionSlot)
        {
            this.sceneFlow = sceneFlow;
            this.sessionSlot = sessionSlot;
        }

        protected override void OnPanelOpened()
        {
            isBusy = false;
        }

        private void Awake()
        {
            if (nextLevelButton != null)
            {
                nextLevelButton.onClick.AddListener(HandleNextLevel);
            }

            if (mainMenuButton != null)
            {
                mainMenuButton.onClick.AddListener(HandleMainMenu);
            }
        }

        private void HandleNextLevel()
        {
            if (isBusy || sessionSlot == null || sessionSlot.Current == null)
            {
                return;
            }

            isBusy = true;
            sessionSlot.Current.LoadNextLevel();
            isBusy = false;
        }

        private void HandleMainMenu()
        {
            if (isBusy || sceneFlow == null)
            {
                return;
            }

            isBusy = true;
            ReturnToMenuAsync().Forget();
        }

        private async UniTaskVoid ReturnToMenuAsync()
        {
            await sceneFlow.LoadMainMenuAsync();
            isBusy = false;
        }
    }
}
