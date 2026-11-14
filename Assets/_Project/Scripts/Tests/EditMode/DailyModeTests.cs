using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using GravityWells.Core;
using GravityWells.Game;
using GravityWells.Meta;

namespace GravityWells.Tests
{
    public class DailyModeTests
    {
        private static readonly DateTime Day1 = new DateTime(2026, 11, 14, 9, 30, 0, DateTimeKind.Utc);

        private string _dir;
        private SaveService _save;
        private EconomyService _economy;
        private DateTime _now;
        private DailyMode _mode;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "GravityWellsDailyTests_" + Guid.NewGuid());
            Directory.CreateDirectory(_dir);
            _save = new SaveService(_dir);
            _economy = new EconomyService(_save);
            _now = Day1;
            var quests = new QuestService(_save, _economy, () => _now);
            _mode = new DailyMode(_save, new RewardService(_economy, quests), () => _now);
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
        public void TheSameUtcDate_GivesTheSamePuzzle_OnAnyDevice()
        {
            LevelDefinition a = _mode.Begin();
            var other = new DailyMode(new SaveService(Path.Combine(_dir, "other")), null, () => Day1.AddHours(8));
            LevelDefinition b = other.Begin();

            Assert.AreEqual(a.Seed, b.Seed);
            Assert.AreEqual(a.Start, b.Start);
            Assert.AreEqual(a.Goal, b.Goal);
            Assert.AreEqual(a.Gates.Length, b.Gates.Length);
            Assert.AreEqual(DailyChallengeService.SeedForDate(Day1), a.Seed);
            Assert.AreEqual(20261114, a.Seed, "the UTC date as a number");
        }

        [Test]
        public void ADifferentDate_GivesADifferentPuzzle()
        {
            LevelDefinition today = _mode.Begin();
            _now = Day1.AddDays(1);
            LevelDefinition tomorrow = _mode.Begin();
            Assert.AreNotEqual(today.Seed, tomorrow.Seed);
            Assert.AreNotEqual(today.Start, tomorrow.Start);
        }

        [Test]
        public void ThePuzzle_IsBuiltAtTheDifficultyOfLevel40()
        {
            LevelDefinition level = _mode.Begin();
            Assert.AreEqual(GameConfig.DailyLevel, level.Level);
            Assert.AreEqual(DifficultyCurve.PlanForLevel(40).Wells, level.Par);
            Assert.AreEqual(40, _mode.LevelNumber);
        }

        [Test]
        public void APuzzleBegunBeforeMidnight_IsFinishedAsTheDayItBegan()
        {
            _now = new DateTime(2026, 11, 14, 23, 59, 0, DateTimeKind.Utc);
            LevelDefinition level = _mode.Begin();
            _now = _now.AddMinutes(5);

            Assert.AreEqual(level.Seed, _mode.Replay().Seed, "a replay is the same puzzle, not the next day's");
        }

        [Test]
        public void TheFirstCompletionToday_PaysDouble_AndStartsTheStreak()
        {
            _mode.Begin();
            Assert.IsFalse(_mode.CompletedToday);

            LevelResult result = _mode.OnWon(wellsUsed: 2, launches: 3, usedExtraWell: false, usedHint: false);

            Assert.IsTrue(result.IsDaily);
            Assert.IsFalse(result.CanContinue);
            Assert.IsTrue(_mode.CompletedToday);
            Assert.AreEqual(1, result.Streak);
            Assert.GreaterOrEqual(result.Coins, Economy.DailyCoins(result.Stars, true));
            Assert.AreEqual(result.Coins, _economy.Coins);
            Assert.AreEqual(1, _save.Data.DailyStreak);
        }

        [Test]
        public void ASecondCompletionToday_PaysNothing_AndDoesNotMoveTheStreak()
        {
            _mode.Begin();
            _mode.OnWon(2, 3, false, false);
            int coins = _economy.Coins;

            _mode.Replay();
            LevelResult again = _mode.OnWon(2, 3, false, false);

            Assert.AreEqual(0, again.Coins);
            Assert.AreEqual(coins, _economy.Coins);
            Assert.AreEqual(1, _save.Data.DailyStreak, "one completion a day");
            Assert.IsFalse(again.NewBest);
        }

        [Test]
        public void ConsecutiveDays_BuildTheStreak_AndAMissedDayBreaksIt()
        {
            for (int day = 0; day < 3; day++)
            {
                _now = Day1.AddDays(day);
                _mode.Begin();
                _mode.OnWon(2, 3, false, false);
            }
            Assert.AreEqual(3, _save.Data.DailyStreak);
            Assert.AreEqual(3, _mode.Streak);

            _now = Day1.AddDays(5); // two days missed
            Assert.AreEqual(0, _mode.Streak, "a lapsed streak shows as none");
            _mode.Begin();
            _mode.OnWon(2, 3, false, false);
            Assert.AreEqual(1, _save.Data.DailyStreak);
            Assert.AreEqual(3, _save.Data.BestDailyStreak, "the best is kept");
        }

        [Test]
        public void TheDaily_NeverTouchesCampaignProgress()
        {
            _mode.Begin();
            _mode.OnWon(1, 1, false, false);
            Assert.AreEqual(1, _save.Data.HighestLevelUnlocked);
            Assert.AreEqual(0, _save.Data.StarsByLevel.Count);
        }

        [Test]
        public void TheDailyPuzzle_IsSolvable_ForManyDays()
        {
            var trajectory = new Trajectory();
            for (int day = 0; day < 20; day++)
            {
                _now = Day1.AddDays(day);
                LevelDefinition level = _mode.Begin();
                FlightSimulator.Simulate(level, level.SolutionWells, level.SolutionWells.Length, trajectory);
                Assert.AreEqual(FlightOutcome.Won, trajectory.Outcome, "day " + day);
            }
        }
    }
}
