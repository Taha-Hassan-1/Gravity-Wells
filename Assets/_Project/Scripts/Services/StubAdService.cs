using System;

namespace GravityWells.Services
{
    // Stand-in until a mediation SDK is wired up: always "ready" and completes at once, so
    // the whole ad flow can be built and tested without an account or a device.
    public sealed class StubAdService : IAdService
    {
        public const string ProviderName = "stub";

        public string Name => ProviderName;
        public bool IsInterstitialReady => true;
        public bool IsRewardedReady => true;
        public AdLoadState RewardedState => AdLoadState.Ready;
        public AdFailure RewardedFailure => AdFailure.None;

        public void Initialize() { }

        public void Tick() { }

        public void RequestRewarded() { }

        public void ShowInterstitial(Action<AdOutcome> onComplete) => onComplete?.Invoke(AdOutcome.Completed);

        public void ShowRewarded(Action<AdOutcome> onComplete) => onComplete?.Invoke(AdOutcome.Completed);
    }
}
