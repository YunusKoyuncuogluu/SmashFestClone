using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

namespace SmashFest.UI
{
    public class UIManager : MonoBehaviour, IUIManager
    {
        [SerializeField] private UICatalogSO catalog;
        [SerializeField] private RectTransform panelsRoot;
        [SerializeField] private RectTransform popupsRoot;

        private readonly Dictionary<Type, UIPanelBase> prefabs = new Dictionary<Type, UIPanelBase>(8);
        private readonly Dictionary<Type, UIPanelBase> instances = new Dictionary<Type, UIPanelBase>(8);
        private readonly List<UIPanelBase> openPopups = new List<UIPanelBase>(4);

        private DiContainer container;
        private UIPanelBase activePanel;

        [Inject]
        public void Construct(DiContainer container) => this.container = container;

        private void Awake()
        {
            if (catalog == null)
                return;

            Register(catalog.MainMenuPanelPrefab);
            Register(catalog.GameHudPanelPrefab);
            Register(catalog.SuccessPanelPrefab);
            Register(catalog.FailPanelPrefab);
            Register(catalog.SettingsPopupPrefab);
        }

        public T ShowPanel<T>() where T : UIPanelBase
        {
            HidePopups();
            T panel = GetOrCreate<T>(panelsRoot);
            if (panel == null)
                return null;

            if (activePanel != null && activePanel != panel)
                activePanel.HideImmediate();

            activePanel = panel;
            panel.Show();
            return panel;
        }

        public T ShowPopup<T>() where T : UIPanelBase
        {
            T popup = GetOrCreate<T>(popupsRoot);
            if (popup == null)
                return null;

            popup.transform.SetAsLastSibling();
            if (!openPopups.Contains(popup))
                openPopups.Add(popup);

            popup.Show();
            return popup;
        }

        public void HidePopup<T>() where T : UIPanelBase
        {
            if (!instances.TryGetValue(typeof(T), out UIPanelBase popup) || popup == null)
                return;

            openPopups.Remove(popup);
            popup.Hide();
        }

        private void Register(UIPanelBase prefab)
        {
            if (prefab != null)
                prefabs[prefab.GetType()] = prefab;
        }

        private T GetOrCreate<T>(Transform parent) where T : UIPanelBase
        {
            if (instances.TryGetValue(typeof(T), out UIPanelBase existing) && existing != null)
                return (T)existing;

            if (!prefabs.TryGetValue(typeof(T), out UIPanelBase prefab) || prefab is not T)
                return null;

            T panel = container.InstantiatePrefabForComponent<T>(prefab, parent);
            instances[typeof(T)] = panel;
            return panel;
        }

        public void HidePopups()
        {
            for (int i = openPopups.Count - 1; i >= 0; i--)
            {
                UIPanelBase popup = openPopups[i];
                openPopups.RemoveAt(i);
                if (popup != null)
                    popup.Hide();
            }
        }
    }
}
