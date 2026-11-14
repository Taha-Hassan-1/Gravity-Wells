using GravityWells.Core;
using GravityWells.Meta;

namespace GravityWells.Game
{
    // The campaign: level after level, each generated from its own number. Where the player stopped is
    // kept by ProgressionService, and what a win pays by RewardService.
    public sealed class CampaignMode : IGameMode
    {
        private readonly ProgressionService _progression;
        private readonly RewardService _rewards;
        private LevelDefinition _current;

        public CampaignMode(ProgressionService progression, RewardService rewards)
        {
            _progression = progression;
            _rewards = rewards;
        }

        public string Key => "campaign";

        public int LevelNumber { get; private set; }

        public LevelDefinition Begin() => Load(_progression.CurrentLevel);

        // Jumps to a particular level (replaying an earlier one, or the first run of the smoke tests).
        public LevelDefinition Begin(int level) => Load(level);

        public LevelDefinition Replay() => Load(LevelNumber);

        public LevelDefinition Next() => Load(LevelNumber + 1);

        public LevelResult OnWon(int wellsUsed, int launches, bool usedExtraWell, bool usedHint)
        {
            int stars = Scoring.StarsFor(wellsUsed, _current.Par, usedExtraWell);
            bool underPar = Scoring.IsUnderPar(wellsUsed, _current.Par) && !usedExtraWell;

            // Coins depend on the best the player had before this win, so read it before it is recorded.
            int previousBest = _progression.StarsFor(LevelNumber);
            bool newBest = _progression.RecordWin(LevelNumber, stars, underPar, launches, wellsUsed);
            RewardSummary reward = _rewards.Award(
                previousBest, stars, underPar, launches, usedHint, isDaily: false, firstDailyCompletionToday: false);

            return new LevelResult(
                LevelNumber, stars, wellsUsed, _current.Par, launches, underPar, newBest, usedExtraWell,
                reward.Coins, reward.QuestText);
        }

        private LevelDefinition Load(int level)
        {
            LevelNumber = System.Math.Max(1, level);
            _current = LevelGenerator.Generate(DifficultyCurve.SeedForLevel(LevelNumber), LevelNumber);
            return _current;
        }
    }
}
