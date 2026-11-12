using System.IO;
using SmashFast.Data;
using UnityEditor;
using UnityEngine;

namespace SmashFast.LevelEditor
{
    internal class JsonLevelDatabaseIO
    {
        public JsonLevelRepository LoadOrCreateRepository()
        {
            JsonLevelRepository repository = new JsonLevelRepository();
            string absolutePath = GetAbsoluteJsonPath();
            if (File.Exists(absolutePath))
            {
                string json = File.ReadAllText(absolutePath);
                repository.LoadFromJson(json);
                if (repository.LevelCount > 0)
                    return repository;
            }

            repository.UpsertRecord(repository.CreateDefaultRecord(1));
            Save(repository, true);
            return repository;
        }

        public void Save(JsonLevelRepository repository, bool prettyPrint)
        {
            if (repository == null)
                return;

            EnsureJsonFolder();
            string absolutePath = GetAbsoluteJsonPath();
            string json = repository.ToJson(prettyPrint);
            File.WriteAllText(absolutePath, json);
            AssetDatabase.ImportAsset(LevelAssetPaths.LevelsJsonPath);
            LevelDatabaseSO database = AssetDatabase.LoadAssetAtPath<LevelDatabaseSO>(LevelAssetPaths.LevelDatabasePath);
            if (database != null)
            {
                TextAsset textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(LevelAssetPaths.LevelsJsonPath);
                if (textAsset != null)
                {
                    SerializedObject serializedDatabase = new SerializedObject(database);
                    SerializedProperty jsonProperty = serializedDatabase.FindProperty("levelsJson");
                    if (jsonProperty != null)
                    {
                        jsonProperty.objectReferenceValue = textAsset;
                        serializedDatabase.ApplyModifiedPropertiesWithoutUndo();
                    }
                }

                database.ReloadFromText(json);
                EditorUtility.SetDirty(database);
            }
        }

        public string GetAbsoluteJsonPath()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, LevelAssetPaths.LevelsJsonPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private void EnsureJsonFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Resources"))
                AssetDatabase.CreateFolder("Assets/_Project", "Resources");

            if (!AssetDatabase.IsValidFolder(LevelAssetPaths.LevelsJsonFolder))
                AssetDatabase.CreateFolder("Assets/_Project/Resources", "Levels");
        }
    }
}
