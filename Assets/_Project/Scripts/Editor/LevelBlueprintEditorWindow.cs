using System;
using System.Collections.Generic;
using SmashFest.Data;
using UnityEditor;
using UnityEngine;

namespace SmashFest.LevelEditor
{
    public partial class LevelBlueprintEditorWindow : EditorWindow
    {
        private const float PalettePreviewSize = 34f;
        private const float FamilyChipHeight = 20f;
        private const float CardDragThresholdSqr = 16f;
        private const int StandaloneCardKeyOffset = 100000;
        private const int DefaultTableWidth = 4;
        private const int DefaultTableDepth = 2;
        private const float MatrixMinHeight = 220f;
        private const float StatusBarHeight = 18f;
        private const float PaletteHeight = 245f;
        private const float PaletteVerticalSlack = 30f;
        private const float LayoutSlack = 14f;
        private const float WindowPadding = 16f;
        private const float MatrixRowLabelWidth = 28f;
        private const float MatrixCellGap = 2f;
        private const float MatrixHorizontalOverhead = 38f;
        private const float MatrixVerticalOverhead = 52f;
        private const float MinMatrixCellSize = 6f;
        private const float NarrowPaletteWidth = 560f;
        private const float SizeStripHeight = 58f;
        private const float StripPadding = 6f;
        private const float StripColumnGap = 12f;
        private const float StripLabelWidth = 30f;
        private const float StripChipGap = 2f;
        private const float StripRowGap = 4f;
        private const float TableStripLabelWidth = 68f;
        private const float GridScrollbarWidth = 17f;
        private const float PaletteCardMinWidth = 145f;
        private const float PaletteCardGap = 4f;
        private const float PaletteCardHeight = 42f;
        private const string LevelPrefKey = "SmashFest.Blueprint.LevelIndex";
        private const string CatalogPrefKey = "SmashFest.Blueprint.Catalog";
        private const int MaxOverwriteHighlights = 16;
        private const int EditorFrameRate = 120;

        [SerializeField] private LevelData currentLevel;
        private BlockCatalogSO activeCatalog;

        private LevelData activeLevel
        {
            get
            {
                return currentLevel;
            }
            set
            {
                currentLevel = value;
            }
        }
        private LevelDatabaseSO levelDatabase;
        private JsonLevelRepository levelRepository;
        private readonly JsonLevelDatabaseIO jsonIo = new JsonLevelDatabaseIO();
        private readonly LevelLayerTools layerTools = new LevelLayerTools();
        private int editingLevelIndex = 1;
        private int jumpLevelField = 1;
        private bool jsonDirty;
        private int selectedPresetIndex;
        private int paletteCategoryIndex;
        private string paletteSearch = string.Empty;
        private readonly string[] paletteCategoryLabels =
        {
            "All",
            "Tables",
            "Wood",
            "Can",
            "Ice",
            "Stone",
            "Jars"
        };
        private readonly string[] presetLabels =
        {
            "Select Preset...",
            "Straight Apartment",
            "45 Degree Angled Tower",
            "Glass Weak-Point",
            "Shelf & Jars",
            "V-Angled Twin Tables"
        };
        private int selectedZLayer;
        private int brushBlockId;
        private bool hasBrush;
        private Vector3 brushRotationEuler;
        private int brushTableWidth = 4;
        private int brushTableDepth = 2;
        private bool showDatabaseSettings;
        private float matrixCellWidth = 24f;
        private float matrixCellHeight = 24f;
        private bool hasSelectedPlacement;
        private BlockPlacementData selectedPlacement;
        private readonly BlockFootprint blockFootprint = new BlockFootprint();
        private LevelArchitecturePresetBuilder presetBuilder;
        private readonly LevelEditorDragPayload dragPayload = new LevelEditorDragPayload();
        private Vector2 paletteScroll;
        private Vector2 paletteCardPressPosition;
        private int selectedCardKey = -1;
        private float topAreaHeight = 230f;
        private readonly List<int> visibleCardKeys = new List<int>(128);
        private bool isPainting;
        private bool isErasing;
        private Vector3Int lastEditedCell;
        private bool hasLastEditedCell;
        private int undoGroup;
        private bool undoGroupOpen;
        private string undoGroupName = "Edit Level";
        private Vector3Int hoveredCell;
        private bool hasHoveredCell;
        private bool hoverChanged;
        private bool dragFootprintActive;
        private int dragFootprintBlockId;
        private bool hasFootprintPreview;
        private Vector3Int footprintAnchor;
        private Vector3Int footprintSize;
        private PlacementValidationStatus footprintStatus;
        private bool hasExistingHover;
        private BlockPlacementData existingHoverPlacement;
        private Vector3Int existingHoverSize;
        private readonly BlockPlacementData[] overwritePlacements = new BlockPlacementData[MaxOverwriteHighlights];
        private readonly Vector3Int[] overwriteSizes = new Vector3Int[MaxOverwriteHighlights];
        private int overwriteCount;
        private HighlightFrame existingFrame;
        private HighlightFrame previewFrame;
        private HighlightFrame moveOriginFrame;
        private readonly HighlightFrame[] overwriteFrames = new HighlightFrame[MaxOverwriteHighlights];
        private bool isMovingExistingBlock;
        private BlockPlacementData moveOriginPlacement;
        private Vector3 moveRotationEuler;
        private Vector3Int moveGrabOffset;
        private Vector3Int moveTargetAnchor;
        private Vector3Int moveFootprintSize;
        private PlacementValidationStatus moveStatus;
        private bool hasMovePreview;
        private string statusMessage = "Select a block from the palette.";
        [NonSerialized] private GUIStyle cellLabelStyle;
        [NonSerialized] private GUIStyle matrixLabelStyle;
        [NonSerialized] private GUIStyle paletteTitleStyle;
        [NonSerialized] private GUIStyle paletteIdStyle;
        private string[] layerLabels = Array.Empty<string>();
        private int cachedLayerCount = -1;
        private Rect[] matrixCellRects = Array.Empty<Rect>();
        private int matrixCellStride;
        private LevelBlueprintScenePreview scenePreview;

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
            activeCatalog = catalog;
            if (hasBrush && !CatalogContainsBrush())
                hasBrush = false;

