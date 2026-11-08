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

        private UIManager uiManager;
        private ISceneFlowManager sceneFlow;
        private IPlayerProgressService progress;
        private LevelItemButton[] items;
        private int listCount;
        private int firstVisibleIndex = -1;
        private bool listReady;
        private bool missingItemWarned;
        private bool isPlayBusy;

        [Inject]
        public void Construct(UIManager uiManager, ISceneFlowManager sceneFlow, IPlayerProgressService progress)
        {
            this.uiManager = uiManager;
            this.sceneFlow = sceneFlow;
            this.progress = progress;
        }

        public void SelectLevel(int levelNumber)
        {
            if (progress == null || !progress.IsLevelUnlocked(levelNumber))
            {
                return;
            }

            progress.SetSelectedLevel(levelNumber);
            firstVisibleIndex = -1;
            RefreshVisibleItems(false);
        }

        protected override void OnPanelOpened()
        {
            isPlayBusy = false;
            RebuildLevelList();
        }

        private void Awake()
        {
            if (playButton != null)
            {
                playButton.onClick.AddListener(HandlePlay);
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.AddListener(HandleSettings);
            }
        }

        private void HandlePlay()
        {
            if (isPlayBusy || sceneFlow == null || progress == null)
            {
                return;
            }

            if (!progress.IsLevelUnlocked(progress.SelectedLevelNumber))
            {
                return;
            }

            isPlayBusy = true;
            StartGameAsync().Forget();
        }

        private void HandleSettings()
        {
            if (uiManager == null)
            {
                return;
            }

            uiManager.ShowPopup<SettingsPopup>();
        }

        private async UniTaskVoid StartGameAsync()
        {
            await sceneFlow.LoadGameSceneAsync(progress.SelectedLevelNumber);
            isPlayBusy = false;
        }

        private void RebuildLevelList()
        {
            if (progress == null)
            {
                return;
            }

            EnsureList();
            if (!listReady)
            {
                return;
            }

            int desired = progress.HighestUnlockedLevel + extraLockedLevels;
            if (desired < minimumListedLevels)
            {
                desired = minimumListedLevels;
            }

            listCount = desired;
            ApplyContentHeight();
            ScrollToSelected();
            firstVisibleIndex = -1;
            RefreshVisibleItems(false);
        }

        private void EnsureList()
        {
            if (listReady)
            {
                return;
            }

            if (levelItemPrefab == null || levelContent == null || levelScroll == null)
            {
                if (!missingItemWarned)
                {
                    missingItemWarned = true;
                    Debug.LogError("MainMenuPanel is missing the scroll, content, or level item prefab.");
                }

                return;
            }

            if (itemHeight < 24f)
            {
                itemHeight = 24f;
            }

            DisableContentDrivers();
            levelContent.anchorMin = new Vector2(0f, 1f);
            levelContent.anchorMax = new Vector2(1f, 1f);
            levelContent.pivot = new Vector2(0.5f, 1f);
            int count = poolSize < 4 ? 4 : poolSize;
            items = new LevelItemButton[count];
            for (int i = 0; i < count; i++)
            {
                LevelItemButton item = Instantiate(levelItemPrefab, levelContent);
                PrepareItem(item);
                items[i] = item;
            }

            levelScroll.vertical = true;
            levelScroll.horizontal = false;
            levelScroll.onValueChanged.AddListener(HandleScroll);
            listReady = true;
        }

        private void DisableContentDrivers()
        {
            LayoutGroup layoutGroup = levelContent.GetComponent<LayoutGroup>();
            if (layoutGroup != null)
            {
                layoutGroup.enabled = false;
            }

            ContentSizeFitter fitter = levelContent.GetComponent<ContentSizeFitter>();
            if (fitter != null)
            {
                fitter.enabled = false;
            }
        }

        private void PrepareItem(LevelItemButton item)
        {
            item.Configure(this);
            RectTransform rect = item.RectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, itemHeight);
        }

        private void HandleScroll(Vector2 normalizedPosition)
        {
            bool atTop = normalizedPosition.y <= 0f && levelContent.anchoredPosition.y <= 0f;
            RefreshVisibleItems(atTop);
        }

        private void RefreshVisibleItems(bool resetWindow)
        {
            if (!listReady)
            {
                return;
            }

            if (resetWindow)
            {
                firstVisibleIndex = -1;
            }

            bool grew = GrowIfNeeded();
            int start = CalculateStartIndex();
            if (!grew && start == firstVisibleIndex)
            {
                return;
            }

            firstVisibleIndex = start;
            int count = items.Length;
            for (int i = 0; i < count; i++)
            {
                BindItem(items[i], start + i);
            }
        }

        private bool GrowIfNeeded()
        {
            int start = CalculateStartIndex();
            int lastCovered = start + items.Length;
            if (lastCovered < listCount - 1)
            {
                return false;
            }

            int step = extraLockedLevels < 8 ? 8 : extraLockedLevels;
            listCount += step;
            ApplyContentHeight();
            return true;
        }

        private int CalculateStartIndex()
        {
            float scrollY = levelContent.anchoredPosition.y;
            if (scrollY < 0f)
            {
                scrollY = 0f;
            }

            int start = (int)(scrollY / itemHeight);
            int maxStart = listCount - items.Length;
            if (maxStart < 0)
            {
                maxStart = 0;
            }

            if (start < 0)
            {
                start = 0;
            }

            if (start > maxStart)
            {
                start = maxStart;
            }

            return start;
        }

        private void BindItem(LevelItemButton item, int zeroBasedIndex)
        {
            if (zeroBasedIndex < 0 || zeroBasedIndex >= listCount)
            {
                item.gameObject.SetActive(false);
                return;
            }

            item.gameObject.SetActive(true);
            RectTransform rect = item.RectTransform;
            rect.anchoredPosition = new Vector2(0f, -zeroBasedIndex * itemHeight);
            int levelNumber = zeroBasedIndex + 1;
            item.Apply(levelNumber, progress.IsLevelUnlocked(levelNumber), levelNumber == progress.SelectedLevelNumber);
        }

        private void ApplyContentHeight()
        {
            Vector2 size = levelContent.sizeDelta;
            size.y = listCount * itemHeight;
            levelContent.sizeDelta = size;
        }

        private void ScrollToSelected()
        {
            float targetY = (progress.SelectedLevelNumber - 1) * itemHeight;
            float viewportHeight = 0f;
            if (levelScroll.viewport != null)
            {
                viewportHeight = levelScroll.viewport.rect.height;
            }

            float maxY = levelContent.sizeDelta.y - viewportHeight;
            if (maxY < 0f)
            {
                maxY = 0f;
            }

            if (targetY < 0f)
            {
                targetY = 0f;
            }

            if (targetY > maxY)
            {
                targetY = maxY;
            }

            Vector2 position = levelContent.anchoredPosition;
            position.y = targetY;
            levelContent.anchoredPosition = position;
        }
    }
}
