using System;
using System.Collections.Generic;
using SmashFest.Data;
using UnityEditor;
using UnityEngine;

namespace SmashFest.LevelEditor
{
    public class LevelBlueprintEditorWindow : EditorWindow
    {
        private const float MatrixMinHeight = 220f;
        private const float StatusBarHeight = 18f;
        private const float LayoutSlack = 14f;
        private const string LevelPrefKey = "SmashFest.Blueprint.LevelIndex";
        private const string CatalogPrefKey = "SmashFest.Blueprint.Catalog";
        private const int EditorFrameRate = 120;

        [SerializeField] private LevelData currentLevel;
        private float topAreaHeight = 230f;
        private LevelEditorState state = new LevelEditorState();
        private LevelEditorActions actions = new LevelEditorActions();
        private LevelEditorToolbar toolbar = new LevelEditorToolbar();
        private LevelEditorMatrixView matrix = new LevelEditorMatrixView();
        private LevelEditorPaletteView palette = new LevelEditorPaletteView();

        public LevelData CurrentLevel
        {
            get => currentLevel;
            set => currentLevel = value;
        }

        [InitializeOnLoadMethod]
        private static void ApplyEditorFrameLimitOnLoad()
        {
            ApplyEditorFrameLimit();
        }

        private static void ApplyEditorFrameLimit()
        {
            Application.targetFrameRate = EditorFrameRate;
            QualitySettings.vSyncCount = 1;
        }

        [MenuItem("Tools/SmashFest/Level Blueprint Editor")]
        public static void Open()
        {
            LevelBlueprintEditorWindow window = GetWindow<LevelBlueprintEditorWindow>();
            window.titleContent = new GUIContent("Level Blueprint");
            window.Show();
        }

        public void SetActiveCatalog(BlockCatalogSO catalog)
        {
            state.activeCatalog = catalog;
            if (state.hasBrush && !actions.CatalogContainsBrush())
                state.hasBrush = false;

            SaveAssetPrefs();
            actions.SyncPreview();
            Repaint();
            SceneView.RepaintAll();
        }

        private void OnEnable()
        {
            BindParts();
            ApplyEditorFrameLimit();
            titleContent = new GUIContent("Level Blueprint");
            minSize = new Vector2(340f, 640f);
            wantsMouseMove = true;
            wantsMouseEnterLeaveWindow = true;
            state.scenePreview = new LevelBlueprintScenePreview();
            state.scenePreview.Enable();
            Selection.selectionChanged += OnSelectionChanged;
            Undo.undoRedoPerformed += OnUndoRedo;
            ObjectChangeEvents.changesPublished += OnAssetChanged;
            RestoreAssets();
            actions.SyncPreview();
            SceneView.RepaintAll();
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            Undo.undoRedoPerformed -= OnUndoRedo;
            ObjectChangeEvents.changesPublished -= OnAssetChanged;
            actions.EndLevelEdit();
            if (state.jsonDirty)
                actions.PersistJson(false);

            if (state.scenePreview != null)
            {
                state.scenePreview.Disable();
                state.scenePreview = null;
            }
        }

        private void OnDestroy()
        {
            if (state.jsonDirty)
                actions.PersistJson(false);
        }

        private void OnFocus()
        {
            RefreshView();
        }

        private void OnSelectionChanged()
        {
            if (Selection.activeObject is BlockCatalogSO selectedCatalog)
                SetActiveCatalog(selectedCatalog);
        }

        private void OnUndoRedo()
        {
            actions.CancelMove();
            state.hasSelectedPlacement = false;
            actions.ClampActiveLayer();
            state.jsonDirty = true;
            RefreshView();
            SceneView.RepaintAll();
        }

        private void OnAssetChanged(ref ObjectChangeEventStream stream)
        {
            if (!TargetsOpenAsset(ref stream))
                return;

            RefreshView();
        }

