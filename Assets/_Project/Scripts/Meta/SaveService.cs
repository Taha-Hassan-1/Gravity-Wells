using System;
using System.IO;
using UnityEngine;

namespace GravityWells.Meta
{
    // Takes a directory explicitly (rather than reading Application.persistentDataPath
    // itself) so tests can point it at a throwaway temp folder instead of the real
    // device save location.
    public sealed class SaveService
    {
        private readonly string _savePath;
        private readonly string _tempPath;

        public SaveData Data { get; private set; }

        public SaveService(string saveDirectory)
        {
            _savePath = Path.Combine(saveDirectory, "save.json");
            _tempPath = Path.Combine(saveDirectory, "save.json.tmp");
            Data = Load();
        }

        private SaveData Load()
        {
            try
            {
                if (!File.Exists(_savePath))
                {
                    return new SaveData();
                }

                string json = File.ReadAllText(_savePath);
                SaveData loaded = JsonUtility.FromJson<SaveData>(json);

                // A missing/corrupt/truncated file deserializes to null, a zero Version,
                // or null collections rather than throwing — treat all of those as
                // "start fresh" too, not just a thrown exception.
                if (loaded == null || loaded.Version <= 0
                    || loaded.StarsByLevel == null || loaded.UnlockedThemeIds == null)
                {
                    return new SaveData();
                }

                return loaded;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"SaveService: save file unreadable, starting fresh ({e.Message})");
                return new SaveData();
            }
        }

        public void Save()
        {
            try
            {
                string json = JsonUtility.ToJson(Data, true);
                File.WriteAllText(_tempPath, json);

                if (File.Exists(_savePath))
                {
                    File.Replace(_tempPath, _savePath, null);
                }
                else
                {
                    File.Move(_tempPath, _savePath);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"SaveService: failed to save ({e.Message})");
            }
        }
    }
}
