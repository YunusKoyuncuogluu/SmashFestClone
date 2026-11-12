using System;
using System.Collections.Generic;
using SmashFast.Data;
using UnityEditor;
using UnityEngine;

namespace SmashFast.LevelEditor
{
    public class LevelBlueprintEditorWindow : EditorWindow
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
        private const float PaletteCardHeight = 42f;        private const string LevelPrefKey = "SmashFast.Blueprint.LevelIndex";
        private const string CatalogPrefKey = "SmashFast.Blueprint.Catalog";
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
        private GUIStyle cellLabelStyle;
        private GUIStyle matrixLabelStyle;
        private GUIStyle paletteTitleStyle;
        private GUIStyle paletteIdStyle;
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

        [MenuItem("Tools/SmashFast/Level Blueprint Editor")]
        public static void Open()
        {
            LevelBlueprintEditorWindow window = GetWindow<LevelBlueprintEditorWindow>();
            window.titleContent = new GUIContent("Level Blueprint");
            window.Show();
        }

        public static void Open(LevelData levelData)
        {
            Open();
            LevelBlueprintEditorWindow window = GetWindow<LevelBlueprintEditorWindow>();
            window.SetActiveLevel(levelData);
        }

        public static void Open(LevelData levelData, BlockCatalogSO catalog)
        {
            Open(levelData);
            LevelBlueprintEditorWindow window = GetWindow<LevelBlueprintEditorWindow>();
            window.SetActiveCatalog(catalog);
        }

        public void SetActiveLevel(LevelData levelData)
        {
            EnsureRepository();
            if (levelData == null)
            {
                return;
            }

            levelRepository.CaptureLevel(levelData);
            PersistJson(false);
            int index = levelData.LevelIndex < 1 ? 1 : levelData.LevelIndex;
            NavigateToLevel(index);
        }

        public void SetActiveCatalog(BlockCatalogSO catalog)
        {
            activeCatalog = catalog;
            if (hasBrush && !CatalogContainsBrush())
            {
                hasBrush = false;
            }

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
            {
                PersistJson(false);
            }

            if (scenePreview != null)
            {
                scenePreview.Disable();
                scenePreview = null;
            }
        }

        private void OnDestroy()
        {
            if (jsonDirty)
            {
                PersistJson(false);
            }
        }

        private void OnFocus()
        {
            RefreshView();
        }

        private void OnSelectionChanged()
        {
            if (Selection.activeObject is BlockCatalogSO selectedCatalog)
            {
                SetActiveCatalog(selectedCatalog);
            }
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
            {
                return;
            }

            RefreshView();
        }

        private bool TargetsOpenAsset(ref ObjectChangeEventStream stream)
        {
            int catalogId = activeCatalog != null ? activeCatalog.GetInstanceID() : 0;
            if (catalogId == 0)
            {
                return false;
            }

            for (int i = 0; i < stream.length; i++)
            {
                if (stream.GetEventType(i) != ObjectChangeKind.ChangeAssetObjectProperties)
                {
                    continue;
                }

                ChangeAssetObjectPropertiesEventArgs args;
                stream.GetChangeAssetObjectPropertiesEvent(i, out args);
                if (args.instanceId == catalogId)
                {
                    return true;
                }
            }

            return false;
        }

        private void RefreshView()
        {
            if (scenePreview != null)
            {
                scenePreview.ClearVisualCache();
            }

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
            {
                SyncPreview();
            }

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
            {
                return;
            }

            if (current.type == EventType.DragExited)
            {
                dragFootprintActive = false;
            }

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
                {
                    current.Use();
                }
            }

            if (isMovingExistingBlock && current.type == EventType.MouseUp && current.button == 0)
            {
                FinishMove();
                current.Use();
            }

            if (current.type != EventType.KeyDown || EditorGUIUtility.editingTextField)
            {
                return;
            }

            if (current.alt || current.control || current.command)
            {
                return;
            }

            if (current.keyCode == KeyCode.R)
            {
                int direction = current.shift ? -1 : 1;
                if (isMovingExistingBlock)
                {
                    StepMoveYaw(direction);
                }
                else
                {
                    StepBrushYaw(direction);
                }

                current.Use();
                return;
            }

