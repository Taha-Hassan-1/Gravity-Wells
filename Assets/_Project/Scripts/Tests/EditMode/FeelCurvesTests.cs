using NUnit.Framework;
using UnityEngine;
using GravityWells.Core;

namespace GravityWells.Tests
{
    public class FeelCurvesTests
    {
        [Test]
        public void ABump_StartsAndEndsAtRest_AndPeaksInTheMiddle()
        {
            Assert.AreEqual(0f, FeelCurves.Bump(0f, 1f), 1e-6f);
            Assert.AreEqual(1f, FeelCurves.Bump(0.5f, 1f), 1e-5f);
            Assert.AreEqual(0f, FeelCurves.Bump(1f, 1f), 1e-6f);
            Assert.AreEqual(0f, FeelCurves.Bump(-1f, 1f));
            Assert.AreEqual(0f, FeelCurves.Bump(2f, 1f));
            Assert.AreEqual(0f, FeelCurves.Bump(0.5f, 0f), "a zero duration is no bump");
        }

        [Test]
        public void ABlinkOfTwo_HasTwoPeaks_AndReturnsToZeroBetweenThem()
        {
            const float duration = 1f;
            int peaks = 0;
            float previous = 0f, before = 0f;
            for (float t = 0.001f; t < duration; t += 0.001f)
            {
                float value = FeelCurves.Blink(t, duration, 2);
                if (previous > before && previous > value && previous > 0.9f)
                {
                    peaks++;
                }
                before = previous;
                previous = value;
            }
            Assert.AreEqual(2, peaks);
            Assert.AreEqual(0f, FeelCurves.Blink(0.5f, duration, 2), 1e-3f, "dark in the gap between the two blinks");
            Assert.AreEqual(0f, FeelCurves.Blink(1.5f, duration, 2));
            Assert.AreEqual(0f, FeelCurves.Blink(0.3f, duration, 0));
        }

        [Test]
        public void ASettle_StartsLarge_AndEndsExactlyAtItsSize()
        {
            Assert.AreEqual(1.4f, FeelCurves.Settle(0f, 0.3f), 1e-4f);
            Assert.AreEqual(1f, FeelCurves.Settle(0.3f, 0.3f));
            Assert.AreEqual(1f, FeelCurves.Settle(5f, 0.3f));

            float min = float.MaxValue;
            for (float t = 0f; t < 0.3f; t += 0.002f)
            {
                min = Mathf.Min(min, FeelCurves.Settle(t, 0.3f));
            }
            Assert.Less(min, 1f, "it overshoots below its size once, so it feels dropped, not shrunk");
            Assert.Greater(min, 0.85f, "and only slightly");
        }

        [Test]
        public void ARing_TravelsInOrOut_AndFadesAtBothEnds()
        {
            Assert.AreEqual(1f, FeelCurves.RingRadius(0f, inward: true), 1e-6f);
            Assert.AreEqual(0.5f, FeelCurves.RingRadius(0.5f, inward: true), 1e-6f);
            Assert.AreEqual(0f, FeelCurves.RingRadius(0f, inward: false), 1e-6f);
            Assert.AreEqual(0.75f, FeelCurves.RingRadius(0.75f, inward: false), 1e-6f);
            Assert.AreEqual(FeelCurves.RingRadius(0.25f, true), FeelCurves.RingRadius(1.25f, true), 1e-5f, "it repeats");

            Assert.AreEqual(0f, FeelCurves.RingAlpha(0f), 1e-6f);
            Assert.AreEqual(1f, FeelCurves.RingAlpha(0.5f), 1e-5f);
            Assert.AreEqual(0f, FeelCurves.RingAlpha(1f), 1e-5f);
        }

        [Test]
        public void TheChargeScale_SwellsThenReturnsToSize()
        {
            Assert.AreEqual(1f, FeelCurves.ChargeScale(0f), 1e-6f);
            Assert.Greater(FeelCurves.ChargeScale(0.85f), 1.2f);
            Assert.Less(FeelCurves.ChargeScale(1f), FeelCurves.ChargeScale(0.85f), "it eases back as the ship goes");
        }
    }
}
