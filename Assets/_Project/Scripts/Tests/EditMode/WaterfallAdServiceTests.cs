using System;
using System.Collections.Generic;
using NUnit.Framework;
using GravityWells.Core;
using GravityWells.Services;

namespace GravityWells.Tests
{
    public class WaterfallAdServiceTests
    {
        private sealed class FakeProvider : IAdService
        {
            public FakeProvider(string name) { Name = name; }

            public string Name { get; }
            public bool InterstitialReady = true;
            public bool RewardedReady = true;
            public AdOutcome InterstitialResult = AdOutcome.Completed;
            public AdOutcome RewardedResult = AdOutcome.Completed;
            public AdLoadState State = AdLoadState.Ready;
            public AdFailure Failure = AdFailure.None;
            public int InitializeCalls;
            public int RequestCalls;
            public int InterstitialShows;
            public int RewardedShows;

            public bool IsInterstitialReady => InterstitialReady;
            public bool IsRewardedReady => RewardedReady;
            public AdLoadState RewardedState => State;
            public AdFailure RewardedFailure => Failure;
            public int TickCalls;
            public void Initialize() => InitializeCalls++;
            public void Tick() => TickCalls++;
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

        private FakeProvider _a;
        private FakeProvider _b;
        private AdProviderRegistry _registry;
        private int _createdA;
        private int _createdB;

        [SetUp]
        public void SetUp()
        {
            _a = new FakeProvider("alpha");
            _b = new FakeProvider("beta");
            _createdA = 0;
            _createdB = 0;
            _registry = new AdProviderRegistry();
            _registry.Register("alpha", () => { _createdA++; return _a; });
            _registry.Register("beta", () => { _createdB++; return _b; });
        }

        private WaterfallAdService Build(params string[] order) => new WaterfallAdService(_registry, order);

        private static AdOutcome ShowRewarded(WaterfallAdService ads)
        {
            AdOutcome result = AdOutcome.NotReady;
            int calls = 0;
            ads.ShowRewarded(o => { result = o; calls++; });
            Assert.AreEqual(1, calls, "the callback must fire exactly once");
            return result;
        }

        private static AdOutcome ShowInterstitial(WaterfallAdService ads)
        {
            AdOutcome result = AdOutcome.NotReady;
            int calls = 0;
            ads.ShowInterstitial(o => { result = o; calls++; });
            Assert.AreEqual(1, calls, "the callback must fire exactly once");
            return result;
        }

        // ---- priority and fallback ----

        [Test]
        public void FirstReadyProvider_Serves_AndTheRestAreNotAsked()
        {
            WaterfallAdService ads = Build("alpha", "beta");

            Assert.AreEqual(AdOutcome.Completed, ShowInterstitial(ads));

            Assert.AreEqual(1, _a.InterstitialShows);
            Assert.AreEqual(0, _b.InterstitialShows);
            Assert.AreEqual("alpha", ads.LastServedBy);
        }

        [Test]
        public void NotReadyProvider_IsSkipped_ForTheNextOne()
        {
            _a.InterstitialReady = false;
            WaterfallAdService ads = Build("alpha", "beta");

            ShowInterstitial(ads);

            Assert.AreEqual(0, _a.InterstitialShows);
            Assert.AreEqual(1, _b.InterstitialShows);
            Assert.AreEqual("beta", ads.LastServedBy);
        }

        [Test]
        public void ProviderThatFailsToShow_FallsThroughToTheNext()
        {
            _a.RewardedResult = AdOutcome.Failed;
            WaterfallAdService ads = Build("alpha", "beta");

            Assert.AreEqual(AdOutcome.Completed, ShowRewarded(ads));

            Assert.AreEqual(1, _a.RewardedShows);
            Assert.AreEqual(1, _b.RewardedShows);
            Assert.AreEqual("beta", ads.LastServedBy);
        }

        [Test]
        public void SkippedRewardedAd_IsFinal_NoSecondAdIsForcedOnThePlayer()
        {
            _a.RewardedResult = AdOutcome.Skipped;
            WaterfallAdService ads = Build("alpha", "beta");

            Assert.AreEqual(AdOutcome.Skipped, ShowRewarded(ads));

            Assert.AreEqual(0, _b.RewardedShows);
        }

