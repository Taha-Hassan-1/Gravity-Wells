using System;
using GravityWells.Core;
using GravityWells.Meta;

namespace GravityWells.Game
{
    // The daily challenge: one puzzle a day, the same for every player, seeded from the UTC date and built at
    // the difficulty of level 40. It pays double and counts toward the streak once a day; playing it again is
    // for fun and pays nothing. The date is read once, when the mode begins, so a puzzle begun before midnight is
    // finished as the day it began.
    public sealed class DailyMode : IGameMode
    {
        private readonly SaveService _save;
        private readonly RewardService _rewards;
        private readonly Func<DateTime> _utcNow;
        private LevelDefinition _current;
        private DateTime _day;

        public DailyMode(SaveService save, RewardService rewards, Func<DateTime> utcNow)
        {
            _save = save;
            _rewards = rewards;
            _utcNow = utcNow;
        }

        public string Key => "daily";

        public int LevelNumber => GameConfig.DailyLevel;

        // Whether today's puzzle has already been completed (the menu shows it as done).
        public bool CompletedToday => DailyChallengeService.HasPlayedToday(_save.Data, _utcNow());

        public int Streak => DailyChallengeService.EffectiveStreak(_save.Data, _utcNow());

        public LevelDefinition Begin()
        {
            _day = _utcNow().Date;
            _current = LevelGenerator.Generate(DailyChallengeService.SeedForDate(_day), GameConfig.DailyLevel);
            return _current;
        }

        public LevelDefinition Replay() => _current ?? Begin();

        // The daily has no next puzzle; asking for one gives the same puzzle again.
        public LevelDefinition Next() => Replay();

        public LevelResult OnWon(int wellsUsed, int launches, bool usedExtraWell, bool usedHint)
        {
            int stars = Scoring.StarsFor(wellsUsed, _current.Par, usedExtraWell);
            bool underPar = Scoring.IsUnderPar(wellsUsed, _current.Par) && !usedExtraWell;

            DateTime now = _utcNow();
            bool first = !DailyChallengeService.HasPlayedToday(_save.Data, now);
            if (first)
            {
                DailyChallengeService.RecordCompletion(_save.Data, now);
                _save.Data.TotalLaunches += Math.Max(0, launches);
                _save.Data.TotalWellsPlaced += Math.Max(0, wellsUsed);
                _save.Data.UnderParCount += underPar ? 1 : 0;
                _save.Save();
            }

            RewardSummary reward = _rewards.Award(0, stars, underPar, launches, usedHint, isDaily: true, firstDailyCompletionToday: first);
            return new LevelResult(
                GameConfig.DailyLevel, stars, wellsUsed, _current.Par, launches, underPar, newBest: first, usedExtraWell,
                reward.Coins, reward.QuestText, isDaily: true, streak: Streak);
        }
    }
}
