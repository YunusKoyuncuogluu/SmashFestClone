using SmashFest.Core.Effects;
using SmashFest.Gameplay.Cannon;
using SmashFest.Level;
using SmashFest.Scenes;
using UnityEngine;
using Zenject;

namespace SmashFest.Installers
{
    public class GameSceneInstaller : MonoInstaller
    {
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private CannonController cannonController;
        [SerializeField] private Collider groundCollider;
        [SerializeField] private Transform levelRoot;
        [SerializeField] private BreakParticles breakEffectsPrefab;
        [SerializeField] private LevelFramingSettings framing = new LevelFramingSettings();

        public Collider GroundCollider => groundCollider;

        public override void InstallBindings()
        {
            BindCamera();
            Container.Bind<ICannonInput>().To<PointerShotInput>().AsSingle();
            BindCannon();
            Container.Bind<Collider>().WithId(LevelSpawner.GroundBindingId).FromInstance(groundCollider).AsSingle();
            Container.BindInstance(levelRoot).WithId(LevelSpawner.RootBindingId);
            Container.Bind<BreakParticles>().FromComponentInNewPrefab(breakEffectsPrefab).AsSingle();
            Container.BindInstance(framing).AsSingle();
            Container.BindInterfacesAndSelfTo<ImpactFeedback>().AsSingle();
            Container.Bind<LevelFraming>().AsSingle();
            Container.BindInterfacesAndSelfTo<LevelSpawner>().AsSingle();
            Container.BindInterfacesAndSelfTo<GameSceneController>().AsSingle().NonLazy();
        }

        private void BindCamera() => Container.Bind<Camera>().FromInstance(gameplayCamera).AsSingle();

        private void BindCannon()
        {
            if (cannonController == null)
                return;

            Container.Bind<CannonController>().FromInstance(cannonController).AsSingle();
            Container.QueueForInject(cannonController);
        }
    }
}
