using GravityWells.Core;
using GravityWells.Services;

namespace GravityWells.Game
{
    // The words the player sees when an ad they asked for can't be shown. Kept apart from the
    // UI so each reason is covered by a test and none can regress to a vague "no ad".
    public static class AdMessages
    {
        public const string LoadingTitle = Strings.Ads.LoadingTitle;
        public const string OpeningTitle = Strings.Ads.OpeningTitle;
        public const string SkippedBody = Strings.Ads.SkippedBody;

        public static void ForFailure(AdFailure failure, out string title, out string body)
        {
            switch (failure)
            {
                case AdFailure.Blocked:
                    title = Strings.Ads.BlockedTitle;
                    body = Strings.Ads.BlockedBody;
                    break;
                case AdFailure.Offline:
                    title = Strings.Ads.OfflineTitle;
                    body = Strings.Ads.OfflineBody;
                    break;
                case AdFailure.NoConsent:
                    title = Strings.Ads.NotReadyTitle;
                    body = Strings.Ads.NotReadyBody;
                    break;
                default:
                    title = Strings.Ads.NoAdTitle;
                    body = Strings.Ads.NoAdBody;
                    break;
            }
        }

        public static string TimedOutBody => Strings.Ads.TimedOutBody;
    }
}
