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

        private UIManager ui;
        private ISceneFlowManager sceneFlow;
        private IPlayerProgressService progress;
        private GameSessionSlot session;
        private bool isBusy;

        private bool IsInGame => session.Current != null;

        [Inject]
        public void Construct(UIManager ui, ISceneFlowManager sceneFlow, IPlayerProgressService progress, GameSessionSlot session)
        {
            this.ui = ui;
            this.sceneFlow = sceneFlow;
            this.progress = progress;
            this.session = session;
        }

        private void Awake()
        {
            if (soundToggle != null)
                soundToggle.onValueChanged.AddListener(enabled => progress.SetSoundEnabled(enabled));

            if (hapticsToggle != null)
                hapticsToggle.onValueChanged.AddListener(enabled => progress.SetHapticsEnabled(enabled));

            if (closeButton != null)
                closeButton.onClick.AddListener(Close);

            if (homeButton != null)
            {
                homeButton.onClick.AddListener(() => GoHome().Forget());
                homeButton.gameObject.SetActive(false);
            }
        }

        protected override void OnPanelOpened()
        {
            isBusy = false;
            if (soundToggle != null)
                soundToggle.SetIsOnWithoutNotify(progress.SoundEnabled);

            if (hapticsToggle != null)
                hapticsToggle.SetIsOnWithoutNotify(progress.HapticsEnabled);

            if (homeButton != null)
                homeButton.gameObject.SetActive(IsInGame);

            SetGameplayInput(false);
        }

        protected override void OnPanelClosed() => SetGameplayInput(true);

        private void SetGameplayInput(bool enabled) => session.Current?.SetGameplayInputEnabled(enabled);

        private void Close() => ui.HidePopup<SettingsPopup>();

        private async UniTaskVoid GoHome()
        {
            if (isBusy)
                return;

            isBusy = true;
            await sceneFlow.LoadMainMenuAsync();
            isBusy = false;
        }
    }
}