        [Test]
        public void EveryProviderFailing_ReportsTheLastFailure()
        {
            _a.InterstitialResult = AdOutcome.Failed;
            _b.InterstitialResult = AdOutcome.Failed;
            WaterfallAdService ads = Build("alpha", "beta");

            Assert.AreEqual(AdOutcome.Failed, ShowInterstitial(ads));
            Assert.AreEqual(1, _a.InterstitialShows);
            Assert.AreEqual(1, _b.InterstitialShows, "each provider is tried once, never in a loop");
        }

        [Test]
        public void NothingReady_ReportsNotReady_WithoutShowing()
        {
            _a.InterstitialReady = false;
            _b.InterstitialReady = false;
            WaterfallAdService ads = Build("alpha", "beta");

            Assert.AreEqual(AdOutcome.NotReady, ShowInterstitial(ads));
            Assert.AreEqual(0, _a.InterstitialShows + _b.InterstitialShows);
        }

        [Test]
        public void NoProviders_ReportsNotReady_AndIsNeverReady()
        {
            WaterfallAdService ads = Build();

            Assert.IsFalse(ads.IsInterstitialReady);
            Assert.IsFalse(ads.IsRewardedReady);
            Assert.AreEqual(AdOutcome.NotReady, ShowRewarded(ads));
            Assert.AreEqual(AdLoadState.Failed, ads.RewardedState);
        }

        // ---- readiness and state ----

        [Test]
        public void Ready_WhenAnyProviderIsReady()
        {
            _a.RewardedReady = false;
            _a.InterstitialReady = false;
            WaterfallAdService ads = Build("alpha", "beta");

            Assert.IsTrue(ads.IsRewardedReady);
            Assert.IsTrue(ads.IsInterstitialReady);

            _b.RewardedReady = false;
            Assert.IsFalse(ads.IsRewardedReady);
        }

        [Test]
        public void ReadinessPollsEveryProvider_SoEachKeepsItsNextAdLoading()
        {
            int polls = 0;
            var probe = new PollCounter("probe", () => polls++);
            var registry = new AdProviderRegistry();
            registry.Register("alpha", () => _a);
            registry.Register("probe", () => probe);
            var ads = new WaterfallAdService(registry, new[] { "alpha", "probe" });

            bool ready = ads.IsInterstitialReady; // alpha is ready, but probe must still be asked

            Assert.IsTrue(ready);
            Assert.AreEqual(1, polls);
        }

        private sealed class PollCounter : IAdService
        {
            private readonly Action _onPoll;
            public PollCounter(string name, Action onPoll) { Name = name; _onPoll = onPoll; }
            public string Name { get; }
            public bool IsInterstitialReady { get { _onPoll(); return false; } }
            public bool IsRewardedReady => false;
            public AdLoadState RewardedState => AdLoadState.Idle;
            public AdFailure RewardedFailure => AdFailure.None;
            public void Initialize() { }
            public void Tick() { }
            public void RequestRewarded() { }
            public void ShowInterstitial(Action<AdOutcome> onComplete) => onComplete(AdOutcome.NotReady);
            public void ShowRewarded(Action<AdOutcome> onComplete) => onComplete(AdOutcome.NotReady);
        }

        [Test]
        public void RewardedState_ReadyBeatsLoadingBeatsIdleBeatsFailed()
        {
            WaterfallAdService ads = Build("alpha", "beta");

            _a.State = AdLoadState.Failed;
            _b.State = AdLoadState.Failed;
            Assert.AreEqual(AdLoadState.Failed, ads.RewardedState);

            _b.State = AdLoadState.Idle;
            Assert.AreEqual(AdLoadState.Idle, ads.RewardedState);

            _b.State = AdLoadState.Loading;
            Assert.AreEqual(AdLoadState.Loading, ads.RewardedState);

            _b.State = AdLoadState.Ready;
            Assert.AreEqual(AdLoadState.Ready, ads.RewardedState);
        }

