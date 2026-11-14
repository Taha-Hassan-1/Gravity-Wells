namespace GravityWells.Core
{
    // Every designer-tunable value lives here as a named constant (no Inspector). Simulation and
    // difficulty constants are added with the phases that use them.
    public static class GameConfig
    {
        // --- Meta ---
        public const float ThemeCrossfadeDuration = 0.3f;

        // --- Ads & monetization ---
        public const int InterstitialMinLevelsBetween = 3;
        public const float InterstitialMinSecondsBetween = 45f;
        public const int InterstitialMinLevel = 5;
        public const float InterstitialMinSecondsAfterLevelStart = 3f;
        public const int FirstSessionNoAdsBeforeLevel = 3;
        public const string RemoveAdsProductId = "remove_ads";
        public const string RemoveAdsFallbackPrice = "$2.99"; // shown only until the store reports the real localized price

        // Ad networks to use, highest priority first (comma separated, names as registered in
        // Bootstrap). The stub is deliberately absent from the device order: it always "completes",
        // so it would hand out rewards for free if it ever served in a release build.
        public const string AdProviderOrder = "admob";
        public const string EditorAdProviderOrder = "stub";

        // --- AdMob ---
        // Google's public TEST app/ad-unit ids: they always fill with a clearly marked test ad and can
        // never earn (or violate policy). Replace all three with the real ids from the AdMob console
        // before a public release; BuildScript warns while any of them is still a test id.
        public const string AdMobAndroidAppId = "ca-app-pub-3940256099942544~3347511713";
        public const string AdMobInterstitialUnitId = "ca-app-pub-3940256099942544/1033173712";
        public const string AdMobRewardedUnitId = "ca-app-pub-3940256099942544/5224354917";
        private const string AdMobTestPublisherPrefix = "ca-app-pub-3940256099942544";

        public static bool IsAdMobTestId(string id)
        {
            return !string.IsNullOrEmpty(id) && id.StartsWith(AdMobTestPublisherPrefix, System.StringComparison.Ordinal);
        }
    }
}
