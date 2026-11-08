using Cysharp.Threading.Tasks;
using SmashFast.Flow;
using SmashFast.Progress;
using SmashFast.Scenes;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace SmashFast.UI.Panels
{
    public class SettingsPopup : UIPanelBase
    {
        [SerializeField] private Toggle soundToggle;
        [SerializeField] private Toggle hapticsToggle;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button homeButton;

        private UIManager uiManager;
        private ISceneFlowManager sceneFlow;
        private IPlayerProgressService progress;
        private GameSessionSlot sessionSlot;
        private bool isBusy;

        [Inject]
        public void Construct(
            UIManager uiManager,
            ISceneFlowManager sceneFlow,
            IPlayerProgressService progress,
            GameSessionSlot sessionSlot)
        {
            this.uiManager = uiManager;
            this.sceneFlow = sceneFlow;
            this.progress = progress;
            this.sessionSlot = sessionSlot;
        }

        protected override void OnPanelOpened()
        {
            isBusy = false;
            if (progress != null && soundToggle != null)
            {
                soundToggle.SetIsOnWithoutNotify(progress.SoundEnabled);
            }

            if (progress != null && hapticsToggle != null)
            {
                hapticsToggle.SetIsOnWithoutNotify(progress.HapticsEnabled);
            }

            SetGameplayInput(false);
            RefreshHomeButton();
        }

        private void SetGameplayInput(bool enabled)
        {
            if (sessionSlot == null || sessionSlot.Current == null)
            {
                return;
            }

            sessionSlot.Current.SetGameplayInputEnabled(enabled);
        }

        protected override void OnPanelClosed()
        {
            SetGameplayInput(true);
        }

        private void Awake()
        {
            if (soundToggle != null)
            {
                soundToggle.onValueChanged.AddListener(HandleSoundChanged);
            }

            if (hapticsToggle != null)
            {
                hapticsToggle.onValueChanged.AddListener(HandleHapticsChanged);
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(HandleClose);
            }

            if (homeButton != null)
            {
                homeButton.onClick.AddListener(HandleHome);
                homeButton.gameObject.SetActive(false);
            }
        }

        private void RefreshHomeButton()
        {
            if (homeButton == null)
            {
                return;
            }

            bool inGame = sessionSlot != null && sessionSlot.Current != null;
            homeButton.gameObject.SetActive(inGame);
        }

        private void HandleSoundChanged(bool enabled)
        {
            if (progress == null)
            {
                return;
            }

            progress.SetSoundEnabled(enabled);
        }

        private void HandleHapticsChanged(bool enabled)
        {
            if (progress == null)
            {
                return;
            }

            progress.SetHapticsEnabled(enabled);
        }

        private void HandleClose()
        {
            if (uiManager == null)
            {
                return;
            }

            uiManager.HidePopup<SettingsPopup>();
        }

        private void HandleHome()
        {
            if (isBusy || sceneFlow == null)
            {
                return;
            }

            isBusy = true;
            GoHomeAsync().Forget();
        }

        private async UniTaskVoid GoHomeAsync()
        {
            await sceneFlow.LoadMainMenuAsync();
            isBusy = false;
        }
    }
}
