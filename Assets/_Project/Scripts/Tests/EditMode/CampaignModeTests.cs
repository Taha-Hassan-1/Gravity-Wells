using System;
using System.IO;
using NUnit.Framework;
using GravityWells.Core;
using GravityWells.Game;
using GravityWells.Meta;

namespace GravityWells.Tests
{
    public class CampaignModeTests
    {
        private string _dir;
        private SaveService _save;
        private ProgressionService _progression;
        private CampaignMode _mode;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "GravityWellsCampaignTests_" + Guid.NewGuid());
            Directory.CreateDirectory(_dir);
            _save = new SaveService(_dir);
            _progression = new ProgressionService(_save);
            _mode = new CampaignMode(_progression);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, recursive: true);
            }
        }

        [Test]
        public void ANewPlayer_StartsAtLevelOne()
        {
            LevelDefinition first = _mode.Begin();
            Assert.AreEqual(1, _mode.LevelNumber);
            Assert.AreEqual(1, first.Level);
            Assert.AreEqual(DifficultyCurve.SeedForLevel(1), first.Seed);
        }

        [Test]
        public void Next_GivesTheFollowingLevel_AndReplayTheSameOneAgain()
        {
            _mode.Begin();
            LevelDefinition second = _mode.Next();
            Assert.AreEqual(2, second.Level);
            Assert.AreEqual(2, _mode.LevelNumber);

            LevelDefinition again = _mode.Replay();
            Assert.AreEqual(2, again.Level);
            Assert.AreEqual(second.Seed, again.Seed);
            Assert.AreEqual(second.Start, again.Start);
        }

        // Wells used relative to par, and the stars that earns.
        [TestCase(-1, 3, true)]
        [TestCase(0, 3, false)]
        [TestCase(1, 2, false)]
        [TestCase(2, 1, false)]
        public void AWin_EarnsStarsByWellsUsedAgainstPar(int offsetFromPar, int stars, bool underPar)
        {
            LevelDefinition level = _mode.Begin(40); // par is at least 2 here, so one under par is possible
            LevelResult result = _mode.OnWon(level.Par + offsetFromPar, launches: 3, usedExtraWell: false);

            Assert.AreEqual(stars, result.Stars);
            Assert.AreEqual(underPar, result.UnderPar);
            Assert.AreEqual(level.Par, result.Par);
            Assert.AreEqual(3, result.Launches);
        }

        [Test]
        public void AWin_IsRecorded_UnlocksTheNextLevel_AndStarsOnlyGoUp()
        {
            _mode.Begin();
            LevelResult first = _mode.OnWon(wellsUsed: 5, launches: 4, usedExtraWell: false);
            Assert.AreEqual(1, first.Stars);
            Assert.IsTrue(first.NewBest);
            Assert.AreEqual(1, _progression.StarsFor(1));
            Assert.AreEqual(2, _progression.HighestLevelUnlocked);
            Assert.AreEqual(2, _progression.CurrentLevel);

            _mode.Replay();
            LevelResult better = _mode.OnWon(wellsUsed: 1, launches: 1, usedExtraWell: false);
            Assert.AreEqual(3, better.Stars);
            Assert.IsTrue(better.NewBest);

            _mode.Replay();
            LevelResult worse = _mode.OnWon(wellsUsed: 2, launches: 9, usedExtraWell: false);
            Assert.AreEqual(2, worse.Stars);
            Assert.IsFalse(worse.NewBest);
            Assert.AreEqual(3, _progression.StarsFor(1), "a worse replay never takes stars away");
            Assert.AreEqual(2, _progression.CurrentLevel, "replaying does not move the player back");
        }

        [Test]
        public void TheExtraWell_AlwaysEarnsOneStar_AndNeverCountsAsUnderPar()
        {
            _mode.Begin(30);
            LevelResult result = _mode.OnWon(wellsUsed: 1, launches: 2, usedExtraWell: true);
            Assert.AreEqual(1, result.Stars);
            Assert.IsFalse(result.UnderPar);
            Assert.IsTrue(result.UsedExtraWell);
        }

        [Test]
        public void UnderPar_IsRecordedInTheSave()
        {
            LevelDefinition level = _mode.Begin(40);
            Assert.GreaterOrEqual(level.Par, 2);
            _mode.OnWon(level.Par - 1, launches: 1, usedExtraWell: false);

            Assert.AreEqual(1, _save.Data.UnderParCount);
            Assert.AreEqual(1, _save.Data.TotalLaunches);
            Assert.AreEqual(level.Par - 1, _save.Data.TotalWellsPlaced);
        }

        [Test]
        public void Progress_SurvivesAReload()
        {
            _mode.Begin();
            _mode.OnWon(wellsUsed: 1, launches: 1, usedExtraWell: false);

            var reloaded = new ProgressionService(new SaveService(_dir));
            Assert.AreEqual(3, reloaded.StarsFor(1));
            Assert.AreEqual(2, reloaded.CurrentLevel);
            Assert.AreEqual(3, reloaded.TotalStars);
        }

        [Test]
        public void SetCurrentLevel_CannotJumpAheadOfWhatIsUnlocked()
        {
            _progression.SetCurrentLevel(50);
            Assert.AreEqual(1, _progression.CurrentLevel);
        }

        [Test]
        public void ALevelBelowOne_IsRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _progression.RecordWin(0, 3, false, 1, 1));
        }
    }
}
