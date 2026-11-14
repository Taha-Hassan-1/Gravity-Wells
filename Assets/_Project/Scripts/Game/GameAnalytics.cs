using System;
using GravityWells.Services;

namespace GravityWells.Game
{
    // Turns what happens in a level into named analytics events, so GameManager says "a level started" and never
    // builds parameter lists. It owns the per-level counters an event needs (when the level began, how many gates
    // the best launch lit) and nothing else; the sinks behind IAnalyticsService decide where events go.
    public sealed class GameAnalytics
    {
        private readonly IAnalyticsService _analytics;
        private readonly Func<float> _now;
        private float _levelStart;
        private int _bestGates;
        private bool _active;

        public GameAnalytics(IAnalyticsService analytics, Func<float> now)
        {
            _analytics = analytics;
            _now = now;
        }

        public bool LevelActive => _active;

        // Seconds on the current level, for the ad pacing and for the events.
        public float SecondsOnLevel => _active ? _now() - _levelStart : 0f;

        public void LevelStarted(string mode, int level, int wells)
        {
            // Starting a level while another is open means the first was left without a win.
            _levelStart = _now();
            _bestGates = 0;
            _active = true;
            _analytics.LogEvent(
                AnalyticsEvents.LevelStart,
                new AnalyticsParam("mode", mode), new AnalyticsParam("level", level), new AnalyticsParam("wells", wells));
        }

        public void Launched(int level, int wells, int gatesLit, bool won)
        {
            if (gatesLit > _bestGates)
            {
                _bestGates = gatesLit;
            }
            _analytics.LogEvent(
                AnalyticsEvents.Launch,
                new AnalyticsParam("level", level), new AnalyticsParam("wells", wells),
                new AnalyticsParam("gates_lit", gatesLit), new AnalyticsParam("outcome", won ? "won" : "failed"));
        }

        public void LevelCompleted(string mode, int level, int wells, int par, int launches, int stars)
        {
            _active = false;
            _analytics.LogEvent(
                AnalyticsEvents.LevelComplete,
                new AnalyticsParam("mode", mode), new AnalyticsParam("level", level), new AnalyticsParam("wells", wells),
                new AnalyticsParam("par", par), new AnalyticsParam("launches", launches),
                new AnalyticsParam("seconds", (int)SecondsOnLevelAtEnd()), new AnalyticsParam("stars", stars));
        }

        // Called when the player leaves (menu, replay) with the level unsolved; a no-op when it was already won.
        public void LevelAbandoned(int level, int launches)
        {
            if (!_active)
            {
                return;
            }
            _active = false;
            _analytics.LogEvent(
                AnalyticsEvents.LevelAbandon,
                new AnalyticsParam("level", level), new AnalyticsParam("launches", launches),
                new AnalyticsParam("seconds", (int)(_now() - _levelStart)), new AnalyticsParam("best_gates_lit", _bestGates));
        }

        public void HintUsed(int level, bool paidWithAd) =>
            _analytics.LogEvent(AnalyticsEvents.HintUsed, new AnalyticsParam("level", level), new AnalyticsParam("paid_with", paidWithAd ? "ad" : "coins"));

        public void ExtraWellUsed(int level) =>
            _analytics.LogEvent(AnalyticsEvents.ExtraWellUsed, new AnalyticsParam("level", level));

        public void CoinsDoubled(int coins) =>
            _analytics.LogEvent(AnalyticsEvents.CoinsDoubled, new AnalyticsParam("coins", coins));

        public void DailyStarted(int level) =>
            _analytics.LogEvent(AnalyticsEvents.DailyStarted, new AnalyticsParam("level", level));

        public void DailyCompleted(int level, int streak) =>
            _analytics.LogEvent(AnalyticsEvents.DailyCompleted, new AnalyticsParam("level", level), new AnalyticsParam("streak", streak));

        // The clock stops at the win, not when the player taps through the result card.
        private float _endTime;

        public void StopClock() => _endTime = _now();

        private float SecondsOnLevelAtEnd() => _endTime > _levelStart ? _endTime - _levelStart : _now() - _levelStart;
    }
}