        private bool TargetsOpenAsset(ref ObjectChangeEventStream stream)
        {
            int catalogId = state.activeCatalog != null ? state.activeCatalog.GetInstanceID() : 0;
            if (catalogId == 0)
                return false;

            for (int i = 0; i < stream.length; i++)
            {
                if (stream.GetEventType(i) != ObjectChangeKind.ChangeAssetObjectProperties)
                    continue;

                ChangeAssetObjectPropertiesEventArgs args;
                stream.GetChangeAssetObjectPropertiesEvent(i, out args);
                if (args.instanceId == catalogId)
                    return true;
            }

            return false;
        }

        private void RefreshView()
        {
            if (state.scenePreview != null)
                state.scenePreview.ClearVisualCache();

            actions.ClampActiveLayer();
            actions.SyncPreview();
            Repaint();
            SceneView.RepaintAll();
        }

        private void OnGUI()
        {
            EnsureStyles();
            actions.ClampActiveLayer();
            HandleGlobalEvents();
            float matrixHeight = Mathf.Max(
                MatrixMinHeight,
                position.height - topAreaHeight - StatusBarHeight - LevelEditorState.PaletteHeight - LayoutSlack);

            EditorGUILayout.BeginVertical(GUILayout.Width(position.width));
            EditorGUILayout.BeginVertical();
            toolbar.DrawCompactHeader();
            toolbar.DrawDatabaseSettingsFoldout();
            toolbar.DrawLayerBar();
            toolbar.DrawActionBar();
            EditorGUILayout.EndVertical();
            if (Event.current != null && Event.current.type == EventType.Repaint)
            {
                float measuredHeight = GUILayoutUtility.GetLastRect().height;
                if (Mathf.Abs(measuredHeight - topAreaHeight) > 1f)
                {
                    topAreaHeight = measuredHeight;
                    Repaint();
                }
            }

            matrix.DrawMatrix(matrixHeight);
            palette.DrawStatusBar();
            palette.DrawBottomPalette();
            EditorGUILayout.EndVertical();
            if (Event.current != null && Event.current.type == EventType.Repaint)
                actions.SyncPreview();

            if (state.hoverChanged)
            {
                state.hoverChanged = false;
                Repaint();
                SceneView.RepaintAll();
            }
        }

        private void HandleGlobalEvents()
        {
            Event current = Event.current;
            if (current == null)
                return;

            if (current.type == EventType.DragExited)
                state.dragFootprintActive = false;

            if (current.type == EventType.MouseLeaveWindow && state.hasHoveredCell)
            {
                state.hasHoveredCell = false;
                state.hoverChanged = true;
            }

            if ((current.type == EventType.MouseUp || current.type == EventType.MouseLeaveWindow) && (state.isPainting || state.isErasing))
            {
                actions.EndLevelEdit();
                state.isPainting = false;
                state.isErasing = false;
                state.hasLastEditedCell = false;
                if (current.type == EventType.MouseUp)
                    current.Use();
            }

            if (state.isMovingExistingBlock && current.type == EventType.MouseUp && current.button == 0)
            {
                actions.FinishMove();
                current.Use();
            }

            if (current.type != EventType.KeyDown || EditorGUIUtility.editingTextField)
                return;

            if (current.alt || current.control || current.command)
                return;

            if (current.keyCode == KeyCode.R)
            {
                int direction = current.shift ? -1 : 1;
                if (state.isMovingExistingBlock)
                    actions.StepMoveYaw(direction);
                else
                    actions.StepBrushYaw(direction);

                current.Use();
                return;
            }

            if (current.keyCode == KeyCode.T)
            {
                toolbar.CycleLengthAxis();
                current.Use();
            }
        }

        private void RestoreAssets()
        {
            actions.EnsureRepository();
            state.activeCatalog = LoadCatalog();
            if (state.activeCatalog != null)
                state.activeCatalog.InvalidateRuntimeBlocks();

            int savedIndex = EditorPrefs.GetInt(LevelPrefKey, 1);
            if (savedIndex < 1)
                savedIndex = 1;

            actions.NavigateToLevel(savedIndex);
            SaveAssetPrefs();
            actions.ClampActiveLayer();
        }

