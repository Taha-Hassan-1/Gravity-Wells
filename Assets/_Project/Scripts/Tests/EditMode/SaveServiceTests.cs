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
        public void Load_EmptyFile_StartsFreshWithoutThrowing()
        {
            File.WriteAllText(Path.Combine(_tempDir, "save.json"), string.Empty);

            SaveService service = null;
            Assert.DoesNotThrow(() => service = new SaveService(_tempDir));
            Assert.AreEqual(1, service.Data.HighestLevelUnlocked);
        }
    }
}
