using System;
using System.IO;
using NUnit.Framework;
using GravityWells.Meta;

namespace GravityWells.Tests
{
    public class SaveServiceTests
    {
        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "GravityWellsSaveServiceTests_" + Guid.NewGuid());
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }

        [Test]
        public void Load_NoExistingFile_ReturnsDefaults()
        {
            var service = new SaveService(_tempDir);

            Assert.AreEqual(1, service.Data.HighestLevelUnlocked);
            Assert.AreEqual(1, service.Data.CurrentLevel);
            Assert.IsTrue(service.Data.SoundOn);
            Assert.IsTrue(service.Data.HapticsOn);
            Assert.IsNotNull(service.Data.StarsByLevel);
            Assert.IsNotNull(service.Data.UnlockedThemeIds);
        }

        [Test]
        public void SaveThenLoad_RoundTripsData()
        {
            var writer = new SaveService(_tempDir);
            writer.Data.HighestLevelUnlocked = 42;
            writer.Data.StarsByLevel.Add(3);
            writer.Data.StarsByLevel.Add(2);
            writer.Data.SoundOn = false;
            writer.Save();

            var reader = new SaveService(_tempDir);

            Assert.AreEqual(42, reader.Data.HighestLevelUnlocked);
            Assert.AreEqual(2, reader.Data.StarsByLevel.Count);
            Assert.AreEqual(3, reader.Data.StarsByLevel[0]);
            Assert.IsFalse(reader.Data.SoundOn);
        }

        [Test]
        public void SaveTwice_SecondSaveOverwritesCleanly()
        {
            var service = new SaveService(_tempDir);
            service.Data.HighestLevelUnlocked = 5;
            service.Save();
            service.Data.HighestLevelUnlocked = 9;
            service.Save();

            var reader = new SaveService(_tempDir);
            Assert.AreEqual(9, reader.Data.HighestLevelUnlocked);
        }

        [Test]
        public void Load_CorruptFile_StartsFreshWithoutThrowing()
        {
            File.WriteAllText(Path.Combine(_tempDir, "save.json"), "{ this is not valid json at all !!!");

            SaveService service = null;
            Assert.DoesNotThrow(() => service = new SaveService(_tempDir));
            Assert.AreEqual(1, service.Data.HighestLevelUnlocked);
        }

        [Test]
        public void Load_NoFile_ReportsFresh()
        {
            Assert.AreEqual(SaveLoadOutcome.Fresh, new SaveService(_tempDir).LoadOutcome);
        }

        [Test]
        public void Load_CorruptFileWithNoBackup_ReportsResetAndKeepsTheBadFile()
        {
            File.WriteAllText(Path.Combine(_tempDir, SaveService.FileName), "{ broken");

            var service = new SaveService(_tempDir);
            service.Save(); // what Bootstrap does at launch

            Assert.AreEqual(SaveLoadOutcome.ResetAfterCorruption, service.LoadOutcome);
            Assert.AreEqual("{ broken", File.ReadAllText(Path.Combine(_tempDir, SaveService.CorruptFileName)));
        }

        [Test]
        public void Save_SecondSave_KeepsThePreviousOneAsBackup()
        {
            var service = new SaveService(_tempDir);
            service.Data.HighestLevelUnlocked = 5;
            service.Save();
            service.Data.HighestLevelUnlocked = 9;
            service.Save();

            File.Copy(Path.Combine(_tempDir, SaveService.BackupFileName), Path.Combine(_tempDir, SaveService.FileName), true);
            Assert.AreEqual(5, new SaveService(_tempDir).Data.HighestLevelUnlocked);
        }

        [Test]
        public void Load_CorruptFile_RecoversFromBackup()
        {
            var writer = new SaveService(_tempDir);
            writer.Data.HighestLevelUnlocked = 5;
            writer.Data.Coins = 120;
            writer.Save();
            writer.Data.HighestLevelUnlocked = 6;
            writer.Save();
            File.WriteAllText(Path.Combine(_tempDir, SaveService.FileName), "{ \"Version\": 1, \"HighestLev");

            var reader = new SaveService(_tempDir);

            Assert.AreEqual(SaveLoadOutcome.RecoveredFromBackup, reader.LoadOutcome);
            Assert.AreEqual(5, reader.Data.HighestLevelUnlocked);
            Assert.AreEqual(120, reader.Data.Coins);
        }

        // The regression this guards: launch saves straight after loading, and that save used to
        // overwrite the only good copy.
        [Test]
        public void Load_CorruptFile_RecoveredProgressSurvivesLaunchSaveAndRelaunch()
        {
            var writer = new SaveService(_tempDir);
            writer.Data.HighestLevelUnlocked = 5;
            writer.Save();
            writer.Data.HighestLevelUnlocked = 6;
            writer.Save();
            File.WriteAllText(Path.Combine(_tempDir, SaveService.FileName), string.Empty);

            var firstLaunch = new SaveService(_tempDir);
            firstLaunch.Data.SessionCount++;
            firstLaunch.Save();
            firstLaunch.Save();

            var secondLaunch = new SaveService(_tempDir);
            Assert.AreEqual(SaveLoadOutcome.Loaded, secondLaunch.LoadOutcome);
            Assert.AreEqual(5, secondLaunch.Data.HighestLevelUnlocked);
        }

        [Test]
        public void Load_MissingMainFileButBackupPresent_UsesBackup()
        {
            var writer = new SaveService(_tempDir);
            writer.Data.HighestLevelUnlocked = 7;
            writer.Save();
            File.Move(Path.Combine(_tempDir, SaveService.FileName), Path.Combine(_tempDir, SaveService.BackupFileName));

            var reader = new SaveService(_tempDir);

            Assert.AreEqual(SaveLoadOutcome.RecoveredFromBackup, reader.LoadOutcome);
            Assert.AreEqual(7, reader.Data.HighestLevelUnlocked);
        }

        [Test]
        public void Load_SaveFromNewerBuild_KeepsKnownFieldsAndSetsTheOriginalAside()
        {
            string newer = "{ \"Version\": 99, \"HighestLevelUnlocked\": 30, \"FutureField\": 4,"
                + " \"StarsByLevel\": [], \"UnlockedThemeIds\": [0] }";
            File.WriteAllText(Path.Combine(_tempDir, SaveService.FileName), newer);

            var service = new SaveService(_tempDir);
            service.Save();

            Assert.IsTrue(service.LoadedFromNewerVersion);
            Assert.AreEqual(30, service.Data.HighestLevelUnlocked);
            Assert.AreEqual(SaveData.CurrentVersion, service.Data.Version);
            Assert.AreEqual(newer, File.ReadAllText(Path.Combine(_tempDir, SaveService.NewerFileName)));
        }

        [Test]
        public void Migrate_OldSave_RunsEachStepOnceInOrder()
        {
            var data = new SaveData { Version = 1 };
            var ran = new System.Collections.Generic.List<int>();
            var steps = new Action<SaveData>[] { _ => ran.Add(1), _ => ran.Add(2), _ => ran.Add(3) };

            SaveMigrator.Migrate(data, steps, 4);

            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, ran);
            Assert.AreEqual(4, data.Version);
        }

        [Test]
        public void Migrate_PartlyOldSave_SkipsStepsAlreadyApplied()
        {
            var data = new SaveData { Version = 3 };
            var ran = new System.Collections.Generic.List<int>();
            var steps = new Action<SaveData>[] { _ => ran.Add(1), _ => ran.Add(2), _ => ran.Add(3) };

            SaveMigrator.Migrate(data, steps, 4);

            CollectionAssert.AreEqual(new[] { 3 }, ran);
        }

        [Test]
        public void Migrate_CurrentSave_RunsNothingAndRepairsNullLists()
        {
            var data = new SaveData { QuestProgress = null, FrozenDatesUtc = null, LastDailyDateUtc = null };

            SaveMigrator.Migrate(data);

            Assert.AreEqual(SaveData.CurrentVersion, data.Version);
            Assert.IsNotNull(data.QuestProgress);
            Assert.IsNotNull(data.FrozenDatesUtc);
            Assert.AreEqual("", data.LastDailyDateUtc);
        }

        [Test]
        public void Load_EmptyFile_StartsFreshWithoutThrowing()
        {
            File.WriteAllText(Path.Combine(_tempDir, "save.json"), string.Empty);

            SaveService service = null;
            Assert.DoesNotThrow(() => service = new SaveService(_tempDir));
            Assert.AreEqual(1, service.Data.HighestLevelUnlocked);
        }
    }
}
