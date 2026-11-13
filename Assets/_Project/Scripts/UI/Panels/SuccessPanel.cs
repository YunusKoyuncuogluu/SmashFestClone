using Cysharp.Threading.Tasks;
using SmashFest.Flow;
using SmashFest.Scenes;
using UnityEngine;
using SmashFest.Progress;
using TMPro;
using UnityEngine.UI;
using Zenject;

namespace SmashFest.UI.Panels
{
    public class SuccessPanel : UIPanelBase
    {
        [SerializeField] private Button nextLevelButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private ConfettiBurst confetti;
        [SerializeField] private Image[] stars;
        [SerializeField] private TMP_Text rewardLabel;
        [SerializeField] private TMP_Text rewardDetailLabel;
        [SerializeField] private Color emptyStarColor = new Color(0.36f, 0.38f, 0.5f, 0.55f);

        private ISceneFlowManager sceneFlow;
        private GameSessionSlot session;
        private LevelResultReveal reveal;
        private bool isBusy;

        [Inject]
        public void Construct(ISceneFlowManager sceneFlow, GameSessionSlot session)
        {
            this.sceneFlow = sceneFlow;
            this.session = session;
        }

        private void Awake()
        {
            if (nextLevelButton != null)
                nextLevelButton.onClick.AddListener(NextLevel);

            if (mainMenuButton != null)
                mainMenuButton.onClick.AddListener(() => GoToMainMenu().Forget());

            if (stars != null && stars.Length > 0)
                reveal = new LevelResultReveal(stars, rewardLabel, rewardDetailLabel, emptyStarColor, gameObject);
        }

        public void ShowResult(LevelResult result) => reveal?.Play(result);

        protected override void OnPanelOpened()
        {
            isBusy = false;
            if (confetti != null)
                confetti.Play();
        }

        protected override void OnPanelClosed()
        {
            if (confetti != null)
                confetti.Stop();
        }

        private void NextLevel()
        {
            if (!isBusy)
                session.Current?.LoadNextLevel();
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
