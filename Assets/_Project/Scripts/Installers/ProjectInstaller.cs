using SmashFest.Audio;
using SmashFest.Core.Effects;
using SmashFest.Core.Feel;
using SmashFest.Core.Performance;
using SmashFest.Core.Pooling;
using SmashFest.Data;
using SmashFest.Flow;
using SmashFest.Progress;
using SmashFest.Scenes;
using SmashFest.Shop;
using SmashFest.UI;
using UnityEngine;
using Zenject;

namespace SmashFest.Installers
{
    public class ProjectInstaller : MonoInstaller
    {
        [SerializeField] private LevelDatabaseSO levelDatabase;
        [SerializeField] private PoolConfigSO poolConfig;
        [SerializeField] private UICatalogSO uiCatalog;
        [SerializeField] private SoundLibrarySO soundLibrary;
        [SerializeField] private BallSkinCatalogSO ballSkinCatalog;
        [SerializeField] private FrameRateSettings frameRate = new FrameRateSettings();
        [SerializeField] private Transform poolRoot;
        [SerializeField] private SoundEmitter soundEmitterPrefab;

        public override void InstallBindings()
        {
            BindData();
            BindUserInterface();
            Container.BindInstance(frameRate).AsSingle();
            Container.BindInterfacesAndSelfTo<FrameRateService>().AsSingle().NonLazy();
            Container.Bind<GameSessionSlot>().AsSingle();
            Container.Bind<PlayerDataStore>().AsSingle();
            Container.BindInterfacesAndSelfTo<PlayerProgressService>().AsSingle().NonLazy();
            Container.BindInterfacesAndSelfTo<CoinWallet>().AsSingle();
            Container.BindInterfacesAndSelfTo<SkinInventory>().AsSingle();
            Container.BindInterfacesAndSelfTo<SoundEmitterPool>().AsSingle().WithArguments(soundEmitterPrefab, poolRoot);
            Container.BindInterfacesAndSelfTo<AudioService>().AsSingle().NonLazy();
            Container.BindInterfacesAndSelfTo<HapticService>().AsSingle().NonLazy();
            Container.BindInstance(poolRoot).WithId(PrefabPool.RootBindingId);
            Container.BindInterfacesAndSelfTo<PrefabPool>().AsSingle().NonLazy();
            Container.BindInterfacesAndSelfTo<SceneFlowManager>().AsSingle().NonLazy();
            Container.BindInterfacesAndSelfTo<InitBootstrapper>().AsSingle().NonLazy();

            Container.BindExecutionOrder<FrameRateService>(-40);
            Container.BindExecutionOrder<PlayerProgressService>(-30);
            Container.BindExecutionOrder<AudioService>(-25);
            Container.BindExecutionOrder<PrefabPool>(-20);
            Container.BindExecutionOrder<InitBootstrapper>(10);
        }

        private void BindData()
        {
            if (levelDatabase != null)
            {
                Container.Bind<LevelDatabaseSO>().FromInstance(levelDatabase).AsSingle();
                Container.Bind<JsonLevelRepository>().FromMethod(_ => levelDatabase.Repository).AsSingle();
                if (levelDatabase.BlockCatalog != null)
                    Container.Bind<BlockCatalogSO>().FromInstance(levelDatabase.BlockCatalog).AsSingle();
            }

            if (poolConfig != null)
                Container.Bind<PoolConfigSO>().FromInstance(poolConfig).AsSingle();

            if (uiCatalog != null)
                Container.Bind<UICatalogSO>().FromInstance(uiCatalog).AsSingle();

            if (soundLibrary != null)
                Container.Bind<SoundLibrarySO>().FromInstance(soundLibrary).AsSingle();

            if (ballSkinCatalog != null)
            {
                Container.Bind<BallSkinCatalogSO>().FromInstance(ballSkinCatalog).AsSingle();
                Container.Bind<IBallSkinShop>().To<BallSkinShop>().AsSingle();
            }
        }

        private void BindUserInterface()
        {
            if (uiCatalog != null && uiCatalog.CanvasPrefab != null)
                Container.Bind<IUIManager>().To<UIManager>().FromComponentInNewPrefab(uiCatalog.CanvasPrefab).AsSingle().NonLazy();
        }
    }
}
