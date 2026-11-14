using NUnit.Framework;
using GravityWells.Core;
using GravityWells.Services;

namespace GravityWells.Tests
{
    public class AdPoolPlannerTests
    {
        [Test]
        public void AnEmptyPool_StartsALoad_ThenWaitsForItsAnswer()
        {
            var pool = new AdPoolPlanner(2);

            Assert.IsTrue(pool.ShouldStartLoad(0f));
            pool.OnLoadStarted();

            Assert.IsFalse(pool.ShouldStartLoad(0f), "only one load is ever in flight");
            Assert.AreEqual(1, pool.InFlight);
        }

        [Test]
        public void ASuccessfulLoad_IsKept_AndTheNextOneStarts_UntilThePoolIsFull()
        {
            var pool = new AdPoolPlanner(2);

            pool.OnLoadStarted();
            Assert.IsTrue(pool.OnLoaded());
            Assert.AreEqual(1, pool.Ready);
            Assert.IsTrue(pool.ShouldStartLoad(0f), "there is room for a second");

            pool.OnLoadStarted();
            Assert.IsTrue(pool.OnLoaded());
            Assert.IsTrue(pool.IsFull);
            Assert.IsFalse(pool.ShouldStartLoad(0f), "a full pool asks for nothing more");
        }

        [Test]
        public void LoadedPlusInFlight_NeverExceedsTheCapacity()
        {
            var pool = new AdPoolPlanner(2);
            var random = new System.Random(3);

            for (int step = 0; step < 5000; step++)
            {
                float now = step;
                switch (random.Next(4))
                {
                    case 0:
                        if (pool.ShouldStartLoad(now)) { pool.OnLoadStarted(); }
                        break;
                    case 1:
                        if (pool.InFlight > 0) { pool.OnLoaded(); }
                        break;
                    case 2:
                        if (pool.InFlight > 0) { pool.OnLoadFailed(now); }
                        break;
                    default:
                        if (pool.Ready > 0) { pool.OnConsumed(); }
                        break;
                }

                Assert.LessOrEqual(pool.Ready + pool.InFlight, pool.Capacity, "step " + step);
                Assert.LessOrEqual(pool.InFlight, 1, "step " + step);
                Assert.GreaterOrEqual(pool.Ready, 0);
            }
        }

        [Test]
        public void AFailedLoad_WaitsBeforeTryingAgain_AndDoesNotRaceASecond()
        {
            var pool = new AdPoolPlanner(2);
            pool.OnLoadStarted();

            pool.OnLoadFailed(100f);

            Assert.AreEqual(0, pool.InFlight);
            Assert.IsFalse(pool.ShouldStartLoad(100f), "no instant retry");
            Assert.IsFalse(pool.ShouldStartLoad(100f + AdPoolPlanner.MinRetrySeconds - 0.1f));
            Assert.IsTrue(pool.ShouldStartLoad(100f + AdPoolPlanner.MinRetrySeconds));
        }

        [Test]
        public void TheWait_DoublesEachFailure_UpToACeiling_AndResetsOnSuccess()
        {
            var pool = new AdPoolPlanner(1);
            float now = 0f;
            float expectedWait = AdPoolPlanner.MinRetrySeconds;

            for (int i = 0; i < 12; i++)
            {
                pool.OnLoadStarted();
                pool.OnLoadFailed(now);
                Assert.AreEqual(now + expectedWait, pool.NextTryTime, 0.001f, "failure " + i);
                now = pool.NextTryTime;
                expectedWait = System.Math.Min(expectedWait * 2f, AdPoolPlanner.MaxRetrySeconds);
            }

            pool.OnLoadStarted();
            pool.OnLoaded();
            pool.OnConsumed();
            pool.OnLoadStarted();
            pool.OnLoadFailed(now);
            Assert.AreEqual(now + AdPoolPlanner.MinRetrySeconds, pool.NextTryTime, 0.001f, "success resets the wait");
        }

        [Test]
        public void ALateArrival_ToAFullPool_IsRejected_SoTheCallerCanDestroyIt()
        {
            var pool = new AdPoolPlanner(1);
            pool.OnLoadStarted();
            pool.OnLoaded();

            // A request answers after the pool filled some other way.
            pool.OnLoadStarted();
            bool kept = pool.OnLoaded();

            Assert.IsFalse(kept);
            Assert.AreEqual(1, pool.Ready);
            Assert.AreEqual(0, pool.InFlight);
        }

        [Test]
        public void UsingAnAd_FreesARoom_ThatIsRefilledAtOnce()
        {
            var pool = new AdPoolPlanner(2);
            pool.OnLoadStarted(); pool.OnLoaded();
            pool.OnLoadStarted(); pool.OnLoaded();

            pool.OnConsumed();

            Assert.AreEqual(1, pool.Ready);
            Assert.IsTrue(pool.ShouldStartLoad(0f));
        }

        [Test]
        public void ConsumingFromAnEmptyPool_NeverGoesNegative()
        {
            var pool = new AdPoolPlanner(2);

            pool.OnConsumed();

            Assert.AreEqual(0, pool.Ready);
        }

        [Test]
        public void CapacityBelowOne_IsRaisedToOne()
        {
            Assert.AreEqual(1, new AdPoolPlanner(0).Capacity);
        }

        // ---- the layers above the adapter pass the tick down ----

        private sealed class Counter : IAdService
        {
            public int Ticks;
            public string Name => "counter";
            public bool IsInterstitialReady => false;
            public bool IsRewardedReady => false;
            public AdLoadState RewardedState => AdLoadState.Idle;
            public AdFailure RewardedFailure => AdFailure.None;
            public void Initialize() { }
            public void Tick() => Ticks++;
            public void RequestRewarded() { }
            public void ShowInterstitial(System.Action<AdOutcome> onComplete) => onComplete(AdOutcome.NotReady);
            public void ShowRewarded(System.Action<AdOutcome> onComplete) => onComplete(AdOutcome.NotReady);
        }

        [Test]
        public void TheWaterfall_TicksEveryActiveProvider()
        {
            var a = new Counter();
            var b = new Counter();
            var registry = new AdProviderRegistry();
            registry.Register("a", () => a);
            registry.Register("b", () => b);
            var waterfall = new WaterfallAdService(registry, new[] { "a", "b" });

            waterfall.Tick();
            waterfall.Tick();

            Assert.AreEqual(2, a.Ticks);
            Assert.AreEqual(2, b.Ticks);
        }

        [Test]
        public void ASwitchedOffProvider_IsNotTicked()
        {
            var a = new Counter();
            var b = new Counter();
            var registry = new AdProviderRegistry();
            registry.Register("a", () => a);
            registry.Register("b", () => b);
            var waterfall = new WaterfallAdService(registry, new[] { "a", "b" });

            waterfall.SetOrder(new[] { "a" });
            waterfall.Tick();

            Assert.AreEqual(1, a.Ticks);
            Assert.AreEqual(0, b.Ticks);
        }
    }
}
