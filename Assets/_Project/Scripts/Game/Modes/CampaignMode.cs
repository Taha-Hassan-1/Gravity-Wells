using GravityWells.Core;
using GravityWells.Meta;

namespace GravityWells.Game
{
    // The campaign: level after level, each generated from its own number. Where the player stopped is
    // kept by ProgressionService.
    public sealed class CampaignMode : IGameMode
    {
        private readonly ProgressionService _progression;
        private LevelDefinition _current;

        public CampaignMode(ProgressionService progression)
        {
            _progression = progression;
        }

        public string Key => "campaign";

        public int LevelNumber { get; private set; }

        public LevelDefinition Begin() => Load(_progression.CurrentLevel);

        // Jumps to a particular level (replaying an earlier one, or the first run of the smoke tests).
        public LevelDefinition Begin(int level) => Load(level);

        public LevelDefinition Replay() => Load(LevelNumber);

        public LevelDefinition Next() => Load(LevelNumber + 1);

        public LevelResult OnWon(int wellsUsed, int launches, bool usedExtraWell)
        {
            int stars = Scoring.StarsFor(wellsUsed, _current.Par, usedExtraWell);
            bool underPar = Scoring.IsUnderPar(wellsUsed, _current.Par) && !usedExtraWell;
            bool newBest = _progression.RecordWin(LevelNumber, stars, underPar, launches, wellsUsed);
            return new LevelResult(LevelNumber, stars, wellsUsed, _current.Par, launches, underPar, newBest, usedExtraWell);
        }

        private LevelDefinition Load(int level)
        {
            LevelNumber = System.Math.Max(1, level);
            _current = LevelGenerator.Generate(DifficultyCurve.SeedForLevel(LevelNumber), LevelNumber);
            return _current;
        }
    }
}
