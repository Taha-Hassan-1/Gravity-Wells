using System;
using GravityWells.Core;

namespace GravityWells.Meta
{
    // Which level the player is on and what they have earned on the ones behind them. A thin layer over
    // SaveData; the screens in Phase 4 add themes and unlocks on top of it.
    public sealed class ProgressionService
    {
        private readonly SaveService _save;

        public ProgressionService(SaveService save)
        {
            _save = save;
        }

        private SaveData Data => _save.Data;

        // The level the campaign will play next.
        public int CurrentLevel => Math.Max(1, Data.CurrentLevel);

        public int HighestLevelUnlocked => Math.Max(1, Data.HighestLevelUnlocked);

        // Best stars on a level, 0 if it has never been won.
        public int StarsFor(int level)
        {
            int index = level - 1;
            return index >= 0 && index < Data.StarsByLevel.Count ? Data.StarsByLevel[index] : 0;
        }

        public int TotalStars
        {
            get
            {
                int total = 0;
                for (int i = 0; i < Data.StarsByLevel.Count; i++)
                {
                    total += Data.StarsByLevel[i];
                }
                return total;
            }
        }

        // Records a win. Stars only ever go up, so replaying for fun never costs the player anything.
        // Returns true if this was a new best.
        public bool RecordWin(int level, int stars, bool underPar, int launches, int wellsPlaced)
        {
            if (level < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(level));
            }

            stars = Math.Max(0, Math.Min(Scoring.MaxStars, stars));
            while (Data.StarsByLevel.Count < level)
            {
                Data.StarsByLevel.Add(0);
            }

            bool better = stars > Data.StarsByLevel[level - 1];
            if (better)
            {
                Data.StarsByLevel[level - 1] = (byte)stars;
            }

            Data.HighestLevelUnlocked = Math.Max(Data.HighestLevelUnlocked, level + 1);
            Data.CurrentLevel = Math.Max(Data.CurrentLevel, level + 1);
            Data.UnderParCount += underPar ? 1 : 0;
            Data.TotalLaunches += Math.Max(0, launches);
            Data.TotalWellsPlaced += Math.Max(0, wellsPlaced);
            _save.Save();
            return better;
        }

        // Where the player chose to play from (replaying an earlier level). Cannot jump ahead of what is unlocked.
        public void SetCurrentLevel(int level)
        {
            Data.CurrentLevel = Math.Max(1, Math.Min(level, HighestLevelUnlocked));
            _save.Save();
        }
    }
}
