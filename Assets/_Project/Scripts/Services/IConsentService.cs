using System;

namespace GravityWells.Services
{
    // Wraps the Google UMP consent flow. Ads may only be requested once the user's
    // consent state allows it (EEA/UK users must be asked first).
    public interface IConsentService
    {
        bool CanRequestAds { get; }

        // True when the user has a privacy-options entry point they must be able to reopen
        // (UMP requires one in Settings for regulated regions).
        bool PrivacyOptionsRequired { get; }

        // Runs the consent flow if needed, then reports whether ads may be requested.
        void GatherConsent(Action<bool> onComplete);

        void ShowPrivacyOptions(Action onClosed);
    }
}
