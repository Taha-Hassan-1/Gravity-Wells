#if UNITY_ANDROID
using System;
using GoogleMobileAds.Common;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace GravityWells.Services
{
    // Google's User Messaging Platform: asks EEA/UK users for consent before any ad is requested
    // and exposes the "privacy options" form the user must be able to reopen. As with ads, SDK
    // callbacks are marshalled onto the main thread before they touch game code.
    public sealed class AdMobConsentService : IConsentService
    {
        public bool CanRequestAds => ConsentInformation.CanRequestAds();

        public bool PrivacyOptionsRequired =>
            ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;

        // Refreshes the consent state, shows the form only if one is required, and reports whether
        // ads may now be requested. A failure is not fatal: stored consent from an earlier
        // launch still lets ads load, and with none stored the answer is correctly "no".
        public void GatherConsent(Action<bool> onComplete)
        {
            // ExecuteInUpdate only delivers once the dispatcher object exists, and nothing else
            // has created it yet this early (MobileAds.Initialize comes after consent). Without
            // this the callbacks below never ran and ads silently never started.
            MobileAdsEventExecutor.Initialize();

            var parameters = new ConsentRequestParameters { TagForUnderAgeOfConsent = false };
            Debug.Log("[Consent] requesting consent info");

            ConsentInformation.Update(parameters, (FormError updateError) =>
                MobileAdsEventExecutor.ExecuteInUpdate(() =>
                {
                    if (updateError != null)
                    {
                        Debug.LogWarning("[Consent] update failed: " + updateError.Message);
                        onComplete?.Invoke(CanRequestAds);
                        return;
                    }

                    Debug.Log("[Consent] updated, canRequestAds=" + CanRequestAds);
                    if (CanRequestAds)
                    {
                        onComplete?.Invoke(true);
                        return;
                    }

                    ConsentForm.LoadAndShowConsentFormIfRequired((FormError showError) =>
                        MobileAdsEventExecutor.ExecuteInUpdate(() =>
                        {
                            if (showError != null)
                            {
                                Debug.LogWarning("[Consent] form failed: " + showError.Message);
                            }
                            Debug.Log("[Consent] form done, canRequestAds=" + CanRequestAds);
                            onComplete?.Invoke(CanRequestAds);
                        }));
                }));
        }

        public void ShowPrivacyOptions(Action onClosed)
        {
            ConsentForm.ShowPrivacyOptionsForm((FormError error) =>
                MobileAdsEventExecutor.ExecuteInUpdate(() =>
                {
                    if (error != null)
                    {
                        Debug.LogWarning("[Consent] privacy options failed: " + error.Message);
                    }
                    onClosed?.Invoke();
                }));
        }
    }
}
#endif
