using SmashFast.Data;
using UnityEditor;
using UnityEngine;

namespace SmashFast.LevelEditor
{
    [CustomEditor(typeof(LevelData))]
    public class LevelDataEditor : UnityEditor.Editor
    {
        private bool showPlacements;
        private GUIStyle openButtonStyle;

        public override void OnInspectorGUI()
        {
            LevelData levelData = (LevelData)target;
            BlockCatalogSO catalog = LoadCatalog();
            EnsureButtonStyle();

            if (GUILayout.Button("Open Level Blueprint Editor", openButtonStyle))
            {
                if (catalog != null)
                {
                    LevelBlueprintEditorWindow.Open(levelData, catalog);
                }
                else
                {
                    LevelBlueprintEditorWindow.Open(levelData);
                }
            }

            EditorGUILayout.Space(6f);
            DrawSummary(levelData, catalog);
            EditorGUILayout.Space(4f);
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "placements");
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.Space(4f);
            showPlacements = EditorGUILayout.Foldout(showPlacements, "Placements", true);
            if (showPlacements)
            {
                DrawPlacementRows(levelData, catalog);
            }

            if (GUILayout.Button("Remove Out Of Bounds Placements"))
            {
                Undo.RecordObject(levelData, "Remove Out Of Bounds Placements");
                int removed = levelData.RemoveOutOfBounds();
                EditorUtility.SetDirty(levelData);
                SceneView.RepaintAll();
                Debug.Log("Removed " + removed.ToString() + " out of bounds placements from " + levelData.name + ".", levelData);
            }
        }

        private void DrawSummary(LevelData levelData, BlockCatalogSO catalog)
        {
            int total = levelData.PlacementCount;
            int tables = 0;
            int targets = 0;
            int wood = 0;
            int stone = 0;
            int glass = 0;
            int ceramic = 0;
            for (int i = 0; i < total; i++)
            {
                BlockPlacementData placement;
                if (!levelData.TryGetPlacementByIndex(i, out placement))
                {
                    continue;
                }

                BlockDefinition definition = default;
                bool hasDefinition = false;
                if (catalog != null)
                {
                    hasDefinition = catalog.TryGetBlockDefinition(placement.blockId, out definition);
                }

                if (hasDefinition && levelData.IsFoundationTable(placement, catalog))
                {
                    tables++;
                }
                else if (!hasDefinition)
                {
                    targets++;
                }
                else if (definition.IsTarget())
                {
                    targets++;
                }

                if (!hasDefinition)
                {
                    continue;
                }

                if (definition.materialType == BlockMaterialType.Wood)
                {
                    wood++;
                }
                else if (definition.materialType == BlockMaterialType.Stone)
                {
                    stone++;
                }
                else if (definition.materialType == BlockMaterialType.Glass)
                {
                    glass++;
                }
                else if (definition.materialType == BlockMaterialType.CeramicJar)
                {
                    ceramic++;
                }
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Level Summary", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Total Blocks", total.ToString());
            EditorGUILayout.LabelField("Tables", tables.ToString());
            EditorGUILayout.LabelField("Breakable Targets", targets.ToString());
            EditorGUILayout.LabelField("Wood / Stone / Glass / Ceramic", wood.ToString() + " / " + stone.ToString() + " / " + glass.ToString() + " / " + ceramic.ToString());
            EditorGUILayout.EndVertical();
        }

        private void DrawPlacementRows(LevelData levelData, BlockCatalogSO catalog)
        {
            int count = levelData.PlacementCount;
            if (count == 0)
            {
                EditorGUILayout.HelpBox("This level has no placements yet.", MessageType.None);
                return;
            }

            for (int i = 0; i < count; i++)
            {
                BlockPlacementData placement;
                if (!levelData.TryGetPlacementByIndex(i, out placement))
                {
                    continue;
                }

                string blockName = "#" + placement.blockId.ToString();
                BlockDefinition definition;
                if (catalog != null && catalog.TryGetBlockDefinition(placement.blockId, out definition) && !string.IsNullOrEmpty(definition.blockName))
                {
                    blockName = definition.blockName;
                }

                Vector3Int cell = placement.gridPosition;
                string row = "#" + placement.blockId.ToString()
                    + " [" + blockName + "] -> Grid ("
                    + cell.x.ToString() + ", " + cell.y.ToString() + ", " + cell.z.ToString()
                    + ") | Yaw: " + placement.rotationEuler.y.ToString("0") + "\u00B0";
                EditorGUILayout.LabelField(row);
            }
        }

        private BlockCatalogSO LoadCatalog()
        {
            BlockCatalogSO catalog = AssetDatabase.LoadAssetAtPath<BlockCatalogSO>(LevelAssetPaths.CatalogPath);
            if (catalog != null)
            {
                return catalog;
            }

            LevelDatabaseSO database = AssetDatabase.LoadAssetAtPath<LevelDatabaseSO>(LevelAssetPaths.LevelDatabasePath);
            if (database != null && database.BlockCatalog != null)
            {
                return database.BlockCatalog;
            }

            string savedPath = EditorPrefs.GetString("SmashFast.Blueprint.Catalog", string.Empty);
            if (!LevelAssetPaths.IsProjectAssetPath(savedPath))
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<BlockCatalogSO>(savedPath);
        }

        private void EnsureButtonStyle()
        {
            if (openButtonStyle != null)
            {
                return;
            }

            openButtonStyle = new GUIStyle(GUI.skin.button);
            openButtonStyle.fontSize = 14;
            openButtonStyle.fontStyle = FontStyle.Bold;
            openButtonStyle.fixedHeight = 36f;
        }
    }
}
