using System;
using System.IO;
using UnityEngine;

namespace SmashFest.Progress
{
    public class PlayerDataStore
    {
        private const string FileName = "player_data.json";
        private const string LegacyHighestUnlockedKey = "SmashFast.HighestUnlockedLevel";
        private const string LegacySelectedLevelKey = "SmashFast.SelectedLevelNumber";
        private const string LegacySoundKey = "SmashFast.SoundEnabled";
        private const string LegacyHapticsKey = "SmashFast.HapticsEnabled";

        private readonly string path = Path.Combine(Application.persistentDataPath, FileName);
        private string TempPath => path + ".tmp";

        public PlayerData Load()
        {
            if (TryRead(path, out PlayerData data) || TryRead(TempPath, out data))
                return data;

            return LoadLegacy();
        }

        public void Save(PlayerData data)
        {
            try
            {
                File.WriteAllText(TempPath, JsonUtility.ToJson(data, true));
                if (File.Exists(path))
                    File.Delete(path);

                File.Move(TempPath, path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private bool TryRead(string filePath, out PlayerData data)
        {
            data = null;
            if (!File.Exists(filePath))
                return false;

            try
            {
                data = JsonUtility.FromJson<PlayerData>(File.ReadAllText(filePath));
            }
            catch (Exception)
            {
                data = null;
            }

            return data != null;
        }

        private PlayerData LoadLegacy()
        {
            PlayerData data = new PlayerData();
            if (!PlayerPrefs.HasKey(LegacyHighestUnlockedKey))
                return data;

            data.highestUnlockedLevel = PlayerPrefs.GetInt(LegacyHighestUnlockedKey, 1);
            data.selectedLevel = PlayerPrefs.GetInt(LegacySelectedLevelKey, 1);
            data.soundEnabled = PlayerPrefs.GetInt(LegacySoundKey, 1) != 0;
            data.hapticsEnabled = PlayerPrefs.GetInt(LegacyHapticsKey, 1) != 0;
            Save(data);
            return data;
        }
    }
}
