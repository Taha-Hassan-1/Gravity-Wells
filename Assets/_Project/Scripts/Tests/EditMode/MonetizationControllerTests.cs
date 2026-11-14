using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using GravityWells.Core;
using GravityWells.Game;
using GravityWells.Meta;
using GravityWells.Services;

namespace GravityWells.Tests
{
    public class MonetizationControllerTests
    {
        private sealed class FakeAds : IAdService
        {
            public bool InterstitialReady = true;
            public bool RewardedReady = true;
            public AdOutcome InterstitialResult = AdOutcome.Completed;
            public AdOutcome RewardedResult = AdOutcome.Completed;
            public int InitializeCalls;
            public int InterstitialShows;
            public int RewardedShows;

            public AdLoadState State = AdLoadState.Ready;
            public AdFailure Failure = AdFailure.None;
            public int RequestCalls;

            public string Name => "fake";
            public bool IsInterstitialReady => InterstitialReady;
            public bool IsRewardedReady => RewardedReady;
            public AdLoadState RewardedState => State;
            public AdFailure RewardedFailure => Failure;
            public void Initialize() => InitializeCalls++;
            public void Tick() { }
            public void RequestRewarded() => RequestCalls++;

            public void ShowInterstitial(Action<AdOutcome> onComplete)
            {
                InterstitialShows++;
                onComplete(InterstitialResult);
            }

            public void ShowRewarded(Action<AdOutcome> onComplete)
            {
                RewardedShows++;
                onComplete(RewardedResult);
            }
        }

        private sealed class FakePurchases : IPurchaseService
        {
            public PurchaseOutcome Result = PurchaseOutcome.Success;
            public string Price;
            public string[] Owned = new string[0];
            public int InitializeCalls;

            public void Initialize() => InitializeCalls++;
            public string GetLocalizedPrice(string productId) => Price;
            public void Purchase(string productId, Action<PurchaseOutcome> onComplete) => onComplete(Result);
            public void Restore(Action<IReadOnlyCollection<string>> onOwnedProducts) => onOwnedProducts(Owned);
        }

        private sealed class FakeConsent : IConsentService
        {
            public bool Can = true;
            public bool OptionsRequired;
            public bool CanRequestAds => Can;
            public bool PrivacyOptionsRequired => OptionsRequired;
            public void GatherConsent(Action<bool> onComplete) => onComplete(Can);
            public void ShowPrivacyOptions(Action onClosed) => onClosed?.Invoke();
        }

        private sealed class RecordingAnalytics : IAnalyticsService
        {
            public readonly List<string> Events = new List<string>();
            public readonly List<AnalyticsParam[]> Params = new List<AnalyticsParam[]>();

            public void LogEvent(string eventName, params AnalyticsParam[] parameters)
            {
                Events.Add(eventName);
                Params.Add(parameters);
            }

            public string ParamValue(int eventIndex, string key)
            {
                foreach (AnalyticsParam p in Params[eventIndex])
                {
                    if (p.Key == key)
                    {
                        return p.Value.ToString();
                    }
                }
                return null;
            }
        }

        private string _tempDir;
        private SaveService _save;
        private FakeAds _ads;
        private FakePurchases _purchases;
        private FakeConsent _consent;
        private RecordingAnalytics _analytics;
        private float _now;
        private MonetizationController _monetization;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "GravityWellsMonetizationTests_" + Guid.NewGuid());
            Directory.CreateDirectory(_tempDir);
            _save = new SaveService(_tempDir);
            _save.Data.SessionCount = 5; // not a first session unless a test says so

