using System;
using System.IO;
using UnityEngine;

namespace GravityWells.Meta
{
    // How the save on disk turned into the data in memory, so the caller can report a recovery
    // or a reset instead of it happening silently.
    public enum SaveLoadOutcome
    {
        Fresh,               // no save existed: a new player
        Loaded,              // the main file was read
        RecoveredFromBackup, // the main file was unusable; the previous good save was used
        ResetAfterCorruption, // the main file was unusable and so was the backup
    }

    // Takes a directory explicitly (rather than reading Application.persistentDataPath
    // itself) so tests can point it at a throwaway temp folder instead of the real
    // device save location.
    public sealed class SaveService
    {
        public const string FileName = "save.json";
        public const string BackupFileName = "save.json.bak";
        public const string CorruptFileName = "save.json.corrupt";
        public const string NewerFileName = "save.json.newer";

        private readonly string _savePath;
        private readonly string _tempPath;
        private readonly string _backupPath;
        private readonly string _corruptPath;
        private readonly string _newerPath;

        public SaveData Data { get; private set; }
        public SaveLoadOutcome LoadOutcome { get; private set; }

        // The file was written by a later build than this one (a sideloaded downgrade). The
        // fields this build knows were kept and the original file was set aside untouched.
        public bool LoadedFromNewerVersion { get; private set; }

        public SaveService(string saveDirectory)
        {
            _savePath = Path.Combine(saveDirectory, FileName);
            _tempPath = Path.Combine(saveDirectory, FileName + ".tmp");
            _backupPath = Path.Combine(saveDirectory, BackupFileName);
            _corruptPath = Path.Combine(saveDirectory, CorruptFileName);
            _newerPath = Path.Combine(saveDirectory, NewerFileName);
            Data = Load();
        }

        private SaveData Load()
        {
            bool mainExists = SafeExists(_savePath);
            if (mainExists && TryRead(_savePath, out SaveData main))
            {
                LoadOutcome = SaveLoadOutcome.Loaded;
                return Prepare(main, _savePath);
            }

            if (mainExists)
            {
                // Move the unreadable file out of the way first. Otherwise the next Save would
                // rotate it into the backup slot and destroy the one good copy that is left.
                SetAside(_savePath, _corruptPath);
            }

            if (SafeExists(_backupPath) && TryRead(_backupPath, out SaveData backup))
            {
                Debug.LogWarning("SaveService: save file unusable, recovered from the backup");
                LoadOutcome = SaveLoadOutcome.RecoveredFromBackup;
                return Prepare(backup, _backupPath);
            }

            if (mainExists)
            {
                Debug.LogWarning("SaveService: save file and backup unusable, starting fresh");
                LoadOutcome = SaveLoadOutcome.ResetAfterCorruption;
            }
            else
            {
                LoadOutcome = SaveLoadOutcome.Fresh;
            }
            return new SaveData();
        }

        // A missing/corrupt/truncated file deserializes to null, a zero Version, or null
        // collections rather than throwing, so all of those count as unreadable too.
        private static bool TryRead(string path, out SaveData data)
        {
            data = null;
            try
            {
                SaveData loaded = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                if (loaded == null || loaded.Version <= 0
                    || loaded.StarsByLevel == null || loaded.UnlockedThemeIds == null)
                {
                    return false;
                }
                data = loaded;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"SaveService: {Path.GetFileName(path)} unreadable ({e.Message})");
                return false;
            }
        }

        private SaveData Prepare(SaveData data, string sourcePath)
        {
            if (data.Version > SaveData.CurrentVersion)
            {
                LoadedFromNewerVersion = true;
                // Keep the newer build's file as it was: this build is about to rewrite the
                // save without the fields it has never heard of.
                TryCopy(sourcePath, _newerPath);
            }
            SaveMigrator.Migrate(data);
            return data;
        }

        public void Save()
        {
            try
            {
                string json = JsonUtility.ToJson(Data, true);
                File.WriteAllText(_tempPath, json);

                if (File.Exists(_savePath))
                {
                    // The file being replaced becomes the backup, so there is always one
                    // earlier good save to fall back to.
                    File.Replace(_tempPath, _savePath, _backupPath);
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

        private static bool SafeExists(string path)
        {
            try
            {
                return File.Exists(path);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void SetAside(string from, string to)
        {
            try
            {
                if (File.Exists(to))
                {
                    File.Delete(to);
                }
                File.Move(from, to);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"SaveService: could not set aside the unusable save ({e.Message})");
                TryDelete(from); // it must not be rotated into the backup slot
            }
        }

        private static void TryCopy(string from, string to)
        {
            try
            {
                File.Copy(from, to, true);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"SaveService: could not keep a copy of the newer save ({e.Message})");
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception)
            {
                // Nothing more can be done; Save will still write a fresh file.
            }
        }
    }
}
