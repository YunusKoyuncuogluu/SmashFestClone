using SmashFest.Data;
using UnityEditor;
using UnityEngine;

namespace SmashFest.LevelEditor
{
    internal class LevelBlueprintSampleAssets
    {
        [MenuItem("Tools/SmashFest/Create Default Level Assets")]
        public static void Create()
        {
            new LevelBlueprintSampleAssets().Build();
        }

        private void Build()
        {
            EnsureFolder("Assets", "_Project");
            EnsureFolder(LevelAssetPaths.ProjectFolder, "Data");
            EnsureFolder("Assets/_Project", "Resources");
            EnsureFolder("Assets/_Project/Resources", "Levels");

            BlockCatalogSO catalog = LoadOrCreateCatalog();
            JsonLevelDatabaseIO jsonIo = new JsonLevelDatabaseIO();
            JsonLevelRepository repository = jsonIo.LoadOrCreateRepository();
            if (repository.LevelCount == 0)
            {
                repository.UpsertRecord(repository.CreateDefaultRecord(1));
                jsonIo.Save(repository, true);
            }

            LevelDatabaseSO database = AssetDatabase.LoadAssetAtPath<LevelDatabaseSO>(LevelAssetPaths.LevelDatabasePath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<LevelDatabaseSO>();
                AssetDatabase.CreateAsset(database, LevelAssetPaths.LevelDatabasePath);
            }

            SerializedObject serializedDatabase = new SerializedObject(database);
            SerializedProperty catalogProperty = serializedDatabase.FindProperty("blockCatalog");
            SerializedProperty jsonProperty = serializedDatabase.FindProperty("levelsJson");
            if (catalogProperty != null)
                catalogProperty.objectReferenceValue = catalog;

            TextAsset levelsJson = AssetDatabase.LoadAssetAtPath<TextAsset>(LevelAssetPaths.LevelsJsonPath);
            if (jsonProperty != null)
                jsonProperty.objectReferenceValue = levelsJson;

            serializedDatabase.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = database;
            EditorGUIUtility.PingObject(database);
            LevelBlueprintEditorWindow.Open();
        }

        private BlockCatalogSO LoadOrCreateCatalog()
        {
            BlockCatalogSO existing = AssetDatabase.LoadAssetAtPath<BlockCatalogSO>(LevelAssetPaths.CatalogPath);
            if (existing != null)
            {
                existing.InvalidateRuntimeBlocks();
                EditorUtility.SetDirty(existing);
                return existing;
            }

            BlockCatalogSO catalog = ScriptableObject.CreateInstance<BlockCatalogSO>();
            AssetDatabase.CreateAsset(catalog, LevelAssetPaths.CatalogPath);
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private void EnsureFolder(string parent, string folderName)
        {
            string combined = parent + "/" + folderName;
            if (AssetDatabase.IsValidFolder(combined))
                return;

            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
