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
    // The ad flow (rewarded spinner / honest failures / timeout, policy-gated interstitial with a watchdog)
    // and the failure wording.
    public class AdFlowTests
    {
        private sealed class FakeAds : IAdService
        {
            public bool InterstitialReady = true;
            public bool RewardedReady = true;
            public AdOutcome RewardedResult = AdOutcome.Completed;
            public AdLoadState State = AdLoadState.Ready;
            public AdFailure Failure = AdFailure.None;
            public bool HoldInterstitial;                 // never calls back until Release is called
            public Action<AdOutcome> HeldInterstitial;
            public int InterstitialShows;
            public int RewardedShows;

            public string Name => "fake";
            public bool IsInterstitialReady => InterstitialReady;
            public bool IsRewardedReady => RewardedReady;
            public AdLoadState RewardedState => State;
            public AdFailure RewardedFailure => Failure;
            public void Initialize() { }
            public void Tick() { }
            public void RequestRewarded() { }

            public void ShowInterstitial(Action<AdOutcome> onComplete)
            {
                InterstitialShows++;
                if (HoldInterstitial)
                {
                    HeldInterstitial = onComplete;
                    return;
                }
                onComplete(AdOutcome.Completed);
            }

            public void ShowRewarded(Action<AdOutcome> onComplete)
            {
                RewardedShows++;
                onComplete(RewardedResult);
            }
        }

        private sealed class FakeConsent : IConsentService
        {
            public bool CanRequestAds => true;
            public bool PrivacyOptionsRequired => false;
            public void GatherConsent(Action<bool> onComplete) => onComplete(true);
            public void ShowPrivacyOptions(Action onClosed) => onClosed?.Invoke();
        }

        private sealed class FakePurchases : IPurchaseService
        {
            public void Initialize() { }
            public string GetLocalizedPrice(string productId) => null;
            public void Purchase(string productId, Action<PurchaseOutcome> onComplete) => onComplete(PurchaseOutcome.Success);
            public void Restore(Action<IReadOnlyCollection<string>> onOwnedProducts) => onOwnedProducts(new string[0]);
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

            public string Value(int index, string key)
            {
                foreach (AnalyticsParam p in Params[index])
                {
                    if (p.Key == key)
                    {
                        return p.Value.ToString();
                    }
                }
                return null;
            }
        }

        private sealed class RecordingPresenter : IAdPresenter
        {
            public readonly List<string> Calls = new List<string>();
            public string LastTitle;
            public string LastBody;
            public bool Cancellable;

            public void ShowAdLoading(string title, string body, bool cancellable)
            {
                Calls.Add("loading:" + title);
                LastTitle = title;
                LastBody = body;
                Cancellable = cancellable;
            }

            public void ShowAdMessage(string title, string body)
            {
                Calls.Add("message:" + title);
                LastTitle = title;
                LastBody = body;
            }

            public void HideAdOverlay() => Calls.Add("hide");

            public void ShowToast(string message) => Calls.Add("toast:" + message);
        }

        private string _dir;
        private float _time;
        private FakeAds _ads;
        private RecordingPresenter _ui;
        private MonetizationController _monetization;
        private AdFlow _flow;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "GravityWellsAdFlowTests_" + Guid.NewGuid());
            Directory.CreateDirectory(_dir);
            var save = new SaveService(_dir);
            save.Data.SessionCount = 3; // not the first session
            _time = 1000f;
            _ads = new FakeAds();
            _ui = new RecordingPresenter();
            _monetization = new MonetizationController(_ads, new FakePurchases(), new FakeConsent(), new RecordingAnalytics(), save, () => _time);
            _flow = new AdFlow(_monetization, _ui, () => _time);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, recursive: true);
            }
        }

        // Earns the break the interstitial policy asks for: 3 levels and 45 s since the last ad, at level 5 or later.
        private void EarnAnInterstitial()
        {
            _monetization.OnLevelCompleted();
            _monetization.OnLevelCompleted();
            _monetization.OnLevelCompleted();
            _time += 60f;
        }

        // ---- interstitial ----

        [Test]
        public void WhenThePolicyBlocksAnAd_TheGameCarriesOnAtOnce_ExactlyOnce()
        {
            int proceeded = 0;
            _flow.ShowInterstitialThen(level: 2, secondsSinceLevelStart: 30f, () => proceeded++);

            Assert.AreEqual(1, proceeded);
            Assert.AreEqual(0, _ads.InterstitialShows, "level 2 is too early for an ad");
            Assert.IsFalse(_flow.Busy);
        }

        [Test]
        public void WhenTheBreakIsEarned_TheAdShows_ThenTheGameCarriesOn()
        {
            EarnAnInterstitial();
            int proceeded = 0;

            _flow.ShowInterstitialThen(level: 6, secondsSinceLevelStart: 30f, () => proceeded++);

            Assert.AreEqual(1, _ads.InterstitialShows);
            Assert.AreEqual(1, proceeded);
            Assert.IsFalse(_flow.Busy);
        }

        [Test]
        public void AnAdTheSdkNeverFinishes_IsGivenUpOnByTheWatchdog_AndNeverRunsTwice()
        {
            EarnAnInterstitial();
            _ads.HoldInterstitial = true;
            int proceeded = 0;

            _flow.ShowInterstitialThen(level: 6, secondsSinceLevelStart: 30f, () => proceeded++);
            Assert.IsTrue(_flow.Busy, "waiting on the ad");
            Assert.AreEqual(0, proceeded);

            _time += 90f;
            _flow.Tick(0.016f);
            Assert.AreEqual(1, proceeded, "the watchdog carries on");
            Assert.IsFalse(_flow.Busy);

            _ads.HeldInterstitial(AdOutcome.Completed); // the SDK finally answers
            Assert.AreEqual(1, proceeded, "and a late answer does not run it again");
        }

        [Test]
        public void LeavingTheLevel_ForgetsAnInterstitialInFlight()
        {
            EarnAnInterstitial();
            _ads.HoldInterstitial = true;
            int proceeded = 0;
            _flow.ShowInterstitialThen(6, 30f, () => proceeded++);

            _flow.Cancel();
            _ads.HeldInterstitial(AdOutcome.Completed);

            Assert.AreEqual(0, proceeded);
            Assert.IsFalse(_flow.Busy);
        }

        // ---- rewarded ----

        [Test]
        public void ARewardedAdWatchedToTheEnd_PaysOut_AndClosesTheOverlay()
        {
            int rewards = 0;
            _flow.WatchRewarded(() => rewards++, "skipped");

            Assert.AreEqual(1, rewards);
            Assert.AreEqual(1, _ads.RewardedShows);
            Assert.AreEqual("hide", _ui.Calls[_ui.Calls.Count - 1]);
            Assert.IsFalse(_flow.Busy);
        }

        [Test]
        public void ASkippedRewardedAd_PaysNothing_AndSaysWhy()
        {
            _ads.RewardedResult = AdOutcome.Skipped;
            int rewards = 0;
            _flow.WatchRewarded(() => rewards++, "Watch the whole ad");

            Assert.AreEqual(0, rewards);
            CollectionAssert.Contains(_ui.Calls, "toast:Watch the whole ad");
        }

        [TestCase(AdFailure.Offline, "No internet")]
        [TestCase(AdFailure.Blocked, "Ads are blocked")]
        [TestCase(AdFailure.NoConsent, "Ads couldn't start")]
        [TestCase(AdFailure.NoFill, "No ad right now")]
        public void WhenNoAdCanBeShown_ThePlayerIsToldTheRealReason(AdFailure failure, string expectedTitle)
        {
            _ads.State = AdLoadState.Failed;
            _ads.Failure = failure;
            int rewards = 0;

            _flow.WatchRewarded(() => rewards++, "skipped");

            Assert.AreEqual(0, rewards);
            Assert.AreEqual(expectedTitle, _ui.LastTitle);
            Assert.AreEqual(0, _ads.RewardedShows, "nothing is shown when the load failed");
        }

        [Test]
        public void AnAdThatIsStillLoading_ShowsASpinner_ThenOpensWhenReady()
        {
            _ads.State = AdLoadState.Loading;
            int rewards = 0;
            _flow.WatchRewarded(() => rewards++, "skipped");

            Assert.IsTrue(_flow.Busy);
            Assert.AreEqual(AdMessages.LoadingTitle, _ui.LastTitle);
            Assert.IsTrue(_ui.Cancellable, "the player can cancel while it loads");

            _ads.State = AdLoadState.Ready;
            _flow.Tick(0.016f);

            Assert.AreEqual(1, rewards);
            Assert.IsFalse(_flow.Busy);
        }

        [Test]
        public void AnAdThatNeverLoads_TimesOutWithAMessage()
        {
            _ads.State = AdLoadState.Loading;
            int rewards = 0;
            _flow.WatchRewarded(() => rewards++, "skipped");

            _time += 25f;
            _flow.Tick(0.016f);

            Assert.AreEqual(0, rewards);
            Assert.AreEqual(AdMessages.TimedOutBody, _ui.LastBody);
            Assert.IsFalse(_flow.Busy, "the board is free again once the message is up");
        }

        [Test]
        public void CancellingWhileItLoads_PaysNothing()
        {
            _ads.State = AdLoadState.Loading;
            int rewards = 0;
            _flow.WatchRewarded(() => rewards++, "skipped");

            _flow.Dismiss();
            _ads.State = AdLoadState.Ready;
            _flow.Tick(0.016f);

            Assert.AreEqual(0, rewards);
            Assert.IsFalse(_flow.Busy);
        }

        // ---- wording ----

        [Test]
        public void EveryFailureReason_HasItsOwnTitle_AndNoneIsVague()
        {
            var titles = new HashSet<string>();
            foreach (AdFailure failure in new[] { AdFailure.Blocked, AdFailure.Offline, AdFailure.NoConsent, AdFailure.NoFill })
            {
                AdMessages.ForFailure(failure, out string title, out string body);
                Assert.IsTrue(titles.Add(title), "duplicate title " + title);
                Assert.Greater(body.Length, 30, failure + " needs a real explanation");
            }
        }
    }
}
