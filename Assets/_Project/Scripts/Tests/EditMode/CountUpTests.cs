using NUnit.Framework;
using GravityWells.Core;

namespace GravityWells.Tests
{
    public class CountUpTests
    {
        [Test]
        public void SnapTo_ShowsTheValueAtOnce_AndIsNotAnimating()
        {
            var count = new CountUp();
            count.SnapTo(120);

            Assert.AreEqual(120, count.Value);
            Assert.IsFalse(count.Animating);
            Assert.IsFalse(count.Tick(0.1f));
        }

        [Test]
        public void IncreasingTarget_CountsUpAndLandsExactly()
        {
            var count = new CountUp();
            count.SnapTo(0);
            count.SetTarget(100);

            int last = 0;
            for (int i = 0; i < 200 && count.Animating; i++)
            {
                count.Tick(0.016f);
                Assert.GreaterOrEqual(count.Value, last, "must never move backwards on the way up");
                last = count.Value;
            }

            Assert.AreEqual(100, count.Value);
            Assert.IsFalse(count.Animating);
        }

        [Test]
        public void DecreasingTarget_CountsDown_WithoutOvershooting()
        {
            var count = new CountUp();
            count.SnapTo(50);
            count.SetTarget(10);

            for (int i = 0; i < 200; i++)
            {
                count.Tick(0.016f);
                Assert.GreaterOrEqual(count.Value, 10, "must not undershoot the target");
            }
            Assert.AreEqual(10, count.Value);
        }

        [Test]
        public void LargeJump_FinishesWithinTheDuration()
        {
            var count = new CountUp();
            count.SnapTo(0);
            count.SetTarget(10000);

            float elapsed = 0f;
            while (count.Animating && elapsed < 5f)
            {
                count.Tick(0.016f);
                elapsed += 0.016f;
            }

            Assert.LessOrEqual(elapsed, CountUp.DurationSeconds + 0.05f);
        }

        [Test]
        public void SmallChange_StillMovesAtAReadablePace_NotInstantlyAndNotForever()
        {
            var count = new CountUp();
            count.SnapTo(10);
            count.SetTarget(11);

            Assert.IsTrue(count.Animating);
            count.Tick(0.016f);
            Assert.AreEqual(10, count.Value, "a one-coin change must not jump in a single frame");

            for (int i = 0; i < 20; i++)
            {
                count.Tick(0.016f);
            }
            Assert.AreEqual(11, count.Value);
        }

        [Test]
        public void Retargeting_MidAnimation_ContinuesFromWhereItIs()
        {
            var count = new CountUp();
            count.SnapTo(0);
            count.SetTarget(100);
            for (int i = 0; i < 15; i++)
            {
                count.Tick(0.016f);
            }
            int midway = count.Value;
            Assert.Greater(midway, 0);
            Assert.Less(midway, 100);

            count.SetTarget(0);
            count.Tick(0.016f);

            Assert.LessOrEqual(count.Value, midway, "reversing must head back down from the current figure");
        }

        [Test]
        public void ZeroOrNegativeDelta_NeverMovesTheNumber()
        {
            var count = new CountUp();
            count.SnapTo(5);
            count.SetTarget(50);

            Assert.IsFalse(count.Tick(0f));
            Assert.IsFalse(count.Tick(-1f));
            Assert.AreEqual(5, count.Value);
        }
    }
}
