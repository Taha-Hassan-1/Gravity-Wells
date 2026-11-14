using System;
using System.Collections.Generic;
using GravityWells.Core;
using GravityWells.Meta;
using GravityWells.Services;

namespace GravityWells.Game
{
    // Everything money-shaped in one place: when an interstitial may run (via AdPolicy),
    // rewarded ads for Assist, the single "remove ads" purchase, consent gating, and the
    // analytics each of those emits. A plain class with an injected clock so it's testable.
    public sealed class MonetizationController
    {
        private readonly IAdService _ads;
        private readonly IPurchaseService _purchases;
        private readonly IConsentService _consent;
        private readonly IAnalyticsService _analytics;
        private readonly SaveService _save;
        private readonly Func<float> _now;

        // A failed consent request (offline at launch, say) must not disable ads for the whole
        // session, so it is retried the next time an ad is wanted.
        private const float ConsentRetrySeconds = 15f;

        private int _levelsSinceLastAd;
        private float _lastAdTime;
        private bool _adsStarted;
        private bool _consentInFlight;
        private float _nextConsentTry;

        public MonetizationController(
            IAdService ads, IPurchaseService purchases, IConsentService consent,
            IAnalyticsService analytics, SaveService save, Func<float> now)
        {
            _ads = ads;
            _purchases = purchases;
            _consent = consent;
            _analytics = analytics;
            _save = save;
            _now = now;

            // Counting from launch means the first interstitial can't arrive before the
            // minimum gap has passed, even if the player blazes through levels.
            _lastAdTime = now();
        }

        public bool AdsRemoved => _save.Data.RemoveAdsPurchased;
        public bool PrivacyOptionsRequired => _consent.PrivacyOptionsRequired;

        public string RemoveAdsPrice =>
            _purchases.GetLocalizedPrice(GameConfig.RemoveAdsProductId) ?? GameConfig.RemoveAdsFallbackPrice;

        public void Initialize()
        {
            _purchases.Initialize();
            _purchases.Restore(OnOwnedProducts);

            EnsureAdsStarted();
        }

        // A player waiting on a rewarded ad skips the retry delay: they just asked for it.
        private void EnsureAdsStarted(bool force = false)
        {
            if (_adsStarted || _consentInFlight || (!force && _now() < _nextConsentTry))
            {
                return;
            }

            _consentInFlight = true;
            _consent.GatherConsent(canRequestAds =>
            {
                _consentInFlight = false;
                if (canRequestAds)
                {
                    _adsStarted = true;
                    _ads.Initialize();
                }
                else
                {
                    _nextConsentTry = _now() + ConsentRetrySeconds;
                }
            });
        }

        public void OnLevelCompleted() => _levelsSinceLastAd++;

        // ---- interstitials ----

        public AdBlockReason EvaluateInterstitial(int currentLevel, bool isDragging, float secondsSinceLevelStart)
        {
            return AdPolicy.EvaluateInterstitial(new AdPolicyInput(
                currentLevel, _levelsSinceLastAd, _now() - _lastAdTime, secondsSinceLevelStart,
                isFirstSession: _save.Data.SessionCount <= 1, isDragging, AdsRemoved, _consent.CanRequestAds));
        }

        // Runs onFinished exactly when the player may carry on — immediately if the policy
        // blocks an ad, or after the ad closes. Never shows an ad the policy didn't allow.
        public void TryShowInterstitial(int currentLevel, bool isDragging, float secondsSinceLevelStart, Action onFinished)
        {
            EnsureAdsStarted();
            if (EvaluateInterstitial(currentLevel, isDragging, secondsSinceLevelStart) != AdBlockReason.None)
            {
                onFinished?.Invoke();
                return;
            }

            if (!_ads.IsInterstitialReady)
            {
                LogAdFailed("interstitial", "not_ready");
                onFinished?.Invoke();
                return;
            }

            _ads.ShowInterstitial(outcome =>
            {
                if (outcome == AdOutcome.Completed)
                {
                    _levelsSinceLastAd = 0;
                    _lastAdTime = _now();
                    LogAdShown("interstitial");
                }
                else
                {
                    LogAdFailed("interstitial", outcome.ToString().ToLowerInvariant());
                }
                onFinished?.Invoke();
            });
        }

