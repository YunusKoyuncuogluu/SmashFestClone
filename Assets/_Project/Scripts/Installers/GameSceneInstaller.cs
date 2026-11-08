using SmashFast.Gameplay.Cannon;
using SmashFast.Level;
using SmashFast.Scenes;
using UnityEngine;
using Zenject;

namespace SmashFast.Installers
{
    public class GameSceneInstaller : MonoInstaller
    {
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private CannonController cannonController;

        public override void InstallBindings()
        {
            BindCamera();
            BindCannon();
            Container.BindInterfacesAndSelfTo<LevelSpawner>().AsSingle();
            Container.BindInterfacesAndSelfTo<GameSceneController>().AsSingle().NonLazy();
        }

        private void BindCamera()
        {
            Camera sceneCamera = gameplayCamera;
            if (sceneCamera == null)
            {
                sceneCamera = Camera.main;
            }

            if (sceneCamera == null)
            {
                return;
            }

            Container.Bind<Camera>().FromInstance(sceneCamera).AsSingle();
        }

        private void BindCannon()
        {
            CannonController sceneCannon = cannonController;
            if (sceneCannon == null)
            {
                sceneCannon = FindAnyObjectByType<CannonController>();
            }

            if (sceneCannon == null)
            {
                Debug.LogWarning("GameSceneInstaller could not find a CannonController.");
                return;
            }

            Container.Bind<CannonController>().FromInstance(sceneCannon).AsSingle();
            Container.QueueForInject(sceneCannon);
        }
    }
}
