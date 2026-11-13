using System.Collections.Generic;
using SmashFest.Data;
using SmashFest.Installers;
using SmashFest.Level;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SmashFest.LevelEditor
{
    internal struct BlueprintMoveGhost
    {
        public bool showTarget;
        public bool showOrigin;
        public PlacementValidationStatus status;
        public BlockPlacementData target;
        public BlockPlacementData origin;
    }

    internal class LevelBlueprintScenePreview
    {
        private const HideFlags PreviewHideFlags = HideFlags.HideAndDontSave;
        private readonly BlockFootprint blockFootprint = new BlockFootprint();
        private readonly Vector3[] scratchVerts = new Vector3[4];
        private readonly List<PreviewInstance> placedPreviews = new List<PreviewInstance>(64);
        private readonly Dictionary<int, bool> prefabIsCompound = new Dictionary<int, bool>(16);

        private LevelData levelData;
        private BlockCatalogSO catalog;
        private int selectedZLayer;
        private Vector3Int hoveredCell;
        private bool showHoverGhost;
        private int hoverBlockId;
        private Vector3Int hoverCustomSize;
        private Vector3 hoverRotationEuler;
        private bool hasExistingHover;
        private BlockPlacementData existingHoverPlacement;
        private readonly BlockPlacementData[] overwritePlacements = new BlockPlacementData[16];
        private int overwriteCount;
        private BlueprintMoveGhost moveGhost;
        private bool isEnabled;
        private Transform previewRoot;
        private GameSceneInstaller installer;
        private readonly LevelGroundAnchor groundAnchorResolver = new LevelGroundAnchor();
        private Vector3 groundAnchor;
        private PreviewInstance hoverPreview;
        private PreviewInstance movePreview;

        public void Bind(
            LevelData level,
            BlockCatalogSO blockCatalog,
            int layer,
            Vector3Int hoverCell,
            bool hoverGhostVisible,
            int ghostBlockId,
            Vector3 ghostRotationEuler,
            Vector3Int ghostCustomSize,
            bool existingHoverVisible,
            BlockPlacementData existingPlacement,
            BlockPlacementData[] overwriteSource,
            int overwriteSourceCount,
            BlueprintMoveGhost movingGhost)
        {
            levelData = level;
            catalog = blockCatalog;
            selectedZLayer = layer;
            hoveredCell = hoverCell;
            showHoverGhost = hoverGhostVisible;
            hoverBlockId = ghostBlockId;
            hoverRotationEuler = ghostRotationEuler;
            hoverCustomSize = ghostCustomSize;
            hasExistingHover = existingHoverVisible;
            existingHoverPlacement = existingPlacement;
            moveGhost = movingGhost;
            overwriteCount = 0;
            if (overwriteSource == null || overwriteSourceCount <= 0)
                return;

            int copyCount = overwriteSourceCount;
            if (copyCount > overwritePlacements.Length)
                copyCount = overwritePlacements.Length;

            for (int i = 0; i < copyCount; i++)
                overwritePlacements[i] = overwriteSource[i];

            overwriteCount = copyCount;
        }

        public void ClearVisualCache()
        {
            DestroyAllPreviews();
        }

        public void Enable()
        {
            if (isEnabled)
                return;

            SceneView.duringSceneGui += OnSceneGui;
            isEnabled = true;
        }

        public void Disable()
        {
            if (!isEnabled)
                return;

            SceneView.duringSceneGui -= OnSceneGui;
            DestroyAllPreviews();
            isEnabled = false;
        }

        private void OnSceneGui(SceneView sceneView)
        {
            if (Application.isPlaying || levelData == null)
            {
                DestroyAllPreviews();
                return;
            }

            Event currentEvent = Event.current;
            if (currentEvent == null || currentEvent.type != EventType.Repaint)
                return;

            CompareFunction previousTest = Handles.zTest;
            Color previousColor = Handles.color;
            bool previousLighting = Handles.lighting;
            Matrix4x4 previousMatrix = Handles.matrix;
            try
            {
                ResolveGroundAnchor();
                EnsurePreviewRoot();
                previewRoot.position = groundAnchor;
                Handles.lighting = false;
                Handles.zTest = CompareFunction.LessEqual;
                Handles.matrix = Matrix4x4.Translate(groundAnchor);
                DrawVolume(levelData);
                DrawFloorGrid(levelData);
                DrawActiveLayerGuides(levelData);
                SyncPlacedPreviews();
                DrawPlacementWires();
                SyncHoverPreview();
                SyncMovePreview();
                DrawHoveredEntities(levelData);
            }
            finally
            {
                Handles.zTest = previousTest;
                Handles.color = previousColor;
                Handles.lighting = previousLighting;
                Handles.matrix = previousMatrix;
            }
        }

        private void ResolveGroundAnchor()
        {
            if (installer == null)
                installer = Object.FindFirstObjectByType<GameSceneInstaller>();

            groundAnchor = installer != null ? groundAnchorResolver.Resolve(installer.GroundCollider) : Vector3.zero;
        }

        private void SyncPlacedPreviews()
        {
            EnsurePreviewRoot();
            int count = levelData.PlacementCount;
            EnsurePlacedCapacity(count);
            for (int i = 0; i < count; i++)
            {
                BlockPlacementData placement;
                if (!levelData.TryGetPlacementByIndex(i, out placement))
                {
                    SetPreviewActive(placedPreviews[i], false);
                    continue;
                }

                bool isMoveOrigin = moveGhost.showOrigin && placement.gridPosition == moveGhost.origin.gridPosition;
                if (isMoveOrigin)
                {
                    SetPreviewActive(placedPreviews[i], false);
                    DrawOriginWire(placement);
                    continue;
                }

                SyncPreview(placedPreviews[i], placement, true);
            }

            for (int i = count; i < placedPreviews.Count; i++)
                SetPreviewActive(placedPreviews[i], false);
        }

        private void SyncHoverPreview()
        {
            if (!showHoverGhost || catalog == null)
            {
                SetPreviewActive(hoverPreview, false);
                return;
            }

            BlockDefinition definition;
            if (!catalog.TryGetBlockDefinition(hoverBlockId, out definition) || definition.prefab == null)
            {
                SetPreviewActive(hoverPreview, false);
                return;
            }

            if (hoverPreview == null)
                hoverPreview = new PreviewInstance();

            BlockPlacementData ghostPlacement = new BlockPlacementData();
            ghostPlacement.blockId = hoverBlockId;
            ghostPlacement.gridPosition = hoveredCell;
            ghostPlacement.rotationEuler = hoverRotationEuler;
            ghostPlacement.customSize = hoverCustomSize;
            SyncPreview(hoverPreview, ghostPlacement, true);

            Vector3Int sizeInCells = levelData.GetSizeInCells(ghostPlacement, catalog);
            Vector3Int effectiveSize = blockFootprint.GetEffectiveSize(sizeInCells, hoverRotationEuler);
            PlacementValidationStatus status = levelData.ValidatePlacement(hoveredCell, effectiveSize, catalog);
            DrawFootprintWire(levelData, ghostPlacement, ColorForStatus(status));
        }

        private void SyncMovePreview()
        {
            if (!moveGhost.showTarget || catalog == null)
            {
                SetPreviewActive(movePreview, false);
                return;
            }

            BlockDefinition definition;
            if (!catalog.TryGetBlockDefinition(moveGhost.target.blockId, out definition) || definition.prefab == null)
            {
                SetPreviewActive(movePreview, false);
                return;
            }

            if (movePreview == null)
                movePreview = new PreviewInstance();

            SyncPreview(movePreview, moveGhost.target, true);
            DrawFootprintWire(levelData, moveGhost.target, ColorForStatus(moveGhost.status));
        }

        private void SyncPreview(PreviewInstance preview, BlockPlacementData placement, bool active)
        {
            if (!active || catalog == null)
            {
                SetPreviewActive(preview, false);
                return;
            }

            BlockDefinition definition;
            if (!catalog.TryGetBlockDefinition(placement.blockId, out definition) || definition.prefab == null)
            {
                SetPreviewActive(preview, false);
                return;
            }

            EnsurePreviewRoot();
            int prefabId = definition.prefab.GetInstanceID();
            if (preview.instance == null || preview.prefabId != prefabId)
            {
                DestroyPreviewInstance(preview);
                preview.instance = CreatePreviewInstance(definition.prefab);
                preview.prefabId = prefabId;
                preview.blockId = definition.blockId;
            }

            Vector3Int sizeInCells = levelData.GetSizeInCells(placement, catalog);
            Vector3Int effectiveSize = blockFootprint.GetEffectiveSize(sizeInCells, placement.rotationEuler);
            Vector3 worldPos;
            Quaternion worldRot;
            LevelLayout layout = new LevelLayout(levelData, catalog);
            layout.GetWorldPose(placement, effectiveSize, 0f, layout.GetCenterShift(), out worldPos, out worldRot);
            worldRot = worldRot * Quaternion.Euler(definition.meshEuler);
            Vector3 scale = ResolveTargetScale(definition, sizeInCells);
            Transform transform = preview.instance.transform;
            transform.SetLocalPositionAndRotation(worldPos, worldRot);
            preview.blockId = definition.blockId;
            SpawnedBlock spawnedBlock = preview.instance.GetComponent<SpawnedBlock>();
            if (spawnedBlock != null)
                spawnedBlock.ApplyEditorPreviewVisuals(scale, catalog.GetScale(), catalog.GetTableHeightFactor(levelData.CellSize.y), sizeInCells);
            else
                transform.localScale = Vector3.Scale(scale, Vector3.one * catalog.GetScale());

            if (!preview.instance.activeSelf)
                preview.instance.SetActive(true);
        }

        private GameObject CreatePreviewInstance(GameObject prefab)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
                instance = Object.Instantiate(prefab);

            instance.name = "BlueprintPreview_" + prefab.name;
            instance.hideFlags = PreviewHideFlags;
            instance.transform.SetParent(previewRoot, false);
            PreparePreviewInstance(instance);
            return instance;
        }

        private void PreparePreviewInstance(GameObject instance)
        {
            Transform[] transforms = instance.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
                transforms[i].gameObject.hideFlags = PreviewHideFlags;

            SpawnedBlock spawnedBlock = instance.GetComponent<SpawnedBlock>();
            if (spawnedBlock != null)
                spawnedBlock.ApplyEditorPreviewVisuals();

            Rigidbody[] bodies = instance.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++)
            {
                bodies[i].detectCollisions = false;
                bodies[i].isKinematic = true;
                bodies[i].useGravity = false;
            }

            Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;

            MonoBehaviour[] behaviours = instance.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
                behaviours[i].enabled = false;
        }

        private Vector3 ResolveTargetScale(BlockDefinition definition, Vector3Int sizeInCells)
        {
            if (definition.IsTable())
                return Vector3.one;

            if (IsCompoundPrefab(definition.prefab))
                return new LevelLayout(levelData, catalog).GetCellScale(definition.baseScale);

            return new LevelLayout(levelData, catalog).GetFootprintScale(definition.baseScale, sizeInCells);
        }

        private bool IsCompoundPrefab(GameObject prefab)
        {
            int prefabId = prefab.GetInstanceID();
            bool isCompound;
            if (prefabIsCompound.TryGetValue(prefabId, out isCompound))
                return isCompound;

            MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            int meshCount = 0;
            for (int i = 0; i < filters.Length; i++)
            {
                if (filters[i] != null && filters[i].sharedMesh != null)
                    meshCount++;
            }

            isCompound = meshCount > 1;
            prefabIsCompound.Add(prefabId, isCompound);
            return isCompound;
        }

        private void DrawPlacementWires()
        {
            int count = levelData.PlacementCount;
            for (int i = 0; i < count; i++)
            {
                BlockPlacementData placement;
                if (!levelData.TryGetPlacementByIndex(i, out placement))
                    continue;

                if (moveGhost.showOrigin && placement.gridPosition == moveGhost.origin.gridPosition)
                    continue;

                BlockDefinition definition = default;
                bool hasDefinition = catalog != null && catalog.TryGetBlockDefinition(placement.blockId, out definition);
                Color editorColor = hasDefinition ? definition.editorColor : new Color(0.75f, 0.75f, 0.75f, 1f);
                Vector3Int occupiedSize = levelData.GetEffectiveSize(placement, catalog);
                int minLayer = placement.gridPosition.z;
                int maxLayer = minLayer + occupiedSize.z - 1;
                bool isActive = selectedZLayer >= minLayer && selectedZLayer <= maxLayer;
                editorColor.a = isActive ? 0.85f : 0.25f;
                DrawFootprintWire(levelData, placement, editorColor);
            }
        }

        private void DrawOriginWire(BlockPlacementData placement)
        {
            DrawDottedWireCube(
                GetFootprintMatrix(placement),
                new Color(0.85f, 0.9f, 0.95f, 0.9f));
        }

        private Matrix4x4 GetFootprintMatrix(BlockPlacementData placement)
        {
            Vector3 baseScale = Vector3.one;
            Vector3Int sizeInCells = levelData.GetSizeInCells(placement, catalog);
            BlockDefinition definition;
            if (catalog != null && catalog.TryGetBlockDefinition(placement.blockId, out definition))
                baseScale = definition.baseScale;

            return new LevelLayout(levelData, catalog).GetWorldMatrix(placement, baseScale, sizeInCells);
        }

        private void DrawVolume(LevelData level)
        {
            Vector3Int dimensions = level.GridDimensions;
            Vector3 cell = new LevelLayout(level, catalog).CellSize;
            Vector3 size = new Vector3(dimensions.x * cell.x, (dimensions.y * cell.y) + new LevelLayout(level, catalog).TableLift, dimensions.z * cell.z);
            Vector3 center = level.OriginOffset + (size * 0.5f) + new LevelLayout(level, catalog).GetCenterShift();
            Color frameColor = new Color(0.45f, 0.9f, 1f, 0.35f);
            using (new Handles.DrawingScope(frameColor, Matrix4x4.Translate(groundAnchor) * Matrix4x4.TRS(center, Quaternion.identity, size)))
            {
                Handles.DrawWireCube(Vector3.zero, Vector3.one);
            }
        }

        private void DrawFloorGrid(LevelData level)
        {
            Vector3Int dimensions = level.GridDimensions;
            Vector3 cell = new LevelLayout(level, catalog).CellSize;
            Vector3 origin = level.OriginOffset + new LevelLayout(level, catalog).GetCenterShift();
            Handles.color = new Color(0.3f, 0.86f, 0.95f, 0.9f);
            for (int x = 0; x <= dimensions.x; x++)
            {
                Vector3 start = origin + new Vector3(x * cell.x, 0f, 0f);
                Vector3 end = origin + new Vector3(x * cell.x, 0f, dimensions.z * cell.z);
                Handles.DrawLine(start, end);
            }

            for (int z = 0; z <= dimensions.z; z++)
            {
                Vector3 start = origin + new Vector3(0f, 0f, z * cell.z);
                Vector3 end = origin + new Vector3(dimensions.x * cell.x, 0f, z * cell.z);
                Handles.DrawLine(start, end);
            }

            float axisLength = Mathf.Max(cell.x, Mathf.Max(cell.y, cell.z));
            Handles.color = new Color(1f, 0.35f, 0.35f, 0.95f);
            Handles.DrawLine(origin, origin + new Vector3(axisLength, 0f, 0f));
            Handles.color = new Color(0.4f, 1f, 0.45f, 0.95f);
            Handles.DrawLine(origin, origin + new Vector3(0f, axisLength, 0f));
            Handles.color = new Color(0.35f, 0.7f, 1f, 0.95f);
            Handles.DrawLine(origin, origin + new Vector3(0f, 0f, axisLength));
            Handles.color = new Color(0.75f, 1f, 1f, 0.95f);
            Handles.Label(origin + new Vector3(0f, axisLength, 0f), "Origin");
        }

        private void DrawActiveLayerGuides(LevelData level)
        {
            Vector3Int dimensions = level.GridDimensions;
            int layer = selectedZLayer;
            if (layer < 0)
                layer = 0;

            if (layer >= dimensions.z)
                layer = dimensions.z - 1;

            Vector3 cell = new LevelLayout(level, catalog).CellSize;
            Vector3 origin = level.OriginOffset + new LevelLayout(level, catalog).GetCenterShift();
            float x0 = origin.x;
            float x1 = origin.x + (dimensions.x * cell.x);
            float y0 = origin.y;
            float y1 = origin.y + (dimensions.y * cell.y) + new LevelLayout(level, catalog).TableLift;
            float z0 = origin.z + (layer * cell.z);
            float z1 = z0 + cell.z;
            float groundY = origin.y + 0.02f;
            scratchVerts[0] = new Vector3(x0, groundY, z0);
            scratchVerts[1] = new Vector3(x1, groundY, z0);
            scratchVerts[2] = new Vector3(x1, groundY, z1);
            scratchVerts[3] = new Vector3(x0, groundY, z1);
            Handles.DrawSolidRectangleWithOutline(
                scratchVerts,
                new Color(0.2f, 0.9f, 1f, 0.14f),
                new Color(0.55f, 1f, 1f, 0.95f));
            float sliceZ = origin.z + ((layer + 0.5f) * cell.z);
            scratchVerts[0] = new Vector3(x0, y0, sliceZ);
            scratchVerts[1] = new Vector3(x1, y0, sliceZ);
            scratchVerts[2] = new Vector3(x1, y1, sliceZ);
            scratchVerts[3] = new Vector3(x0, y1, sliceZ);
            Handles.DrawSolidRectangleWithOutline(
                scratchVerts,
                new Color(0.3f, 0.85f, 1f, 0.035f),
                new Color(0.6f, 1f, 1f, 0.22f));
            Handles.color = new Color(0.75f, 1f, 1f, 0.95f);
            Handles.Label(new Vector3(x0, y1 + (cell.y * 0.15f), sliceZ), "Layer " + layer.ToString());
        }

        private void DrawHoveredEntities(LevelData level)
        {
            if (!hasExistingHover && overwriteCount <= 0)
                return;

            CompareFunction previousTest = Handles.zTest;
            Handles.zTest = CompareFunction.Always;
            for (int i = 0; i < overwriteCount; i++)
                DrawFootprintWire(level, overwritePlacements[i], new Color(1f, 0.38f, 0.1f, 1f));

            if (hasExistingHover && !ContainsOverwriteAnchor(existingHoverPlacement.gridPosition))
                DrawFootprintWire(level, existingHoverPlacement, new Color(1f, 0.95f, 0.45f, 1f));

            Handles.zTest = previousTest;
        }

        private void DrawFootprintWire(LevelData level, BlockPlacementData placement, Color wireColor)
        {
            Vector3 baseScale = Vector3.one;
            Vector3Int sizeInCells = level.GetSizeInCells(placement, catalog);
            BlockDefinition definition;
            if (catalog != null && catalog.TryGetBlockDefinition(placement.blockId, out definition))
                baseScale = definition.baseScale;

            Matrix4x4 matrix = new LevelLayout(level, catalog).GetWorldMatrix(placement, baseScale, sizeInCells);
            CompareFunction previousTest = Handles.zTest;
            Handles.zTest = CompareFunction.Always;
            using (new Handles.DrawingScope(wireColor, Matrix4x4.Translate(groundAnchor) * matrix))
            {
                Handles.DrawWireCube(Vector3.zero, Vector3.one);
            }

            Handles.zTest = previousTest;
        }

        private void DrawDottedWireCube(Matrix4x4 matrix, Color wireColor)
        {
            Vector3 half = new Vector3(0.5f, 0.5f, 0.5f);
            Vector3 x = new Vector3(half.x, 0f, 0f);
            Vector3 y = new Vector3(0f, half.y, 0f);
            Vector3 z = new Vector3(0f, 0f, half.z);
            Vector3 origin = Vector3.zero;
            CompareFunction previousTest = Handles.zTest;
            Handles.zTest = CompareFunction.Always;
            using (new Handles.DrawingScope(wireColor, Matrix4x4.Translate(groundAnchor) * matrix))
            {
                Handles.DrawDottedLine(origin - x - y - z, origin + x - y - z, 4f);
                Handles.DrawDottedLine(origin - x + y - z, origin + x + y - z, 4f);
                Handles.DrawDottedLine(origin - x - y + z, origin + x - y + z, 4f);
                Handles.DrawDottedLine(origin - x + y + z, origin + x + y + z, 4f);
                Handles.DrawDottedLine(origin - x - y - z, origin - x + y - z, 4f);
                Handles.DrawDottedLine(origin + x - y - z, origin + x + y - z, 4f);
                Handles.DrawDottedLine(origin - x - y + z, origin - x + y + z, 4f);
                Handles.DrawDottedLine(origin + x - y + z, origin + x + y + z, 4f);
                Handles.DrawDottedLine(origin - x - y - z, origin - x - y + z, 4f);
                Handles.DrawDottedLine(origin + x - y - z, origin + x - y + z, 4f);
                Handles.DrawDottedLine(origin - x + y - z, origin - x + y + z, 4f);
                Handles.DrawDottedLine(origin + x + y - z, origin + x + y + z, 4f);
            }

            Handles.zTest = previousTest;
        }

        private bool ContainsOverwriteAnchor(Vector3Int anchor)
        {
            for (int i = 0; i < overwriteCount; i++)
            {
                if (overwritePlacements[i].gridPosition == anchor)
                    return true;
            }

            return false;
        }

        private Color ColorForStatus(PlacementValidationStatus status)
        {
            if (status == PlacementValidationStatus.ValidEmpty)
                return new Color(0.25f, 1f, 0.4f, 0.9f);

            if (status == PlacementValidationStatus.ValidOverwrite)
                return new Color(1f, 0.72f, 0.15f, 0.9f);

            return new Color(1f, 0.22f, 0.18f, 0.95f);
        }

        private void EnsurePreviewRoot()
        {
            if (previewRoot != null)
                return;

            GameObject root = new GameObject("SmashFest_BlueprintPreviewRoot");
            root.hideFlags = PreviewHideFlags;
            previewRoot = root.transform;
        }

        private void EnsurePlacedCapacity(int count)
        {
            while (placedPreviews.Count < count)
                placedPreviews.Add(new PreviewInstance());
        }

        private void SetPreviewActive(PreviewInstance preview, bool active)
        {
            if (preview == null || preview.instance == null)
                return;

            if (preview.instance.activeSelf != active)
                preview.instance.SetActive(active);
        }

        private void DestroyAllPreviews()
        {
            for (int i = 0; i < placedPreviews.Count; i++)
                DestroyPreviewInstance(placedPreviews[i]);

            placedPreviews.Clear();
            DestroyPreviewInstance(hoverPreview);
            hoverPreview = null;
            DestroyPreviewInstance(movePreview);
            movePreview = null;
            if (previewRoot != null)
            {
                Object.DestroyImmediate(previewRoot.gameObject);
                previewRoot = null;
            }

            prefabIsCompound.Clear();
        }

        private void DestroyPreviewInstance(PreviewInstance preview)
        {
            if (preview == null || preview.instance == null)
                return;

            Object.DestroyImmediate(preview.instance);
            preview.instance = null;
            preview.prefabId = 0;
            preview.blockId = 0;
        }

        private class PreviewInstance
        {
            public GameObject instance;
            public int prefabId;
            public int blockId;
        }
    }
}
