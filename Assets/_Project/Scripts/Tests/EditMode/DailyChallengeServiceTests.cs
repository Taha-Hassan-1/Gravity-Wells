using System;
using NUnit.Framework;
using GravityWells.Core;
using GravityWells.Meta;

namespace GravityWells.Tests
{
    public class DailyChallengeServiceTests
    {
        [Test]
        public void SeedForDate_IsDeterministicForTheSameDate()
        {
            var date = new DateTime(2026, 3, 5);
            Assert.AreEqual(DailyChallengeService.SeedForDate(date), DailyChallengeService.SeedForDate(date));
        }

        [Test]
        public void SeedForDate_MatchesExpectedFormat()
        {
            var date = new DateTime(2026, 3, 5);
            Assert.AreEqual(20260305, DailyChallengeService.SeedForDate(date));
        }

        [Test]
        public void HasPlayedToday_MatchesLastDailyDate()
        {
            var data = new SaveData { LastDailyDateUtc = "2026-03-05" };
            Assert.IsTrue(DailyChallengeService.HasPlayedToday(data, new DateTime(2026, 3, 5)));
            Assert.IsFalse(DailyChallengeService.HasPlayedToday(data, new DateTime(2026, 3, 6)));
        }

        [Test]
        public void RecordCompletion_OnConsecutiveDay_IncrementsStreak()
        {
            var data = new SaveData { LastDailyDateUtc = "2026-03-04", DailyStreak = 5 };
            DailyChallengeService.RecordCompletion(data, new DateTime(2026, 3, 5));

            Assert.AreEqual(6, data.DailyStreak);
            Assert.AreEqual("2026-03-05", data.LastDailyDateUtc);
        }

        [Test]
        public void RecordCompletion_AfterAGap_ResetsStreakToOne()
        {
            var data = new SaveData { LastDailyDateUtc = "2026-03-01", DailyStreak = 10 };
            DailyChallengeService.RecordCompletion(data, new DateTime(2026, 3, 5));

            Assert.AreEqual(1, data.DailyStreak);
        }

        [Test]
        public void RecordCompletion_TracksBestStreakAcrossAReset()
        {
            var data = new SaveData { LastDailyDateUtc = "2026-03-04", DailyStreak = 5, BestDailyStreak = 5 };
            DailyChallengeService.RecordCompletion(data, new DateTime(2026, 3, 5)); // streak 6
            DailyChallengeService.RecordCompletion(data, new DateTime(2026, 3, 9)); // gap -> streak 1

            Assert.AreEqual(1, data.DailyStreak);
            Assert.AreEqual(6, data.BestDailyStreak);
        }

        [Test]
        public void CompletedDays_MarksTheCurrentStreakRun()
        {
            var data = new SaveData { LastDailyDateUtc = "2026-03-05", DailyStreak = 3 };

            bool[] days = DailyChallengeService.CompletedDays(data, new DateTime(2026, 3, 5), 7);

            // 7 days ending today (Mar 5): Feb 27 .. Mar 5; the streak covers Mar 3, 4, 5.
            Assert.AreEqual(new[] { false, false, false, false, true, true, true }, days);
        }

        [Test]
        public void CompletedDays_StreakEndingYesterday_StillShowsButNotToday()
        {
            var data = new SaveData { LastDailyDateUtc = "2026-03-04", DailyStreak = 2 };

            bool[] days = DailyChallengeService.CompletedDays(data, new DateTime(2026, 3, 5), 7);

            Assert.AreEqual(new[] { false, false, false, false, true, true, false }, days);
        }

        [Test]
        public void CompletedDays_LapsedStreak_ShowsNothing()
        {
            var data = new SaveData { LastDailyDateUtc = "2026-03-01", DailyStreak = 4 };

            bool[] days = DailyChallengeService.CompletedDays(data, new DateTime(2026, 3, 5), 7);

            CollectionAssert.AreEqual(new bool[7], days);
        }

        [Test]
        public void CompletedDays_NeverPlayed_ShowsNothing()
        {
            bool[] days = DailyChallengeService.CompletedDays(new SaveData(), new DateTime(2026, 3, 5), 7);

            CollectionAssert.AreEqual(new bool[7], days);
        }

        [Test]
        public void RecordCompletion_CalledTwiceSameDay_DoesNotDoubleIncrement()
        {
            var data = new SaveData { LastDailyDateUtc = "2026-03-04", DailyStreak = 5 };
            DailyChallengeService.RecordCompletion(data, new DateTime(2026, 3, 5));
            DailyChallengeService.RecordCompletion(data, new DateTime(2026, 3, 5));

            Assert.AreEqual(6, data.DailyStreak);
        }
    }
}