            SaveAssetPrefs();
            SyncPreview();
            Repaint();
            SceneView.RepaintAll();
        }

        private void OnEnable()
        {
            ApplyEditorFrameLimit();
            titleContent = new GUIContent("Level Blueprint");
            minSize = new Vector2(340f, 640f);
            wantsMouseMove = true;
            wantsMouseEnterLeaveWindow = true;
            scenePreview = new LevelBlueprintScenePreview();
            scenePreview.Enable();
            Selection.selectionChanged += OnSelectionChanged;
            Undo.undoRedoPerformed += OnUndoRedo;
            ObjectChangeEvents.changesPublished += OnAssetChanged;
            RestoreAssets();
            SyncPreview();
            SceneView.RepaintAll();
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            Undo.undoRedoPerformed -= OnUndoRedo;
            ObjectChangeEvents.changesPublished -= OnAssetChanged;
            EndLevelEdit();
            if (jsonDirty)
                PersistJson(false);

            if (scenePreview != null)
            {
                scenePreview.Disable();
                scenePreview = null;
            }
        }

        private void OnDestroy()
        {
            if (jsonDirty)
                PersistJson(false);
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
            CancelMove();
            hasSelectedPlacement = false;
            ClampActiveLayer();
            jsonDirty = true;
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
            int catalogId = activeCatalog != null ? activeCatalog.GetInstanceID() : 0;
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
            if (scenePreview != null)
                scenePreview.ClearVisualCache();

            ClampActiveLayer();
            SyncPreview();
            Repaint();
            SceneView.RepaintAll();
        }