        // ---- rewarded (Assist) ----

        // Until consent has let the SDK start there is nothing to load, so that phase is reported
        // here: still asking (Loading) or it didn't work out (Failed/NoConsent).
        public AdLoadState RewardedState
        {
            get
            {
                if (_adsStarted)
                {
                    return _ads.RewardedState;
                }
                return _consentInFlight ? AdLoadState.Loading : AdLoadState.Failed;
            }
        }

        public AdFailure RewardedFailure => _adsStarted ? _ads.RewardedFailure : AdFailure.NoConsent;

        // Lets the ad network keep its next ads loaded in the background.
        public void Tick() => _ads.Tick();

        public void RequestRewarded()
        {
            EnsureAdsStarted(force: true);
            if (_adsStarted)
            {
                _ads.RequestRewarded();
            }
        }

        // Opt-in only and never subject to AdPolicy: the player asked for it, and having
        // bought "remove ads" must not take Assist away.
        public void ShowRewarded(Action<AdOutcome> onComplete)
        {
            EnsureAdsStarted();
            if (!_consent.CanRequestAds)
            {
                LogAdFailed("rewarded", "no_consent");
                onComplete?.Invoke(AdOutcome.NotReady);
                return;
            }
            if (!_ads.IsRewardedReady)
            {
                LogAdFailed("rewarded", "not_ready");
                onComplete?.Invoke(AdOutcome.NotReady);
                return;
            }

            _ads.ShowRewarded(outcome =>
            {
                if (outcome == AdOutcome.Completed || outcome == AdOutcome.Skipped)
                {
                    LogAdShown("rewarded");
                }
                else
                {
                    LogAdFailed("rewarded", outcome.ToString().ToLowerInvariant());
                }
                onComplete?.Invoke(outcome);
            });
        }

        // ---- remove ads ----

        public void PurchaseRemoveAds(Action<PurchaseOutcome> onComplete)
        {
            if (AdsRemoved)
            {
                onComplete?.Invoke(PurchaseOutcome.Success);
                return;
            }

            _purchases.Purchase(GameConfig.RemoveAdsProductId, outcome =>
            {
                if (outcome == PurchaseOutcome.Success)
                {
                    GrantRemoveAds();
                    _analytics.LogEvent(
                        AnalyticsEvents.IapPurchased,
                        new AnalyticsParam("product", GameConfig.RemoveAdsProductId));
                }
                onComplete?.Invoke(outcome);
            });
        }

        // Restore only ever grants; it never revokes, so an unreachable store can't take
        // away something the player already paid for.
        public void RestorePurchases(Action<bool> onComplete)
        {
            _purchases.Restore(owned =>
            {
                bool hasRemoveAds = Owns(owned);
                if (hasRemoveAds)
                {
                    GrantRemoveAds();
                }
                onComplete?.Invoke(hasRemoveAds);
            });
        }

        public void ShowPrivacyOptions(Action onClosed) => _consent.ShowPrivacyOptions(onClosed);

        private void OnOwnedProducts(IReadOnlyCollection<string> owned)
        {
            if (Owns(owned))
            {
                GrantRemoveAds();
            }
        }

        private static bool Owns(IReadOnlyCollection<string> owned)
        {
            if (owned == null)
            {
                return false;
            }
            foreach (string product in owned)
            {
                if (product == GameConfig.RemoveAdsProductId)
                {
                    return true;
                }
            }
            return false;
        }

        private void GrantRemoveAds()
        {
            if (_save.Data.RemoveAdsPurchased)
            {
                return;
            }
            _save.Data.RemoveAdsPurchased = true;
            _save.Save();
        }

        private void LogAdShown(string type)
        {
            _analytics.LogEvent(AnalyticsEvents.AdShown, new AnalyticsParam("type", type));
        }

        private void LogAdFailed(string type, string reason)
        {
            _analytics.LogEvent(
                AnalyticsEvents.AdFailed, new AnalyticsParam("type", type), new AnalyticsParam("reason", reason));
        }
    }
}
