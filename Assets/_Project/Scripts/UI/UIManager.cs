using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Zenject;
using Object = UnityEngine.Object;

namespace SmashFast.UI
{
    public class UIManager : MonoBehaviour
    {
        [SerializeField] private UICatalogSO catalog;
        [SerializeField] private RectTransform panelsRoot;
        [SerializeField] private RectTransform popupsRoot;

        private readonly Dictionary<Type, UIPanelBase> prefabs = new Dictionary<Type, UIPanelBase>(8);
        private readonly Dictionary<Type, UIPanelBase> instances = new Dictionary<Type, UIPanelBase>(8);
        private readonly List<UIPanelBase> openPopups = new List<UIPanelBase>(4);

        private DiContainer container;
        private UIPanelBase activePanel;
        private bool isReady;

        [Inject]
        public void Construct(DiContainer container, [InjectOptional] UICatalogSO injectedCatalog)
        {
            this.container = container;
            if (catalog == null)
                catalog = injectedCatalog;
        }

        private void Awake() => Setup();

        public T ShowPanel<T>() where T : UIPanelBase
        {
            Setup();
            HidePopups();
            T panel = GetOrCreate<T>(panelsRoot);
            if (panel == null)
                return null;

            if (activePanel != null && activePanel != panel)
                activePanel.HideAsync().Forget();

            activePanel = panel;
            panel.transform.SetParent(panelsRoot, false);
            panel.ShowAsync().Forget();
            return panel;
        }

        public T ShowPopup<T>() where T : UIPanelBase
        {
            Setup();
            T popup = GetOrCreate<T>(popupsRoot);
            if (popup == null)
                return null;

            popup.transform.SetParent(popupsRoot, false);
            popup.transform.SetAsLastSibling();
            if (!openPopups.Contains(popup))
                openPopups.Add(popup);

            popup.ShowAsync().Forget();
            return popup;
        }

        public void HidePopup<T>() where T : UIPanelBase
        {
            Setup();
            if (!instances.TryGetValue(typeof(T), out UIPanelBase popup) || popup == null)
                return;

            openPopups.Remove(popup);
            popup.HideAsync().Forget();
        }

        public void HideAllPanels()
        {
            Setup();
            HidePopups();
            if (activePanel == null)
                return;

            UIPanelBase panel = activePanel;
            activePanel = null;
            panel.HideAsync().Forget();
        }

        public void RemoveDuplicateEventSystems()
        {
            EventSystem[] systems = FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (systems.Length == 0)
                return;

            EventSystem keeper = Array.Find(systems, system => system.gameObject.scene.name == "DontDestroyOnLoad");
            if (keeper == null)
            {
                keeper = systems[0];
                if (Application.isPlaying)
                    DontDestroyOnLoad(keeper.gameObject);
            }

            foreach (EventSystem system in systems)
            {
                if (system != keeper)
                    Destroy(system.gameObject);
            }
        }

        private void Setup()
        {
            if (isReady)
                return;

            isReady = true;
            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);

            FitCanvas();
            SetupEventSystem();
            RegisterPrefabs();
        }

        private void FitCanvas()
        {
            if (transform is RectTransform rect)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.pivot = new Vector2(0.5f, 0.5f);
                if (rect.localScale.sqrMagnitude < 0.0001f)
                    rect.localScale = Vector3.one;
            }

            if (!TryGetComponent(out Canvas canvas))
                canvas = gameObject.AddComponent<Canvas>();

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            if (!TryGetComponent(out GraphicRaycaster _))
                gameObject.AddComponent<GraphicRaycaster>();
        }

        private void RegisterPrefabs()
        {
            if (catalog == null)
                return;

            RegisterPrefab(catalog.MainMenuPanelPrefab);
            RegisterPrefab(catalog.GameHudPanelPrefab);
            RegisterPrefab(catalog.SuccessPanelPrefab);
            RegisterPrefab(catalog.FailPanelPrefab);
            RegisterPrefab(catalog.SettingsPopupPrefab);
        }

        private void RegisterPrefab(UIPanelBase prefab)
        {
            if (prefab != null)
                prefabs[prefab.GetType()] = prefab;
        }

        private T GetOrCreate<T>(Transform parent) where T : UIPanelBase
        {
            Type type = typeof(T);
            if (instances.TryGetValue(type, out UIPanelBase existing) && existing != null)
                return (T)existing;

            if (!prefabs.TryGetValue(type, out UIPanelBase prefab) || prefab == null)
                return null;

            if (container == null || parent == null)
                return null;

            GameObject instance = container.InstantiatePrefab(prefab.gameObject, parent);
            T panel = instance.GetComponentInChildren<T>(true);
            if (panel == null)
            {
                Destroy(instance);
                return null;
            }

            instances[type] = panel;
            return panel;
        }

        private void HidePopups()
        {
            for (int i = openPopups.Count - 1; i >= 0; i--)
            {
                UIPanelBase popup = openPopups[i];
                openPopups.RemoveAt(i);
                if (popup != null)
                    popup.HideAsync().Forget();
            }
        }

        private void SetupEventSystem()
        {
            EventSystem eventSystem = FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                eventSystem = new GameObject("EventSystem").AddComponent<EventSystem>();
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }
            else if (!eventSystem.TryGetComponent(out InputSystemUIInputModule _))
            {
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }

            if (Application.isPlaying)
                DontDestroyOnLoad(eventSystem.gameObject);
        }
    }
}
