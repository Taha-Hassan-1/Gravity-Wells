using NUnit.Framework;
using GravityWells.Core;

namespace GravityWells.Tests
{
    // Which Android effect each moment plays, per Android version. The feel itself can only be
    // judged on a phone; these keep the choice from drifting and from using an effect a version lacks.
    public class HapticPlanTests
    {
        private static readonly HapticKind[] Kinds = (HapticKind[])System.Enum.GetValues(typeof(HapticKind));

        [Test]
        public void Tap_UsesTheKeyboardEffect()
        {
            Assert.AreEqual(HapticPlan.KeyboardTap, HapticPlan.FeedbackConstant(HapticKind.Tap, 34, strong: false));
            Assert.AreEqual(HapticPlan.KeyboardTap, HapticPlan.FeedbackConstant(HapticKind.Tap, 25, strong: false));
        }

        [Test]
        public void Grab_UsesTheNewestEffectThePhoneHas()
        {
            Assert.AreEqual(HapticPlan.DragStart, HapticPlan.FeedbackConstant(HapticKind.Grab, 34, false));
            Assert.AreEqual(HapticPlan.GestureStart, HapticPlan.FeedbackConstant(HapticKind.Grab, 33, false));
            Assert.AreEqual(HapticPlan.GestureStart, HapticPlan.FeedbackConstant(HapticKind.Grab, 30, false));
            Assert.AreEqual(HapticPlan.VirtualKey, HapticPlan.FeedbackConstant(HapticKind.Grab, 29, false));
        }

        [Test]
        public void Solve_UsesConfirmWhereItExists()
        {
            Assert.AreEqual(HapticPlan.Confirm, HapticPlan.FeedbackConstant(HapticKind.Solve, 30, false));
            Assert.AreEqual(HapticPlan.LongPress, HapticPlan.FeedbackConstant(HapticKind.Solve, 29, false));
        }

        // The game's minimum is API 25: nothing chosen there may need a newer Android.
        [Test]
        public void OnTheOldestSupportedAndroid_OnlyEffectsItHasAreChosen([ValueSource(nameof(Kinds))] HapticKind kind)
        {
            foreach (bool strong in new[] { false, true })
            {
                int constant = HapticPlan.FeedbackConstant(kind, 25, strong);
                Assert.LessOrEqual(constant, HapticPlan.ContextClick, kind + (strong ? " strong" : string.Empty));
            }
        }

        [Test]
        public void Strong_ChangesEveryMomentButTheSolve([ValueSource(nameof(Kinds))] HapticKind kind)
        {
            if (kind == HapticKind.Solve)
            {
                Assert.Pass("the solve is already the heaviest effect");
            }
            Assert.AreNotEqual(
                HapticPlan.FeedbackConstant(kind, 34, strong: false), HapticPlan.FeedbackConstant(kind, 34, strong: true));
        }

        [Test]
        public void Fallback_IsNeverWeakerWhenStrong([ValueSource(nameof(Kinds))] HapticKind kind)
        {
            Assert.GreaterOrEqual(HapticPlan.FallbackMillis(kind, true), HapticPlan.FallbackMillis(kind, false));
            Assert.GreaterOrEqual(HapticPlan.FallbackAmplitude(kind, true), HapticPlan.FallbackAmplitude(kind, false));
            Assert.That(HapticPlan.FallbackAmplitude(kind, false), Is.InRange(1, 255));
        }

        [Test]
        public void Crossings_ArePaced_SoASweepIsTicksNotABuzz()
        {
            Assert.IsTrue(HapticPlan.IsDue(HapticKind.Cross, now: 10f, lastCrossTime: -1f));
            Assert.IsFalse(HapticPlan.IsDue(HapticKind.Cross, 10.01f, 10f));
            Assert.IsTrue(HapticPlan.IsDue(HapticKind.Cross, 10f + HapticPlan.CrossMinIntervalSeconds, 10f));
        }

        [Test]
        public void OtherMoments_AreNeverHeldBack()
        {
            Assert.IsTrue(HapticPlan.IsDue(HapticKind.Grab, 10.001f, 10f));
            Assert.IsTrue(HapticPlan.IsDue(HapticKind.Tap, 10.001f, 10f));
            Assert.IsTrue(HapticPlan.IsDue(HapticKind.Solve, 10.001f, 10f));
        }

        // ---- the build's manifest edit ----

        private const string Vibrate = "android.permission.VIBRATE";

        [Test]
        public void Manifest_WithoutThePermission_GetsItBeforeApplication()
        {
            string manifest = "<manifest>\n  <uses-permission android:name=\"android.permission.INTERNET\" />\n  <application />\n</manifest>";

            string patched = ManifestPatch.AddPermission(manifest, Vibrate);

            StringAssert.Contains("<uses-permission android:name=\"android.permission.VIBRATE\" />", patched);
            Assert.Less(patched.IndexOf(Vibrate), patched.IndexOf("<application"));
        }

        [Test]
        public void Manifest_AlreadyHavingThePermission_IsLeftAlone()
        {
            string manifest = "<manifest><uses-permission android:name=\"android.permission.VIBRATE\" /><application /></manifest>";

            Assert.IsNull(ManifestPatch.AddPermission(manifest, Vibrate));
        }

        [Test]
        public void Manifest_WithNoApplication_IsLeftAlone()
        {
            Assert.IsNull(ManifestPatch.AddPermission("<manifest></manifest>", Vibrate));
            Assert.IsNull(ManifestPatch.AddPermission(string.Empty, Vibrate));
        }
    }
}
