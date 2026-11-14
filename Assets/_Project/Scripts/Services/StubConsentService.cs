using System;

namespace GravityWells.Services
{
    // Treats every user as outside a consent-regulated region until the UMP SDK is wired in.
    public sealed class StubConsentService : IConsentService
    {
        public bool CanRequestAds => true;
        public bool PrivacyOptionsRequired => false;

        public void GatherConsent(Action<bool> onComplete) => onComplete?.Invoke(true);

        public void ShowPrivacyOptions(Action onClosed) => onClosed?.Invoke();
    }
}
