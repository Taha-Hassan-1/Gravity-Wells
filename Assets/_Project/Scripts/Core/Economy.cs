namespace GravityWells.Core
{
    public enum PowerUpKind
    {
        Pin,    // lock one node in place
        Guide,  // show where a node could go
        Bridge, // let one line pass over another so that crossing stops counting
        Assist, // the existing "move the most tangled node for me" help
    }

    // Coin earning and spending rules in one place, so the numbers are easy to tune and test.
    public static class Economy
    {
        public const int LevelBaseCoins = 10;
        public const int CoinsPerStar = 5;
        public const int DailyMultiplier = 2;

        public const int TimeAttackCoinsPerLevel = 6;
        public const int TimeAttackNewBestBonus = 25;

        public const int EndlessCoinsPerPuzzle = 4;
        public const int EndlessNewBestBonus = 25;

        public const int PinCost = 15;
        public const int GuideCost = 25;
        public const int AssistCost = 30;
        public const int BridgeCost = 40;

        public static int Cost(PowerUpKind kind)
        {
            switch (kind)
            {
                case PowerUpKind.Pin: return PinCost;
                case PowerUpKind.Guide: return GuideCost;
                case PowerUpKind.Bridge: return BridgeCost;
                default: return AssistCost;
            }
        }

        public static string Name(PowerUpKind kind)
        {
            switch (kind)
            {
                case PowerUpKind.Pin: return "Pin";
                case PowerUpKind.Guide: return "Guide";
                case PowerUpKind.Bridge: return "Bridge";
                default: return "Assist";
            }
        }

        // A campaign level pays out in full the first time it is solved, and afterwards only for
        // each extra star it improves on — so replaying a solved level is never a coin farm.
        public static int LevelCoins(int previousBestStars, int stars)
        {
            if (stars <= previousBestStars)
            {
                return 0;
            }
            if (previousBestStars <= 0)
            {
                return LevelBaseCoins + CoinsPerStar * stars;
            }
            return CoinsPerStar * (stars - previousBestStars);
        }

        // Time Attack pays per level cleared, plus a flat bonus for beating the personal best. An
        // empty run earns nothing, so opening and abandoning it is not a coin source.
        public static int TimeAttackCoins(int levelsCleared, bool newBest)
        {
            if (levelsCleared <= 0)
            {
                return 0;
            }
            return levelsCleared * TimeAttackCoinsPerLevel + (newBest ? TimeAttackNewBestBonus : 0);
        }

        // Endless pays per puzzle solved, plus a flat bonus for a new personal best; a run that solved
        // nothing earns nothing.
        public static int EndlessCoins(int puzzlesSolved, bool newBest)
        {
            if (puzzlesSolved <= 0)
            {
                return 0;
            }
            return puzzlesSolved * EndlessCoinsPerPuzzle + (newBest ? EndlessNewBestBonus : 0);
        }

        // The daily pays double, once per day: replays of it are for fun.
        public static int DailyCoins(int stars, bool firstCompletionToday)
        {
            return firstCompletionToday ? (LevelBaseCoins + CoinsPerStar * stars) * DailyMultiplier : 0;
        }
    }
}
