namespace GravityWells.Services
{
    public readonly struct AnalyticsParam
    {
        public readonly string Key;
        public readonly object Value;

        public AnalyticsParam(string key, object value)
        {
            Key = key;
            Value = value;
        }
    }

    // Swappable backend: gameplay logs named events with parameters and never knows
    // whether they go to a console, Firebase, Unity Analytics, or nowhere.
    public interface IAnalyticsService
    {
        void LogEvent(string eventName, params AnalyticsParam[] parameters);
    }

    // The event vocabulary, in one place so names can't drift between call sites.
    public static class AnalyticsEvents
    {
        public const string LevelStart = "level_start";
        public const string LevelComplete = "level_complete";
        public const string LevelAbandon = "level_abandon";
        public const string HintUsed = "hint_used";
        public const string AssistUsed = "assist_used";
        public const string AdShown = "ad_shown";
        public const string AdFailed = "ad_failed";
        public const string ThemeUnlocked = "theme_unlocked";
        public const string DailyStarted = "daily_started";
        public const string DailyCompleted = "daily_completed";
        public const string IapPurchased = "iap_purchased";
        public const string StreakFreezeEarned = "streak_freeze_earned";
        public const string StreakFreezeUsed = "streak_freeze_used";
        public const string PowerUpUsed = "power_up_used";
        public const string QuestCompleted = "quest_completed";
        public const string CoinsDoubled = "coins_doubled";
        public const string HowToPlayOpened = "how_to_play_opened";
        public const string ModeStarted = "mode_started";
        public const string ModeFinished = "mode_finished";
        public const string PuzzleShared = "puzzle_shared";
        public const string ModeContinued = "mode_continued";
        public const string SessionStart = "session_start";
        public const string AppException = "app_exception";
    }
}
