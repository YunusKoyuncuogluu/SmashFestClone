using Cysharp.Threading.Tasks;
using SmashFast.Flow;
using SmashFast.Progress;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace SmashFast.UI.Panels
{
    public class MainMenuPanel : UIPanelBase
    {
        [SerializeField] private ScrollRect levelScroll;
        [SerializeField] private RectTransform levelContent;
        [SerializeField] private LevelItemButton levelItemPrefab;
        [SerializeField] private Button playButton;
        [SerializeField] private Button settingsButton;
        [SerializeField, Min(24f)] private float itemHeight = 88f;
        [SerializeField, Min(4)] private int poolSize = 14;
        [SerializeField, Min(8)] private int extraLockedLevels = 24;
        [SerializeField, Min(8)] private int minimumListedLevels = 48;

        private UIManager ui;
        private ISceneFlowManager sceneFlow;
        private IPlayerProgressService progress;
        private LevelItemButton[] items;
        private int listCount;
        private int firstVisible = -1;
        private bool isListReady;
        private bool isStarting;

        [Inject]
        public void Construct(UIManager ui, ISceneFlowManager sceneFlow, IPlayerProgressService progress)
        {
            this.ui = ui;
            this.sceneFlow = sceneFlow;
            this.progress = progress;
        }

        private void Awake()
        {
            if (playButton != null)
                playButton.onClick.AddListener(() => StartGame().Forget());

            if (settingsButton != null)
                settingsButton.onClick.AddListener(OpenSettings);
        }

        public void SelectLevel(int level)
        {
            if (!progress.IsLevelUnlocked(level))
                return;

            progress.SetSelectedLevel(level);
            firstVisible = -1;
            RefreshItems(false);
        }

        protected override void OnPanelOpened()
        {
            isStarting = false;
            RebuildList();
        }

        private async UniTaskVoid StartGame()
        {
            if (isStarting || !progress.IsLevelUnlocked(progress.SelectedLevelNumber))
                return;

            isStarting = true;
            await sceneFlow.LoadGameSceneAsync(progress.SelectedLevelNumber);
            isStarting = false;
        }

        private void OpenSettings() => ui.ShowPopup<SettingsPopup>();

        private void RebuildList()
        {
            CreateItems();
            if (!isListReady)
                return;

            listCount = Mathf.Max(minimumListedLevels, progress.HighestUnlockedLevel + extraLockedLevels);
            UpdateContentHeight();
            ScrollToSelected();
            firstVisible = -1;
            RefreshItems(false);
        }

        private void CreateItems()
        {
            if (isListReady || levelItemPrefab == null || levelContent == null || levelScroll == null)
                return;

            itemHeight = Mathf.Max(24f, itemHeight);
            DisableLayoutDrivers();
            levelContent.anchorMin = new Vector2(0f, 1f);
            levelContent.anchorMax = new Vector2(1f, 1f);
            levelContent.pivot = new Vector2(0.5f, 1f);

            items = new LevelItemButton[Mathf.Max(4, poolSize)];
            for (int i = 0; i < items.Length; i++)
            {
                LevelItemButton item = Instantiate(levelItemPrefab, levelContent);
                SetupItem(item);
                items[i] = item;
            }

            levelScroll.vertical = true;
            levelScroll.horizontal = false;
            levelScroll.onValueChanged.AddListener(OnScroll);
            isListReady = true;
        }

        private void DisableLayoutDrivers()
        {
            if (levelContent.TryGetComponent(out LayoutGroup layoutGroup))
                layoutGroup.enabled = false;

            if (levelContent.TryGetComponent(out ContentSizeFitter fitter))
                fitter.enabled = false;
        }

        private void SetupItem(LevelItemButton item)
        {
            item.Configure(this);
            RectTransform rect = item.Rect;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, itemHeight);
        }

        private void OnScroll(Vector2 normalizedPosition)
        {
            bool isAtTop = normalizedPosition.y <= 0f && levelContent.anchoredPosition.y <= 0f;
            RefreshItems(isAtTop);
        }

        private void RefreshItems(bool forceRebind)
        {
            if (!isListReady)
                return;

            if (forceRebind)
                firstVisible = -1;

            bool grew = GrowIfNeeded();
            int start = GetFirstVisibleIndex();
            if (!grew && start == firstVisible)
                return;

            firstVisible = start;
            for (int i = 0; i < items.Length; i++)
                BindItem(items[i], start + i);
        }

        private bool GrowIfNeeded()
        {
            int lastCovered = GetFirstVisibleIndex() + items.Length;
            if (lastCovered < listCount - 1)
                return false;

            listCount += Mathf.Max(8, extraLockedLevels);
            UpdateContentHeight();
            return true;
        }

        private int GetFirstVisibleIndex()
        {
            float scrollY = Mathf.Max(0f, levelContent.anchoredPosition.y);
            int maxStart = Mathf.Max(0, listCount - items.Length);
            return Mathf.Clamp((int)(scrollY / itemHeight), 0, maxStart);
        }

        private void BindItem(LevelItemButton item, int index)
        {
            if (index < 0 || index >= listCount)
            {
                item.gameObject.SetActive(false);
                return;
            }

            item.gameObject.SetActive(true);
            item.Rect.anchoredPosition = new Vector2(0f, -index * itemHeight);

            int level = index + 1;
            item.Apply(level, progress.IsLevelUnlocked(level), level == progress.SelectedLevelNumber);
        }

        private void UpdateContentHeight()
        {
            Vector2 size = levelContent.sizeDelta;
            size.y = listCount * itemHeight;
            levelContent.sizeDelta = size;
        }

        private void ScrollToSelected()
        {
            float viewportHeight = levelScroll.viewport != null ? levelScroll.viewport.rect.height : 0f;
            float maxY = Mathf.Max(0f, levelContent.sizeDelta.y - viewportHeight);
            float targetY = Mathf.Clamp((progress.SelectedLevelNumber - 1) * itemHeight, 0f, maxY);

            Vector2 position = levelContent.anchoredPosition;
            position.y = targetY;
            levelContent.anchoredPosition = position;
        }
    }
}
