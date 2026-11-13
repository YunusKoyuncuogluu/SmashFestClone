using System;
using System.Collections.Generic;
using SmashFest.Data;
using UnityEditor;
using UnityEngine;

namespace SmashFest.LevelEditor
{
    [Serializable]
    internal class LevelEditorState
    {
        public const float PaletteHeight = 245f;
        public const float WindowPadding = 16f;
        public const float NarrowPaletteWidth = 560f;
        public const int MaxOverwriteHighlights = 16;

        [NonSerialized] private LevelBlueprintEditorWindow window;
        public BlockCatalogSO activeCatalog;
        public JsonLevelRepository levelRepository;
        public readonly LevelLayerTools layerTools = new LevelLayerTools();
        public int editingLevelIndex = 1;
        public int jumpLevelField = 1;
        public bool jsonDirty;
        public int selectedZLayer;
        public int brushBlockId;
        public bool hasBrush;
        public Vector3 brushRotationEuler;
        public bool hasSelectedPlacement;
        public BlockPlacementData selectedPlacement;
        public readonly BlockFootprint blockFootprint = new BlockFootprint();
        public readonly LevelEditorDragPayload dragPayload = new LevelEditorDragPayload();
        public bool isPainting;
        public bool isErasing;
        public bool hasLastEditedCell;
        public Vector3Int hoveredCell;
        public bool hasHoveredCell;
        public bool hoverChanged;
        public bool dragFootprintActive;
        public bool hasFootprintPreview;
        public Vector3Int footprintAnchor;
        public Vector3Int footprintSize;
        public PlacementValidationStatus footprintStatus;
        public bool hasExistingHover;
        public BlockPlacementData existingHoverPlacement;
        public readonly BlockPlacementData[] overwritePlacements = new BlockPlacementData[MaxOverwriteHighlights];
        public int overwriteCount;
        public bool isMovingExistingBlock;
        public BlockPlacementData moveOriginPlacement;
        public Vector3 moveRotationEuler;
        public bool hasMovePreview;
        public string statusMessage = "Select a block from the palette.";
        [NonSerialized] public GUIStyle cellLabelStyle;
        [NonSerialized] public GUIStyle matrixLabelStyle;
        [NonSerialized] public GUIStyle paletteTitleStyle;
        [NonSerialized] public GUIStyle paletteIdStyle;
        public string[] layerLabels = Array.Empty<string>();
        public LevelBlueprintScenePreview scenePreview;

        public LevelData ActiveLevel
        {
            get => window.CurrentLevel;
            set => window.CurrentLevel = value;
        }

        public void Bind(LevelBlueprintEditorWindow window) => this.window = window;
    }
}
