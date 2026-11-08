using System;
using SmashFast.Data;
using UnityEditor;
using UnityEngine;

namespace SmashFast.LevelEditor
{
    public class LevelBlueprintEditorWindow : EditorWindow
    {
        private const float CellWidth = 96f;
        private const float CellHeight = 64f;
        private const float PaletteCardWidth = 118f;
        private const float PaletteCardHeight = 132f;
        private const string LevelPrefKey = "SmashFast.Blueprint.Level";
        private const string CatalogPrefKey = "SmashFast.Blueprint.Catalog";
        private const int MaxOverwriteHighlights = 16;
        private const int EditorFrameRate = 120;

        private LevelData activeLevel;
        private BlockCatalogSO activeCatalog;
        private int selectedZLayer;
        private int brushBlockId;
        private bool hasBrush;
        private Vector3 brushRotationEuler;
        private bool showGridSettings;
        private bool hasSelectedPlacement;
        private BlockPlacementData selectedPlacement;
        private readonly BlockFootprint blockFootprint = new BlockFootprint();
        private LevelArchitecturePresetBuilder presetBuilder;
        private readonly LevelEditorDragPayload dragPayload = new LevelEditorDragPayload();
        private Vector2 matrixScroll;
        private Vector2 paletteScroll;
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
            if (activeLevel == levelData)
            {
                ClampActiveLayer();
                SyncPreview();
                return;
            }

            activeLevel = levelData;
            hasLastEditedCell = false;
            ClampActiveLayer();
            SaveAssetPrefs();
            SyncPreview();
            Repaint();
            SceneView.RepaintAll();
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
            minSize = new Vector2(760f, 560f);
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
            if (scenePreview != null)
            {
                scenePreview.Disable();
                scenePreview = null;
            }
        }

        private void OnFocus()
        {
            RefreshView();
        }

        private void OnSelectionChanged()
        {
            if (Selection.activeObject is LevelData selectedLevel)
            {
                SetActiveLevel(selectedLevel);
            }

            if (Selection.activeObject is BlockCatalogSO selectedCatalog)
            {
                SetActiveCatalog(selectedCatalog);
            }
        }

        private void OnUndoRedo()
        {
            CancelMove();
            ClampActiveLayer();
            RefreshView();
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
            int levelId = activeLevel != null ? activeLevel.GetInstanceID() : 0;
            if (catalogId == 0 && levelId == 0)
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
                if (args.instanceId == catalogId || args.instanceId == levelId)
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
            DrawSettings();
            DrawPresetBar();
            DrawLayerBar();
            DrawBrushBar();
            DrawAngleRow();
            DrawMatrix();
            DrawPalette();
            DrawFooter();
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

            if (current.keyCode != KeyCode.R || current.alt || current.control || current.command)
            {
                return;
            }

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
        }

        private void DrawSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Level Blueprint", EditorStyles.boldLabel);