            _ads = new FakeAds();
            _purchases = new FakePurchases();
            _consent = new FakeConsent();
            _analytics = new RecordingAnalytics();
            _now = 0f;
            _monetization = new MonetizationController(_ads, _purchases, _consent, _analytics, _save, () => _now);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }

        private void CompleteLevels(int count)
        {
            for (int i = 0; i < count; i++)
            {
                _monetization.OnLevelCompleted();
            }
        }

        private int TryInterstitial(int level = 5, bool dragging = false, float secondsSinceStart = 60f)
        {
            int finished = 0;
            _monetization.TryShowInterstitial(level, dragging, secondsSinceStart, () => finished++);
            return finished;
        }

        // ---- consent / ad start-up ----

        [Test]
        public void FailedConsent_IsRetriedWhenAnAdIsNextWanted_NotOnlyAtLaunch()
        {
            _consent.Can = false;
            _monetization.Initialize();
            Assert.AreEqual(0, _ads.InitializeCalls, "no consent at launch, so the SDK stays off");

            _consent.Can = true; // e.g. the network came back
            _now = 5f;
            _monetization.ShowRewarded(_ => { });
            Assert.AreEqual(0, _ads.InitializeCalls, "too soon to ask again");

            _now = 16f;
            _monetization.ShowRewarded(_ => { });
            Assert.AreEqual(1, _ads.InitializeCalls, "retry succeeded, SDK starts");
        }

        [Test]
        public void RequestRewarded_StartsTheSdk_AndAsksForALoad_IgnoringTheRetryDelay()
        {
            _consent.Can = false;
            _monetization.Initialize(); // fails now, so the next normal retry is 15 s away
            _consent.Can = true;
            _now = 1f;

            _monetization.RequestRewarded();

            Assert.AreEqual(1, _ads.InitializeCalls, "a player's tap must not wait out the retry delay");
            Assert.AreEqual(1, _ads.RequestCalls);
        }

        [Test]
        public void RewardedState_BeforeTheSdkStarts_IsFailed_WithAConsentReason()
        {
            _consent.Can = false;
            _monetization.Initialize();

            Assert.AreEqual(AdLoadState.Failed, _monetization.RewardedState);
            Assert.AreEqual(AdFailure.NoConsent, _monetization.RewardedFailure);
        }

        [Test]
        public void RewardedState_AfterTheSdkStarts_ReportsTheServicesOwnState()
        {
            _monetization.Initialize();
            _ads.State = AdLoadState.Failed;
            _ads.Failure = AdFailure.Blocked;

            Assert.AreEqual(AdLoadState.Failed, _monetization.RewardedState);
            Assert.AreEqual(AdFailure.Blocked, _monetization.RewardedFailure);
        }

        [Test]
        public void AdsAreInitialisedOnlyOnce_EvenWhenAdsAreWantedRepeatedly()
        {
            _monetization.Initialize();
            _now = 100f;
            _monetization.ShowRewarded(_ => { });
            TryInterstitial();
            Assert.AreEqual(1, _ads.InitializeCalls);
        }

        // ---- interstitials ----

        [Test]
        public void Interstitial_WaitsForThreeLevelsAndFortyFiveSeconds_ThenShows()
        {
            CompleteLevels(3);
            _now = 30f;
            Assert.AreEqual(1, TryInterstitial());
            Assert.AreEqual(0, _ads.InterstitialShows, "only 30s since launch");

            _now = 46f;
            Assert.AreEqual(1, TryInterstitial());
            Assert.AreEqual(1, _ads.InterstitialShows);
            Assert.Contains(AnalyticsEvents.AdShown, _analytics.Events);
        }

        [Test]
        public void Interstitial_AfterShowing_ResetsBothCounters()
        {
            CompleteLevels(3);
            _now = 50f;
            TryInterstitial();
            Assert.AreEqual(1, _ads.InterstitialShows);

            CompleteLevels(1);
            _now = 100f;
            TryInterstitial();
            Assert.AreEqual(1, _ads.InterstitialShows, "only one level and 50s since the last ad");

            CompleteLevels(2);
            TryInterstitial();
            Assert.AreEqual(2, _ads.InterstitialShows);
        }

        [Test]
        public void Interstitial_ContinuesImmediately_WhenBlocked()
        {
            Assert.AreEqual(1, TryInterstitial()); // no levels completed yet
            Assert.AreEqual(0, _ads.InterstitialShows);
        }

        [Test]
        public void Interstitial_NeverShownDuringADrag()
        {
            CompleteLevels(3);
            _now = 60f;

            Assert.AreEqual(1, TryInterstitial(dragging: true));
            Assert.AreEqual(0, _ads.InterstitialShows);
        }

        [Test]
        public void Interstitial_NeverShownInFirstSessionBeforeLevelThree()
        {
            _save.Data.SessionCount = 1;
            CompleteLevels(3);
            _now = 60f;

            Assert.AreEqual(1, TryInterstitial(level: 2));
            Assert.AreEqual(0, _ads.InterstitialShows);
        }

        [Test]
        public void RemoveAds_StopsInterstitials_ButNotRewarded()
        {
            _save.Data.RemoveAdsPurchased = true;
            CompleteLevels(10);
            _now = 500f;

            TryInterstitial();
            Assert.AreEqual(0, _ads.InterstitialShows);

            AdOutcome outcome = AdOutcome.Failed;
            _monetization.ShowRewarded(o => outcome = o);
            Assert.AreEqual(AdOutcome.Completed, outcome);
            Assert.AreEqual(1, _ads.RewardedShows);
        }

        [Test]
        public void Interstitial_NotReady_LogsFailureAndKeepsCounters()
        {
            _ads.InterstitialReady = false;
            CompleteLevels(3);
            _now = 60f;

            Assert.AreEqual(1, TryInterstitial());
            Assert.AreEqual(0, _ads.InterstitialShows);
            Assert.AreEqual(AnalyticsEvents.AdFailed, _analytics.Events[0]);
            Assert.AreEqual("not_ready", _analytics.ParamValue(0, "reason"));

            _ads.InterstitialReady = true; // becomes ready later: still owed, so it shows now
            TryInterstitial();
            Assert.AreEqual(1, _ads.InterstitialShows);
        }

        [Test]
        public void Interstitial_SdkFailure_LogsReasonAndStillContinues()
        {
            _ads.InterstitialResult = AdOutcome.Failed;
            CompleteLevels(3);
            _now = 60f;

            Assert.AreEqual(1, TryInterstitial());
            Assert.AreEqual(AnalyticsEvents.AdFailed, _analytics.Events[0]);
            Assert.AreEqual("failed", _analytics.ParamValue(0, "reason"));
        }

        // ---- consent ----

        [Test]
        public void Consent_Denied_BlocksInterstitialsAndRewarded()
        {
            _consent.Can = false;
            CompleteLevels(3);
            _now = 60f;

            TryInterstitial();
            Assert.AreEqual(0, _ads.InterstitialShows);

            AdOutcome outcome = AdOutcome.Completed;
            _monetization.ShowRewarded(o => outcome = o);
            Assert.AreEqual(AdOutcome.NotReady, outcome);
            Assert.AreEqual(0, _ads.RewardedShows);
            Assert.AreEqual("no_consent", _analytics.ParamValue(0, "reason"));
        }

        [Test]
        public void Initialize_OnlyStartsTheAdSdk_WhenConsentAllowsIt()
        {
            _monetization.Initialize();
            Assert.AreEqual(1, _ads.InitializeCalls);

            _consent.Can = false;
            _monetization.Initialize();
            Assert.AreEqual(1, _ads.InitializeCalls);
        }

        // ---- rewarded ----

        [Test]
        public void Rewarded_Completed_LogsAdShown()
        {
            AdOutcome outcome = AdOutcome.Failed;
            _monetization.ShowRewarded(o => outcome = o);

            Assert.AreEqual(AdOutcome.Completed, outcome);
            Assert.AreEqual(AnalyticsEvents.AdShown, _analytics.Events[0]);
            Assert.AreEqual("rewarded", _analytics.ParamValue(0, "type"));
        }

        [Test]
        public void Rewarded_ClosedEarly_ReportsSkipped_AndStillCountsAsShown()
        {
            _ads.RewardedResult = AdOutcome.Skipped;
            AdOutcome outcome = AdOutcome.Completed;
            _monetization.ShowRewarded(o => outcome = o);

            Assert.AreEqual(AdOutcome.Skipped, outcome);
            Assert.AreEqual(AnalyticsEvents.AdShown, _analytics.Events[0]);
        }

        [Test]
        public void Rewarded_NotReady_LogsFailure()
        {
            _ads.RewardedReady = false;
            AdOutcome outcome = AdOutcome.Completed;
            _monetization.ShowRewarded(o => outcome = o);

            Assert.AreEqual(AdOutcome.NotReady, outcome);
            Assert.AreEqual(AnalyticsEvents.AdFailed, _analytics.Events[0]);
        }

        // ---- remove ads ----

        [Test]
        public void Purchase_Success_GrantsPersistsAndLogs()
        {
            PurchaseOutcome outcome = PurchaseOutcome.Failed;
            _monetization.PurchaseRemoveAds(o => outcome = o);

            Assert.AreEqual(PurchaseOutcome.Success, outcome);
            Assert.IsTrue(_monetization.AdsRemoved);
            Assert.IsTrue(new SaveService(_tempDir).Data.RemoveAdsPurchased, "must survive a relaunch");
            Assert.AreEqual(AnalyticsEvents.IapPurchased, _analytics.Events[0]);
            Assert.AreEqual(GameConfig.RemoveAdsProductId, _analytics.ParamValue(0, "product"));
        }

        [Test]
        public void Purchase_CancelledFailedOrUnavailable_NeverGrants()
        {
            foreach (PurchaseOutcome result in new[] { PurchaseOutcome.Cancelled, PurchaseOutcome.Failed, PurchaseOutcome.Unavailable })
            {
                _purchases.Result = result;
                _monetization.PurchaseRemoveAds(_ => { });

                Assert.IsFalse(_monetization.AdsRemoved, result.ToString());
            }
            Assert.IsEmpty(_analytics.Events);
        }

        [Test]
        public void Purchase_WhenAlreadyOwned_DoesNotChargeAgain()
        {
            _save.Data.RemoveAdsPurchased = true;
            _purchases.Result = PurchaseOutcome.Failed; // would fail if it reached the store

            PurchaseOutcome outcome = PurchaseOutcome.Failed;
            _monetization.PurchaseRemoveAds(o => outcome = o);

            Assert.AreEqual(PurchaseOutcome.Success, outcome);
        }

        [Test]
        public void Restore_GrantsWhenTheStoreReportsOwnership()
        {
            _purchases.Owned = new[] { GameConfig.RemoveAdsProductId };
            bool found = false;
            _monetization.RestorePurchases(f => found = f);

            Assert.IsTrue(found);
            Assert.IsTrue(_monetization.AdsRemoved);
        }

        [Test]
        public void Restore_NeverRevokesAnExistingPurchase()
        {
            _save.Data.RemoveAdsPurchased = true;
            _purchases.Owned = new string[0]; // an unreachable/empty store response

            bool found = true;
            _monetization.RestorePurchases(f => found = f);

            Assert.IsFalse(found);
            Assert.IsTrue(_monetization.AdsRemoved);
        }

        [Test]
        public void Initialize_RestoresOwnedPurchasesSilently()
        {
            _purchases.Owned = new[] { GameConfig.RemoveAdsProductId };
            _monetization.Initialize();

            Assert.IsTrue(_monetization.AdsRemoved);
            Assert.IsEmpty(_analytics.Events, "a restore is not a new purchase");
        }

        [Test]
        public void RemoveAdsPrice_UsesTheStorePrice_WithAFallback()
        {
            Assert.AreEqual("$2.99", _monetization.RemoveAdsPrice);

            _purchases.Price = "2,99 EUR";
            Assert.AreEqual("2,99 EUR", _monetization.RemoveAdsPrice);
        }
    }
}
