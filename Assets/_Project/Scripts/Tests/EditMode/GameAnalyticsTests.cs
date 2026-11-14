using System.Collections.Generic;
using NUnit.Framework;
using GravityWells.Game;
using GravityWells.Services;

namespace GravityWells.Tests
{
    public class GameAnalyticsTests
    {
        private sealed class Recorder : IAnalyticsService
        {
            public readonly List<string> Names = new List<string>();
            public readonly List<AnalyticsParam[]> Params = new List<AnalyticsParam[]>();

            public void LogEvent(string eventName, params AnalyticsParam[] parameters)
            {
                Names.Add(eventName);
                Params.Add(parameters);
            }
        }

        private static object Get(AnalyticsParam[] parameters, string key)
        {
            foreach (AnalyticsParam p in parameters)
            {
                if (p.Key == key)
                {
                    return p.Value;
                }
            }
            return null;
        }

        [Test]
        public void StartThenWin_LogsStartLaunchAndComplete_AndNoAbandon()
        {
            float clock = 0f;
            var rec = new Recorder();
            var analytics = new GameAnalytics(rec, () => clock);

            analytics.LevelStarted("campaign", 7, 3);
            clock = 12f;
            analytics.Launched(7, 2, 2, won: false);
            clock = 20f;
            analytics.Launched(7, 3, 4, won: true);
            analytics.StopClock();
            clock = 40f; // the player lingers on the result card
            analytics.LevelCompleted("campaign", 7, 3, 3, 2, 3);
            analytics.LevelAbandoned(7, 2); // leaving after a win must not report an abandon

            CollectionAssert.AreEqual(
                new[] { "level_start", "launch", "launch", "level_complete" }, rec.Names);
            Assert.AreEqual(20, Get(rec.Params[3], "seconds"), "the clock stops at the win");
        }

        [Test]
        public void LeavingAnUnsolvedLevel_LogsAbandonOnceWithBestGates()
        {
            float clock = 0f;
            var rec = new Recorder();
            var analytics = new GameAnalytics(rec, () => clock);

            analytics.LevelStarted("campaign", 9, 3);
            analytics.Launched(9, 2, 3, won: false);
            analytics.Launched(9, 2, 1, won: false);
            clock = 30f;
            analytics.LevelAbandoned(9, 2);
            analytics.LevelAbandoned(9, 2);

            Assert.AreEqual(1, rec.Names.FindAll(n => n == "level_abandon").Count);
            AnalyticsParam[] abandon = rec.Params[rec.Names.IndexOf("level_abandon")];
            Assert.AreEqual(3, Get(abandon, "best_gates_lit"));
            Assert.AreEqual(30, Get(abandon, "seconds"));
        }

        [Test]
        public void SecondsOnLevel_IsZeroWhenNoLevelIsOpen()
        {
            var analytics = new GameAnalytics(new Recorder(), () => 99f);
            Assert.AreEqual(0f, analytics.SecondsOnLevel);
        }
    }
}
