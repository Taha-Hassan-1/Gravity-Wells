using GravityWells.Core;

namespace GravityWells.Meta
{
    // What finishing a level paid, for the result card.
    public readonly struct RewardSummary
    {
        public readonly int Coins;          // in all: the level's own coins plus any quest and bonus coins
        public readonly string QuestText;   // "Quest complete! +30 coins", or empty

        public RewardSummary(int coins, string questText)
        {
            Coins = coins;
            QuestText = questText ?? string.Empty;
        }
    }

    // Pays for a win: the level's coins (first solve in full, then only for stars gained; the daily double,
    // once a day) and whatever daily quests the win completes. One place, so the campaign and the daily cannot
    // pay by different rules.
    public sealed class RewardService
    {
        private readonly EconomyService _economy;
        private readonly QuestService _quests;

        public RewardService(EconomyService economy, QuestService quests)
        {
            _economy = economy;
            _quests = quests;
        }

        public EconomyService Economy => _economy;
        public QuestService Quests => _quests;

        public RewardSummary Award(
            int previousBestStars, int stars, bool underPar, int launches, bool usedHint, bool isDaily, bool firstDailyCompletionToday)
        {
            int levelCoins = isDaily
                ? Core.Economy.DailyCoins(stars, firstDailyCompletionToday)
                : Core.Economy.LevelCoins(previousBestStars, stars);
            _economy.Earn(levelCoins);

            // A replayed daily is for fun and moves no quest either: the day's completion counts once.
            bool countsForQuests = !isDaily || firstDailyCompletionToday;
            QuestUpdate quest = countsForQuests
                ? _quests.OnLevelSolved(new LevelOutcome(stars, underPar, launches, isDaily, usedHint))
                : new QuestUpdate(0, 0, false);

            return new RewardSummary(levelCoins + quest.CoinsAwarded, quest.Summary());
        }
    }
}
