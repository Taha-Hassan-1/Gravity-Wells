namespace GravityWells.Core
{
    public enum AdBlockReason
    {
        None,
        AdsRemoved,
        NoConsent,
        DragInProgress,
        LevelJustStarted,
        FirstSessionTooEarly,
        LevelTooLow,
        TooFewLevelsSinceLastAd,
        TooSoonSinceLastAd,
    }

    public readonly struct AdPolicyInput
    {
        public readonly int CurrentLevel;
        public readonly int LevelsSinceLastAd;
        public readonly float SecondsSinceLastAd;
        public readonly float SecondsSinceLevelStart;
        public readonly bool IsFirstSession;
        public readonly bool IsDragging;
        public readonly bool AdsRemoved;
        public readonly bool CanRequestAds;

        public AdPolicyInput(
            int currentLevel, int levelsSinceLastAd, float secondsSinceLastAd, float secondsSinceLevelStart,
            bool isFirstSession, bool isDragging, bool adsRemoved, bool canRequestAds)
        {
            CurrentLevel = currentLevel;
            LevelsSinceLastAd = levelsSinceLastAd;
            SecondsSinceLastAd = secondsSinceLastAd;
            SecondsSinceLevelStart = secondsSinceLevelStart;
            IsFirstSession = isFirstSession;
            IsDragging = isDragging;
            AdsRemoved = adsRemoved;
            CanRequestAds = canRequestAds;
        }
    }

    // Every interstitial rule lives here and nowhere else, so "may we show an ad right
    // now?" has exactly one answer and one place to change. Rewarded ads are opt-in from
    // the Assist button and deliberately never go through this: the player asked for them,
    // and removing ads must not take Assist away.
    public static class AdPolicy
    {
        // Returns the first rule that blocks an interstitial, or None when one is allowed.
        public static AdBlockReason EvaluateInterstitial(AdPolicyInput input)
        {
            if (input.AdsRemoved)
            {
                return AdBlockReason.AdsRemoved;
            }
            if (!input.CanRequestAds)
            {
                return AdBlockReason.NoConsent;
            }
            if (input.IsDragging)
            {
                return AdBlockReason.DragInProgress;
            }
            if (input.SecondsSinceLevelStart < GameConfig.InterstitialMinSecondsAfterLevelStart)
            {
                return AdBlockReason.LevelJustStarted;
            }
            if (input.IsFirstSession && input.CurrentLevel < GameConfig.FirstSessionNoAdsBeforeLevel)
            {
                return AdBlockReason.FirstSessionTooEarly;
            }
            if (input.CurrentLevel < GameConfig.InterstitialMinLevel)
            {
                return AdBlockReason.LevelTooLow;
            }
            if (input.LevelsSinceLastAd < GameConfig.InterstitialMinLevelsBetween)
            {
                return AdBlockReason.TooFewLevelsSinceLastAd;
            }
            if (input.SecondsSinceLastAd < GameConfig.InterstitialMinSecondsBetween)
            {
                return AdBlockReason.TooSoonSinceLastAd;
            }
            return AdBlockReason.None;
        }
    }
}
