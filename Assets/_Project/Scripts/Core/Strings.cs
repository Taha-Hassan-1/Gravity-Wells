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