            if (current.keyCode == KeyCode.T)
            {
                CycleLengthAxis();
                current.Use();
            }
        }

        private void DrawCompactHeader()
        {
            EnsureRepository();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("-10", GUILayout.MinWidth(30f)))
            {
                NavigateToLevel(editingLevelIndex - 10);
            }

            if (GUILayout.Button("Prev", GUILayout.MinWidth(30f)))
            {
                NavigateToLevel(editingLevelIndex - 1);
            }

            EditorGUILayout.LabelField("Level", GUILayout.Width(34f));
            jumpLevelField = EditorGUILayout.IntField(jumpLevelField, GUILayout.MinWidth(30f), GUILayout.MaxWidth(64f));
            EditorGUILayout.LabelField("/ " + JsonLevelRepository.MaxLevelCount.ToString(), GUILayout.Width(44f));
            if (GUILayout.Button("Go", GUILayout.MinWidth(26f)))
            {
                NavigateToLevel(jumpLevelField);
            }

            if (GUILayout.Button("Next", GUILayout.MinWidth(30f)))
            {
                NavigateToLevel(editingLevelIndex + 1);
            }

            if (GUILayout.Button("+10", GUILayout.MinWidth(30f)))
            {
                NavigateToLevel(editingLevelIndex + 10);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(activeLevel == null);
            EditorGUILayout.LabelField("Max Ammo", GUILayout.Width(64f));
            EditorGUI.BeginChangeCheck();
            int maxAmmo = activeLevel != null ? activeLevel.MaxAmmo : 0;
            maxAmmo = EditorGUILayout.IntField(maxAmmo, GUILayout.Width(40f));
            if (EditorGUI.EndChangeCheck() && activeLevel != null)
            {
                int clampedAmmo = maxAmmo < 0 ? 0 : maxAmmo;
                CommitLevelChange("Edit Max Ammo", () => activeLevel.MaxAmmo = clampedAmmo);
            }

            if (GUILayout.Button("Frame Scene", GUILayout.MinWidth(70f)))
            {
                FrameLevel();
            }

            if (GUILayout.Button("Save JSON", GUILayout.MinWidth(70f)))
            {
                PersistJson(true);
                statusMessage = "Saved LevelsDatabase.json.";
            }

            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
            DrawWorldScaleRow();
            EditorGUILayout.EndVertical();
        }

        private void DrawWorldScaleRow()
        {
            EditorGUI.BeginDisabledGroup(activeCatalog == null);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("World Scale", GUILayout.Width(76f));
            float currentScale = activeCatalog != null ? activeCatalog.globalBlockScale : 1f;
            EditorGUI.BeginChangeCheck();
            float nextScale = EditorGUILayout.Slider(currentScale, BlockCatalogSO.MinGlobalBlockScale, BlockCatalogSO.MaxGlobalBlockScale);
            if (EditorGUI.EndChangeCheck() && activeCatalog != null)
            {
                Undo.RecordObject(activeCatalog, "Change World Scale");
                activeCatalog.SetScale(nextScale);
                EditorUtility.SetDirty(activeCatalog);
                SceneView.RepaintAll();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUI.EndDisabledGroup();
        }

        private void DrawDatabaseSettingsFoldout()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            showDatabaseSettings = EditorGUILayout.Foldout(showDatabaseSettings, "Database & Grid Settings", true);
            if (!showDatabaseSettings)
            {
                EditorGUILayout.EndVertical();
                return;
            }

            if (activeCatalog == null)
            {
                EditorGUILayout.HelpBox("Block Catalog missing. Create default project assets.", MessageType.Info);
                if (GUILayout.Button("Create Default Level Assets"))
                {
                    LevelBlueprintSampleAssets.Create();
                    GUIUtility.ExitGUI();
                }
            }

            EditorGUI.BeginChangeCheck();
            BlockCatalogSO nextCatalog = (BlockCatalogSO)EditorGUILayout.ObjectField(
                "Block Catalog",
                activeCatalog,
                typeof(BlockCatalogSO),
                false);
            if (EditorGUI.EndChangeCheck())
            {
                SetActiveCatalog(nextCatalog);
            }

            if (activeLevel != null)
            {
                Vector3Int dimensions = activeLevel.GridDimensions;
                EditorGUI.BeginChangeCheck();
                int gridX = EditorGUILayout.DelayedIntField("Grid X", dimensions.x);
                int gridY = EditorGUILayout.DelayedIntField("Grid Y", dimensions.y);
                int gridZ = EditorGUILayout.DelayedIntField("Grid Z", dimensions.z);
                if (EditorGUI.EndChangeCheck())
                {
                    Vector3Int clampedDimensions = new Vector3Int(
                        Mathf.Max(1, gridX),
                        Mathf.Max(1, gridY),
                        Mathf.Max(1, gridZ));
                    CommitLevelChange("Edit Grid Dimensions", () =>
                    {
                        activeLevel.GridDimensions = clampedDimensions;
                        activeLevel.RemoveOutOfBounds(activeCatalog);
                    });
                }

                EditorGUI.BeginChangeCheck();
                Vector3 cellSize = EditorGUILayout.Vector3Field("Cell Size", activeLevel.CellSize);
                if (EditorGUI.EndChangeCheck())
                {
                    CommitLevelChange("Edit Cell Size", () => activeLevel.CellSize = cellSize);
                }

                EditorGUI.BeginChangeCheck();
                Vector3 originOffset = EditorGUILayout.Vector3Field("Origin Offset", activeLevel.OriginOffset);
                if (EditorGUI.EndChangeCheck())
                {
                    CommitLevelChange("Edit Origin Offset", () => activeLevel.OriginOffset = originOffset);
                }
            }

            EditorGUI.BeginDisabledGroup(activeCatalog == null);
            if (GUILayout.Button("Generate All 1000 Levels To JSON", GUILayout.Height(24f)))
            {
                GenerateAllLevelsToJson();
            }

            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();
        }

        private void ApplyPreset(int presetIndex)
        {
            if (activeLevel == null || activeCatalog == null)
            {
                statusMessage = "Select a level and a catalog first.";
                return;
            }

            if (Application.isPlaying)
            {
                statusMessage = "Exit Play Mode before applying a preset.";
                return;
            }

            CancelMove();
            if (presetBuilder == null)
            {
                presetBuilder = new LevelArchitecturePresetBuilder();
            }

            string message = string.Empty;
            CommitLevelChange("Apply Architecture Preset", () =>
            {
                message = presetBuilder.Apply(activeLevel, activeCatalog, presetIndex, selectedZLayer);
            });
            if (presetBuilder.LastAnchorLayer != selectedZLayer)
            {
                selectedZLayer = presetBuilder.LastAnchorLayer;
            }

            PersistJson(true);
            hasSelectedPlacement = false;
            statusMessage = message;
        }

        private void RegenerateCurrentLevel()
        {
            if (activeLevel == null || activeCatalog == null)
            {
                statusMessage = "Select a catalog first.";
                return;
            }

            if (Application.isPlaying)
            {
                statusMessage = "Exit Play Mode before regenerating.";
                return;
            }

            CancelMove();
            if (presetBuilder == null)
            {
                presetBuilder = new LevelArchitecturePresetBuilder();
            }

            int index = editingLevelIndex < 1 ? 1 : editingLevelIndex;
            string message = string.Empty;
            CommitLevelChange("Regenerate Current Level", () =>
            {
                activeLevel.LevelIndex = index;
                message = presetBuilder.GenerateForLevelIndex(activeLevel, activeCatalog, index);
            });
            selectedZLayer = presetBuilder.LastAnchorLayer;
            PersistJson(true);
            hasSelectedPlacement = false;
            statusMessage = message;
        }

        private void GenerateAllLevelsToJson()
        {
            if (activeCatalog == null)
            {
                statusMessage = "Select a catalog first.";
                return;
            }

            if (Application.isPlaying)
            {
                statusMessage = "Exit Play Mode before generating levels.";
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "Generate 1000 Levels",
                "This will overwrite every level in LevelsDatabase.json (1.."
                    + JsonLevelRepository.MaxLevelCount.ToString()
                    + "). Continue?",
                "Generate",
                "Cancel");
            if (!confirmed)
            {
                statusMessage = "Generate 1000 cancelled.";
                return;
            }

            CancelMove();
            EnsureRepository();
            if (presetBuilder == null)
            {
                presetBuilder = new LevelArchitecturePresetBuilder();
            }

            LevelData template = activeLevel;
            LevelData temp = new LevelData();
            if (template != null)
            {
                CopyGrid(template, temp);
            }
            else
            {
                temp.GridDimensions = new Vector3Int(16, 14, 6);
                temp.CellSize = Vector3.one;
                temp.OriginOffset = Vector3.zero;
                temp.FallEliminationY = -1.5f;
            }

            List<LevelRecord> records = new List<LevelRecord>(JsonLevelRepository.MaxLevelCount);
            int emptyCount = 0;
            try
            {
                for (int index = 1; index <= JsonLevelRepository.MaxLevelCount; index++)
                {
                    if ((index % 20) == 0 || index == JsonLevelRepository.MaxLevelCount)
                    {
                        float progress = index / (float)JsonLevelRepository.MaxLevelCount;
                        EditorUtility.DisplayProgressBar(
                            "Generate 1000 Levels",
                            "Building level " + index.ToString() + " / " + JsonLevelRepository.MaxLevelCount.ToString(),
                            progress);
                    }

                    string message = presetBuilder.GenerateForLevelIndex(temp, activeCatalog, index);
                    if (temp.PlacementCount <= 0)
                    {
                        emptyCount++;
                    }

                    records.Add(levelRepository.CreateRecordFromLevel(temp));
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            levelRepository.ReplaceAll(records);
            PersistJson(true);
            jsonDirty = false;
            NavigateToLevel(editingLevelIndex);
            selectedZLayer = 0;
            hasSelectedPlacement = false;
            statusMessage = "Generated " + records.Count.ToString()
                + " levels into LevelsDatabase.json. Empty=" + emptyCount.ToString() + ".";
        }

        private void CopyGrid(LevelData source, LevelData target)
        {
            target.GridDimensions = source.GridDimensions;
            target.CellSize = source.CellSize;
            target.OriginOffset = source.OriginOffset;
            target.FallEliminationY = source.FallEliminationY;
            target.MaxAmmo = source.MaxAmmo;
            target.ParAmmo = source.ParAmmo;
        }

        private void DrawActionBar()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(activeLevel == null || activeCatalog == null);
            if (GUILayout.Button("Regenerate", GUILayout.MinWidth(70f)))
            {
                RegenerateCurrentLevel();
            }

            selectedPresetIndex = EditorGUILayout.Popup(selectedPresetIndex, presetLabels, GUILayout.MinWidth(90f));
            EditorGUI.BeginDisabledGroup(selectedPresetIndex <= 0);
            if (GUILayout.Button("Apply", GUILayout.Width(50f)))
            {
                ApplyPreset(selectedPresetIndex - 1);
            }

            EditorGUI.EndDisabledGroup();
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            string brushLabel = "None";
            if (hasBrush && activeCatalog != null && activeCatalog.TryGetBlockDefinition(brushBlockId, out BlockDefinition brushDefinition))
            {
                brushLabel = GetShortBlockName(brushDefinition.blockName) + " #" + brushDefinition.blockId.ToString();
            }

            EditorGUILayout.LabelField("Brush", GUILayout.Width(36f));
            EditorGUILayout.LabelField(brushLabel, EditorStyles.boldLabel, GUILayout.MinWidth(40f));
            if (GUILayout.Button("Clear Brush", GUILayout.MinWidth(76f)))
            {
                hasBrush = false;
                brushBlockId = 0;
                statusMessage = "Brush cleared.";
                hoverChanged = true;
            }

            EditorGUILayout.EndHorizontal();

            if (hasBrush || hasSelectedPlacement)
            {
                DrawCompactOrientationRow();
            }

            if (!hasBrush && hasSelectedPlacement && activeLevel != null
                && activeLevel.IsFoundationTable(selectedPlacement, activeCatalog))
            {
                DrawSelectedTableScaleRow();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawCompactOrientationRow()
        {
            EditorGUILayout.BeginHorizontal();
            string title = hasBrush ? "Angle" : "Block";
            if (!hasBrush && hasSelectedPlacement && activeLevel != null && activeLevel.IsFoundationTable(selectedPlacement, activeCatalog))
            {
                title = "Table";
            }

            EditorGUILayout.LabelField(title, GUILayout.Width(38f));
            if (GUILayout.Button("-45", GUILayout.MinWidth(24f)))
            {
                ApplyAngle(-45f);
            }

            if (GUILayout.Button("-30", GUILayout.MinWidth(24f)))
            {
                ApplyAngle(-30f);
            }

            if (GUILayout.Button("0", GUILayout.MinWidth(20f)))
            {
                ApplyAngle(0f);
            }

            if (GUILayout.Button("+30", GUILayout.MinWidth(24f)))
            {
                ApplyAngle(30f);
            }

            if (GUILayout.Button("+45", GUILayout.MinWidth(24f)))
            {
                ApplyAngle(45f);
            }

            if (GUILayout.Button("+90", GUILayout.MinWidth(24f)))
            {
                ApplyAngle(90f);
            }

            EditorGUILayout.LabelField("Axis", GUILayout.Width(28f));
            if (GUILayout.Button("X", GUILayout.MinWidth(20f)))
            {
                ApplyLengthAxis(0);
            }

            if (GUILayout.Button("Y", GUILayout.MinWidth(20f)))
            {
                ApplyLengthAxis(1);
            }

            if (GUILayout.Button("Z", GUILayout.MinWidth(20f)))
            {
                ApplyLengthAxis(2);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSelectedTableScaleRow()
        {
            Vector3Int size = activeLevel.GetSizeInCells(selectedPlacement, activeCatalog);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("TableTop", GUILayout.Width(52f));
            EditorGUILayout.LabelField("Width", GUILayout.Width(38f));
            if (GUILayout.Button("-", GUILayout.Width(22f)))
            {
                ApplySelectedTableSize(size.x - 1, size.z);
            }

            EditorGUILayout.LabelField(size.x.ToString(), GUILayout.Width(16f));
            if (GUILayout.Button("+", GUILayout.Width(22f)))
            {
                ApplySelectedTableSize(size.x + 1, size.z);
            }

            EditorGUILayout.LabelField("Depth", GUILayout.Width(38f));
            if (GUILayout.Button("-", GUILayout.Width(22f)))
            {
                ApplySelectedTableSize(size.x, size.z - 1);
            }

            EditorGUILayout.LabelField(size.z.ToString(), GUILayout.Width(16f));
            if (GUILayout.Button("+", GUILayout.Width(22f)))
            {
                ApplySelectedTableSize(size.x, size.z + 1);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void ApplySelectedTableSize(int width, int depth)
        {
            if (activeLevel == null || !hasSelectedPlacement)
            {
                return;
            }

            int clampedWidth = Mathf.Clamp(width, 1, 8);
            int clampedDepth = Mathf.Clamp(depth, 1, 6);
            Vector3Int newSize = new Vector3Int(clampedWidth, 1, clampedDepth);
            Vector3Int currentSize = activeLevel.GetEffectiveSize(selectedPlacement, activeCatalog);
            if (newSize == activeLevel.GetSizeInCells(selectedPlacement, activeCatalog))
            {
                return;
            }

            Vector3Int rotated = blockFootprint.GetEffectiveSize(newSize, selectedPlacement.rotationEuler);
            PlacementValidationStatus status = activeLevel.ValidatePlacementIgnoring(
                selectedPlacement.gridPosition,
                rotated,
                activeCatalog,
                selectedPlacement.gridPosition);
            if (status == PlacementValidationStatus.InvalidOutOfBounds)
            {
                statusMessage = "TableTop " + clampedWidth.ToString() + "x" + clampedDepth.ToString() + " does not fit.";
                Repaint();
                return;
            }

            BeginLevelEdit("Resize Table");
            BlockPlacementData updated = selectedPlacement;
            updated.customSize = newSize;
            activeLevel.TryRemoveAt(selectedPlacement.gridPosition);
            if (!activeLevel.PlaceBlock(updated, rotated, activeCatalog))
            {
                activeLevel.PlaceBlock(selectedPlacement, currentSize, activeCatalog);
                EndLevelEdit();
                statusMessage = "TableTop resize failed.";
                return;
            }

            selectedPlacement = updated;
            hasSelectedPlacement = true;
            statusMessage = "TableTop resized to " + clampedWidth.ToString() + "x1x" + clampedDepth.ToString() + ".";
            EndLevelEdit();
            hoverChanged = true;
            Repaint();
            SceneView.RepaintAll();
        }

        private void ApplyAngle(float yaw)
        {
            Vector3 euler = hasBrush ? brushRotationEuler : selectedPlacement.rotationEuler;
            euler.y = yaw;
            ApplyOrientation(euler, "Yaw set to " + yaw.ToString("0") + ".");
        }

        private void ApplyLengthAxis(int axis)
        {
            BlockDefinition definition;
            if (!TryGetActiveDefinition(out definition))
            {
                return;
            }

            Vector3Int sizeInCells = ResolveActiveSizeInCells(definition);
            Vector3 euler = blockFootprint.ResolveLengthAxisEuler(sizeInCells, axis);
            string axisName = "X";
            if (axis == 1)
            {
                axisName = "Y";
            }
            else if (axis == 2)
            {
                axisName = "Z";
            }

            ApplyOrientation(euler, "Axis set to " + axisName + ".");
        }

        private Vector3Int ResolveActiveSizeInCells(BlockDefinition definition)
        {
            if (!hasBrush && hasSelectedPlacement && activeLevel != null)
            {
                return activeLevel.GetSizeInCells(selectedPlacement, activeCatalog);
            }

            Vector3Int customSize = ResolveBrushCustomSize(definition);
            if (customSize.x > 0)
            {
                return customSize;
            }

            return definition.sizeInCells;
        }

        private void CycleLengthAxis()
        {
            BlockDefinition definition;
            if (!TryGetActiveDefinition(out definition))
            {
                return;
            }

            Vector3 current = hasBrush ? brushRotationEuler : selectedPlacement.rotationEuler;
            Vector3Int effective = blockFootprint.GetEffectiveSize(ResolveActiveSizeInCells(definition), current);
            int currentAxis = 0;
            if (effective.y >= effective.x && effective.y >= effective.z)
            {
                currentAxis = 1;
            }
            else if (effective.z >= effective.x && effective.z >= effective.y)
            {
                currentAxis = 2;
            }

            int nextAxis = currentAxis + 1;
            if (nextAxis > 2)
            {
                nextAxis = 0;
            }

            ApplyLengthAxis(nextAxis);
        }

        private void ApplyOrientation(Vector3 euler, string message)
        {
            if (hasBrush)
            {
                brushRotationEuler = euler;
                statusMessage = message;
                hoverChanged = true;
                Repaint();
                SceneView.RepaintAll();
                return;
            }

            if (!hasSelectedPlacement || activeLevel == null || activeCatalog == null)
            {
                return;
            }

            BlockDefinition definition;
            if (!activeCatalog.TryGetBlockDefinition(selectedPlacement.blockId, out definition))
            {
                return;
            }

            Vector3Int sizeInCells = activeLevel.GetSizeInCells(selectedPlacement, activeCatalog);
            Vector3Int size = blockFootprint.GetEffectiveSize(sizeInCells, euler);
            PlacementValidationStatus status = activeLevel.ValidatePlacementIgnoring(
                selectedPlacement.gridPosition,
                size,
                activeCatalog,
                selectedPlacement.gridPosition);
            if (status == PlacementValidationStatus.InvalidOutOfBounds)
            {
                statusMessage = "That orientation does not fit inside the grid.";
                return;
            }

            BlockPlacementData updated = selectedPlacement;
            updated.rotationEuler = euler;
            CommitLevelChange("Set Block Orientation", () => activeLevel.TrySetPlacement(updated));
            selectedPlacement = updated;
            if (isMovingExistingBlock)
            {
                moveRotationEuler = euler;
                moveOriginPlacement.rotationEuler = euler;
            }

            AssetDatabase.SaveAssets();
            statusMessage = message;
            hoverChanged = true;
            Repaint();
            SceneView.RepaintAll();
        }

        private bool TryGetActiveDefinition(out BlockDefinition definition)
        {
            definition = default;
            if (activeCatalog == null)
            {
                return false;
            }

            int blockId = brushBlockId;
            if (!hasBrush)
            {
                if (!hasSelectedPlacement)
                {
                    return false;
                }

                blockId = selectedPlacement.blockId;
            }

            return activeCatalog.TryGetBlockDefinition(blockId, out definition);
        }

        private void DrawLayerBar()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (activeLevel == null)
            {
                EditorGUILayout.LabelField("No level loaded.");
                EditorGUILayout.EndVertical();
                return;
            }

            int layerCount = activeLevel.GridDimensions.z;
            EnsureLayerLabels(layerCount);
            int maxLayer = layerCount - 1;
            int toolbarLayer = GUILayout.Toolbar(selectedZLayer, layerLabels, GUILayout.MinHeight(22f));
            if (toolbarLayer != selectedZLayer)
            {
                selectedZLayer = toolbarLayer;
                SyncPreview();
                SceneView.RepaintAll();
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(selectedZLayer <= 0);
            bool copyPrevious = GUILayout.Button("Copy Prev", GUILayout.MinWidth(60f));
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(selectedZLayer >= maxLayer);
            bool copyNext = GUILayout.Button("Copy Next", GUILayout.MinWidth(60f));
            EditorGUI.EndDisabledGroup();
            bool clearLayer = GUILayout.Button("Clear Layer", GUILayout.MinWidth(60f));
            EditorGUILayout.EndHorizontal();

            if (copyPrevious)
            {
                CopyPreviousLayer();
            }

            if (copyNext)
            {
                CopyLayerToNext();
            }

            if (clearLayer)
            {
                ClearActiveLayer();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawMatrix(float matrixHeight)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(matrixHeight));
            EditorGUILayout.LabelField("Matrix  |  Y = 0 is the ground row", EditorStyles.boldLabel);

            if (activeLevel == null)
            {
                if (isMovingExistingBlock)
                {
                    CancelMove();
                }

                if (hasHoveredCell || hasExistingHover || overwriteCount > 0)
                {
                    hasHoveredCell = false;
                    hasExistingHover = false;
                    overwriteCount = 0;
                    hoverChanged = true;
                }

                EditorGUILayout.HelpBox("No level is selected.", MessageType.None);
                EditorGUILayout.EndVertical();
                return;
            }

            Vector3Int dimensions = activeLevel.GridDimensions;
            UpdateMatrixCellMetrics(dimensions, matrixHeight);

            bool trackHover = ShouldTrackHover(Event.current);
            bool hadHover = hasHoveredCell;
            Vector3Int previousHoveredCell = hoveredCell;
            if (trackHover)
            {
                hasHoveredCell = false;
            }

            if (!trackHover)
            {
                UpdateHoverPresentation();
            }

            EditorGUILayout.BeginVertical();
            EnsureCellRectBuffer(dimensions.x, dimensions.y);
            DrawColumnHeaders(dimensions.x);
            for (int y = dimensions.y - 1; y >= 0; y--)
            {
                EditorGUILayout.BeginHorizontal();
                Rect rowLabelRect = GUILayoutUtility.GetRect(
                    MatrixRowLabelWidth,
                    matrixCellHeight,
                    GUILayout.Width(MatrixRowLabelWidth),
                    GUILayout.Height(matrixCellHeight));
                if (Event.current.type == EventType.Repaint)
                {
                    cellLabelStyle.normal.textColor = new Color(0.75f, 0.92f, 0.96f, 1f);
                    GUI.Label(rowLabelRect, y.ToString(), cellLabelStyle);
                }

                for (int x = 0; x < dimensions.x; x++)
                {
                    DrawCell(new Vector3Int(x, y, selectedZLayer));
                    if (x < dimensions.x - 1)
                    {
                        GUILayout.Space(MatrixCellGap);
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            if (Event.current != null && Event.current.type == EventType.Repaint)
            {
                DrawMergedBlockLabels();
                DrawUnifiedFrames();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndVertical();

            if (!trackHover)
            {
                return;
            }

            Vector3Int currentCell = hoveredCell;
            bool hoverCellChanged = hadHover != hasHoveredCell || (hasHoveredCell && currentCell != previousHoveredCell);
            if (hoverCellChanged)
            {
                hoverChanged = true;
            }

            UpdateHoverPresentation();
        }

        private void UpdateMatrixCellMetrics(Vector3Int dimensions, float matrixHeight)
        {
            int rows = Mathf.Max(1, dimensions.y);
            int columns = Mathf.Max(1, dimensions.x);
            float matrixWidth = position.width - WindowPadding;
            float widthBudget = matrixWidth - MatrixHorizontalOverhead - (MatrixCellGap * (columns - 1));
            float heightBudget = matrixHeight - MatrixVerticalOverhead;
            matrixCellWidth = Mathf.Max(MinMatrixCellSize, Mathf.Floor(widthBudget / columns));
            matrixCellHeight = Mathf.Max(MinMatrixCellSize, Mathf.Floor(heightBudget / rows));
        }

        private void DrawColumnHeaders(int width)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(28f);
            for (int x = 0; x < width; x++)
            {
                Rect headerRect = GUILayoutUtility.GetRect(
                    matrixCellWidth,
                    16f,
                    GUILayout.Width(matrixCellWidth),
                    GUILayout.Height(16f));
                if (Event.current.type == EventType.Repaint)
                {
                    cellLabelStyle.normal.textColor = new Color(0.75f, 0.92f, 0.96f, 1f);
                    GUI.Label(headerRect, x.ToString(), cellLabelStyle);
                }

                if (x < width - 1)
                {
                    GUILayout.Space(2f);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawCell(Vector3Int cell)
        {
            Rect cellRect = GUILayoutUtility.GetRect(
                matrixCellWidth,
                matrixCellHeight,
                GUILayout.Width(matrixCellWidth),
                GUILayout.Height(matrixCellHeight));
            Event current = Event.current;
            bool inside = current != null && cellRect.Contains(current.mousePosition);
            if (inside && ShouldTrackHover(current))
            {
                hoveredCell = cell;
                hasHoveredCell = true;
            }

            TrackCellHighlight(cell, cellRect);
            StoreCellRect(cell, cellRect);

            BlockPlacementData placement = default;
            bool occupied = activeLevel != null && activeLevel.TryGetBlockAt(cell, activeCatalog, out placement, out _);
            BlockDefinition definition = default;
            bool hasDefinition = occupied && activeCatalog != null && activeCatalog.TryGetBlockDefinition(placement.blockId, out definition);

            if (current != null && current.type == EventType.Repaint)
            {
                PaintCellVisual(cellRect, cell, occupied, hasDefinition, placement, definition, inside);
            }

            if (inside)
            {
                HandleCellInput(cell, current);
            }
        }

        private void PaintCellVisual(
            Rect cellRect,
            Vector3Int cell,
            bool occupied,
            bool hasDefinition,
            BlockPlacementData placement,
            BlockDefinition definition,
            bool inside)
        {
            Vector3Int effectiveSize = Vector3Int.one;
            if (occupied && hasDefinition && activeLevel != null)
            {
                effectiveSize = activeLevel.GetEffectiveSize(placement, activeCatalog);
            }

            bool multiCell = occupied && (effectiveSize.x > 1 || effectiveSize.y > 1);
            Color fill;
            if (occupied && hasDefinition)
            {
                fill = definition.editorColor;
                fill.a = 1f;
            }
            else if (occupied)
            {
                fill = new Color(0.45f, 0.45f, 0.48f, 1f);
            }
            else if (cell.y == 0)
            {
                fill = new Color(0.13f, 0.2f, 0.18f, 1f);
            }
            else
            {
                fill = new Color(0.16f, 0.17f, 0.2f, 1f);
            }

            EditorGUI.DrawRect(cellRect, fill);
            if (multiCell)
            {
                int localX = cell.x - placement.gridPosition.x;
                if (localX < effectiveSize.x - 1)
                {
                    EditorGUI.DrawRect(new Rect(cellRect.xMax, cellRect.y, 2f, cellRect.height), fill);
                }
            }

            bool inPreview = TryGetPreviewMembership(cell);
            bool moveOriginCell = IsMoveOriginCell(cell);
            if (moveOriginCell && !inPreview)
            {
                EditorGUI.DrawRect(cellRect, new Color(0.04f, 0.05f, 0.07f, 0.55f));
            }
            bool existingCell = IsExistingHoverCell(cell);
            bool overwriteCell = IsOverwriteCell(cell);
            if (overwriteCell && !inPreview)
            {
                EditorGUI.DrawRect(cellRect, new Color(1f, 0.34f, 0.08f, 0.42f));
            }
            else if (existingCell && !inPreview)
            {
                EditorGUI.DrawRect(cellRect, new Color(1f, 0.92f, 0.4f, 0.24f));
            }

            if (inPreview)
            {
                Color highlight = ColorForStatus(footprintStatus);
                highlight.a = footprintStatus == PlacementValidationStatus.InvalidOutOfBounds ? 0.62f : 0.48f;
                EditorGUI.DrawRect(cellRect, highlight);
            }

            Color border = new Color(0.28f, 0.34f, 0.38f, 1f);
            bool showCursorCell = inside && !inPreview && !existingCell && !overwriteCell;
            if (showCursorCell)
            {
                border = new Color(1f, 0.95f, 0.55f, 1f);
            }

            if (multiCell)
            {
                DrawMultiCellSeams(cellRect, cell, placement.gridPosition, effectiveSize);
                if (showCursorCell)
                {
                    DrawBorder(cellRect, border, 2f);
                }
            }
            else
            {
                DrawBorder(cellRect, border, showCursorCell ? 2f : 1f);
            }

            if (inPreview && footprintStatus == PlacementValidationStatus.InvalidOutOfBounds && cell == footprintAnchor)
            {
                DrawCenteredLabel(cellRect, "OUT OF BOUNDS", Color.black);
                return;
            }

            if (!occupied || multiCell)
            {
                return;
            }

            string label = hasDefinition
                ? FormatMatrixBlockLabel(definition.blockName, effectiveSize, true)
                : ("#" + placement.blockId.ToString() + "\n" + FormatFootprint(effectiveSize));
            DrawCenteredLabel(cellRect, label, fill);
        }

        private void DrawMergedBlockLabels()
        {
            if (activeLevel == null)
            {
                return;
            }

            int count = activeLevel.PlacementCount;
            for (int i = 0; i < count; i++)
            {
                BlockPlacementData placement;
                if (!activeLevel.TryGetPlacementByIndex(i, out placement))
                {
                    continue;
                }

                BlockDefinition definition = default;
                bool hasDefinition = activeCatalog != null && activeCatalog.TryGetBlockDefinition(placement.blockId, out definition);
                Vector3Int size = Vector3Int.one;
                if (hasDefinition)
                {
                    size = blockFootprint.GetEffectiveSize(definition.sizeInCells, placement.rotationEuler);
                }

                if (size.x <= 1 && size.y <= 1)
                {
                    continue;
                }

                int minZ = placement.gridPosition.z;
                int maxZ = minZ + size.z - 1;
                if (selectedZLayer < minZ || selectedZLayer > maxZ)
                {
                    continue;
                }

                Rect mergedRect;
                if (!TryGetMergedRect(placement.gridPosition, size, out mergedRect))
                {
                    continue;
                }

                DrawBorder(mergedRect, new Color(0.08f, 0.09f, 0.1f, 0.95f), 2f);
                string label = hasDefinition
                    ? FormatMatrixBlockLabel(definition.blockName, size, false)
                    : ("#" + placement.blockId.ToString() + " " + FormatFootprint(size));
                Color fill = hasDefinition ? definition.editorColor : new Color(0.45f, 0.45f, 0.48f, 1f);
                DrawCenteredLabel(mergedRect, label, fill);
            }
        }

        private void DrawCenteredLabel(Rect rect, string text, Color background)
        {
            matrixLabelStyle.normal.textColor = GetReadableTextColor(background);
            GUI.Label(rect, text, matrixLabelStyle);
        }

        private void DrawMultiCellSeams(Rect cellRect, Vector3Int cell, Vector3Int anchor, Vector3Int size)
        {
            Color seam = new Color(1f, 1f, 1f, 0.16f);
            int localX = cell.x - anchor.x;
            int localY = cell.y - anchor.y;
            if (localX > 0)
            {
                EditorGUI.DrawRect(new Rect(cellRect.x, cellRect.y, 1f, cellRect.height), seam);
            }

            if (localX < size.x - 1)
            {
                EditorGUI.DrawRect(new Rect(cellRect.xMax - 1f, cellRect.y, 1f, cellRect.height), seam);
            }

            if (localY > 0)
            {
                EditorGUI.DrawRect(new Rect(cellRect.x, cellRect.yMax - 1f, cellRect.width, 1f), seam);
            }

            if (localY < size.y - 1)
            {
                EditorGUI.DrawRect(new Rect(cellRect.x, cellRect.y, cellRect.width, 1f), seam);
            }
        }

        private bool TryGetMergedRect(Vector3Int anchor, Vector3Int size, out Rect mergedRect)
        {
            mergedRect = default;
            Rect firstCell;
            Rect lastCell;
            if (!TryGetStoredCellRect(anchor.x, anchor.y, out firstCell))
            {
                return false;
            }

            if (!TryGetStoredCellRect(anchor.x + size.x - 1, anchor.y + size.y - 1, out lastCell))
            {
                return false;
            }

            float xMin = firstCell.x < lastCell.x ? firstCell.x : lastCell.x;
            float yMin = firstCell.y < lastCell.y ? firstCell.y : lastCell.y;
            float xMax = firstCell.xMax > lastCell.xMax ? firstCell.xMax : lastCell.xMax;
            float yMax = firstCell.yMax > lastCell.yMax ? firstCell.yMax : lastCell.yMax;
            mergedRect = new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
            return mergedRect.width > 0f && mergedRect.height > 0f;
        }

        private bool TryGetStoredCellRect(int x, int y, out Rect cellRect)
        {
            cellRect = default;
            if (x < 0 || y < 0 || matrixCellStride <= 0)
            {
                return false;
            }

            int index = x + (y * matrixCellStride);
            if (index < 0 || index >= matrixCellRects.Length)
            {
                return false;
            }

            cellRect = matrixCellRects[index];
            return cellRect.width > 0f && cellRect.height > 0f;
        }

        private void EnsureCellRectBuffer(int width, int height)
        {
            int needed = width * height;
            if (needed < 1)
            {
                needed = 1;
            }

            if (matrixCellStride == width && matrixCellRects.Length >= needed)
            {
                return;
            }

            matrixCellRects = new Rect[needed];
            matrixCellStride = width;
        }

        private void StoreCellRect(Vector3Int cell, Rect cellRect)
        {
            if (cell.x < 0 || cell.y < 0 || matrixCellStride <= 0)
            {
                return;
            }

            int index = cell.x + (cell.y * matrixCellStride);
            if (index < 0 || index >= matrixCellRects.Length)
            {
                return;
            }

            matrixCellRects[index] = cellRect;
        }

        private void HandleCellInput(Vector3Int cell, Event current)
        {
            if (current.type == EventType.DragUpdated || current.type == EventType.DragPerform)
            {
                HandleCellDrop(cell, current);
                return;
            }

            if (current.type == EventType.MouseDown && current.button == 0)
            {
                if (isMovingExistingBlock)
                {
                    CancelMove();
                }

                if (!hasBrush && !dragFootprintActive)
                {
                    if (TryBeginMove(cell))
                    {
                        current.Use();
                        return;
                    }

                    hasSelectedPlacement = false;
                }

                isPainting = true;
                isErasing = false;
                hasLastEditedCell = false;
                PaintCell(cell);
                current.Use();
                return;
            }

            if (isMovingExistingBlock)
            {
                if (current.type != EventType.Layout && current.type != EventType.Repaint)
                {
                    current.Use();
                }

                return;
            }

            if (current.type == EventType.MouseDown && current.button == 1)
            {
                isErasing = true;
                isPainting = false;
                hasLastEditedCell = false;
                EraseCell(cell);
                current.Use();
                return;
            }

            if (current.type == EventType.MouseDrag && isPainting)
            {
                PaintCell(cell);
                current.Use();
                return;
            }

            if (current.type == EventType.MouseDrag && isErasing)
            {
                EraseCell(cell);
                current.Use();
            }
        }

        private void HandleCellDrop(Vector3Int cell, Event current)
        {
            int blockId;
            if (!dragPayload.TryGetBlockId(out blockId))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                current.Use();
                return;
            }

            BlockDefinition definition;
            if (activeCatalog == null || !activeCatalog.TryGetBlockDefinition(blockId, out definition))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                current.Use();
                return;
            }

            dragFootprintActive = true;
            dragFootprintBlockId = blockId;
            hasBrush = true;
            brushBlockId = blockId;
            Vector3Int customSize = ResolveBrushCustomSize(definition);
            Vector3Int sizeInCells = customSize.x > 0 ? customSize : definition.sizeInCells;
            Vector3Int effectiveSize = blockFootprint.GetEffectiveSize(sizeInCells, brushRotationEuler);
            PlacementValidationStatus status = PlacementValidationStatus.InvalidOutOfBounds;
            if (activeLevel != null)
            {
                status = activeLevel.ValidatePlacement(cell, effectiveSize, activeCatalog);
            }

            if (status == PlacementValidationStatus.InvalidOutOfBounds)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
            }
            else
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            }

            if (current.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                hasBrush = true;
                brushBlockId = blockId;
                hasLastEditedCell = false;
                if (status != PlacementValidationStatus.InvalidOutOfBounds)
                {
                    PaintCell(cell);
                    statusMessage = "Placed " + GetBlockLabel(definition) + ".";
                }
                else
                {
                    statusMessage = "OUT OF BOUNDS. The footprint does not fit.";
                }

                EndLevelEdit();
                isPainting = false;
                dragFootprintActive = false;
            }

            current.Use();
        }

        private void DrawBottomPalette()
        {
            float availWidth = position.width - WindowPadding - 8f;
            bool narrow = position.width < NarrowPaletteWidth;
            float filterHeight = narrow ? 48f : 26f;
            float gridHeight = PaletteHeight - filterHeight - SizeStripHeight - PaletteVerticalSlack;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(PaletteHeight));
            DrawPaletteFilterRow(availWidth, narrow);

            if (activeCatalog == null)
            {
                EditorGUILayout.HelpBox("Assign a Block Catalog to choose blocks.", MessageType.None);
            }
            else if (activeCatalog.FamilyCount == 0 && activeCatalog.StandaloneCount == 0)
            {
                EditorGUILayout.HelpBox("The catalog has no families or standalone blocks yet.", MessageType.Warning);
            }
            else
            {
                DrawSizeStrip(availWidth);
                DrawPaletteGrid(availWidth, gridHeight);
            }

            EditorGUILayout.EndVertical();

            if (AssetPreview.IsLoadingAssetPreviews())
            {
                Repaint();
            }
        }

        private void DrawPaletteFilterRow(float availWidth, bool narrow)
        {
            if (narrow)
            {
                paletteCategoryIndex = GUILayout.Toolbar(
                    paletteCategoryIndex,
                    paletteCategoryLabels,
                    GUILayout.Height(22f));
                EditorGUILayout.BeginHorizontal(GUILayout.Height(22f));
                EditorGUILayout.LabelField("Search", GUILayout.Width(44f));
                paletteSearch = EditorGUILayout.TextField(paletteSearch);
                EditorGUILayout.EndHorizontal();
                return;
            }

            EditorGUILayout.BeginHorizontal(GUILayout.Height(26f));
            paletteCategoryIndex = GUILayout.Toolbar(
                paletteCategoryIndex,
                paletteCategoryLabels,
                GUILayout.Height(22f),
                GUILayout.Width(availWidth * 0.62f));
            EditorGUILayout.LabelField("Search", GUILayout.Width(44f));
            paletteSearch = EditorGUILayout.TextField(paletteSearch);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSizeStrip(float availWidth)
        {
            Rect stripRect = GUILayoutUtility.GetRect(
                0f,
                SizeStripHeight,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(SizeStripHeight));
            stripRect.width = availWidth;
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(stripRect, new Color(0.1f, 0.11f, 0.13f, 1f));
                DrawBorder(stripRect, new Color(0.34f, 0.38f, 0.42f, 1f), 1f);
            }

            if (selectedCardKey < 0)
            {
                GUI.Label(stripRect, "Click a block card to pick a size. Drag any card to place its 1x block.", EditorStyles.wordWrappedMiniLabel);
                return;
            }

            if (selectedCardKey >= StandaloneCardKeyOffset)
            {
                BlockDefinition standalone;
                if (!activeCatalog.TryGetStandalone(selectedCardKey - StandaloneCardKeyOffset, out standalone))
                {
                    return;
                }

                if (IsStandaloneTable(standalone))
                {
                    DrawTableStrip(
                        stripRect,
                        standalone,
                        new Color(0.72f, 0.48f, 0.28f, 1f),
                        new Color(0.82f, 0.58f, 0.35f, 1f));
                    return;
                }

                GUI.Label(stripRect, GetBlockLabel(standalone) + ": " + FormatFootprint(standalone.sizeInCells) + " only. Drag the card onto the matrix.", EditorStyles.wordWrappedMiniLabel);
                return;
            }

            BlockFamilyDefinition family;
            if (!activeCatalog.TryGetFamily(selectedCardKey, out family))
            {
                return;
            }

            if (family.foundationTable)
            {
                BlockDefinition tableDefinition;
                if (TryResolveFamilyDefault(selectedCardKey, family, out tableDefinition))
                {
                    DrawTableStrip(stripRect, tableDefinition, family.xColor, family.zColor);
                }

                return;
            }

            if (family.GetHighestAssignedLength() <= 1)
            {
                GUI.Label(stripRect, family.familyName + ": 1x1 only. Drag the card onto the matrix.", EditorStyles.wordWrappedMiniLabel);
                return;
            }

            DrawFamilyStrip(stripRect, selectedCardKey, family);
        }

        private void DrawFamilyStrip(Rect stripRect, int familyIndex, BlockFamilyDefinition family)
        {
            float innerWidth = stripRect.width - (StripPadding * 2f);
            if (family.onlyVerticalAxis)
            {
                float verticalChipWidth = Mathf.Clamp((innerWidth - StripLabelWidth - (4f * StripChipGap)) / 5f, 16f, 64f);
                DrawStripAxis(stripRect.x + StripPadding, stripRect.y + StripPadding, verticalChipWidth, familyIndex, family, 1);
                return;
            }

            float half = (innerWidth - StripColumnGap) * 0.5f;
            float chipWidth = Mathf.Max(16f, (half - StripLabelWidth - (4f * StripChipGap)) / 5f);
            float rowOneY = stripRect.y + StripPadding;
            float rowTwoY = rowOneY + FamilyChipHeight + StripRowGap;
            float leftX = stripRect.x + StripPadding;
            float rightX = leftX + half + StripColumnGap;
            DrawStripAxis(leftX, rowOneY, chipWidth, familyIndex, family, 0);
            DrawStripAxis(rightX, rowOneY, chipWidth, familyIndex, family, 1);
            DrawStripAxis(leftX, rowTwoY, chipWidth, familyIndex, family, 2);
        }

        private void DrawStripAxis(float originX, float originY, float chipWidth, int familyIndex, BlockFamilyDefinition family, int axis)
        {
            Color rowColor = family.xColor;
            string axisLabel = "x";
            if (axis == 1)
            {
                rowColor = family.yColor;
                axisLabel = "y";
            }
            else if (axis == 2)
            {
                rowColor = family.zColor;
                axisLabel = "z";
            }

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(new Rect(originX, originY + 4f, 4f, FamilyChipHeight - 8f), rowColor);
                paletteIdStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(originX + 6f, originY, StripLabelWidth - 6f, FamilyChipHeight), axisLabel.ToUpperInvariant() + ":", paletteIdStyle);
            }

            for (int length = 1; length <= BlockFamilyDefinition.MaxLength; length++)
            {
                Rect chipRect = new Rect(
                    originX + StripLabelWidth + ((length - 1) * (chipWidth + StripChipGap)),
                    originY,
                    chipWidth,
                    FamilyChipHeight);
                BlockDefinition definition = default;
                bool available = length <= family.GetHighestAssignedLength()
                    && activeCatalog.TryGetFamilyVariant(familyIndex, length, axis, out definition);

                DrawFamilyChip(chipRect, length.ToString() + axisLabel, rowColor, available, definition);
            }
        }

        private void DrawTableStrip(Rect stripRect, BlockDefinition definition, Color widthColor, Color depthColor)
        {
            float innerWidth = stripRect.width - (StripPadding * 2f);
            float chipWidth = Mathf.Max(16f, (innerWidth - TableStripLabelWidth - (7f * StripChipGap)) / 8f);
            float rowOneY = stripRect.y + StripPadding;
            float rowTwoY = rowOneY + FamilyChipHeight + StripRowGap;
            float originX = stripRect.x + StripPadding;
            if (Event.current.type == EventType.Repaint)
            {
                paletteIdStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(originX, rowOneY, TableStripLabelWidth, FamilyChipHeight), "Width (X):", paletteIdStyle);
                GUI.Label(new Rect(originX, rowTwoY, TableStripLabelWidth, FamilyChipHeight), "Depth (Z):", paletteIdStyle);
            }

            for (int width = 1; width <= 8; width++)
            {
                Rect chipRect = new Rect(
                    originX + TableStripLabelWidth + ((width - 1) * (chipWidth + StripChipGap)),
                    rowOneY,
                    chipWidth,
                    FamilyChipHeight);
                bool selected = hasBrush
                    && brushBlockId == definition.blockId
                    && brushTableWidth == width;
                DrawTableWidthChip(chipRect, width.ToString() + "x", widthColor, definition, width, selected);
            }

            for (int depth = 1; depth <= 4; depth++)
            {
                Rect chipRect = new Rect(
                    originX + TableStripLabelWidth + ((depth - 1) * (chipWidth + StripChipGap)),
                    rowTwoY,
                    chipWidth,
                    FamilyChipHeight);
                bool selected = hasBrush
                    && brushBlockId == definition.blockId
                    && brushTableDepth == depth;
                DrawTableDepthChip(chipRect, depth.ToString() + "z", depthColor, definition, depth, selected);
            }
        }

        private void DrawPaletteGrid(float availWidth, float gridHeight)
        {
            BuildVisibleCardKeys();
            float contentWidth = availWidth - GridScrollbarWidth;
            paletteScroll = EditorGUILayout.BeginScrollView(
                paletteScroll,
                false,
                true,
                GUIStyle.none,
                GUI.skin.verticalScrollbar,
                GUI.skin.scrollView,
                GUILayout.Height(gridHeight));

            int cardCount = visibleCardKeys.Count;
            if (cardCount == 0)
            {
                EditorGUILayout.LabelField("No blocks match the filter.", EditorStyles.miniLabel);
                EditorGUILayout.EndScrollView();
                return;
            }

            int columns = Mathf.Clamp((int)(contentWidth / PaletteCardMinWidth), 2, 5);
            float cardWidth = (contentWidth - ((columns - 1) * PaletteCardGap)) / columns;
            int rows = (cardCount + columns - 1) / columns;
            float contentHeight = rows * (PaletteCardHeight + PaletteCardGap);
            Rect contentRect = GUILayoutUtility.GetRect(
                contentWidth,
                contentHeight,
                GUILayout.Width(contentWidth),
                GUILayout.Height(contentHeight));

            for (int i = 0; i < cardCount; i++)
            {
                int column = i % columns;
                int row = i / columns;
                Rect cardRect = new Rect(
                    contentRect.x + (column * (cardWidth + PaletteCardGap)),
                    contentRect.y + (row * (PaletteCardHeight + PaletteCardGap)),
                    cardWidth,
                    PaletteCardHeight);
                DrawPaletteCard(cardRect, visibleCardKeys[i], gridHeight);
            }

            EditorGUILayout.EndScrollView();
        }

        private void BuildVisibleCardKeys()
        {
            visibleCardKeys.Clear();
            int familyCount = activeCatalog.FamilyCount;
            for (int i = 0; i < familyCount; i++)
            {
                BlockFamilyDefinition family;
                if (!activeCatalog.TryGetFamily(i, out family))
                {
                    continue;
                }

                if (PassesPaletteFilter(family.familyName, family.foundationTable, family.fragileProp, family.materialType))
                {
                    visibleCardKeys.Add(i);
                }
            }

            int standaloneCount = activeCatalog.StandaloneCount;
            for (int i = 0; i < standaloneCount; i++)
            {
                BlockDefinition definition;
                if (!activeCatalog.TryGetStandalone(i, out definition))
                {
                    continue;
                }

                bool isJar = definition.shapeRole == BlockShapeRole.FragileProp
                    || definition.shapeRole == BlockShapeRole.TopOrnament;
                if (PassesPaletteFilter(definition.blockName, IsStandaloneTable(definition), isJar, definition.materialType))
                {
                    visibleCardKeys.Add(StandaloneCardKeyOffset + i);
                }
            }
        }

        private bool IsStandaloneTable(BlockDefinition definition)
        {
            return definition.shapeRole == BlockShapeRole.FoundationTable
                || definition.materialType == BlockMaterialType.TableFoundation;
        }

        private bool PassesPaletteFilter(string name, bool isTable, bool isJarOrProp, BlockMaterialType materialType)
        {
            if (!string.IsNullOrEmpty(paletteSearch))
            {
                if (string.IsNullOrEmpty(name) || name.IndexOf(paletteSearch, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return false;
                }
            }

            if (paletteCategoryIndex <= 0)
            {
                return true;
            }

            string safeName = string.IsNullOrEmpty(name) ? string.Empty : name;
            if (paletteCategoryIndex == 1)
            {
                return isTable || safeName.IndexOf("Table", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            if (paletteCategoryIndex == 2)
            {
                return safeName.IndexOf("Log", StringComparison.OrdinalIgnoreCase) >= 0
                    || safeName.IndexOf("Wood", StringComparison.OrdinalIgnoreCase) >= 0
                    || safeName.IndexOf("Snow", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            if (paletteCategoryIndex == 3)
            {
                return safeName.IndexOf("Can", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            if (paletteCategoryIndex == 4)
            {
                return safeName.IndexOf("Ice", StringComparison.OrdinalIgnoreCase) >= 0
                    || safeName.IndexOf("Glass", StringComparison.OrdinalIgnoreCase) >= 0
                    || materialType == BlockMaterialType.Glass;
            }

            if (paletteCategoryIndex == 5)
            {
                return safeName.IndexOf("Box", StringComparison.OrdinalIgnoreCase) >= 0
                    || safeName.IndexOf("Stone", StringComparison.OrdinalIgnoreCase) >= 0
                    || materialType == BlockMaterialType.Stone;
            }

            return isJarOrProp
                || safeName.IndexOf("Jar", StringComparison.OrdinalIgnoreCase) >= 0
                || safeName.IndexOf("Cone", StringComparison.OrdinalIgnoreCase) >= 0
                || safeName.IndexOf("Ornament", StringComparison.OrdinalIgnoreCase) >= 0
                || materialType == BlockMaterialType.CeramicJar;
        }

        private void DrawPaletteCard(Rect rect, int cardKey, float gridHeight)
        {
            BlockDefinition dragDefinition = default;
            bool hasDragDefinition;
            string title;
            Texture preview;
            Color stripeColor;
            bool isTable;
            if (cardKey >= StandaloneCardKeyOffset)
            {
                BlockDefinition standalone;
                if (!activeCatalog.TryGetStandalone(cardKey - StandaloneCardKeyOffset, out standalone))
                {
                    return;
                }

                dragDefinition = standalone;
                hasDragDefinition = true;
                title = GetCardDisplayName(GetBlockLabel(standalone));
                preview = GetPreviewTexture(standalone);
                stripeColor = standalone.editorColor;
                isTable = IsStandaloneTable(standalone);
            }
            else
            {
                BlockFamilyDefinition family;
                if (!activeCatalog.TryGetFamily(cardKey, out family))
                {
                    return;
                }

                hasDragDefinition = TryResolveFamilyDefault(cardKey, family, out dragDefinition);
                title = GetCardDisplayName(string.IsNullOrEmpty(family.familyName) ? "Family" : family.familyName);
                preview = GetFamilyPreviewTexture(family);
                stripeColor = family.xColor;
                isTable = family.foundationTable;
            }

            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            Event current = Event.current;
            EventType eventType = current.GetTypeForControl(controlId);

            if (eventType == EventType.Repaint)
            {
                bool selected = cardKey == selectedCardKey;
                EditorGUI.DrawRect(rect, selected ? new Color(0.2f, 0.26f, 0.31f, 1f) : new Color(0.17f, 0.18f, 0.2f, 1f));
                DrawBorder(
                    rect,
                    selected ? new Color(0.4f, 0.95f, 1f, 1f) : new Color(0.34f, 0.38f, 0.42f, 1f),
                    selected ? 2f : 1f);
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), stripeColor);
                Rect previewRect = new Rect(
                    rect.x + 8f,
                    rect.y + ((rect.height - PalettePreviewSize) * 0.5f),
                    PalettePreviewSize,
                    PalettePreviewSize);
                if (preview != null)
                {
                    GUI.DrawTexture(previewRect, preview, ScaleMode.ScaleToFit);
                }

                paletteTitleStyle.normal.textColor = Color.white;
                GUI.Label(
                    new Rect(previewRect.xMax + 4f, rect.y, rect.xMax - previewRect.xMax - 6f, rect.height),
                    title,
                    paletteTitleStyle);
                return;
            }

            bool insideView = current.mousePosition.y >= paletteScroll.y
                && current.mousePosition.y <= paletteScroll.y + gridHeight;
            if (eventType == EventType.MouseDown
                && current.button == 0
                && insideView
                && rect.Contains(current.mousePosition))
            {
                GUIUtility.hotControl = controlId;
                paletteCardPressPosition = current.mousePosition;
                current.Use();
                return;
            }

            if (GUIUtility.hotControl != controlId)
            {
                return;
            }

            if (eventType == EventType.MouseDrag)
            {
                if ((current.mousePosition - paletteCardPressPosition).sqrMagnitude >= CardDragThresholdSqr)
                {
                    GUIUtility.hotControl = 0;
                    if (hasDragDefinition)
                    {
                        BeginCardDrag(dragDefinition, isTable);
                    }
                }

                current.Use();
                return;
            }

            if (eventType != EventType.MouseUp)
            {
                return;
            }

            GUIUtility.hotControl = 0;
            if (rect.Contains(current.mousePosition))
            {
                selectedCardKey = cardKey;
                if (hasDragDefinition)
                {
                    SelectCardBrush(dragDefinition);
                }
            }

            current.Use();
            Repaint();
        }

        private string GetCardDisplayName(string blockName)
        {
            if (string.IsNullOrEmpty(blockName))
            {
                return "Block";
            }

            string trimmed = blockName;
            if (trimmed.EndsWith("Cylinder", StringComparison.Ordinal) && trimmed.Length > "Cylinder".Length)
            {
                trimmed = trimmed.Substring(0, trimmed.Length - "Cylinder".Length);
            }
            else if (trimmed.EndsWith("Block", StringComparison.Ordinal) && trimmed.Length > "Block".Length)
            {
                trimmed = trimmed.Substring(0, trimmed.Length - "Block".Length);
            }

            return trimmed;
        }

        private bool TryResolveFamilyDefault(int familyIndex, BlockFamilyDefinition family, out BlockDefinition definition)
        {
            if (family.foundationTable && activeCatalog.TryGetFoundationTable(out definition))
            {
                return true;
            }

            return activeCatalog.TryGetFamilyVariant(familyIndex, 1, family.onlyVerticalAxis ? 1 : 0, out definition);
        }

        private void BeginCardDrag(BlockDefinition definition, bool isTable)
        {
            CancelMove();
            hasBrush = true;
            brushBlockId = definition.blockId;
            string label = GetBlockLabel(definition);
            if (isTable)
            {
                brushTableWidth = DefaultTableWidth;
                brushTableDepth = DefaultTableDepth;
                label = "Table " + DefaultTableWidth.ToString() + "x" + DefaultTableDepth.ToString();
            }

            statusMessage = "Brush: " + label + ". Drag it onto the matrix.";
            dragPayload.Begin(definition.blockId, label);
            Repaint();
        }

        private void SelectCardBrush(BlockDefinition definition)
        {
            CancelMove();
            hasBrush = true;
            brushBlockId = definition.blockId;
            statusMessage = "Brush: " + GetBlockLabel(definition) + ". Drag it onto the matrix or click cells to paint.";
        }

        private void DrawTableWidthChip(
            Rect chipRect,
            string label,
            Color rowColor,
            BlockDefinition definition,
            int width,
            bool selected)
        {
            Event current = Event.current;
            if (current != null && current.type == EventType.Repaint)
            {
                Color fill = selected ? Color.Lerp(rowColor, Color.white, 0.35f) : rowColor;
                EditorGUI.DrawRect(chipRect, fill);
                DrawBorder(chipRect, selected ? Color.white : new Color(0.1f, 0.1f, 0.1f, 0.6f), selected ? 2f : 1f);
                paletteIdStyle.normal.textColor = GetReadableTextColor(fill);
                GUI.Label(chipRect, label, paletteIdStyle);
            }

            if (current == null || current.type != EventType.MouseDown || current.button != 0)
            {
                return;
            }

            if (!chipRect.Contains(current.mousePosition))
            {
                return;
            }

            CancelMove();
            hasBrush = true;
            brushBlockId = definition.blockId;
            brushTableWidth = width;
            statusMessage = "Brush: Table "
                + brushTableWidth.ToString()
                + "x1x"
                + brushTableDepth.ToString()
                + ". Drag onto the matrix.";
            dragPayload.Begin(definition.blockId, "Table " + brushTableWidth.ToString() + "x" + brushTableDepth.ToString());
            current.Use();
            Repaint();
        }

        private void DrawTableDepthChip(
            Rect chipRect,
            string label,
            Color rowColor,
            BlockDefinition definition,
            int depth,
            bool selected)
        {
            Event current = Event.current;
            if (current != null && current.type == EventType.Repaint)
            {
                Color fill = selected ? Color.Lerp(rowColor, Color.white, 0.35f) : rowColor;
                EditorGUI.DrawRect(chipRect, fill);
                DrawBorder(chipRect, selected ? Color.white : new Color(0.1f, 0.1f, 0.1f, 0.6f), selected ? 2f : 1f);
                paletteIdStyle.normal.textColor = GetReadableTextColor(fill);
                GUI.Label(chipRect, label, paletteIdStyle);
            }

            if (current == null || current.type != EventType.MouseDown || current.button != 0)
            {
                return;
            }

            if (!chipRect.Contains(current.mousePosition))
            {
                return;
            }

            CancelMove();
            hasBrush = true;
            brushBlockId = definition.blockId;
            brushTableDepth = depth;
            statusMessage = "Brush: Table "
                + brushTableWidth.ToString()
                + "x1x"
                + brushTableDepth.ToString()
                + ". Drag onto the matrix.";
            dragPayload.Begin(definition.blockId, "Table " + brushTableWidth.ToString() + "x" + brushTableDepth.ToString());
            current.Use();
            Repaint();
        }

        private Vector3Int ResolveBrushCustomSize(BlockDefinition definition)
        {
            if (definition.shapeRole == BlockShapeRole.FoundationTable
                || definition.materialType == BlockMaterialType.TableFoundation)
            {
                return new Vector3Int(
                    Mathf.Clamp(brushTableWidth, 1, 8),
                    1,
                    Mathf.Clamp(brushTableDepth, 1, 6));
            }

            return Vector3Int.zero;
        }

        private Texture GetFamilyPreviewTexture(BlockFamilyDefinition family)
        {
            if (family == null)
            {
                return null;
            }

            for (int length = 1; length <= BlockFamilyDefinition.MaxLength; length++)
            {
                GameObject prefab = family.GetLengthPrefab(length);
                if (prefab == null)
                {
                    continue;
                }

                Texture preview = AssetPreview.GetAssetPreview(prefab);
                if (preview == null)
                {
                    preview = AssetPreview.GetMiniThumbnail(prefab);
                }

                if (preview != null)
                {
                    return preview;
                }
            }

            return null;
        }

        private void DrawFamilyChip(Rect chipRect, string label, Color rowColor, bool available, BlockDefinition definition)
        {
            Event current = Event.current;
            bool selected = available && hasBrush && brushBlockId == definition.blockId;
            if (current != null && current.type == EventType.Repaint)
            {
                Color fill = available ? rowColor : new Color(0.25f, 0.25f, 0.25f, 1f);
                if (selected)
                {
                    fill = Color.Lerp(fill, Color.white, 0.35f);
                }

                EditorGUI.DrawRect(chipRect, fill);
                DrawBorder(chipRect, selected ? Color.white : new Color(0.1f, 0.1f, 0.1f, 0.6f), selected ? 2f : 1f);
                paletteIdStyle.normal.textColor = GetReadableTextColor(fill);
                GUI.Label(chipRect, label, paletteIdStyle);
            }

            if (!available || current == null || current.type != EventType.MouseDown || current.button != 0)
            {
                return;
            }

            if (!chipRect.Contains(current.mousePosition))
            {
                return;
            }

            CancelMove();
            hasBrush = true;
            brushBlockId = definition.blockId;
            statusMessage = "Brush: " + GetBlockLabel(definition) + ". Drag the chip onto the matrix.";
            dragPayload.Begin(definition.blockId, GetBlockLabel(definition));
            current.Use();
            Repaint();
        }

        private void DrawStatusBar()
        {
            string hoverText = hasHoveredCell
                ? ("Cell " + hoveredCell.x + "," + hoveredCell.y + "," + hoveredCell.z)
                : "No cell";
            EditorGUILayout.LabelField(statusMessage + "  |  " + hoverText + "  |  R rotate  T axis", EditorStyles.miniLabel);
        }

        private void PaintCell(Vector3Int cell)
        {
            if (activeLevel == null || !hasBrush)
            {
                statusMessage = "Select a block from the palette.";
                return;
            }

            BlockDefinition definition;
            if (activeCatalog == null || !activeCatalog.TryGetBlockDefinition(brushBlockId, out definition))
            {
                statusMessage = "The brush block is missing from the catalog.";
                return;
            }

            if (hasLastEditedCell && lastEditedCell == cell)
            {
                return;
            }

            Vector3Int customSize = ResolveBrushCustomSize(definition);
            Vector3Int sizeInCells = customSize.x > 0 ? customSize : definition.sizeInCells;
            Vector3Int effectiveSize = blockFootprint.GetEffectiveSize(sizeInCells, brushRotationEuler);
            PlacementValidationStatus status = activeLevel.ValidatePlacement(cell, effectiveSize, activeCatalog);
            if (status == PlacementValidationStatus.InvalidOutOfBounds)
            {
                lastEditedCell = cell;
                hasLastEditedCell = true;
                statusMessage = "OUT OF BOUNDS. " + GetBlockLabel(definition) + " does not fit at " + cell.x + ", " + cell.y + ", " + cell.z + ".";
                Repaint();
                SceneView.RepaintAll();
                return;
            }

            BlockPlacementData existing;
            if (activeLevel.TryGetPlacement(cell, out existing)
                && existing.blockId == brushBlockId
                && existing.rotationEuler == brushRotationEuler
                && existing.customSize == customSize)
            {
                lastEditedCell = cell;
                hasLastEditedCell = true;
                return;
            }

            BeginLevelEdit("Paint Block");
            BlockPlacementData placement = new BlockPlacementData();
            placement.blockId = brushBlockId;
            placement.gridPosition = cell;
            placement.rotationEuler = brushRotationEuler;
            placement.customSize = customSize;
            bool placed = activeLevel.PlaceBlock(placement, effectiveSize, activeCatalog);
            if (!placed)
            {
                EndLevelEdit();
                statusMessage = "OUT OF BOUNDS. The footprint does not fit.";
                return;
            }

            lastEditedCell = cell;
            hasLastEditedCell = true;
            statusMessage = "Placed " + GetBlockLabel(definition) + " " + FormatFootprint(effectiveSize) + " at " + cell.x + ", " + cell.y + ", " + cell.z + ".";
            Repaint();
            SceneView.RepaintAll();
        }

        private void EraseCell(Vector3Int cell)
        {
            if (activeLevel == null)
            {
                return;
            }

            BlockPlacementData placement;
            bool isAnchorCell;
            if (!activeLevel.TryGetBlockAt(cell, activeCatalog, out placement, out isAnchorCell))
            {
                return;
            }

            if (hasLastEditedCell && lastEditedCell == placement.gridPosition)
            {
                return;
            }

            BeginLevelEdit("Erase Block");
            activeLevel.TryRemoveAt(placement.gridPosition);
            lastEditedCell = placement.gridPosition;
            hasLastEditedCell = true;
            if (isAnchorCell)
            {
                statusMessage = "Erased the anchor at " + placement.gridPosition.x + ", " + placement.gridPosition.y + ", " + placement.gridPosition.z + ".";
            }
            else
            {
                statusMessage = "Erased the block covering " + cell.x + ", " + cell.y + ", " + cell.z + ".";
            }
            Repaint();
            SceneView.RepaintAll();
        }

        private void CopyPreviousLayer()
        {
            if (activeLevel == null || selectedZLayer <= 0)
            {
                return;
            }

            int copied = 0;
            int sourceLayer = selectedZLayer - 1;
            CommitLevelChange("Copy Previous Layer", () =>
            {
                copied = activeLevel.CopyLayer(sourceLayer, selectedZLayer, activeCatalog);
            });
            statusMessage = "Copied " + copied.ToString() + " blocks from layer " + sourceLayer.ToString() + ".";
        }

        private void CopyLayerToNext()
        {
            if (activeLevel == null)
            {
                return;
            }

            int maxLayer = activeLevel.GridDimensions.z - 1;
            if (selectedZLayer >= maxLayer)
            {
                return;
            }

            int copied = 0;
            int destinationLayer = selectedZLayer + 1;
            CommitLevelChange("Copy Layer To Next", () =>
            {
                copied = activeLevel.CopyLayer(selectedZLayer, destinationLayer, activeCatalog);
            });
            statusMessage = "Copied " + copied.ToString() + " blocks to layer " + destinationLayer.ToString() + ".";
        }

        private void ClearActiveLayer()
        {
            if (activeLevel == null)
            {
                return;
            }

            if (activeLevel.CountLayer(selectedZLayer, activeCatalog) == 0)
            {
                statusMessage = "Layer " + selectedZLayer.ToString() + " is already empty.";
                return;
            }

            int removed = 0;
            CommitLevelChange("Clear Layer", () =>
            {
                removed = activeLevel.ClearLayer(selectedZLayer, activeCatalog);
            });
            statusMessage = "Cleared " + removed.ToString() + " blocks from layer " + selectedZLayer.ToString() + ".";
        }

        private void CommitLevelChange(string undoName, Action mutation)
        {
            if (activeLevel == null || mutation == null)
            {
                return;
            }

            Undo.RecordObject(this, undoName);
            mutation();
            jsonDirty = true;
            EditorUtility.SetDirty(this);
            ClampActiveLayer();
            SyncPreview();
            Repaint();
            SceneView.RepaintAll();
        }

        private void BeginLevelEdit(string undoName)
        {
            if (undoGroupOpen)
            {
                return;
            }

            undoGroupName = undoName;
            Undo.RecordObject(this, undoName);
            undoGroupOpen = true;
        }

        private void EndLevelEdit()
        {
            if (!undoGroupOpen)
            {
                return;
            }

            undoGroupOpen = false;
            jsonDirty = true;
            EditorUtility.SetDirty(this);
        }

        private void MarkJsonDirtyAndAutosave()
        {
            jsonDirty = true;
        }

        private void PersistJson(bool forcePretty)
        {
            EnsureRepository();
            if (activeLevel != null)
            {
                levelRepository.CaptureLevel(activeLevel);
            }

            jsonIo.Save(levelRepository, forcePretty);
            jsonDirty = false;
            if (levelDatabase != null)
            {
                levelDatabase.ReloadFromText(levelRepository.ToJson(false));
            }
        }

        private void EnsureRepository()
        {
            if (levelRepository != null)
            {
                return;
            }

            levelDatabase = AssetDatabase.LoadAssetAtPath<LevelDatabaseSO>(LevelAssetPaths.LevelDatabasePath);
            levelRepository = jsonIo.LoadOrCreateRepository();
            if (levelDatabase != null)
            {
                levelDatabase.ReloadFromText(levelRepository.ToJson(false));
            }
        }

        private void NavigateToLevel(int levelIndex)
        {
            EnsureRepository();
            int clamped = levelIndex;
            if (clamped < 1)
            {
                clamped = 1;
            }

            if (clamped > JsonLevelRepository.MaxLevelCount)
            {
                clamped = JsonLevelRepository.MaxLevelCount;
            }

            if (activeLevel != null && jsonDirty)
            {
                levelRepository.CaptureLevel(activeLevel);
            }

            editingLevelIndex = clamped;
            jumpLevelField = clamped;
            activeLevel = levelRepository.GetOrCreateLevelData(clamped);
            hasLastEditedCell = false;
            hasSelectedPlacement = false;
            CancelMove();
            ClampActiveLayer();
            SaveAssetPrefs();
            SyncPreview();
            Repaint();
            SceneView.RepaintAll();
        }

        private bool TryBeginMove(Vector3Int cell)
        {
            if (activeLevel == null || activeCatalog == null)
            {
                return false;
            }

            BlockPlacementData placement;
            bool isAnchorCell;
            if (!activeLevel.TryGetBlockAt(cell, activeCatalog, out placement, out isAnchorCell))
            {
                return false;
            }

            if (isAnchorCell && placement.gridPosition != cell)
            {
                return false;
            }

            hasSelectedPlacement = true;
            selectedPlacement = placement;
            isMovingExistingBlock = true;
            moveOriginPlacement = placement;
            moveRotationEuler = placement.rotationEuler;
            moveGrabOffset = cell - placement.gridPosition;
            RefreshMovePreview(true, cell);
            statusMessage = "Moving block. Release to drop. R rotates. Drop outside the grid to cancel.";
            Repaint();
            SceneView.RepaintAll();
            return true;
        }

        private void RefreshMovePreview(bool previewVisible, Vector3Int hovered)
        {
            hasMovePreview = false;
            hasFootprintPreview = false;
            if (!isMovingExistingBlock || !previewVisible || activeLevel == null || activeCatalog == null)
            {
                return;
            }

            BlockDefinition definition;
            if (!activeCatalog.TryGetBlockDefinition(moveOriginPlacement.blockId, out definition))
            {
                return;
            }

            moveTargetAnchor = hovered - moveGrabOffset;
            Vector3Int sizeInCells = activeLevel.GetSizeInCells(moveOriginPlacement, activeCatalog);
            moveFootprintSize = blockFootprint.GetEffectiveSize(sizeInCells, moveRotationEuler);
            moveStatus = activeLevel.ValidatePlacementIgnoring(
                moveTargetAnchor,
                moveFootprintSize,
                activeCatalog,
                moveOriginPlacement.gridPosition);
            footprintAnchor = moveTargetAnchor;
            footprintSize = moveFootprintSize;
            footprintStatus = moveStatus;
            hasFootprintPreview = true;
            hasMovePreview = true;
        }

        private void StepMoveYaw(int direction)
        {
            float snapped = Mathf.Round(moveRotationEuler.y / 90f) * 90f;
            moveRotationEuler.y = Mathf.Repeat(snapped + (90f * direction), 360f);
            if (hasHoveredCell)
            {
                RefreshMovePreview(true, hoveredCell);
            }

            statusMessage = "Move yaw " + moveRotationEuler.y.ToString("0") + ".";
            Repaint();
            SceneView.RepaintAll();
        }

        private void FinishMove()
        {
            if (!isMovingExistingBlock)
            {
                return;
            }

            bool changed = moveTargetAnchor != moveOriginPlacement.gridPosition || moveRotationEuler != moveOriginPlacement.rotationEuler;
            bool canCommit = hasHoveredCell && hasMovePreview && moveStatus != PlacementValidationStatus.InvalidOutOfBounds && changed;
            if (!canCommit)
            {
                statusMessage = "Move cancelled. The block stayed in place.";
                CancelMove();
                return;
            }

            BlockPlacementData origin = moveOriginPlacement;
            Vector3Int targetAnchor = moveTargetAnchor;
            Vector3 rotation = moveRotationEuler;
            CommitLevelChange("Move Block", () =>
            {
                BlockPlacementData placed = origin;
                placed.gridPosition = targetAnchor;
                placed.rotationEuler = rotation;
                Vector3Int effectiveSize = activeLevel.GetEffectiveSize(placed, activeCatalog);
                activeLevel.TryRemoveAt(origin.gridPosition);
                bool placedOk = activeLevel.PlaceBlock(placed, effectiveSize, activeCatalog);
                if (!placedOk)
                {
                    activeLevel.TryAddPlacement(origin);
                }
            });
            statusMessage = "Moved block to " + targetAnchor.x + ", " + targetAnchor.y + ", " + targetAnchor.z + ".";
            CancelMove();
        }

        private void CancelMove()
        {
            if (!isMovingExistingBlock && !hasMovePreview)
            {
                return;
            }

            isMovingExistingBlock = false;
            hasMovePreview = false;
            hasFootprintPreview = false;
            moveGrabOffset = Vector3Int.zero;
            moveTargetAnchor = Vector3Int.zero;
            moveFootprintSize = Vector3Int.one;
            moveRotationEuler = Vector3.zero;
            moveOriginPlacement = default;
            hoverChanged = true;
            Repaint();
            SceneView.RepaintAll();
        }

        private bool IsMoveOriginCell(Vector3Int cell)
        {
            if (!isMovingExistingBlock || activeLevel == null)
            {
                return false;
            }

            Vector3Int originSize = activeLevel.GetEffectiveSize(moveOriginPlacement, activeCatalog);
            return blockFootprint.ContainsCell(moveOriginPlacement.gridPosition, originSize, cell);
        }

        private void StepBrushYaw(int direction)
        {
            if (isMovingExistingBlock)
            {
                StepMoveYaw(direction);
                return;
            }

            float snapped = Mathf.Round(brushRotationEuler.y / 90f) * 90f;
            brushRotationEuler.y = Mathf.Repeat(snapped + (90f * direction), 360f);
            statusMessage = "Brush yaw " + brushRotationEuler.y.ToString("0") + ".";
            Repaint();
            SceneView.RepaintAll();
        }

        private void FrameLevel()
        {
            if (activeLevel == null)
            {
                return;
            }

            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                statusMessage = "Open a Scene view to frame the blueprint.";
                return;
            }

            sceneView.Frame(activeLevel.GetWorldBounds(activeCatalog), false);
            sceneView.Repaint();
        }

        private void EnsureLayerLabels(int layerCount)
        {
            if (layerLabels == null || layerLabels.Length != layerCount)
            {
                layerLabels = new string[layerCount];
            }

            for (int i = 0; i < layerCount; i++)
            {
                int blockCount = activeLevel != null ? activeLevel.CountLayer(i, activeCatalog) : 0;
                layerLabels[i] = (position.width < NarrowPaletteWidth ? "L" : "Layer ") + i.ToString() + " (" + blockCount.ToString() + ")";
            }

            cachedLayerCount = layerCount;
        }

        private string FormatMatrixBlockLabel(string blockName, Vector3Int size, bool multiLine)
        {
            string shortName = GetShortBlockName(blockName);
            string footprint = FormatFootprint(size);
            if (multiLine)
            {
                return shortName + "\n" + footprint;
            }

            return shortName + " " + footprint;
        }

        private string GetShortBlockName(string blockName)
        {
            if (string.IsNullOrEmpty(blockName))
            {
                return "Block";
            }

            string trimmed = blockName;
            int underscore = trimmed.LastIndexOf('_');
            if (underscore > 0 && underscore < trimmed.Length - 1)
            {
                string suffix = trimmed.Substring(underscore + 1);
                if (suffix.Length >= 2
                    && suffix.Length <= 3
                    && char.IsDigit(suffix[0])
                    && (suffix[suffix.Length - 1] == 'x'
                        || suffix[suffix.Length - 1] == 'y'
                        || suffix[suffix.Length - 1] == 'z'
                        || suffix[suffix.Length - 1] == 'X'
                        || suffix[suffix.Length - 1] == 'Y'
                        || suffix[suffix.Length - 1] == 'Z'))
                {
                    trimmed = trimmed.Substring(0, underscore);
                }
            }

            if (trimmed.EndsWith("Cylinder", StringComparison.Ordinal))
            {
                trimmed = trimmed.Substring(0, trimmed.Length - "Cylinder".Length);
            }
            else if (trimmed.EndsWith("Block", StringComparison.Ordinal))
            {
                trimmed = trimmed.Substring(0, trimmed.Length - "Block".Length);
            }

            if (trimmed.Length > 10)
            {
                trimmed = trimmed.Substring(0, 8);
            }

            if (string.IsNullOrEmpty(trimmed))
            {
                return "Block";
            }

            return trimmed;
        }

        private void ClampActiveLayer()
        {
            if (activeLevel == null)
            {
                selectedZLayer = 0;
                return;
            }

            int maxLayer = activeLevel.GridDimensions.z - 1;
            if (maxLayer < 0)
            {
                maxLayer = 0;
            }

            if (selectedZLayer < 0)
            {
                selectedZLayer = 0;
            }
            else if (selectedZLayer > maxLayer)
            {
                selectedZLayer = maxLayer;
            }
        }

        private void UpdateHoverPresentation()
        {
            RefreshFootprintPreview(hasHoveredCell, hoveredCell);
            RefreshEntityHighlights();
            PrepareHighlightFrames();
        }

        private void RefreshEntityHighlights()
        {
            hasExistingHover = false;
            overwriteCount = 0;
            if (!hasHoveredCell || activeLevel == null)
            {
                return;
            }

            BlockPlacementData placement;
            bool isAnchorCell;
            if (activeLevel.TryGetBlockAt(hoveredCell, activeCatalog, out placement, out isAnchorCell))
            {
                bool movingThisBlock = isMovingExistingBlock && placement.gridPosition == moveOriginPlacement.gridPosition;
                if (!movingThisBlock)
                {
                    existingHoverPlacement = placement;
                    existingHoverSize = activeLevel.GetEffectiveSize(placement, activeCatalog);
                    hasExistingHover = isAnchorCell || existingHoverSize.x > 0;
                }
            }

            if (!hasFootprintPreview || footprintStatus != PlacementValidationStatus.ValidOverwrite)
            {
                return;
            }

            int count = activeLevel.PlacementCount;
            for (int i = 0; i < count; i++)
            {
                if (overwriteCount >= MaxOverwriteHighlights)
                {
                    break;
                }

                BlockPlacementData candidate;
                if (!activeLevel.TryGetPlacementByIndex(i, out candidate))
                {
                    continue;
                }

                if (isMovingExistingBlock && candidate.gridPosition == moveOriginPlacement.gridPosition)
                {
                    continue;
                }

                Vector3Int candidateSize = activeLevel.GetEffectiveSize(candidate, activeCatalog);
                if (!blockFootprint.Overlaps(footprintAnchor, footprintSize, candidate.gridPosition, candidateSize))
                {
                    continue;
                }

                overwritePlacements[overwriteCount] = candidate;
                overwriteSizes[overwriteCount] = candidateSize;
                overwriteCount++;
            }
        }

        private void PrepareHighlightFrames()
        {
            existingFrame.active = hasExistingHover && !ContainsOverwriteAnchor(existingHoverPlacement.gridPosition);
            existingFrame.anchor = existingHoverPlacement.gridPosition;
            existingFrame.size = existingHoverSize;
            existingFrame.color = new Color(1f, 0.94f, 0.45f, 1f);
            existingFrame.hasTopLeft = false;
            existingFrame.hasBottomRight = false;

            previewFrame.active = hasFootprintPreview;
            previewFrame.anchor = footprintAnchor;
            previewFrame.size = footprintSize;
            previewFrame.color = ColorForStatus(footprintStatus);
            previewFrame.hasTopLeft = false;
            previewFrame.hasBottomRight = false;

            moveOriginFrame.active = false;
            moveOriginFrame.hasTopLeft = false;
            moveOriginFrame.hasBottomRight = false;
            moveOriginFrame.dashed = false;
            if (isMovingExistingBlock)
            {
                moveOriginFrame.active = true;
                moveOriginFrame.anchor = moveOriginPlacement.gridPosition;
                moveOriginFrame.size = activeLevel != null
                    ? activeLevel.GetEffectiveSize(moveOriginPlacement, activeCatalog)
                    : Vector3Int.one;
                moveOriginFrame.color = new Color(0.82f, 0.88f, 0.92f, 0.95f);
                moveOriginFrame.dashed = true;
            }

            for (int i = 0; i < MaxOverwriteHighlights; i++)
            {
                overwriteFrames[i].active = false;
                overwriteFrames[i].hasTopLeft = false;
                overwriteFrames[i].hasBottomRight = false;
            }

            for (int i = 0; i < overwriteCount; i++)
            {
                overwriteFrames[i].active = true;
                overwriteFrames[i].anchor = overwritePlacements[i].gridPosition;
                overwriteFrames[i].size = overwriteSizes[i];
                overwriteFrames[i].color = new Color(1f, 0.32f, 0.08f, 1f);
            }
        }

        private void TrackCellHighlight(Vector3Int cell, Rect cellRect)
        {
            TrackHighlightRect(ref existingFrame, cell, cellRect);
            TrackHighlightRect(ref previewFrame, cell, cellRect);
            TrackHighlightRect(ref moveOriginFrame, cell, cellRect);
            for (int i = 0; i < overwriteCount; i++)
            {
                TrackHighlightRect(ref overwriteFrames[i], cell, cellRect);
            }
        }

        private void TrackHighlightRect(ref HighlightFrame frame, Vector3Int cell, Rect cellRect)
        {
            if (!frame.active || !blockFootprint.ContainsCell(frame.anchor, frame.size, cell))
            {
                return;
            }

            if (!frame.hasTopLeft)
            {
                frame.topLeft = cellRect;
                frame.bottomRight = cellRect;
                frame.hasTopLeft = true;
                frame.hasBottomRight = true;
                return;
            }

            float xMin = frame.topLeft.x < cellRect.x ? frame.topLeft.x : cellRect.x;
            float yMin = frame.topLeft.y < cellRect.y ? frame.topLeft.y : cellRect.y;
            float xMax = frame.bottomRight.xMax > cellRect.xMax ? frame.bottomRight.xMax : cellRect.xMax;
            float yMax = frame.bottomRight.yMax > cellRect.yMax ? frame.bottomRight.yMax : cellRect.yMax;
            frame.topLeft = new Rect(xMin, yMin, 0f, 0f);
            frame.bottomRight = new Rect(xMax, yMax, 0f, 0f);
        }

        private void DrawUnifiedFrames()
        {
            DrawUnifiedFrame(moveOriginFrame);
            DrawUnifiedFrame(previewFrame);
            for (int i = 0; i < overwriteCount; i++)
            {
                DrawUnifiedFrame(overwriteFrames[i]);
            }

            DrawUnifiedFrame(existingFrame);
        }

        private void DrawUnifiedFrame(HighlightFrame frame)
        {
            if (!frame.active || !frame.hasTopLeft || !frame.hasBottomRight)
            {
                return;
            }

            float x = frame.topLeft.x;
            float y = frame.topLeft.y;
            float width = frame.bottomRight.xMax - x;
            float height = frame.bottomRight.yMax - y;
            if (width <= 0f || height <= 0f)
            {
                return;
            }

            Rect unifiedRect = new Rect(x, y, width, height);
            if (frame.dashed)
            {
                DrawDashedBorder(unifiedRect, frame.color, 3f);
                return;
            }

            DrawBorder(unifiedRect, frame.color, 4f);
        }

        private void DrawDashedBorder(Rect rect, Color color, float thickness)
        {
            DrawDashedHorizontal(rect.x, rect.y, rect.width, thickness, color);
            DrawDashedHorizontal(rect.x, rect.yMax - thickness, rect.width, thickness, color);
            DrawDashedVertical(rect.x, rect.y, rect.height, thickness, color);
            DrawDashedVertical(rect.xMax - thickness, rect.y, rect.height, thickness, color);
        }

        private void DrawDashedHorizontal(float x, float y, float width, float thickness, Color color)
        {
            const float dash = 6f;
            const float gap = 4f;
            float cursor = 0f;
            while (cursor < width)
            {
                float length = dash;
                if (cursor + length > width)
                {
                    length = width - cursor;
                }

                EditorGUI.DrawRect(new Rect(x + cursor, y, length, thickness), color);
                cursor += dash + gap;
            }
        }

        private void DrawDashedVertical(float x, float y, float height, float thickness, Color color)
        {
            const float dash = 6f;
            const float gap = 4f;
            float cursor = 0f;
            while (cursor < height)
            {
                float length = dash;
                if (cursor + length > height)
                {
                    length = height - cursor;
                }

                EditorGUI.DrawRect(new Rect(x, y + cursor, thickness, length), color);
                cursor += dash + gap;
            }
        }

        private bool IsExistingHoverCell(Vector3Int cell)
        {
            if (!hasExistingHover)
            {
                return false;
            }

            return blockFootprint.ContainsCell(existingHoverPlacement.gridPosition, existingHoverSize, cell);
        }

        private bool IsOverwriteCell(Vector3Int cell)
        {
            for (int i = 0; i < overwriteCount; i++)
            {
                if (blockFootprint.ContainsCell(overwritePlacements[i].gridPosition, overwriteSizes[i], cell))
                {
                    return true;
                }
            }

            return false;
        }

        private bool ContainsOverwriteAnchor(Vector3Int anchor)
        {
            for (int i = 0; i < overwriteCount; i++)
            {
                if (overwritePlacements[i].gridPosition == anchor)
                {
                    return true;
                }
            }

            return false;
        }

        private void RefreshFootprintPreview(bool previewVisible, Vector3Int previewAnchor)
        {
            hasFootprintPreview = false;
            hasMovePreview = false;
            if (isMovingExistingBlock)
            {
                RefreshMovePreview(previewVisible, previewAnchor);
                return;
            }

            if (!previewVisible || activeLevel == null || activeCatalog == null)
            {
                return;
            }

            int blockId;
            if (!TryResolveFootprintBlock(out blockId))
            {
                return;
            }

            BlockDefinition definition;
            if (!activeCatalog.TryGetBlockDefinition(blockId, out definition))
            {
                return;
            }

            footprintAnchor = previewAnchor;
            Vector3Int customSize = ResolveBrushCustomSize(definition);
            Vector3Int sizeInCells = customSize.x > 0 ? customSize : definition.sizeInCells;
            footprintSize = blockFootprint.GetEffectiveSize(sizeInCells, brushRotationEuler);
            footprintStatus = activeLevel.ValidatePlacement(footprintAnchor, footprintSize, activeCatalog);
            hasFootprintPreview = true;
        }

        private bool TryGetPreviewMembership(Vector3Int cell)
        {
            if (!hasFootprintPreview)
            {
                return false;
            }

            return blockFootprint.ContainsCell(footprintAnchor, footprintSize, cell);
        }

        private bool TryResolveFootprintBlock(out int blockId)
        {
            if (dragFootprintActive)
            {
                blockId = dragFootprintBlockId;
                return true;
            }

            if (hasBrush)
            {
                blockId = brushBlockId;
                return true;
            }

            blockId = 0;
            return false;
        }

        private Color ColorForStatus(PlacementValidationStatus status)
        {
            if (status == PlacementValidationStatus.ValidEmpty)
            {
                return new Color(0.2f, 0.9f, 0.35f, 1f);
            }

            if (status == PlacementValidationStatus.ValidOverwrite)
            {
                return new Color(1f, 0.68f, 0.12f, 1f);
            }

            return new Color(1f, 0.18f, 0.14f, 1f);
        }

        private string FormatFootprint(Vector3Int size)
        {
            Vector3Int clamped = blockFootprint.ClampSize(size);
            if (clamped.z == 1)
            {
                return "[" + clamped.x.ToString() + "x" + clamped.y.ToString() + "]";
            }

            return "[" + clamped.x.ToString() + "x" + clamped.y.ToString() + "x" + clamped.z.ToString() + "]";
        }

        private bool CatalogContainsBrush()
        {
            if (activeCatalog == null)
            {
                return false;
            }

            BlockDefinition definition;
            if (!activeCatalog.TryGetBlockDefinition(brushBlockId, out definition))
            {
                return false;
            }

            return definition.blockId == brushBlockId;
        }

        private void SyncPreview()
        {
            if (scenePreview == null)
            {
                return;
            }

            int ghostBlockId = 0;
            bool showGhost = !isMovingExistingBlock && hasHoveredCell && TryResolveFootprintBlock(out ghostBlockId);
            BlueprintMoveGhost moveGhost = new BlueprintMoveGhost();
            moveGhost.showOrigin = isMovingExistingBlock;
            moveGhost.origin = moveOriginPlacement;
            moveGhost.showTarget = isMovingExistingBlock && hasMovePreview;
            moveGhost.status = moveStatus;
            moveGhost.target.blockId = moveOriginPlacement.blockId;
            moveGhost.target.gridPosition = moveTargetAnchor;
            moveGhost.target.rotationEuler = moveRotationEuler;
            moveGhost.target.customSize = moveOriginPlacement.customSize;
            Vector3Int ghostCustomSize = Vector3Int.zero;
            if (showGhost && activeCatalog != null)
            {
                BlockDefinition ghostDefinition;
                if (activeCatalog.TryGetBlockDefinition(ghostBlockId, out ghostDefinition))
                {
                    ghostCustomSize = ResolveBrushCustomSize(ghostDefinition);
                }
            }

            scenePreview.Bind(
                activeLevel,
                activeCatalog,
                selectedZLayer,
                hoveredCell,
                showGhost,
                ghostBlockId,
                brushRotationEuler,
                ghostCustomSize,
                hasExistingHover,
                existingHoverPlacement,
                overwritePlacements,
                overwriteCount,
                moveGhost);
        }

        private void RestoreAssets()
        {
            EnsureRepository();
            activeCatalog = LoadCatalog();
            if (activeCatalog != null)
            {
                activeCatalog.InvalidateRuntimeBlocks();
            }

            int savedIndex = EditorPrefs.GetInt(LevelPrefKey, 1);
            if (savedIndex < 1)
            {
                savedIndex = 1;
            }

            NavigateToLevel(savedIndex);
            SaveAssetPrefs();
            ClampActiveLayer();
        }

        private BlockCatalogSO LoadCatalog()
        {
            string savedPath = EditorPrefs.GetString(CatalogPrefKey, string.Empty);
            BlockCatalogSO savedCatalog = LoadProjectAsset<BlockCatalogSO>(savedPath);
            if (savedCatalog != null)
            {
                return savedCatalog;
            }

            BlockCatalogSO projectCatalog = AssetDatabase.LoadAssetAtPath<BlockCatalogSO>(LevelAssetPaths.CatalogPath);
            if (projectCatalog != null)
            {
                return projectCatalog;
            }

            LevelDatabaseSO database = AssetDatabase.LoadAssetAtPath<LevelDatabaseSO>(LevelAssetPaths.LevelDatabasePath);
            if (database != null && IsProjectAsset(database.BlockCatalog))
            {
                return database.BlockCatalog;
            }

            return null;
        }

        private static bool IsProjectAsset(UnityEngine.Object asset)
        {
            if (asset == null)
            {
                return false;
            }

            return LevelAssetPaths.IsProjectAssetPath(AssetDatabase.GetAssetPath(asset));
        }

        private static TAsset LoadProjectAsset<TAsset>(string path) where TAsset : UnityEngine.Object
        {
            if (!LevelAssetPaths.IsProjectAssetPath(path))
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<TAsset>(path);
        }

        private void SaveAssetPrefs()
        {
            EditorPrefs.SetInt(LevelPrefKey, editingLevelIndex);
            string catalogPath = activeCatalog == null ? string.Empty : AssetDatabase.GetAssetPath(activeCatalog);
            EditorPrefs.SetString(CatalogPrefKey, catalogPath);
        }

        private TAsset LoadAsset<TAsset>(string path) where TAsset : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<TAsset>(path);
        }

        private void EnsureStyles()
        {
            if (cellLabelStyle != null && matrixLabelStyle != null && paletteTitleStyle != null && paletteIdStyle != null)
            {
                return;
            }

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

        private bool ShouldTrackHover(Event current)
        {
            if (current == null)
            {
                return false;
            }

            return current.type == EventType.MouseMove
                || current.type == EventType.MouseDrag
                || current.type == EventType.DragUpdated;
        }

        private void DrawBorder(Rect rect, Color color, float thickness)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private Color GetReadableTextColor(Color background)
        {
            float luminance = (background.r * 0.299f) + (background.g * 0.587f) + (background.b * 0.114f);
            if (luminance > 0.62f)
            {
                return new Color(0.08f, 0.08f, 0.08f, 1f);
            }

            return Color.white;
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

        private string GetBlockLabel(BlockDefinition definition)
        {
            if (string.IsNullOrEmpty(definition.blockName))
            {
                return "Block " + definition.blockId.ToString();
            }

            return definition.blockName;
        }

        private Texture GetPreviewTexture(BlockDefinition definition)
        {
            Texture preview = null;
            if (definition.prefab != null)
            {
                preview = AssetPreview.GetAssetPreview(definition.prefab);
                if (preview == null)
                {
                    preview = AssetPreview.GetMiniThumbnail(definition.prefab);
                }
            }

            if (preview == null && definition.previewMesh != null)
            {
                preview = AssetPreview.GetAssetPreview(definition.previewMesh);
                if (preview == null)
                {
                    preview = AssetPreview.GetMiniThumbnail(definition.previewMesh);
                }
            }

            return preview;
        }
    }
}
