using NUnit.Framework;
using GravityWells.Core;

namespace GravityWells.Tests
{
    public class AdPolicyTests
    {
        // A player for whom every rule is satisfied; each test breaks exactly one.
        private static AdPolicyInput Allowed(
            int level = 5, int levelsSince = 3, float secondsSinceAd = 45f, float secondsSinceStart = 3f,
            bool firstSession = false, bool dragging = false, bool removed = false, bool consent = true)
        {
            return new AdPolicyInput(level, levelsSince, secondsSinceAd, secondsSinceStart, firstSession, dragging, removed, consent);
        }

        [Test]
        public void AllRulesSatisfied_AtExactlyTheThresholds_IsAllowed()
        {
            Assert.AreEqual(AdBlockReason.None, AdPolicy.EvaluateInterstitial(Allowed()));
        }

        [Test]
        public void AdsRemoved_Blocks()
        {
            Assert.AreEqual(AdBlockReason.AdsRemoved, AdPolicy.EvaluateInterstitial(Allowed(removed: true)));
        }

        [Test]
        public void NoConsent_Blocks()
        {
            Assert.AreEqual(AdBlockReason.NoConsent, AdPolicy.EvaluateInterstitial(Allowed(consent: false)));
        }

        [Test]
        public void DuringADrag_Blocks()
        {
            Assert.AreEqual(AdBlockReason.DragInProgress, AdPolicy.EvaluateInterstitial(Allowed(dragging: true)));
        }

        [Test]
        public void WithinThreeSecondsOfLevelStart_Blocks()
        {
            Assert.AreEqual(AdBlockReason.LevelJustStarted, AdPolicy.EvaluateInterstitial(Allowed(secondsSinceStart: 2.9f)));
        }

        [Test]
        public void FirstSessionBeforeLevelThree_Blocks()
        {
            Assert.AreEqual(AdBlockReason.FirstSessionTooEarly, AdPolicy.EvaluateInterstitial(Allowed(level: 2, firstSession: true)));
        }

        [Test]
        public void FirstSessionAtLevelFive_StillAllowed()
        {
            // The first-session guard stops at level 3; the level-5 floor is the binding rule.
            Assert.AreEqual(AdBlockReason.None, AdPolicy.EvaluateInterstitial(Allowed(level: 5, firstSession: true)));
        }

        [Test]
        public void BelowLevelFive_Blocks()
        {
            Assert.AreEqual(AdBlockReason.LevelTooLow, AdPolicy.EvaluateInterstitial(Allowed(level: 4)));
        }

        [Test]
        public void FewerThanThreeLevelsSinceLastAd_Blocks()
        {
            Assert.AreEqual(AdBlockReason.TooFewLevelsSinceLastAd, AdPolicy.EvaluateInterstitial(Allowed(levelsSince: 2)));
        }

        [Test]
        public void UnderFortyFiveSecondsSinceLastAd_Blocks()
        {
            Assert.AreEqual(AdBlockReason.TooSoonSinceLastAd, AdPolicy.EvaluateInterstitial(Allowed(secondsSinceAd: 44.9f)));
        }

        [Test]
        public void AdsRemoved_TakesPrecedenceOverEveryOtherReason()
        {
            AdPolicyInput input = Allowed(level: 1, levelsSince: 0, secondsSinceAd: 0f, secondsSinceStart: 0f,
                firstSession: true, dragging: true, removed: true, consent: false);

            Assert.AreEqual(AdBlockReason.AdsRemoved, AdPolicy.EvaluateInterstitial(input));
        }
    }
}
