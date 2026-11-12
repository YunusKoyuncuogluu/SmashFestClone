using SmashFast.UI;
using SmashFast.UI.Panels;
using UnityEngine;
using Zenject;

namespace SmashFast.Installers
{
    public class MainMenuSceneInstaller : MonoInstaller
    {
        private UIManager uiManager;

        [Inject]
        public void Construct(UIManager uiManager)
        {
            this.uiManager = uiManager;
        }

        public override void InstallBindings()
        {
        }

        private void Start()
        {
            if (uiManager == null)
            {
                return;
            }

            uiManager.ShowPanel<MainMenuPanel>();
        }
    }
}
