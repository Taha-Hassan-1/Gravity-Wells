namespace GravityWells.Core
{
    // Coin earning and spending rules in one place, so the numbers are easy to tune and test.
    public static class Economy
    {
        public const int LevelBaseCoins = 10;
        public const int CoinsPerStar = 5;
        public const int DailyMultiplier = 2;

        // A hint costs this many coins; when the player is short they may watch a rewarded ad instead.
        public const int HintCost = 30;

        // A campaign level pays out in full the first time it is solved, and afterwards only for
        // each extra star it improves on, so replaying a solved level is never a coin farm.
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

        // The daily pays double, once per day: replays of it are for fun.
        public static int DailyCoins(int stars, bool firstCompletionToday)
        {
            return firstCompletionToday ? (LevelBaseCoins + CoinsPerStar * stars) * DailyMultiplier : 0;
        }
    }
}
