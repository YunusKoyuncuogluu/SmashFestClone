using System.IO;
using UnityEditor;
using UnityEngine;

namespace SmashFest.LevelEditor
{
    public class PersistentDataMenu
    {
        private const string SaveFileName = "player_data.json";

        [MenuItem("Tools/SmashFest/Open Persistent Data Folder")]
        private static void OpenFromMenu() => new PersistentDataMenu().Open();

        public void Open()
        {
            string folder = Application.persistentDataPath;
            Directory.CreateDirectory(folder);

            string saveFile = Path.Combine(folder, SaveFileName);
            if (File.Exists(saveFile))
                EditorUtility.RevealInFinder(saveFile);
            else
                Application.OpenURL("file://" + folder);
        }
    }
}