        private void OnGUI()
        {
            EnsureStyles();
            ClampActiveLayer();
            HandleGlobalEvents();
            float matrixHeight = Mathf.Max(
                MatrixMinHeight,
                position.height - topAreaHeight - StatusBarHeight - PaletteHeight - LayoutSlack);

            EditorGUILayout.BeginVertical(GUILayout.Width(position.width));
            EditorGUILayout.BeginVertical();
            DrawCompactHeader();
            DrawDatabaseSettingsFoldout();
            DrawLayerBar();
            DrawActionBar();
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

            DrawMatrix(matrixHeight);
            DrawStatusBar();
            DrawBottomPalette();
            EditorGUILayout.EndVertical();
            if (Event.current != null && Event.current.type == EventType.Repaint)
                SyncPreview();

            if (hoverChanged)
            {
                hoverChanged = false;
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
                dragFootprintActive = false;

            if (current.type == EventType.MouseLeaveWindow && hasHoveredCell)
            {
                hasHoveredCell = false;
                hoverChanged = true;
            }

            if ((current.type == EventType.MouseUp || current.type == EventType.MouseLeaveWindow) && (isPainting || isErasing))
            {
                EndLevelEdit();
                isPainting = false;
                isErasing = false;
                hasLastEditedCell = false;
                if (current.type == EventType.MouseUp)
                    current.Use();
            }

            if (isMovingExistingBlock && current.type == EventType.MouseUp && current.button == 0)
            {
                FinishMove();
                current.Use();
            }

            if (current.type != EventType.KeyDown || EditorGUIUtility.editingTextField)
                return;

            if (current.alt || current.control || current.command)
                return;

            if (current.keyCode == KeyCode.R)
            {
                int direction = current.shift ? -1 : 1;
                if (isMovingExistingBlock)
                    StepMoveYaw(direction);
                else
                    StepBrushYaw(direction);

                current.Use();
                return;
            }

            if (current.keyCode == KeyCode.T)
            {
                CycleLengthAxis();
                current.Use();
            }
        }

        private void RestoreAssets()
        {
            EnsureRepository();
            activeCatalog = LoadCatalog();
            if (activeCatalog != null)
                activeCatalog.InvalidateRuntimeBlocks();

            int savedIndex = EditorPrefs.GetInt(LevelPrefKey, 1);
            if (savedIndex < 1)
                savedIndex = 1;

            NavigateToLevel(savedIndex);
            SaveAssetPrefs();
            ClampActiveLayer();
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

        private void SaveAssetPrefs()
        {
            EditorPrefs.SetInt(LevelPrefKey, editingLevelIndex);
            string catalogPath = activeCatalog == null ? string.Empty : AssetDatabase.GetAssetPath(activeCatalog);
            EditorPrefs.SetString(CatalogPrefKey, catalogPath);
        }

        private void EnsureStyles()
        {
            if (cellLabelStyle != null && matrixLabelStyle != null && paletteTitleStyle != null && paletteIdStyle != null)
                return;

            cellLabelStyle = new GUIStyle(EditorStyles.miniBoldLabel);
            cellLabelStyle.alignment = TextAnchor.MiddleCenter;
            cellLabelStyle.wordWrap = true;
            cellLabelStyle.clipping = TextClipping.Clip;

            matrixLabelStyle = new GUIStyle(EditorStyles.boldLabel);
            matrixLabelStyle.alignment = TextAnchor.MiddleCenter;
            matrixLabelStyle.wordWrap = true;
            matrixLabelStyle.clipping = TextClipping.Clip;
            matrixLabelStyle.fontSize = 11;
            matrixLabelStyle.fixedHeight = 0f;
            matrixLabelStyle.fixedWidth = 0f;
            matrixLabelStyle.stretchWidth = true;
            matrixLabelStyle.stretchHeight = true;
            matrixLabelStyle.padding = new RectOffset(2, 2, 2, 2);
            matrixLabelStyle.margin = new RectOffset(0, 0, 0, 0);
            matrixLabelStyle.contentOffset = Vector2.zero;

            paletteTitleStyle = new GUIStyle(EditorStyles.miniBoldLabel);
            paletteTitleStyle.alignment = TextAnchor.MiddleLeft;
            paletteTitleStyle.fontSize = 11;
            paletteTitleStyle.clipping = TextClipping.Clip;
            paletteTitleStyle.wordWrap = true;

            paletteIdStyle = new GUIStyle(EditorStyles.miniLabel);
            paletteIdStyle.alignment = TextAnchor.MiddleCenter;
        }

        private struct HighlightFrame
        {
            public bool active;
            public bool dashed;
            public Vector3Int anchor;
            public Vector3Int size;
            public Color color;
            public bool hasTopLeft;
            public bool hasBottomRight;
            public Rect topLeft;
            public Rect bottomRight;
        }
    }
}
