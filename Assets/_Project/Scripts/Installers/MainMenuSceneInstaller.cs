using SmashFest.UI;
using SmashFest.UI.Panels;
using Zenject;

namespace SmashFest.Installers
{
    public class MainMenuSceneInstaller : MonoInstaller
    {
        private IUIManager uiManager;

        [Inject]
        public void Construct(IUIManager uiManager) => this.uiManager = uiManager;

        public override void InstallBindings()
        {
        }

        public override void Start()
        {
            base.Start();
            if (uiManager != null)
                uiManager.ShowPanel<MainMenuPanel>();
        }
    }
}