        [Test]
        public void RewardedFailure_PrefersTheConnectionProblem()
        {
            _a.State = AdLoadState.Failed;
            _b.State = AdLoadState.Failed;
            _a.Failure = AdFailure.NoFill;
            _b.Failure = AdFailure.Blocked;
            WaterfallAdService ads = Build("alpha", "beta");

            Assert.AreEqual(AdFailure.Blocked, ads.RewardedFailure);

            _a.Failure = AdFailure.Offline;
            Assert.AreEqual(AdFailure.Offline, ads.RewardedFailure);
        }

        [Test]
        public void RequestRewarded_IsForwardedToEveryProvider()
        {
            WaterfallAdService ads = Build("alpha", "beta");

            ads.RequestRewarded();

            Assert.AreEqual(1, _a.RequestCalls);
            Assert.AreEqual(1, _b.RequestCalls);
        }

        // ---- lifecycle and switching ----

        [Test]
        public void Initialize_StartsEveryActiveProvider()
        {
            WaterfallAdService ads = Build("alpha", "beta");

            ads.Initialize();

            Assert.AreEqual(1, _a.InitializeCalls);
            Assert.AreEqual(1, _b.InitializeCalls);
        }

        [Test]
        public void UnlistedProviders_AreNeverConstructed()
        {
            Build("alpha");

            Assert.AreEqual(1, _createdA);
            Assert.AreEqual(0, _createdB, "a provider that is not in the order must not touch its SDK");
        }

        [Test]
        public void UnknownNames_AreSkipped_AndLogged()
        {
            var log = new List<string>();
            var ads = new WaterfallAdService(_registry, new[] { "nope", "alpha" }, log.Add);

            Assert.AreEqual(1, ads.Active.Count);
            Assert.AreEqual(1, log.Count);
            StringAssert.Contains("nope", log[0]);
        }

        [Test]
        public void SetOrder_ChangesPriorityAtRuntime()
        {
            WaterfallAdService ads = Build("alpha", "beta");
            ShowInterstitial(ads);
            Assert.AreEqual("alpha", ads.LastServedBy);

            ads.SetOrder(new[] { "beta", "alpha" });
            ShowInterstitial(ads);

            Assert.AreEqual("beta", ads.LastServedBy);
        }

        [Test]
        public void SetOrder_CanSwitchAProviderOff_AndOn()
        {
            WaterfallAdService ads = Build("alpha", "beta");

            ads.SetOrder(new[] { "beta" });
            ShowInterstitial(ads);
            Assert.AreEqual(0, _a.InterstitialShows);

            ads.SetOrder(new[] { "alpha", "beta" });
            ShowInterstitial(ads);
            Assert.AreEqual(1, _a.InterstitialShows);
            Assert.AreEqual(1, _createdA, "switching back must reuse the provider, not build a second one");
        }

        [Test]
        public void ProviderSwitchedOnAfterInitialize_IsInitializedOnActivation()
        {
            WaterfallAdService ads = Build("alpha");
            ads.Initialize();
            Assert.AreEqual(0, _b.InitializeCalls);

            ads.SetOrder(new[] { "alpha", "beta" });

            Assert.AreEqual(1, _b.InitializeCalls);
        }

        // ---- config parsing ----

        [Test]
        public void ParseOrder_TrimsLowercasesAndDropsBlanksAndRepeats()
        {
            CollectionAssert.AreEqual(
                new[] { "admob", "applovin" }, AdProviderOrder.Parse(" AdMob , applovin,, ADMOB ,"));
        }

        [Test]
        public void ParseOrder_EmptyOrNull_GivesNoProviders()
        {
            Assert.IsEmpty(AdProviderOrder.Parse(null));
            Assert.IsEmpty(AdProviderOrder.Parse("  "));
        }

        [Test]
        public void ShippedConfig_NeverContainsTheStub_OnDevices()
        {
            // The stub always "completes", which would grant rewards for free in a release build.
            CollectionAssert.DoesNotContain(AdProviderOrder.Parse(GameConfig.AdProviderOrder), StubAdService.ProviderName);
        }

        [Test]
        public void Registry_RejectsBlankNames_AndMatchesCaseInsensitively()
        {
            var registry = new AdProviderRegistry();
            Assert.Throws<ArgumentException>(() => registry.Register(" ", () => _a));

            registry.Register("AdMob", () => _a);
            Assert.IsTrue(registry.IsRegistered("admob"));
            Assert.IsFalse(registry.IsRegistered("other"));
        }
    }
}
