using SmashFast.Data;
using UnityEditor;
using UnityEngine;

namespace SmashFast.LevelEditor
{
    internal class LevelBlueprintSampleAssets
    {
        [MenuItem("Tools/SmashFast/Create Default Level Assets")]
        public static void Create()
        {
            new LevelBlueprintSampleAssets().Build();
        }

        private void Build()
        {
            EnsureFolder("Assets", "_Project");
            EnsureFolder(LevelAssetPaths.ProjectFolder, "GameData");
            EnsureFolder(LevelAssetPaths.GameDataFolder, "Levels");

            BlockCatalogSO catalog = LoadOrCreateCatalog();
            LevelData level = LoadOrCreateLevel();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = level;
            EditorGUIUtility.PingObject(level);
            LevelBlueprintEditorWindow.Open(level, catalog);
        }

        private BlockCatalogSO LoadOrCreateCatalog()
        {
            BlockCatalogSO existing = AssetDatabase.LoadAssetAtPath<BlockCatalogSO>(LevelAssetPaths.CatalogPath);
            if (existing != null)
            {
                EnsureDemoFootprintBlocks(existing);
                return existing;
            }

            BlockCatalogSO catalog = ScriptableObject.CreateInstance<BlockCatalogSO>();
            AssetDatabase.CreateAsset(catalog, LevelAssetPaths.CatalogPath);
            Mesh cubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            SerializedObject serializedCatalog = new SerializedObject(catalog);
            SerializedProperty blocksProperty = serializedCatalog.FindProperty("blocks");
            blocksProperty.arraySize = 5;
            WriteDemoBlocks(blocksProperty, cubeMesh);
            AppendIfMissing(blocksProperty, 6, "Glass", new Color(0.62f, 0.9f, 1f, 1f), 0.45f, Vector3.one, Vector3Int.one, false, cubeMesh);
            AppendIfMissing(blocksProperty, 7, "Jar", new Color(0.35f, 0.75f, 0.45f, 1f), 0.5f, Vector3.one, Vector3Int.one, false, cubeMesh);
            AppendIfMissing(blocksProperty, 8, "Ornament", new Color(0.95f, 0.85f, 0.3f, 1f), 0.35f, Vector3.one, Vector3Int.one, false, cubeMesh);
            ApplyKnownArchitecture(blocksProperty);
            serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private void EnsureDemoFootprintBlocks(BlockCatalogSO catalog)
        {
            Mesh cubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            SerializedObject serializedCatalog = new SerializedObject(catalog);
            SerializedProperty blocksProperty = serializedCatalog.FindProperty("blocks");
            StretchDemoPlank(blocksProperty);
            AppendIfMissing(blocksProperty, 4, "HalfTable", new Color(0.72f, 0.48f, 0.28f, 1f), 2.4f, Vector3.one, new Vector3Int(2, 2, 1), true, cubeMesh);
            AppendIfMissing(blocksProperty, 5, "FullTable", new Color(0.55f, 0.34f, 0.2f, 1f), 4.5f, Vector3.one, new Vector3Int(4, 2, 2), true, cubeMesh);
            AppendIfMissing(blocksProperty, 6, "Glass", new Color(0.62f, 0.9f, 1f, 1f), 0.45f, Vector3.one, Vector3Int.one, false, cubeMesh);
            AppendIfMissing(blocksProperty, 7, "Jar", new Color(0.35f, 0.75f, 0.45f, 1f), 0.5f, Vector3.one, Vector3Int.one, false, cubeMesh);
            AppendIfMissing(blocksProperty, 8, "Ornament", new Color(0.95f, 0.85f, 0.3f, 1f), 0.35f, Vector3.one, Vector3Int.one, false, cubeMesh);
            ApplyKnownArchitecture(blocksProperty);
            serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private void WriteDemoBlocks(SerializedProperty blocksProperty, Mesh cubeMesh)
        {
            WriteBlock(blocksProperty.GetArrayElementAtIndex(0), 1, "Crate", new Color(0.86f, 0.42f, 0.24f, 1f), 1.2f, Vector3.one, new Vector3Int(1, 1, 1), false, cubeMesh);
            WriteBlock(blocksProperty.GetArrayElementAtIndex(1), 2, "Stone", new Color(0.42f, 0.66f, 0.78f, 1f), 3.4f, new Vector3(0.82f, 1.15f, 0.82f), new Vector3Int(1, 1, 1), false, cubeMesh);
            WriteBlock(blocksProperty.GetArrayElementAtIndex(2), 3, "Plank", new Color(0.93f, 0.76f, 0.28f, 1f), 0.7f, Vector3.one, new Vector3Int(3, 1, 1), false, cubeMesh);
            WriteBlock(blocksProperty.GetArrayElementAtIndex(3), 4, "HalfTable", new Color(0.72f, 0.48f, 0.28f, 1f), 2.4f, Vector3.one, new Vector3Int(2, 2, 1), true, cubeMesh);
            WriteBlock(blocksProperty.GetArrayElementAtIndex(4), 5, "FullTable", new Color(0.55f, 0.34f, 0.2f, 1f), 4.5f, Vector3.one, new Vector3Int(4, 2, 2), true, cubeMesh);
        }

        private void StretchDemoPlank(SerializedProperty blocksProperty)
        {
            for (int i = 0; i < blocksProperty.arraySize; i++)
            {
                SerializedProperty element = blocksProperty.GetArrayElementAtIndex(i);
                SerializedProperty nameProperty = element.FindPropertyRelative("blockName");
                SerializedProperty sizeProperty = element.FindPropertyRelative("sizeInCells");
                if (nameProperty == null || sizeProperty == null)
                {
                    continue;
                }

                if (nameProperty.stringValue != "Plank")
                {
                    continue;
                }

                if (sizeProperty.vector3IntValue != Vector3Int.one)
                {
                    continue;
                }

                sizeProperty.vector3IntValue = new Vector3Int(3, 1, 1);
                SerializedProperty scaleProperty = element.FindPropertyRelative("baseScale");
                if (scaleProperty != null)
                {
                    scaleProperty.vector3Value = Vector3.one;
                }
            }
        }

        private void AppendIfMissing(
            SerializedProperty blocksProperty,
            int id,
            string blockName,
            Color color,
            float mass,
            Vector3 baseScale,
            Vector3Int sizeInCells,
            bool isKinematicBase,
            Mesh previewMesh)
        {
            for (int i = 0; i < blocksProperty.arraySize; i++)
            {
                SerializedProperty existing = blocksProperty.GetArrayElementAtIndex(i);
                SerializedProperty idProperty = existing.FindPropertyRelative("blockId");
                if (idProperty != null && idProperty.intValue == id)
                {
                    return;
                }
            }

            int index = blocksProperty.arraySize;
            blocksProperty.arraySize++;
            SerializedProperty element = blocksProperty.GetArrayElementAtIndex(index);
            WriteBlock(
                element,
                id,
                blockName,
                color,
                mass,
                baseScale,
                sizeInCells,
                isKinematicBase,
                previewMesh);
            CopyPrefabFromFirst(blocksProperty, element);
        }

        private void ApplyKnownArchitecture(SerializedProperty blocksProperty)
        {
            int count = blocksProperty.arraySize;
            for (int i = 0; i < count; i++)
            {
                SerializedProperty element = blocksProperty.GetArrayElementAtIndex(i);
                SerializedProperty idProperty = element.FindPropertyRelative("blockId");
                if (idProperty == null)
                {
                    continue;
                }

                ApplyKnownRole(element, idProperty.intValue);
            }
        }

        private void ApplyKnownRole(SerializedProperty element, int blockId)
        {
            if (blockId == 1)
            {
                ApplyRole(element, BlockMaterialType.Wood, BlockShapeRole.StandardBlock, true, 3.5f);
                return;
            }

            if (blockId == 2)
            {
                ApplyRole(element, BlockMaterialType.Stone, BlockShapeRole.StandardBlock, true, 6f);
                return;
            }

            if (blockId == 3)
            {
                ApplyRole(element, BlockMaterialType.Wood, BlockShapeRole.HorizontalPlank, true, 3.5f);
                return;
            }

            if (blockId == 4 || blockId == 5)
            {
                ApplyRole(element, BlockMaterialType.TableFoundation, BlockShapeRole.FoundationTable, true, 99f);
                return;
            }

            if (blockId == 6)
            {
                ApplyRole(element, BlockMaterialType.Glass, BlockShapeRole.StandardBlock, true, 2.5f);
                return;
            }

            if (blockId == 7)
            {
                ApplyRole(element, BlockMaterialType.CeramicJar, BlockShapeRole.FragileProp, false, 2.2f);
                return;
            }

            if (blockId == 8)
            {
                ApplyRole(element, BlockMaterialType.Wood, BlockShapeRole.TopOrnament, false, 3.5f);
            }
        }

        private void ApplyRole(
            SerializedProperty element,
            BlockMaterialType materialType,
            BlockShapeRole shapeRole,
            bool canSupportBlocksAbove,
            float impactBreakThreshold)
        {
            SerializedProperty materialProperty = element.FindPropertyRelative("materialType");
            SerializedProperty roleProperty = element.FindPropertyRelative("shapeRole");
            SerializedProperty supportProperty = element.FindPropertyRelative("canSupportBlocksAbove");
            SerializedProperty impactProperty = element.FindPropertyRelative("impactBreakThreshold");
            if (materialProperty != null)
            {
                materialProperty.enumValueIndex = (int)materialType;
            }

            if (roleProperty != null)
            {
                roleProperty.enumValueIndex = (int)shapeRole;
            }

            if (supportProperty != null)
            {
                supportProperty.boolValue = canSupportBlocksAbove;
            }

            if (impactProperty != null)
            {
                impactProperty.floatValue = impactBreakThreshold;
            }
        }

        private void CopyPrefabFromFirst(SerializedProperty blocksProperty, SerializedProperty element)
        {
            if (blocksProperty.arraySize <= 1)
            {
                return;
            }

            SerializedProperty first = blocksProperty.GetArrayElementAtIndex(0);
            SerializedProperty source = first.FindPropertyRelative("prefab");
            SerializedProperty target = element.FindPropertyRelative("prefab");
            if (source == null || target == null || source.objectReferenceValue == null)
            {
                return;
            }

            target.objectReferenceValue = source.objectReferenceValue;
        }

        private void WriteBlock(
            SerializedProperty element,
            int id,
            string blockName,
            Color color,
            float mass,
            Vector3 baseScale,
            Vector3Int sizeInCells,
            bool isKinematicBase,
            Mesh previewMesh)
        {
            element.FindPropertyRelative("blockId").intValue = id;
            element.FindPropertyRelative("blockName").stringValue = blockName;
            element.FindPropertyRelative("baseScale").vector3Value = baseScale;
            element.FindPropertyRelative("sizeInCells").vector3IntValue = sizeInCells;
            element.FindPropertyRelative("editorColor").colorValue = color;
            element.FindPropertyRelative("isKinematicBase").boolValue = isKinematicBase;
            element.FindPropertyRelative("mass").floatValue = mass;
            element.FindPropertyRelative("previewMesh").objectReferenceValue = previewMesh;
        }

        private LevelData LoadOrCreateLevel()
        {
            LevelData existing = AssetDatabase.LoadAssetAtPath<LevelData>(LevelAssetPaths.LevelPath);
            if (existing != null)
            {
                return existing;
            }

            LevelData level = ScriptableObject.CreateInstance<LevelData>();
            level.LevelIndex = 1;
            level.MaxAmmo = 12;
            level.GridDimensions = new Vector3Int(9, 12, 5);
            level.CellSize = Vector3.one;
            level.OriginOffset = Vector3.zero;

            PlaceDemoBlock(level, 5, new Vector3Int(0, 0, 0), Vector3.zero, new Vector3Int(4, 2, 2));
            PlaceDemoBlock(level, 4, new Vector3Int(5, 0, 0), Vector3.zero, new Vector3Int(2, 2, 1));
            PlaceDemoBlock(level, 3, new Vector3Int(0, 2, 0), Vector3.zero, new Vector3Int(3, 1, 1));
            PlaceDemoBlock(level, 3, new Vector3Int(6, 2, 0), new Vector3(0f, 90f, 0f), new Vector3Int(1, 1, 3));
            PlaceDemoBlock(level, 1, new Vector3Int(4, 2, 0), Vector3.zero, Vector3Int.one);

            AssetDatabase.CreateAsset(level, LevelAssetPaths.LevelPath);
            EditorUtility.SetDirty(level);
            return level;
        }

        private void PlaceDemoBlock(LevelData level, int blockId, Vector3Int anchor, Vector3 rotationEuler, Vector3Int effectiveSize)
        {
            BlockPlacementData placement = new BlockPlacementData();
            placement.blockId = blockId;
            placement.gridPosition = anchor;
            placement.rotationEuler = rotationEuler;
            level.PlaceBlock(placement, effectiveSize, null);
        }

        private void EnsureFolder(string parent, string folderName)
        {
            string combined = parent + "/" + folderName;
            if (AssetDatabase.IsValidFolder(combined))
            {
                return;
            }

            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