        private BlockCatalogSO LoadCatalog()
        {
            string savedPath = EditorPrefs.GetString(CatalogPrefKey, string.Empty);
            BlockCatalogSO savedCatalog = LoadProjectAsset<BlockCatalogSO>(savedPath);
            if (savedCatalog != null)
                return savedCatalog;

            BlockCatalogSO projectCatalog = AssetDatabase.LoadAssetAtPath<BlockCatalogSO>(LevelAssetPaths.CatalogPath);
            if (projectCatalog != null)
                return projectCatalog;

            LevelDatabaseSO database = AssetDatabase.LoadAssetAtPath<LevelDatabaseSO>(LevelAssetPaths.LevelDatabasePath);
            if (database != null && IsProjectAsset(database.BlockCatalog))
                return database.BlockCatalog;

            return null;
        }

        private bool IsProjectAsset(UnityEngine.Object asset)
        {
            return asset != null && IsProjectAssetPath(AssetDatabase.GetAssetPath(asset));
        }

        private TAsset LoadProjectAsset<TAsset>(string path) where TAsset : UnityEngine.Object
        {
            return IsProjectAssetPath(path) ? AssetDatabase.LoadAssetAtPath<TAsset>(path) : null;
        }

        private bool IsProjectAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            return path.StartsWith(LevelAssetPaths.ProjectFolder + "/", StringComparison.Ordinal)
                || path.StartsWith(LevelAssetPaths.ProjectFolder + "\\", StringComparison.Ordinal);
        }

        public void SaveAssetPrefs()
        {
            EditorPrefs.SetInt(LevelPrefKey, state.editingLevelIndex);
            string catalogPath = state.activeCatalog == null ? string.Empty : AssetDatabase.GetAssetPath(state.activeCatalog);
            EditorPrefs.SetString(CatalogPrefKey, catalogPath);
        }

        private void EnsureStyles()
        {
            if (state.cellLabelStyle != null && state.matrixLabelStyle != null && state.paletteTitleStyle != null && state.paletteIdStyle != null)
                return;

            state.cellLabelStyle = new GUIStyle(EditorStyles.miniBoldLabel);
            state.cellLabelStyle.alignment = TextAnchor.MiddleCenter;
            state.cellLabelStyle.wordWrap = true;
            state.cellLabelStyle.clipping = TextClipping.Clip;

            state.matrixLabelStyle = new GUIStyle(EditorStyles.boldLabel);
            state.matrixLabelStyle.alignment = TextAnchor.MiddleCenter;
            state.matrixLabelStyle.wordWrap = true;
            state.matrixLabelStyle.clipping = TextClipping.Clip;
            state.matrixLabelStyle.fontSize = 11;
            state.matrixLabelStyle.fixedHeight = 0f;
            state.matrixLabelStyle.fixedWidth = 0f;
            state.matrixLabelStyle.stretchWidth = true;
            state.matrixLabelStyle.stretchHeight = true;
            state.matrixLabelStyle.padding = new RectOffset(2, 2, 2, 2);
            state.matrixLabelStyle.margin = new RectOffset(0, 0, 0, 0);
            state.matrixLabelStyle.contentOffset = Vector2.zero;

            state.paletteTitleStyle = new GUIStyle(EditorStyles.miniBoldLabel);
            state.paletteTitleStyle.alignment = TextAnchor.MiddleLeft;
            state.paletteTitleStyle.fontSize = 11;
            state.paletteTitleStyle.clipping = TextClipping.Clip;
            state.paletteTitleStyle.wordWrap = true;

            state.paletteIdStyle = new GUIStyle(EditorStyles.miniLabel);
            state.paletteIdStyle.alignment = TextAnchor.MiddleCenter;
        }

        private void BindParts()
        {
            state ??= new LevelEditorState();
            actions ??= new LevelEditorActions();
            toolbar ??= new LevelEditorToolbar();
            matrix ??= new LevelEditorMatrixView();
            palette ??= new LevelEditorPaletteView();
            state.Bind(this);
            actions.Bind(this, state, matrix, palette);
            toolbar.Bind(this, state, actions, palette);
            matrix.Bind(this, state, actions, palette);
            palette.Bind(this, state, actions, matrix);
        }
    }
}
