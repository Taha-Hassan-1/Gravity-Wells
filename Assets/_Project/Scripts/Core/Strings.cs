namespace GravityWells.Core
{
    // Every word the player reads, in one place, so a translation is a table swap and wording can be reviewed on one page.
    // Fixed text is a constant; text with a number in it is a method, because the word order around a number differs between
    // languages. Names of things the code needs (GameObject names, icon names, analytics events) do not belong here.
    // Only the sections the shared code needs exist so far; each phase adds the text of the screens it builds.
    public static class Strings
    {
        public static class Common
        {
            public const string Ok = "OK";
            public const string Cancel = "Cancel";
        }

        public static class Menu
        {
            public const string Title = "Gravity Wells";
            public const string Tagline = "Bend the path.";
            public const string Play = "Play";
            public const string DailyTitle = "Daily challenge";
            public const string DailyDone = "Done today";
            public const string DailyReady = "A new puzzle every day";
            public const string Themes = "Themes";
            public const string Stats = "Stats";
            public const string Settings = "Settings";
            public const string QuestsTitle = "Today's quests";
            public const string PressBackAgain = "Press back again to exit";

            public static string PlayLevel(int level) => "Level " + level;
            public static string Streak(int days) => days == 1 ? "1 day streak" : days + " day streak";
            public static string QuestProgress(int progress, int target) => progress + "/" + target;
        }

        public static class Settings
        {
            public const string Title = "Settings";
            public const string Sound = "Sound";
            public const string Music = "Music";
            public const string Vibrations = "Vibrations";
            public const string RemoveAds = "Remove ads";
            public const string AdsRemoved = "Ads removed";
            public const string RestorePurchase = "Restore purchase";
            public const string PrivacyPolicy = "Privacy policy";
            public const string PrivacyPolicyNotPublished = "The privacy policy will be published with the release.";
            public const string PrivacyOptions = "Privacy options";
            public const string RestoreDone = "Purchases restored";
            public const string RestoreNothing = "No purchase to restore";
            public const string PurchaseUnavailable = "Purchases are not available yet.";

            public static string VibrationsValue(bool on, bool strong) => !on ? "Off" : strong ? "Strong" : "On";
            public static string Version(string version) => "Version " + version;
        }

        public static class Stats
        {
            public const string Title = "Stats";
            public const string LevelsSolved = "Levels solved";
            public const string StarsEarned = "Stars earned";
            public const string UnderPar = "Under par";
            public const string Launches = "Launches";
            public const string WellsPlaced = "Wells placed";
            public const string BestStreak = "Best daily streak";
            public const string TimePlayed = "Time played";

            public static string DurationSeconds(int s) => s + "s";
            public static string DurationMinutes(int m, int s) => m + "m " + s.ToString("00") + "s";
            public static string DurationHours(int h, int m) => h + "h " + m.ToString("00") + "m";
        }

        public static class Themes
        {
            public const string Title = "Themes";
            public const string Locked = "Locked";
            public const string Selected = "In use";

            public static string UnlocksAt(int level) => "Unlocks at level " + level;

            // The short form for the list row, where space is tight: the lock icon beside it says the rest.
            public static string RowLocked(int level) => "Level " + level;
        }

        public static class Game
        {
            public const string NoWellsLeft = "No wells left. Take one back first.";
            public const string NoBetterMove = "No better move found from here.";
            public const string HintPaid = "Hint";
            public const string HintNeedsAd = "Watch the whole ad to get the hint";
            public const string ExtraWellNeedsAd = "Watch the whole ad to get the extra well";
            public const string DoubleCoinsNeedsAd = "Watch the whole ad to double your coins";
            public static string DoubleCoins(int coins) => "Double coins  +" + coins;
            public const string ExtraWell = "Extra well";
            public const string Menu = "Menu";
            public const string Paused = "Paused";
            public const string Resume = "Resume";
            public const string RestartLevel = "Restart level";
            public const string NextLevel = "Next level";
            public const string Replay = "Replay";
            public const string UnderParBadge = "Under par!";
            public const string ExtraWellBadge = "Extra well used";
            public const string NewBestBadge = "New best";
            public const string FirstLaunch = "First launch";
            public const string DailyDone = "Daily complete";

            public static string Level(int level) => "Level " + level;
            public static string Coins(int coins) => "+" + coins + " coins";
            public static string WellsAgainstPar(int used, int par) => "Wells " + used + "  (par " + par + ")";
            public static string Launches(int n) => n == 1 ? FirstLaunch : n + " launches";
            public static string Streak(int days) => days == 1 ? "1 day streak" : days + " day streak";
        }

        public static class Ads
        {
            public const string LoadingTitle = "Loading ad";
            public const string OpeningTitle = "Opening ad";
            public const string LoadingBody = "This can take a few seconds.";
            public const string SkippedBody = "Watch the whole ad to get the reward.";
            public const string BlockedTitle = "Ads are blocked";
            public const string BlockedBody = "An ad blocker, Private DNS or VPN on this device is blocking ads. Turn it off for a moment, then try again.";
            public const string OfflineTitle = "No internet";
            public const string OfflineBody = "Connect to the internet to watch an ad, then try again.";
            public const string NotReadyTitle = "Ads couldn't start";
            public const string NotReadyBody = "Check your internet connection and that ads are allowed in Settings > Privacy options, then try again.";
            public const string NoAdTitle = "No ad right now";
            public const string NoAdBody = "There isn't an ad to show at the moment. Please try again in a little while.";
            public const string TimedOutBody = "The ad took too long to load. Please try again.";
        }
    }
}
