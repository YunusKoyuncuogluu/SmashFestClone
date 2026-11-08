using SmashFast.Core.Pooling;
using SmashFast.Data;
using SmashFast.Flow;
using SmashFast.Progress;
using SmashFast.Scenes;
using SmashFast.UI;
using UnityEngine;
using Zenject;

namespace SmashFast.Installers
{
    public class ProjectInstaller : MonoInstaller
    {
        [SerializeField] private LevelDatabaseSO levelDatabase;
        [SerializeField] private PoolConfigSO poolConfig;
        [SerializeField] private UICatalogSO uiCatalog;

        public override void InstallBindings()
        {
            if (levelDatabase != null)
            {
                Container.Bind<LevelDatabaseSO>().FromInstance(levelDatabase).AsSingle();
                if (levelDatabase.BlockCatalog != null)
                {
                    Container.Bind<BlockCatalogSO>().FromInstance(levelDatabase.BlockCatalog).AsSingle();
                }
            }
            else
            {
                Debug.LogError("ProjectInstaller is missing LevelDatabaseSO.");
            }

            if (poolConfig != null)
            {
                Container.Bind<PoolConfigSO>().FromInstance(poolConfig).AsSingle();
            }

            if (uiCatalog != null)
            {
                Container.Bind<UICatalogSO>().FromInstance(uiCatalog).AsSingle();
            }
            else
            {
                Debug.LogError("ProjectInstaller is missing UICatalogSO.");
            }

            BindUserInterface();
            Container.Bind<GameSessionSlot>().AsSingle();
            Container.BindInterfacesAndSelfTo<PlayerProgressService>().AsSingle().NonLazy();
            Container.BindInterfacesAndSelfTo<UniversalObjectPool>().AsSingle().NonLazy();
            Container.BindInterfacesAndSelfTo<SceneFlowManager>().AsSingle().NonLazy();
            Container.BindInterfacesAndSelfTo<InitBootstrapper>().AsSingle().NonLazy();

            Container.BindExecutionOrder<PlayerProgressService>(-30);
            Container.BindExecutionOrder<UniversalObjectPool>(-20);
            Container.BindExecutionOrder<InitBootstrapper>(10);
        }

        private void BindUserInterface()
        {
            if (Container.IsValidating)
            {
                Container.Bind<UIManager>().FromNewComponentOnNewGameObject().AsSingle();
                return;
            }

            if (uiCatalog == null || uiCatalog.CanvasPrefab == null)
            {
                Debug.LogError("ProjectInstaller is missing the UI canvas prefab.");
                return;
            }

            GameObject canvasObject = Container.InstantiatePrefab(uiCatalog.CanvasPrefab);
            UnityEngine.Object.DontDestroyOnLoad(canvasObject);
            UIManager manager = canvasObject.GetComponent<UIManager>();
            if (manager == null)
            {
                Debug.LogError("Canvas prefab is missing the UIManager component.");
                return;
            }

            Container.Bind<UIManager>().FromInstance(manager).AsSingle();
        }
    }
}
