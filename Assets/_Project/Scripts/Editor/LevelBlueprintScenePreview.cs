using System.Collections.Generic;
using SmashFast.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SmashFast.LevelEditor
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
        private readonly BlockFootprint blockFootprint = new BlockFootprint();
        private readonly int baseColorId = Shader.PropertyToID("_BaseColor");
        private readonly int colorId = Shader.PropertyToID("_Color");
        private readonly Vector3[] scratchVerts = new Vector3[4];

        private LevelData levelData;
        private BlockCatalogSO catalog;
        private int selectedZLayer;
        private Vector3Int hoveredCell;
        private bool showHoverGhost;
        private int hoverBlockId;
        private Vector3 hoverRotationEuler;
        private bool hasExistingHover;
        private BlockPlacementData existingHoverPlacement;
        private readonly BlockPlacementData[] overwritePlacements = new BlockPlacementData[16];
        private int overwriteCount;
        private BlueprintMoveGhost moveGhost;
        private bool isEnabled;
        private Material cachedBlueprintMaterial;
        private bool materialHasBaseColor;
        private bool materialHasColor;
        private Dictionary<int, CachedBlockVisual> visualCache;
        private int cachedCatalogId;
        private Mesh fallbackCubeMesh;
        private readonly Mesh[] emptyMeshes = new Mesh[0];
        private readonly Matrix4x4[] emptyMatrices = new Matrix4x4[0];

        public void Bind(
            LevelData level,
            BlockCatalogSO blockCatalog,
            int layer,
            Vector3Int hoverCell,
            bool hoverGhostVisible,
            int ghostBlockId,
            Vector3 ghostRotationEuler,
            bool existingHoverVisible,
            BlockPlacementData existingPlacement,
            BlockPlacementData[] overwriteSource,
            int overwriteSourceCount,
            BlueprintMoveGhost movingGhost)
        {
            int nextCatalogId = blockCatalog != null ? blockCatalog.GetInstanceID() : 0;
            if (cachedCatalogId != nextCatalogId)
            {
                cachedCatalogId = nextCatalogId;
                if (visualCache != null)
                {
                    visualCache.Clear();
                }
            }

            levelData = level;
            catalog = blockCatalog;
            selectedZLayer = layer;
            hoveredCell = hoverCell;
            showHoverGhost = hoverGhostVisible;
            hoverBlockId = ghostBlockId;
            hoverRotationEuler = ghostRotationEuler;
            hasExistingHover = existingHoverVisible;
            existingHoverPlacement = existingPlacement;
            moveGhost = movingGhost;
            overwriteCount = 0;
            if (overwriteSource == null || overwriteSourceCount <= 0)
            {
                return;
            }

            int copyCount = overwriteSourceCount;
            if (copyCount > overwritePlacements.Length)
            {
                copyCount = overwritePlacements.Length;
            }

            for (int i = 0; i < copyCount; i++)
            {
                overwritePlacements[i] = overwriteSource[i];
            }

            overwriteCount = copyCount;
        }

        public void ClearVisualCache()
        {
            if (visualCache != null)
            {
                visualCache.Clear();
            }
        }

        public void Enable()
        {
            if (isEnabled)
            {
                return;
            }

            EnsureMaterial();
            SceneView.duringSceneGui += OnSceneGui;
            isEnabled = true;
        }

        public void Disable()
        {
            if (!isEnabled)
            {
                return;
            }

            SceneView.duringSceneGui -= OnSceneGui;
            DestroyMaterial();
            if (visualCache != null)
            {
                visualCache.Clear();
            }

            isEnabled = false;
        }

        private void OnSceneGui(SceneView sceneView)
        {
            if (Application.isPlaying || levelData == null)
            {
                return;
            }

            Event currentEvent = Event.current;
            if (currentEvent == null || currentEvent.type != EventType.Repaint)
            {
                return;
            }

            CompareFunction previousTest = Handles.zTest;
            Color previousColor = Handles.color;
            bool previousLighting = Handles.lighting;
            Matrix4x4 previousMatrix = Handles.matrix;
            try
            {
                Handles.lighting = false;
                Handles.zTest = CompareFunction.LessEqual;
                Handles.matrix = Matrix4x4.identity;
                DrawVolume(levelData);
                DrawFloorGrid(levelData);
                DrawActiveLayerGuides(levelData);
                DrawPlacements(false);
                DrawPlacements(true);
                DrawHoverGhost(levelData);
                DrawMoveTarget(levelData);
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

        private void DrawVolume(LevelData level)
        {
            Vector3Int dimensions = level.GridDimensions;
            Vector3 cell = level.CellSize;
            Vector3 size = new Vector3(dimensions.x * cell.x, dimensions.y * cell.y, dimensions.z * cell.z);
            Vector3 center = level.OriginOffset + (size * 0.5f) + level.GetHorizontalCenterShift(catalog);
            Color frameColor = new Color(0.45f, 0.9f, 1f, 0.35f);
            using (new Handles.DrawingScope(frameColor, Matrix4x4.TRS(center, Quaternion.identity, size)))
            {
                Handles.DrawWireCube(Vector3.zero, Vector3.one);
            }
        }

        private void DrawFloorGrid(LevelData level)
        {
            Vector3Int dimensions = level.GridDimensions;
            Vector3 cell = level.CellSize;
            Vector3 origin = level.OriginOffset + level.GetHorizontalCenterShift(catalog);
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
            {
                layer = 0;
            }

            if (layer >= dimensions.z)
            {
                layer = dimensions.z - 1;
            }

            Vector3 cell = level.CellSize;
            Vector3 origin = level.OriginOffset + level.GetHorizontalCenterShift(catalog);
            float x0 = origin.x;
            float x1 = origin.x + (dimensions.x * cell.x);
            float y0 = origin.y;
            float y1 = origin.y + (dimensions.y * cell.y);
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

        private void DrawPlacements(bool activeOnly)
        {
            int count = levelData.PlacementCount;
            for (int i = 0; i < count; i++)
            {
                BlockPlacementData placement;
                if (!levelData.TryGetPlacementByIndex(i, out placement))
                {
                    continue;
                }

                Vector3Int occupiedSize = levelData.GetEffectiveSize(placement, catalog);
                int minLayer = placement.gridPosition.z;
                int maxLayer = minLayer + occupiedSize.z - 1;
                bool isActive = selectedZLayer >= minLayer && selectedZLayer <= maxLayer;
                if (isActive != activeOnly)
                {
                    continue;
                }

                DrawPlacement(placement, isActive);
            }
        }

        private void DrawPlacement(BlockPlacementData placement, bool isActive)
        {
            BlockDefinition definition = default;
            bool hasDefinition = catalog != null && catalog.TryGetBlockDefinition(placement.blockId, out definition);
            Color editorColor = hasDefinition ? definition.editorColor : new Color(0.75f, 0.75f, 0.75f, 1f);
            if (editorColor.a <= 0f)
            {
                editorColor.a = 1f;
            }

            Vector3 baseScale = hasDefinition ? definition.baseScale : Vector3.one;
            Vector3Int sizeInCells = hasDefinition ? definition.sizeInCells : Vector3Int.one;
            Matrix4x4 footprintMatrix = levelData.GetWorldMatrix(placement, baseScale, sizeInCells, catalog);
            Color ghostColor = BuildGhostColor(editorColor, isActive);
            Color wireColor = editorColor;
            wireColor.a = isActive ? 1f : 0.28f;
            bool isMoveOrigin = moveGhost.showOrigin && placement.gridPosition == moveGhost.origin.gridPosition;
            CachedBlockVisual visual = GetVisual(placement.blockId, definition, hasDefinition);
            Matrix4x4 blockWorldMatrix = GetBlockWorldMatrix(levelData, placement.gridPosition, placement.rotationEuler, baseScale, sizeInCells, visual);
            if (isMoveOrigin)
            {
                ghostColor.a = 0.16f;
                DrawVisual(visual, blockWorldMatrix, ghostColor);
                DrawDottedWireCube(footprintMatrix, new Color(0.85f, 0.9f, 0.95f, 0.9f));
                return;
            }

            DrawVisual(visual, blockWorldMatrix, ghostColor);

            using (new Handles.DrawingScope(wireColor, footprintMatrix))
            {
                Handles.DrawWireCube(Vector3.zero, Vector3.one);
            }
        }

        private void DrawMoveTarget(LevelData level)
        {
            if (!moveGhost.showTarget || catalog == null)
            {
                return;
            }

            BlockDefinition definition;
            if (!catalog.TryGetBlockDefinition(moveGhost.target.blockId, out definition))
            {
                return;
            }

            Color ghostColor = ColorForStatus(moveGhost.status);
            Matrix4x4 footprintMatrix = level.GetWorldMatrix(moveGhost.target, definition.baseScale, definition.sizeInCells, catalog);
            CachedBlockVisual visual = GetVisual(moveGhost.target.blockId, definition, true);
            Matrix4x4 blockWorldMatrix = GetBlockWorldMatrix(
                level,
                moveGhost.target.gridPosition,
                moveGhost.target.rotationEuler,
                definition.baseScale,
                definition.sizeInCells,
                visual);
            CompareFunction previousTest = Handles.zTest;
            Handles.zTest = CompareFunction.Always;
            DrawVisual(visual, blockWorldMatrix, ghostColor);
            using (new Handles.DrawingScope(ghostColor, footprintMatrix))
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
            using (new Handles.DrawingScope(wireColor, matrix))
            {
                DrawDottedEdge(origin - x - y - z, origin + x - y - z);
                DrawDottedEdge(origin - x + y - z, origin + x + y - z);
                DrawDottedEdge(origin - x - y + z, origin + x - y + z);
                DrawDottedEdge(origin - x + y + z, origin + x + y + z);
                DrawDottedEdge(origin - x - y - z, origin - x + y - z);
                DrawDottedEdge(origin + x - y - z, origin + x + y - z);
                DrawDottedEdge(origin - x - y + z, origin - x + y + z);
                DrawDottedEdge(origin + x - y + z, origin + x + y + z);
                DrawDottedEdge(origin - x - y - z, origin - x - y + z);
                DrawDottedEdge(origin + x - y - z, origin + x - y + z);
                DrawDottedEdge(origin - x + y - z, origin - x + y + z);
                DrawDottedEdge(origin + x + y - z, origin + x + y + z);
            }

            Handles.zTest = previousTest;
        }

        private void DrawDottedEdge(Vector3 start, Vector3 end)
        {
            Handles.DrawDottedLine(start, end, 4f);
        }

        private void DrawHoverGhost(LevelData level)
        {
            if (!showHoverGhost || catalog == null)
            {
                return;
            }

            BlockDefinition definition;
            if (!catalog.TryGetBlockDefinition(hoverBlockId, out definition))
            {
                return;
            }

            BlockPlacementData ghostPlacement = new BlockPlacementData();
            ghostPlacement.blockId = hoverBlockId;
            ghostPlacement.gridPosition = hoveredCell;
            ghostPlacement.rotationEuler = hoverRotationEuler;
            Vector3Int effectiveSize = blockFootprint.GetEffectiveSize(definition.sizeInCells, hoverRotationEuler);
            PlacementValidationStatus status = level.ValidatePlacement(hoveredCell, effectiveSize, catalog);
            Color ghostColor = ColorForStatus(status);
            Matrix4x4 footprintMatrix = level.GetWorldMatrix(ghostPlacement, definition.baseScale, definition.sizeInCells, catalog);
            CachedBlockVisual visual = GetVisual(hoverBlockId, definition, true);
            Matrix4x4 blockWorldMatrix = GetBlockWorldMatrix(level, hoveredCell, hoverRotationEuler, definition.baseScale, definition.sizeInCells, visual);
            CompareFunction previousTest = Handles.zTest;
            Handles.zTest = CompareFunction.Always;
            DrawVisual(visual, blockWorldMatrix, ghostColor);
            using (new Handles.DrawingScope(ghostColor, footprintMatrix))
            {
                Handles.DrawWireCube(Vector3.zero, Vector3.one);
            }

            Handles.zTest = previousTest;
        }

        private void DrawHoveredEntities(LevelData level)
        {
            if (!hasExistingHover && overwriteCount <= 0)
            {
                return;
            }

            CompareFunction previousTest = Handles.zTest;
            Handles.zTest = CompareFunction.Always;
            for (int i = 0; i < overwriteCount; i++)
            {
                DrawFootprintWire(level, overwritePlacements[i], new Color(1f, 0.38f, 0.1f, 1f));
            }

            if (hasExistingHover && !ContainsOverwriteAnchor(existingHoverPlacement.gridPosition))
            {
                DrawFootprintWire(level, existingHoverPlacement, new Color(1f, 0.95f, 0.45f, 1f));
            }

            Handles.zTest = previousTest;
        }

        private void DrawFootprintWire(LevelData level, BlockPlacementData placement, Color wireColor)
        {
            Vector3 baseScale = Vector3.one;
            Vector3Int sizeInCells = Vector3Int.one;
            BlockDefinition definition;
            if (catalog != null && catalog.TryGetBlockDefinition(placement.blockId, out definition))
            {
                baseScale = definition.baseScale;
                sizeInCells = definition.sizeInCells;
            }

            Matrix4x4 matrix = level.GetWorldMatrix(placement, baseScale, sizeInCells, catalog);
            using (new Handles.DrawingScope(wireColor, matrix))
            {
                Handles.DrawWireCube(Vector3.zero, Vector3.one);
            }
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

        private Matrix4x4 GetBlockWorldMatrix(
            LevelData level,
            Vector3Int anchor,
            Vector3 rotationEuler,
            Vector3 baseScale,
            Vector3Int sizeInCells,
            CachedBlockVisual visual)
        {
            BlockPlacementData placement = new BlockPlacementData();
            placement.gridPosition = anchor;
            placement.rotationEuler = rotationEuler;
            Vector3Int effectiveSize = blockFootprint.GetEffectiveSize(sizeInCells, rotationEuler);
            Vector3 worldPos;
            Quaternion worldRot;
            level.GetWorldPose(placement, effectiveSize, catalog, 0f, out worldPos, out worldRot);
            Vector3 scale = level.GetFootprintWorldScale(baseScale, sizeInCells);
            if (visual != null && visual.usesPrefabParts)
            {
                scale = level.GetCellWorldScale(baseScale);
            }

            return Matrix4x4.TRS(worldPos, worldRot, scale);
        }

        private Color ColorForStatus(PlacementValidationStatus status)
        {
            if (status == PlacementValidationStatus.ValidEmpty)
            {
                return new Color(0.25f, 1f, 0.4f, 0.9f);
            }

            if (status == PlacementValidationStatus.ValidOverwrite)
            {
                return new Color(1f, 0.72f, 0.15f, 0.9f);
            }

            return new Color(1f, 0.22f, 0.18f, 0.95f);
        }

        private void DrawVisual(CachedBlockVisual visual, Matrix4x4 blockWorldMatrix, Color ghostColor)
        {
            EnsureMaterial();
            if (cachedBlueprintMaterial == null || visual == null || visual.partCount <= 0)
            {
                return;
            }

            if (materialHasBaseColor)
            {
                cachedBlueprintMaterial.SetColor(baseColorId, ghostColor);
            }

            if (materialHasColor)
            {
                cachedBlueprintMaterial.SetColor(colorId, ghostColor);
            }

            if (!cachedBlueprintMaterial.SetPass(0))
            {
                return;
            }

            int partCount = visual.partCount;
            Mesh[] meshes = visual.meshes;
            Matrix4x4[] localMatrices = visual.localMatrices;
            for (int i = 0; i < partCount; i++)
            {
                Mesh mesh = meshes[i];
                if (mesh == null)
                {
                    continue;
                }

                Graphics.DrawMeshNow(mesh, blockWorldMatrix * localMatrices[i]);
            }
        }

        private CachedBlockVisual GetVisual(int blockId, BlockDefinition definition, bool hasDefinition)
        {
            if (visualCache == null)
            {
                visualCache = new Dictionary<int, CachedBlockVisual>(16);
            }

            int prefabId = 0;
            int previewId = 0;
            if (hasDefinition && definition.prefab != null)
            {
                prefabId = definition.prefab.GetInstanceID();
            }

            if (hasDefinition && definition.previewMesh != null)
            {
                previewId = definition.previewMesh.GetInstanceID();
            }

            CachedBlockVisual cached;
            if (visualCache.TryGetValue(blockId, out cached))
            {
                if (cached.prefabId == prefabId && cached.previewMeshId == previewId)
                {
                    return cached;
                }
            }

            CachedBlockVisual created = BuildVisual(definition, hasDefinition, prefabId, previewId);
            visualCache[blockId] = created;
            return created;
        }

        private CachedBlockVisual BuildVisual(BlockDefinition definition, bool hasDefinition, int prefabId, int previewId)
        {
            CachedBlockVisual visual = new CachedBlockVisual();
            visual.prefabId = prefabId;
            visual.previewMeshId = previewId;
            if (hasDefinition && definition.prefab != null && TryBuildPrefabParts(definition.prefab, visual))
            {
                return visual;
            }

            Mesh fallback = null;
            if (hasDefinition)
            {
                fallback = definition.previewMesh;
            }

            if (fallback == null)
            {
                fallback = GetFallbackCube();
            }

            if (fallback == null)
            {
                visual.meshes = emptyMeshes;
                visual.localMatrices = emptyMatrices;
                visual.partCount = 0;
                return visual;
            }

            visual.meshes = new Mesh[1];
            visual.localMatrices = new Matrix4x4[1];
            visual.meshes[0] = fallback;
            visual.localMatrices[0] = Matrix4x4.identity;
            visual.partCount = 1;
            visual.usesPrefabParts = false;
            return visual;
        }

        private bool TryBuildPrefabParts(GameObject prefab, CachedBlockVisual visual)
        {
            MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            int filterCount = filters.Length;
            int validCount = 0;
            for (int i = 0; i < filterCount; i++)
            {
                MeshFilter filter = filters[i];
                if (filter == null || filter.sharedMesh == null)
                {
                    continue;
                }

                validCount++;
            }

            if (validCount == 0)
            {
                return false;
            }

            Mesh[] meshes = new Mesh[validCount];
            Matrix4x4[] localMatrices = new Matrix4x4[validCount];
            Matrix4x4 rootWorldToLocal = prefab.transform.worldToLocalMatrix;
            int writeIndex = 0;
            for (int i = 0; i < filterCount; i++)
            {
                MeshFilter filter = filters[i];
                if (filter == null || filter.sharedMesh == null)
                {
                    continue;
                }

                meshes[writeIndex] = filter.sharedMesh;
                localMatrices[writeIndex] = rootWorldToLocal * filter.transform.localToWorldMatrix;
                writeIndex++;
            }

            visual.meshes = meshes;
            visual.localMatrices = localMatrices;
            visual.partCount = validCount;
            visual.usesPrefabParts = true;
            return true;
        }

        private Mesh GetFallbackCube()
        {
            if (fallbackCubeMesh != null)
            {
                return fallbackCubeMesh;
            }

            fallbackCubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            return fallbackCubeMesh;
        }

        private Color BuildGhostColor(Color editorColor, bool isActive)
        {
            Color hologram = Color.Lerp(new Color(0.35f, 0.86f, 1f, 1f), editorColor, 0.72f);
            hologram.a = isActive ? 0.24f : 0.08f;
            return hologram;
        }

        private void EnsureMaterial()
        {
            if (cachedBlueprintMaterial != null)
            {
                return;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Transparent");
            }

            if (shader == null)
            {
                shader = Shader.Find("UI/Default");
            }

            if (shader == null)
            {
                return;
            }

            cachedBlueprintMaterial = new Material(shader);
            cachedBlueprintMaterial.name = "SmashFast Blueprint Ghost";
            cachedBlueprintMaterial.hideFlags = HideFlags.HideAndDontSave;
            ConfigureTransparent(cachedBlueprintMaterial);
            materialHasBaseColor = cachedBlueprintMaterial.HasProperty(baseColorId);
            materialHasColor = cachedBlueprintMaterial.HasProperty(colorId);
        }

        private void ConfigureTransparent(Material material)
        {
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0f);
            }

            material.SetOverrideTag("RenderType", "Transparent");
            if (material.HasProperty("_SrcBlend"))
            {
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetInt("_ZWrite", 0);
            }

            if (material.HasProperty("_ZTest"))
            {
                material.SetInt("_ZTest", (int)CompareFunction.LessEqual);
            }

            if (material.HasProperty("_Cull"))
            {
                material.SetInt("_Cull", (int)CullMode.Off);
            }

            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", Color.white);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", Color.white);
            }
        }

        private void DestroyMaterial()
        {
            if (cachedBlueprintMaterial == null)
            {
                return;
            }

            Object.DestroyImmediate(cachedBlueprintMaterial);
            cachedBlueprintMaterial = null;
        }

        private class CachedBlockVisual
        {
            public Mesh[] meshes;
            public Matrix4x4[] localMatrices;
            public int partCount;
            public int prefabId;
            public int previewMeshId;
            public bool usesPrefabParts;
        }
    }
}
