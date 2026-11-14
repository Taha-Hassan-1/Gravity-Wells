using System;

namespace GravityWells.Services
{
    public enum AdOutcome
    {
        Completed, // interstitial shown and closed, or rewarded ad watched to the reward
        Skipped,   // rewarded ad shown but closed before the reward
        NotReady,  // nothing was loaded to show
        Failed,    // the SDK reported an error while loading/showing
    }

    public enum AdLoadState
    {
        Idle,    // nothing requested yet
        Loading, // a request is in flight; worth waiting for
        Ready,   // an ad can be shown right now
        Failed,  // the last request failed; see the AdFailure
    }

    // Why an ad isn't available, so the player gets a useful message instead of a shrug.
    public enum AdFailure
    {
        None,
        Blocked,   // the ad servers are unreachable by name: an ad-blocking DNS, VPN or hosts file
        Offline,   // no internet at all
        NoConsent, // the player has not agreed to ads
        NoFill,    // the network had no ad to give
        Other,
    }

    // The one seam between gameplay and whatever ad SDK ships. Gameplay never asks
    // *whether it may* show an ad (that's AdPolicy) — only to show one and report back.
    // Each ad network is one adapter implementing this; WaterfallAdService combines several.
    public interface IAdService
    {
        // Short lowercase id used in config and analytics, e.g. "admob".
        string Name { get; }

        // Called once consent allows ad requests; a no-op for the stub. Must be safe to call
        // more than once, because a provider switched on at runtime is initialized on activation.
        void Initialize();

        // Called a few times a second so a provider can keep its next ads loaded ahead of time.
        void Tick();

        bool IsInterstitialReady { get; }
        bool IsRewardedReady { get; }

        // For the opt-in rewarded ad, which the player is waiting on and so needs honest feedback.
        AdLoadState RewardedState { get; }
        AdFailure RewardedFailure { get; }

        // The player asked for a rewarded ad: start loading one now (ignoring any retry delay)
        // unless one is already loaded or loading.
        void RequestRewarded();

        void ShowInterstitial(Action<AdOutcome> onComplete);
        void ShowRewarded(Action<AdOutcome> onComplete);
    }
}
