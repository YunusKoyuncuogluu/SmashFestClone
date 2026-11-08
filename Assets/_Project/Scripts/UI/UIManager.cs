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
        private const string PanelsRootName = "PanelsRoot";
        private const string PopupsRootName = "PopupsRoot";

        [SerializeField] private UICatalogSO catalog;

        private DiContainer container;
        private readonly Dictionary<Type, UIPanelBase> prefabsByType = new Dictionary<Type, UIPanelBase>(8);
        private readonly Dictionary<Type, UIPanelBase> instancesByType = new Dictionary<Type, UIPanelBase>(8);
        private readonly List<UIPanelBase> openPopups = new List<UIPanelBase>(4);
        private Transform panelsRoot;
        private Transform popupsRoot;
        private UIPanelBase activePanel;
        private bool hierarchyReady;

        [Inject]
        public void Construct(DiContainer injectedContainer, [InjectOptional] UICatalogSO injectedCatalog)
        {
            container = injectedContainer;
            if (catalog == null)
            {
                catalog = injectedCatalog;
            }
        }

        public T ShowPanel<T>() where T : UIPanelBase
        {
            EnsureHierarchy();
            HidePopups();
            T panel = GetOrSpawn<T>(panelsRoot);
            if (panel == null)
            {
                return null;
            }

            if (activePanel != null && activePanel != panel)
            {
                activePanel.HideAsync().Forget();
            }

            activePanel = panel;
            panel.transform.SetParent(panelsRoot, false);
            panel.ShowAsync().Forget();
            return panel;
        }

        public T ShowPopup<T>() where T : UIPanelBase
        {
            EnsureHierarchy();
            T popup = GetOrSpawn<T>(popupsRoot);
            if (popup == null)
            {
                return null;
            }

            popup.transform.SetParent(popupsRoot, false);
            popup.transform.SetAsLastSibling();
            TrackPopup(popup);
            popup.ShowAsync().Forget();
            return popup;
        }

        public void HidePopup<T>() where T : UIPanelBase
        {
            EnsureHierarchy();
            UIPanelBase popup;
            if (!instancesByType.TryGetValue(typeof(T), out popup) || popup == null)
            {
                return;
            }

            RemovePopup(popup);
            popup.HideAsync().Forget();
        }

        public void HideAllPanels()
        {
            EnsureHierarchy();
            HidePopups();
            if (activePanel == null)
            {
                return;
            }

            UIPanelBase panel = activePanel;
            activePanel = null;
            panel.HideAsync().Forget();
        }

        public void RemoveDuplicateEventSystems()
        {
            EventSystem[] systems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            EventSystem keeper = null;
            int count = systems.Length;
            for (int i = 0; i < count; i++)
            {
                EventSystem candidate = systems[i];
                if (candidate != null && candidate.gameObject.scene.name == "DontDestroyOnLoad")
                {
                    keeper = candidate;
                    break;
                }
            }

            if (keeper == null && count > 0)
            {
                keeper = systems[0];
                if (keeper != null && Application.isPlaying)
                {
                    Object.DontDestroyOnLoad(keeper.gameObject);
                }
            }

            for (int i = 0; i < count; i++)
            {
                EventSystem candidate = systems[i];
                if (candidate == null || candidate == keeper)
                {
                    continue;
                }

                Object.Destroy(candidate.gameObject);
            }
        }

        private void Awake()
        {
            EnsureHierarchy();
        }

        private void EnsureHierarchy()
        {
            if (hierarchyReady)
            {
                return;
            }

            hierarchyReady = true;
            if (Application.isPlaying)
            {
                Object.DontDestroyOnLoad(gameObject);
            }

            FitCanvas();
            EnsureEventSystem();
            panelsRoot = FindOrCreateRoot(transform, PanelsRootName);
            popupsRoot = FindOrCreateRoot(transform, PopupsRootName);
            RegisterPrefabs();
        }

        private void FitCanvas()
        {
            RectTransform rect = transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.pivot = new Vector2(0.5f, 0.5f);
                if (rect.localScale.sqrMagnitude < 0.0001f)
                {
                    rect.localScale = Vector3.one;
                }
            }

            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        private void RegisterPrefabs()
        {
            if (catalog == null)
            {
                Debug.LogError("UIManager has no UICatalogSO.");
                return;
            }

            RegisterPrefab(catalog.MainMenuPanelPrefab);
            RegisterPrefab(catalog.GameHudPanelPrefab);
            RegisterPrefab(catalog.SuccessPanelPrefab);
            RegisterPrefab(catalog.FailPanelPrefab);
            RegisterPrefab(catalog.SettingsPopupPrefab);
        }

        private void RegisterPrefab(UIPanelBase prefab)
        {
            if (prefab == null)
            {
                return;
            }

            prefabsByType[prefab.GetType()] = prefab;
        }

        private T GetOrSpawn<T>(Transform parent) where T : UIPanelBase
        {
            Type panelType = typeof(T);
            UIPanelBase existing;
            if (instancesByType.TryGetValue(panelType, out existing) && existing != null)
            {
                return (T)existing;
            }

            UIPanelBase prefab;
            if (!prefabsByType.TryGetValue(panelType, out prefab) || prefab == null)
            {
                Debug.LogError("UI catalog is missing " + panelType.Name + ".");
                return null;
            }

            if (container == null)
            {
                Debug.LogError("UIManager has no Zenject container.");
                return null;
            }

            if (parent == null)
            {
                Debug.LogError("UIManager has no canvas root.");
                return null;
            }

            GameObject instanceObject = container.InstantiatePrefab(prefab.gameObject, parent);
            T panel = instanceObject.GetComponent<T>();
            if (panel == null)
            {
                panel = instanceObject.GetComponentInChildren<T>(true);
            }

            if (panel == null)
            {
                Debug.LogError(panelType.Name + " is not on the catalog prefab.");
                Object.Destroy(instanceObject);
                return null;
            }

            instancesByType[panelType] = panel;
            return panel;
        }

        private void TrackPopup(UIPanelBase popup)
        {
            int count = openPopups.Count;
            for (int i = 0; i < count; i++)
            {
                if (openPopups[i] == popup)
                {
                    return;
                }
            }

            openPopups.Add(popup);
        }

        private void RemovePopup(UIPanelBase popup)
        {
            int count = openPopups.Count;
            for (int i = 0; i < count; i++)
            {
                if (openPopups[i] != popup)
                {
                    continue;
                }

                int last = count - 1;
                openPopups[i] = openPopups[last];
                openPopups.RemoveAt(last);
                return;
            }
        }

        private void HidePopups()
        {
            for (int i = openPopups.Count - 1; i >= 0; i--)
            {
                UIPanelBase popup = openPopups[i];
                openPopups.RemoveAt(i);
                if (popup != null)
                {
                    popup.HideAsync().Forget();
                }
            }
        }

        private Transform FindOrCreateRoot(Transform canvasTransform, string rootName)
        {
            Transform existing = canvasTransform.Find(rootName);
            if (existing != null)
            {
                return existing;
            }

            GameObject rootObject = new GameObject(rootName, typeof(RectTransform));
            RectTransform rect = rootObject.GetComponent<RectTransform>();
            rect.SetParent(canvasTransform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            return rect;
        }

        private void EnsureEventSystem()
        {
            EventSystem eventSystem = Object.FindAnyObjectByType<EventSystem>();
            if (eventSystem != null)
            {
                if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
                {
                    eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
                }

                if (Application.isPlaying)
                {
                    Object.DontDestroyOnLoad(eventSystem.gameObject);
                }

                return;
            }

            GameObject eventObject = new GameObject("EventSystem");
            if (Application.isPlaying)
            {
                Object.DontDestroyOnLoad(eventObject);
            }

            eventObject.AddComponent<EventSystem>();
            eventObject.AddComponent<InputSystemUIInputModule>();
        }
    }
}