            if (activeLevel == null || activeCatalog == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign a Level Data asset and a Block Catalog, or create the default test assets.",
                    MessageType.Info);
                if (GUILayout.Button("Create Default Level Assets"))
                {
                    LevelBlueprintSampleAssets.Create();
                    GUIUtility.ExitGUI();
                }
            }

            EditorGUI.BeginChangeCheck();
            LevelData nextLevel = (LevelData)EditorGUILayout.ObjectField("Level Data", activeLevel, typeof(LevelData), false);
            if (EditorGUI.EndChangeCheck())
            {
                SetActiveLevel(nextLevel);
            }

            EditorGUI.BeginChangeCheck();
            BlockCatalogSO nextCatalog = (BlockCatalogSO)EditorGUILayout.ObjectField("Block Catalog", activeCatalog, typeof(BlockCatalogSO), false);
            if (EditorGUI.EndChangeCheck())
            {
                SetActiveCatalog(nextCatalog);
            }

            if (activeLevel == null)
            {
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUI.BeginChangeCheck();
            int levelIndex = EditorGUILayout.IntField("Level Index", activeLevel.LevelIndex);
            if (EditorGUI.EndChangeCheck())
            {
                int clampedIndex = levelIndex < 0 ? 0 : levelIndex;
                CommitLevelChange("Edit Level Index", () => activeLevel.LevelIndex = clampedIndex);
            }

            EditorGUI.BeginChangeCheck();
            int maxAmmo = EditorGUILayout.IntField("Max Ammo", activeLevel.MaxAmmo);
            if (EditorGUI.EndChangeCheck())
            {
                int clampedAmmo = maxAmmo < 0 ? 0 : maxAmmo;
                CommitLevelChange("Edit Max Ammo", () => activeLevel.MaxAmmo = clampedAmmo);
            }

            showGridSettings = EditorGUILayout.Foldout(showGridSettings, "Grid Settings", true);
            if (showGridSettings)
            {
                EditorGUI.indentLevel++;
                EditorGUI.BeginChangeCheck();
                Vector3Int dimensions = EditorGUILayout.Vector3IntField("Grid Dimensions", activeLevel.GridDimensions);
                if (EditorGUI.EndChangeCheck())
                {
                    Vector3Int clampedDimensions = new Vector3Int(
                        Mathf.Max(1, dimensions.x),
                        Mathf.Max(1, dimensions.y),
                        Mathf.Max(1, dimensions.z));
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

                if (GUILayout.Button("Frame Blueprint In Scene"))
                {
                    FrameLevel();
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPresetBar()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Quick Architecture Presets", EditorStyles.boldLabel);
            if (activeLevel == null || activeCatalog == null)
            {
                EditorGUILayout.HelpBox("Select a level and a catalog to build a preset.", MessageType.None);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.BeginHorizontal();
            bool autoBuild = GUILayout.Button("\u26A1 Auto-Build By Level Index (Smart Difficulty)");
            bool batchBuild = GUILayout.Button("Batch Generate Levels (1..30)");
            EditorGUILayout.EndHorizontal();
            if (autoBuild)
            {
                AutoBuildByLevelIndex();
            }

            if (batchBuild)
            {
                BatchGenerateLevels();
            }

            EditorGUILayout.BeginHorizontal();
            bool straight = GUILayout.Button("Preset: Straight Apartment");
            bool angled = GUILayout.Button("Preset: 45 Degree Angled Tower");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            bool glass = GUILayout.Button("Preset: Glass Weak-Point");
            bool shelf = GUILayout.Button("Preset: Shelf & Jars");
            EditorGUILayout.EndHorizontal();
            bool twin = GUILayout.Button("Preset: V-Angled Twin Tables");
            if (straight)
            {
                ApplyPreset(0);
            }

            if (angled)
            {
                ApplyPreset(1);
            }

            if (glass)
            {
                ApplyPreset(2);
            }

            if (shelf)
            {
                ApplyPreset(3);
            }

            if (twin)
            {
                ApplyPreset(4);
            }

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

            AssetDatabase.SaveAssets();
            hasSelectedPlacement = false;
            statusMessage = message;
        }

        private void AutoBuildByLevelIndex()
        {
            if (activeLevel == null || activeCatalog == null)
            {
                statusMessage = "Select a level and a catalog first.";
                return;
            }

            if (Application.isPlaying)
            {
                statusMessage = "Exit Play Mode before building a level.";
                return;
            }

            CancelMove();
            if (presetBuilder == null)
            {
                presetBuilder = new LevelArchitecturePresetBuilder();
            }

            int index = activeLevel.LevelIndex;
            if (index < 1)
            {
                index = 1;
            }

            string message = string.Empty;
            CommitLevelChange("Auto Build Level", () =>
            {
                activeLevel.LevelIndex = index;
                message = presetBuilder.GenerateForLevelIndex(activeLevel, activeCatalog, index);
            });
            selectedZLayer = presetBuilder.LastAnchorLayer;
            AssetDatabase.SaveAssets();
            hasSelectedPlacement = false;
            statusMessage = message;
        }

        private void BatchGenerateLevels()
        {
            if (activeLevel == null || activeCatalog == null)
            {
                statusMessage = "Select a level and a catalog first.";
                return;
            }

            if (Application.isPlaying)
            {
                statusMessage = "Exit Play Mode before generating levels.";
                return;
            }

            CancelMove();
            if (presetBuilder == null)
            {
                presetBuilder = new LevelArchitecturePresetBuilder();
            }

            string folder = ResolveLevelFolder();
            int built = 0;
            for (int index = 1; index <= 30; index++)
            {
                string path = folder + "/Level_" + index.ToString("00") + ".asset";
                LevelData level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
                if (level == null)
                {
                    level = ScriptableObject.CreateInstance<LevelData>();
                    CopyGrid(activeLevel, level);
                    AssetDatabase.CreateAsset(level, path);
                }

                presetBuilder.GenerateForLevelIndex(level, activeCatalog, index);
                EditorUtility.SetDirty(level);
                built++;
            }

            RegisterLevels(folder);
            AssetDatabase.SaveAssets();
            selectedZLayer = 0;
            hasSelectedPlacement = false;
            statusMessage = "Built " + built.ToString() + " levels (1..30) in " + folder + ".";
        }

        private string ResolveLevelFolder()
        {
            string assetPath = AssetDatabase.GetAssetPath(activeLevel);
            int slash = assetPath.LastIndexOf('/');
            if (slash > 0)
            {
                return assetPath.Substring(0, slash);
            }

            return LevelAssetPaths.LevelsFolder;
        }

        private void CopyGrid(LevelData source, LevelData target)
        {
            target.GridDimensions = source.GridDimensions;
            target.CellSize = source.CellSize;
            target.OriginOffset = source.OriginOffset;
            target.FallEliminationY = source.FallEliminationY;
        }

        private void RegisterLevels(string folder)
        {
            string[] databaseGuids = AssetDatabase.FindAssets("t:LevelDatabaseSO");
            for (int databaseIndex = 0; databaseIndex < databaseGuids.Length; databaseIndex++)
            {
                string databasePath = AssetDatabase.GUIDToAssetPath(databaseGuids[databaseIndex]);
                LevelDatabaseSO database = AssetDatabase.LoadAssetAtPath<LevelDatabaseSO>(databasePath);
                if (database == null)
                {
                    continue;
                }

                SerializedObject serializedDatabase = new SerializedObject(database);
                SerializedProperty levels = serializedDatabase.FindProperty("levels");
                if (levels == null || !levels.isArray)
                {
                    continue;
                }

                for (int index = 1; index <= 30; index++)
                {
                    string path = folder + "/Level_" + index.ToString("00") + ".asset";
                    LevelData level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
                    if (level == null || ContainsLevel(levels, level))
                    {
                        continue;
                    }

                    int slot = levels.arraySize;
                    levels.arraySize++;
                    levels.GetArrayElementAtIndex(slot).objectReferenceValue = level;
                }

                serializedDatabase.ApplyModifiedProperties();
                EditorUtility.SetDirty(database);
            }
        }

        private bool ContainsLevel(SerializedProperty levels, LevelData level)
        {
            for (int i = 0; i < levels.arraySize; i++)
            {
                if (levels.GetArrayElementAtIndex(i).objectReferenceValue == level)
                {
                    return true;
                }
            }

            return false;
        }

        private void DrawAngleRow()
        {
            if (!hasBrush && !hasSelectedPlacement)
            {
                return;
            }

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            string title = hasBrush ? "Brush Angle" : "Block Angle";
            if (!hasBrush && hasSelectedPlacement && activeLevel != null && activeLevel.IsFoundationTable(selectedPlacement, activeCatalog))
            {
                title = "Table Angle";
            }

            EditorGUILayout.LabelField(title, GUILayout.Width(92f));
            if (GUILayout.Button("-45"))
            {
                ApplyAngle(-45f);
            }

            if (GUILayout.Button("-30"))
            {
                ApplyAngle(-30f);
            }

            if (GUILayout.Button("0"))
            {
                ApplyAngle(0f);
            }

            if (GUILayout.Button("+30"))
            {
                ApplyAngle(30f);
            }

            if (GUILayout.Button("+45"))
            {
                ApplyAngle(45f);
            }

            if (GUILayout.Button("+90"))
            {
                ApplyAngle(90f);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void ApplyAngle(float yaw)
        {
            if (hasBrush)
            {
                brushRotationEuler.y = yaw;
                statusMessage = "Brush yaw " + yaw.ToString("0") + ".";
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

            Vector3 euler = selectedPlacement.rotationEuler;
            euler.y = yaw;
            Vector3Int size = blockFootprint.GetEffectiveSize(definition.sizeInCells, euler);
            PlacementValidationStatus status = activeLevel.ValidatePlacementIgnoring(
                selectedPlacement.gridPosition,
                size,
                activeCatalog,
                selectedPlacement.gridPosition);
            if (status == PlacementValidationStatus.InvalidOutOfBounds)
            {
                statusMessage = "That angle does not fit inside the grid.";
                return;
            }

            BlockPlacementData updated = selectedPlacement;
            updated.rotationEuler = euler;
            CommitLevelChange("Set Block Yaw", () => activeLevel.TrySetPlacement(updated));
            selectedPlacement = updated;
            if (isMovingExistingBlock)
            {
                moveRotationEuler = euler;
                moveOriginPlacement.rotationEuler = euler;
            }

            AssetDatabase.SaveAssets();
            statusMessage = "Yaw set to " + yaw.ToString("0") + ".";
        }

        private void DrawLayerBar()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (activeLevel == null)
            {
                EditorGUILayout.LabelField("Select a Level Data asset to edit a Z layer.");
                EditorGUILayout.EndVertical();
                return;
            }

            int layerCount = activeLevel.GridDimensions.z;
            EnsureLayerLabels(layerCount);
            int maxLayer = layerCount - 1;
            if (layerCount <= 8)
            {
                int toolbarLayer = GUILayout.Toolbar(selectedZLayer, layerLabels);
                if (toolbarLayer != selectedZLayer)
                {
                    selectedZLayer = toolbarLayer;
                    SyncPreview();
                    SceneView.RepaintAll();
                }
            }

            EditorGUI.BeginChangeCheck();
            int sliderLayer = EditorGUILayout.IntSlider("Z Layer", selectedZLayer, 0, maxLayer);
            if (EditorGUI.EndChangeCheck())
            {
                selectedZLayer = sliderLayer;
                SyncPreview();
                SceneView.RepaintAll();
            }

            EditorGUILayout.LabelField("Blocks on layer " + selectedZLayer.ToString(), activeLevel.CountLayer(selectedZLayer, activeCatalog).ToString());

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(selectedZLayer <= 0);
            bool copyPrevious = GUILayout.Button("Copy Previous Into Layer");
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(selectedZLayer >= maxLayer);
            bool copyNext = GUILayout.Button("Copy Layer To Next");
            EditorGUI.EndDisabledGroup();
            bool clearLayer = GUILayout.Button("Clear Layer");
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

        private void DrawBrushBar()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            string brushLabel = "None";
            if (hasBrush && activeCatalog != null && activeCatalog.TryGetBlockDefinition(brushBlockId, out BlockDefinition definition))
            {
                brushLabel = GetBlockLabel(definition) + " (#" + definition.blockId.ToString() + ")";
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Brush", brushLabel);
            if (GUILayout.Button("Clear Brush", GUILayout.Width(110f)))
            {
                hasBrush = false;
                statusMessage = "Brush cleared.";
                hoverChanged = true;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawMatrix()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandHeight(true));
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
            int cellCount = dimensions.x * dimensions.y;
            if (cellCount > 600)
            {
                EditorGUILayout.HelpBox(
                    "This layer has " + cellCount.ToString() + " cells. Large matrices are slower to draw.",
                    MessageType.Warning);
            }

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

            matrixScroll = EditorGUILayout.BeginScrollView(matrixScroll, GUILayout.ExpandHeight(true));
            EnsureCellRectBuffer(dimensions.x, dimensions.y);
            DrawColumnHeaders(dimensions.x);
            for (int y = dimensions.y - 1; y >= 0; y--)
            {
                EditorGUILayout.BeginHorizontal();
                Rect rowLabelRect = GUILayoutUtility.GetRect(28f, CellHeight, GUILayout.Width(28f), GUILayout.Height(CellHeight));
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
                        GUILayout.Space(2f);
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            if (Event.current != null && Event.current.type == EventType.Repaint)
            {
                DrawMergedBlockLabels();
                DrawUnifiedFrames();
            }

            EditorGUILayout.EndScrollView();
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

        private void DrawColumnHeaders(int width)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(28f);
            for (int x = 0; x < width; x++)
            {
                Rect headerRect = GUILayoutUtility.GetRect(CellWidth, 18f, GUILayout.Width(CellWidth), GUILayout.Height(18f));
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
                CellWidth,
                CellHeight,
                GUILayout.Width(CellWidth),
                GUILayout.Height(CellHeight));
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
            if (occupied && hasDefinition)
            {
                effectiveSize = blockFootprint.GetEffectiveSize(definition.sizeInCells, placement.rotationEuler);
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

            string blockName = hasDefinition ? GetBlockLabel(definition) : ("#" + placement.blockId.ToString());
            DrawCenteredLabel(cellRect, blockName + "\n" + FormatFootprint(effectiveSize), fill);
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
                string blockName = hasDefinition ? GetBlockLabel(definition) : ("#" + placement.blockId.ToString());
                Color fill = hasDefinition ? definition.editorColor : new Color(0.45f, 0.45f, 0.48f, 1f);
                DrawCenteredLabel(mergedRect, blockName + "\n" + FormatFootprint(size), fill);
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
            Vector3Int effectiveSize = blockFootprint.GetEffectiveSize(definition.sizeInCells, brushRotationEuler);
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

        private void DrawPalette()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Block Palette", EditorStyles.boldLabel);
            if (activeCatalog == null)
            {
                EditorGUILayout.HelpBox("Assign a Block Catalog to choose blocks.", MessageType.None);
                EditorGUILayout.EndVertical();
                return;
            }

            if (activeCatalog.BlockCount == 0)
            {
                EditorGUILayout.HelpBox("The catalog has no blocks yet. Add Block Definition entries on the catalog asset.", MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            paletteScroll = EditorGUILayout.BeginScrollView(paletteScroll, true, false, GUILayout.Height(156f));
            EditorGUILayout.BeginHorizontal();
            int count = activeCatalog.BlockCount;
            for (int i = 0; i < count; i++)
            {
                BlockDefinition definition;
                if (!activeCatalog.TryGetBlockByIndex(i, out definition))
                {
                    continue;
                }

                DrawPaletteCard(definition);
                if (i < count - 1)
                {
                    GUILayout.Space(6f);
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            if (AssetPreview.IsLoadingAssetPreviews())
            {
                Repaint();
            }
        }

        private void DrawPaletteCard(BlockDefinition definition)
        {
            Rect cardRect = GUILayoutUtility.GetRect(
                PaletteCardWidth,
                PaletteCardHeight,
                GUILayout.Width(PaletteCardWidth),
                GUILayout.Height(PaletteCardHeight));
            bool selected = hasBrush && brushBlockId == definition.blockId;
            Event current = Event.current;
            if (current != null && current.type == EventType.Repaint)
            {
                Color fill = selected ? new Color(0.1f, 0.22f, 0.26f, 1f) : new Color(0.18f, 0.19f, 0.2f, 1f);
                EditorGUI.DrawRect(cardRect, fill);
                Rect swatch = new Rect(cardRect.x + 8f, cardRect.y + 8f, cardRect.width - 16f, 14f);
                EditorGUI.DrawRect(swatch, definition.editorColor);
                Texture preview = GetPreviewTexture(definition);
                if (preview != null)
                {
                    Rect previewRect = new Rect(cardRect.x + 28f, cardRect.y + 28f, 62f, 62f);
                    GUI.DrawTexture(previewRect, preview, ScaleMode.ScaleToFit);
                }

                paletteTitleStyle.normal.textColor = Color.white;
                paletteIdStyle.normal.textColor = new Color(0.75f, 0.9f, 0.95f, 1f);
                GUI.Label(new Rect(cardRect.x + 4f, cardRect.y + 94f, cardRect.width - 8f, 18f), GetBlockLabel(definition), paletteTitleStyle);
                string cardId = "#" + definition.blockId.ToString() + "  " + FormatFootprint(blockFootprint.ClampSize(definition.sizeInCells));
                GUI.Label(new Rect(cardRect.x + 4f, cardRect.y + 112f, cardRect.width - 8f, 16f), cardId, paletteIdStyle);
                Color border = selected ? new Color(0.4f, 0.95f, 1f, 1f) : new Color(0.32f, 0.36f, 0.4f, 1f);
                DrawBorder(cardRect, border, selected ? 2f : 1f);
            }

            if (current == null || current.type != EventType.MouseDown || current.button != 0)
            {
                return;
            }

            if (!cardRect.Contains(current.mousePosition))
            {
                return;
            }

            CancelMove();
            hasBrush = true;
            brushBlockId = definition.blockId;
            statusMessage = "Brush: " + GetBlockLabel(definition) + ". Drag it onto the matrix or click cells to paint.";
            dragPayload.Begin(definition.blockId, GetBlockLabel(definition));
            current.Use();
            Repaint();
        }

        private void DrawFooter()
        {
            string hoverText = hasHoveredCell ? ("Cell " + hoveredCell.x + ", " + hoveredCell.y + ", " + hoveredCell.z) : "No cell";
            EditorGUILayout.LabelField(statusMessage + "   |   " + hoverText, EditorStyles.miniLabel);
            EditorGUILayout.LabelField("No brush: drag a block to move it. R rotates the move. Green fits. Orange overwrites. Red is out of bounds.", EditorStyles.miniLabel);
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

            Vector3Int effectiveSize = blockFootprint.GetEffectiveSize(definition.sizeInCells, brushRotationEuler);
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
            if (activeLevel.TryGetPlacement(cell, out existing) && existing.blockId == brushBlockId && existing.rotationEuler == brushRotationEuler)
            {
                lastEditedCell = cell;
                hasLastEditedCell = true;
                return;
            }

            BeginLevelEdit("Paint Block");
            RecordLevel();
            BlockPlacementData placement = new BlockPlacementData();
            placement.blockId = brushBlockId;
            placement.gridPosition = cell;
            placement.rotationEuler = brushRotationEuler;
            bool placed = activeLevel.PlaceBlock(placement, effectiveSize, activeCatalog);
            if (!placed)
            {
                EndLevelEdit();
                statusMessage = "OUT OF BOUNDS. The footprint does not fit.";
                return;
            }

            EditorUtility.SetDirty(activeLevel);
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
            RecordLevel();
            activeLevel.TryRemoveAt(placement.gridPosition);
            EditorUtility.SetDirty(activeLevel);
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

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            Undo.RecordObject(activeLevel, undoName);
            mutation();
            EditorUtility.SetDirty(activeLevel);
            Undo.CollapseUndoOperations(group);
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
            Undo.IncrementCurrentGroup();
            undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoGroupName);
            undoGroupOpen = true;
        }

        private void RecordLevel()
        {
            if (activeLevel == null)
            {
                return;
            }

            Undo.RecordObject(activeLevel, Undo.GetCurrentGroupName());
        }

        private void EndLevelEdit()
        {
            if (!undoGroupOpen)
            {
                return;
            }

            Undo.SetCurrentGroupName(undoGroupName);
            Undo.CollapseUndoOperations(undoGroup);
            undoGroupOpen = false;
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
            moveFootprintSize = blockFootprint.GetEffectiveSize(definition.sizeInCells, moveRotationEuler);
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
            if (cachedLayerCount == layerCount && layerLabels != null && layerLabels.Length == layerCount)
            {
                return;
            }

            layerLabels = new string[layerCount];
            for (int i = 0; i < layerCount; i++)
            {
                layerLabels[i] = "Layer " + i.ToString();
            }

            cachedLayerCount = layerCount;
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
            footprintSize = blockFootprint.GetEffectiveSize(definition.sizeInCells, brushRotationEuler);
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
            scenePreview.Bind(
                activeLevel,
                activeCatalog,
                selectedZLayer,
                hoveredCell,
                showGhost,
                ghostBlockId,
                brushRotationEuler,
                hasExistingHover,
                existingHoverPlacement,
                overwritePlacements,
                overwriteCount,
                moveGhost);
        }

        private void RestoreAssets()
        {
            if (activeLevel == null)
            {
                activeLevel = LoadAsset<LevelData>(EditorPrefs.GetString(LevelPrefKey, string.Empty));
            }

            if (activeLevel == null)
            {
                activeLevel = AssetDatabase.LoadAssetAtPath<LevelData>(LevelAssetPaths.LevelPath);
            }

            if (!IsProjectAsset(activeCatalog))
            {
                activeCatalog = LoadCatalog();
            }

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
            string levelPath = activeLevel == null ? string.Empty : AssetDatabase.GetAssetPath(activeLevel);
            string catalogPath = activeCatalog == null ? string.Empty : AssetDatabase.GetAssetPath(activeCatalog);
            EditorPrefs.SetString(LevelPrefKey, levelPath);
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
            paletteTitleStyle.alignment = TextAnchor.MiddleCenter;
            paletteTitleStyle.clipping = TextClipping.Clip;

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
