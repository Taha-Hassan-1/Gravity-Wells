using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using GravityWells.Core;
using GravityWells.Meta;

namespace GravityWells.Tests
{
    public class EconomyAndQuestTests
    {
        private static readonly DateTime Day1 = new DateTime(2026, 5, 10, 9, 0, 0, DateTimeKind.Utc);

        private string _dir;
        private SaveService _save;
        private EconomyService _economy;
        private DateTime _now;
        private QuestService _quests;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "GravityWellsEconomyTests_" + Guid.NewGuid());
            Directory.CreateDirectory(_dir);
            _save = new SaveService(_dir);
            _economy = new EconomyService(_save);
            _now = Day1;
            _quests = new QuestService(_save, _economy, () => _now);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, recursive: true);
            }
        }

        // ---- pricing and rewards ----

        [Test]
        public void FirstSolve_PaysBasePlusStars_AndReplaysOnlyPayImprovements()
        {
            Assert.AreEqual(15, Economy.LevelCoins(0, 1));
            Assert.AreEqual(25, Economy.LevelCoins(0, 3));
            Assert.AreEqual(10, Economy.LevelCoins(1, 3), "two more stars, five each");
            Assert.AreEqual(0, Economy.LevelCoins(3, 3), "replaying a perfect level pays nothing");
            Assert.AreEqual(0, Economy.LevelCoins(2, 1), "a worse replay pays nothing");
        }

        [Test]
        public void Daily_PaysDoubleOncePerDay()
        {
            Assert.AreEqual(50, Economy.DailyCoins(3, firstCompletionToday: true));
            Assert.AreEqual(0, Economy.DailyCoins(3, firstCompletionToday: false));
        }

        [Test]
        public void AHint_CostsThirtyCoins()
        {
            Assert.AreEqual(30, Economy.HintCost);
        }

        // ---- the balance ----

        [Test]
        public void Earn_AddsAndPersists()
        {
            _economy.Earn(40);

            Assert.AreEqual(40, _economy.Coins);
            Assert.AreEqual(40, new SaveService(_dir).Data.Coins, "survives a restart");
        }

        [Test]
        public void Earn_IgnoresZeroAndNegative()
        {
            _economy.Earn(0);
            _economy.Earn(-5);

            Assert.AreEqual(0, _economy.Coins);
        }

        [Test]
        public void TrySpend_CannotOverdraw()
        {
            _economy.Earn(20);

            Assert.IsFalse(_economy.TrySpend(21));
            Assert.AreEqual(20, _economy.Coins, "a refused spend changes nothing");
            Assert.IsTrue(_economy.TrySpend(20));
            Assert.AreEqual(0, _economy.Coins);
            Assert.IsFalse(_economy.TrySpend(1));
        }

        [Test]
        public void ChangedEvent_ReportsTheNewBalance()
        {
            int seen = -1;
            _economy.Changed += coins => seen = coins;

            _economy.Earn(15);
            Assert.AreEqual(15, seen);
            _economy.TrySpend(5);
            Assert.AreEqual(10, seen);
        }

        [Test]
        public void OldSaveWithoutCoinFields_LoadsAsEmptyWallet()
        {
            File.WriteAllText(Path.Combine(_dir, "save.json"), "{\"Version\":1,\"HighestLevelUnlocked\":4}");

            SaveData data = new SaveService(_dir).Data;

            Assert.AreEqual(0, data.Coins);
            Assert.AreEqual(0, data.QuestDay);
            Assert.IsNotNull(data.QuestProgress);
        }

        // ---- quest board ----

        [Test]
        public void QuestBoard_IsDeterministicPerDay_AndDiffersAcrossDays()
        {
            QuestDef[] a = QuestBoard.ForDay(500);
            QuestDef[] b = QuestBoard.ForDay(500);
            CollectionAssert.AreEqual(a.Select(q => (q.Kind, q.Target, q.Reward)), b.Select(q => (q.Kind, q.Target, q.Reward)));

            bool anyDifferent = false;
            for (int day = 501; day < 540; day++)
            {
                if (!QuestBoard.ForDay(day).Select(q => q.Kind).SequenceEqual(a.Select(q => q.Kind)))
                {
                    anyDifferent = true;
                }
            }
            Assert.IsTrue(anyDifferent);
        }

        [Test]
        public void EveryDay_HasThreeDistinctSaneQuests()
        {
            for (int day = 1; day <= 800; day++)
            {
                QuestDef[] quests = QuestBoard.ForDay(day);
                Assert.AreEqual(QuestBoard.QuestsPerDay, quests.Length);
                Assert.AreEqual(quests.Length, quests.Select(q => q.Kind).Distinct().Count(), "day " + day);
                foreach (QuestDef quest in quests)
                {
                    Assert.Greater(quest.Target, 0);
                    Assert.Greater(quest.Reward, 0);
                    Assert.IsNotEmpty(quest.Describe());
                }
            }
        }

        [Test]
        public void ProgressFor_ReflectsWhatHappenedInTheLevel()
        {
            var brilliant = new LevelOutcome(3, true, 1, false, usedHint: false);
            var messy = new LevelOutcome(1, false, 6, false, usedHint: true);
            var daily = new LevelOutcome(2, false, 2, true, false);

            Assert.AreEqual(1, QuestBoard.ProgressFor(QuestKind.SolveLevels, messy));
            Assert.AreEqual(3, QuestBoard.ProgressFor(QuestKind.EarnStars, brilliant));
            Assert.AreEqual(1, QuestBoard.ProgressFor(QuestKind.EarnStars, messy));
            Assert.AreEqual(1, QuestBoard.ProgressFor(QuestKind.SolveUnderPar, brilliant));
            Assert.AreEqual(0, QuestBoard.ProgressFor(QuestKind.SolveUnderPar, messy));
            Assert.AreEqual(1, QuestBoard.ProgressFor(QuestKind.SolveFirstTry, brilliant));
            Assert.AreEqual(0, QuestBoard.ProgressFor(QuestKind.SolveFirstTry, messy));
            Assert.AreEqual(1, QuestBoard.ProgressFor(QuestKind.SolveWithoutHint, brilliant));
            Assert.AreEqual(0, QuestBoard.ProgressFor(QuestKind.SolveWithoutHint, messy));
            Assert.AreEqual(1, QuestBoard.ProgressFor(QuestKind.PlayDaily, daily));
            Assert.AreEqual(0, QuestBoard.ProgressFor(QuestKind.PlayDaily, brilliant));
        }

        [Test]
        public void DayNumber_AdvancesAtUtcMidnight()
        {
            int today = QuestBoard.DayNumber(new DateTime(2026, 5, 10, 23, 59, 59, DateTimeKind.Utc));
            int tomorrow = QuestBoard.DayNumber(new DateTime(2026, 5, 11, 0, 0, 1, DateTimeKind.Utc));

            Assert.AreEqual(today + 1, tomorrow);
        }

        // ---- quest service ----

        [Test]
        public void StartsTheDayWithThreeQuestsAtZero()
        {
            Assert.AreEqual(3, _quests.Today.Length);
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(0, _quests.Progress(i));
                Assert.IsFalse(_quests.IsDone(i));
            }
        }

        // One star, over par, four launches, a hint taken: advances only "solve N levels" (and stars, by one).
        private static readonly LevelOutcome SolveOnly = new LevelOutcome(1, false, 4, false, usedHint: true);

        [Test]
        public void SolvingLevels_PaysTheRewardExactlyWhenTheTargetIsReached()
        {
            int solveIndex = IndexOfOrSkip(QuestKind.SolveLevels);
            int target = _quests.Today[solveIndex].Target;
            int reward = _quests.Today[solveIndex].Reward;

            for (int i = 0; i < target - 1; i++)
            {
                Assert.IsFalse(_quests.OnLevelSolved(SolveOnly).Any);
            }
            Assert.AreEqual(0, _economy.Coins, "nothing is paid before the quest completes");

            QuestUpdate last = _quests.OnLevelSolved(SolveOnly);

            Assert.IsTrue(last.Any);
            Assert.AreEqual(reward, last.CoinsAwarded);
            Assert.AreEqual(reward, _economy.Coins);
            Assert.IsTrue(_quests.IsDone(solveIndex));
        }

        [Test]
        public void ACompletedQuest_PaysOnlyOnce_AndProgressStaysCapped()
        {
            int solveIndex = IndexOfOrSkip(QuestKind.SolveLevels);
            int target = _quests.Today[solveIndex].Target;
            for (int i = 0; i < target; i++)
            {
                _quests.OnLevelSolved(SolveOnly);
            }
            int coinsAtDone = _economy.Coins;

            QuestUpdate extra = _quests.OnLevelSolved(SolveOnly);

            Assert.AreEqual(target, _quests.Progress(solveIndex));
            Assert.IsFalse(extra.Any);
            Assert.AreEqual(0, extra.CoinsAwarded);
            Assert.AreEqual(coinsAtDone, _economy.Coins);
        }

        [Test]
        public void FinishingEveryQuest_PaysTheBonusExactlyOnce()
        {
            // Walk forward to a day that has no power-up quest, so one clean level pattern can finish all three.
            for (int step = 0; step < 1 && !BoardIsFinishableByLevels(_quests.Today); step++)
            {
                _now = _now.AddDays(1);
                _quests.EnsureToday();
            }
            Assert.IsTrue(BoardIsFinishableByLevels(_quests.Today));

            var clean = new LevelOutcome(3, true, 1, true, false);
            int bonusCount = 0;
            for (int i = 0; i < 60 && _quests.DoneCount < 3; i++)
            {
                QuestUpdate update = _quests.OnLevelSolved(clean);
                if (update.BonusAwarded)
                {
                    bonusCount++;
                }
            }

            Assert.AreEqual(3, _quests.DoneCount);
            Assert.AreEqual(1, bonusCount);
            Assert.IsTrue(_quests.BonusPaid);

            int coins = _economy.Coins;
            _quests.OnLevelSolved(clean);
            Assert.AreEqual(coins, _economy.Coins, "nothing more to earn once all three are done");
        }

        [Test]
        public void NewDay_ResetsProgressAndBonus()
        {
            _quests.OnLevelSolved(new LevelOutcome(3, true, 1, true, false));
            Assert.Greater(Enumerable.Range(0, 3).Sum(i => _quests.Progress(i)), 0);

            _now = _now.AddDays(1);
            _quests.EnsureToday();

            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(0, _quests.Progress(i));
            }
            Assert.IsFalse(_quests.BonusPaid);
        }

        [Test]
        public void SameDay_DoesNotResetProgress()
        {
            _quests.OnLevelSolved(new LevelOutcome(3, true, 1, true, false));
            int before = Enumerable.Range(0, 3).Sum(i => _quests.Progress(i));

            _quests.EnsureToday();

            Assert.AreEqual(before, Enumerable.Range(0, 3).Sum(i => _quests.Progress(i)));
        }

        [Test]
        public void ProgressSurvivesARestart_WithinTheSameDay()
        {
            _quests.OnLevelSolved(new LevelOutcome(3, true, 1, true, false));
            int before = Enumerable.Range(0, 3).Sum(i => _quests.Progress(i));

            var reloaded = new QuestService(new SaveService(_dir), new EconomyService(new SaveService(_dir)), () => _now);

            Assert.AreEqual(before, Enumerable.Range(0, 3).Sum(i => reloaded.Progress(i)));
        }

        [Test]
        public void Summary_NamesWhatHappened()
        {
            Assert.AreEqual(string.Empty, new QuestUpdate(0, 0, false).Summary());
            StringAssert.Contains("Quest complete", new QuestUpdate(30, 1, false).Summary());
            StringAssert.Contains("+30", new QuestUpdate(30, 1, false).Summary());
            StringAssert.Contains("2 quests", new QuestUpdate(60, 2, false).Summary());
            StringAssert.Contains("All daily quests", new QuestUpdate(90, 1, true).Summary());
        }

        // ---- helpers ----

        private int IndexOfOrSkip(QuestKind kind)
        {
            // Find a day whose board contains the quest, so the test doesn't depend on the calendar.
            for (int step = 0; step < 400; step++)
            {
                int index = Array.FindIndex(_quests.Today, q => q.Kind == kind);
                if (index >= 0)
                {
                    return index;
                }
                _now = _now.AddDays(1);
                _quests.EnsureToday();
            }
            Assert.Fail("no day within 400 has a " + kind);
            return -1;
        }

        private static bool BoardIsFinishableByLevels(QuestDef[] board)
        {
            return board.Length == QuestBoard.QuestsPerDay;
        }
    }
}
