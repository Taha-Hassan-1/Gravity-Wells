using NUnit.Framework;
using UnityEngine;
using GravityWells.Core;

namespace GravityWells.Tests
{
    // The decisions behind the feel: which moments get a sound, how time bends on the final approach, how the
    // line eases, when the hint pulses. Pure, so none of it needs a scene.
    public class FeelLogicTests
    {
        // ---- gates lighting in the preview ----

        [Test]
        public void ALightingGate_IsReportedOnce_WithTheCountSoFar()
        {
            var tracker = new PreviewTracker();
            tracker.Reset(0, false);

            PreviewChange first = tracker.Update(0b001, false);
            Assert.AreEqual(0b001, first.NewlyLitMask);
            Assert.AreEqual(1, first.LitCount);
            Assert.AreEqual(1, first.NewlyLitCount);

            PreviewChange same = tracker.Update(0b001, false);
            Assert.AreEqual(0, same.NewlyLitMask, "a gate that stays lit is not celebrated again");

            PreviewChange second = tracker.Update(0b101, false);
            Assert.AreEqual(0b100, second.NewlyLitMask);
            Assert.AreEqual(2, second.LitCount);
        }

        [Test]
        public void AGateGoingDark_MakesNoSound_ButCanLightAgainLater()
        {
            var tracker = new PreviewTracker();
            tracker.Reset(0b011, false);

            PreviewChange dark = tracker.Update(0b001, false);
            Assert.AreEqual(0, dark.NewlyLitMask, "un-lighting plays nothing");
            Assert.AreEqual(1, dark.LitCount);

            PreviewChange again = tracker.Update(0b011, false);
            Assert.AreEqual(0b010, again.NewlyLitMask, "lighting it again is a new moment");
        }

        [Test]
        public void TwoGatesAtOnce_AreBothReported()
        {
            var tracker = new PreviewTracker();
            tracker.Reset(0, false);
            PreviewChange change = tracker.Update(0b110, false);
            Assert.AreEqual(2, change.NewlyLitCount);
            Assert.AreEqual(2, change.LitCount);
        }

        [Test]
        public void TheGoal_IsReachedOnTheFrameTheLineStartsToWin_AndNotWhileItKeepsWinning()
        {
            var tracker = new PreviewTracker();
            tracker.Reset(0b1, false);

            Assert.IsTrue(tracker.Update(0b1, true).GoalReached);
            Assert.IsFalse(tracker.Update(0b1, true).GoalReached, "the chord is not struck again while it keeps winning");
            Assert.IsFalse(tracker.Update(0b1, false).GoalReached, "losing it is silent");
            Assert.IsTrue(tracker.Update(0b1, true).GoalReached, "winning again is a new moment");
        }

        [Test]
        public void Reset_AdoptsAStateSilently()
        {
            var tracker = new PreviewTracker();
            tracker.Reset(0b111, true);
            PreviewChange change = tracker.Update(0b111, true);
            Assert.AreEqual(0, change.NewlyLitMask);
            Assert.IsFalse(change.GoalReached);
        }

        [Test]
        public void TheGateNotes_RiseUpAScale_AndNeverSoundWrong()
        {
            Assert.AreEqual(1f, GateScale.PitchFor(1), 1e-5f);
            float previous = 0f;
            for (int count = 1; count <= GameConfig.MaxGates; count++)
            {
                float pitch = GateScale.PitchFor(count);
                Assert.Greater(pitch, previous, "gate " + count + " must sing higher than the one before");
                previous = pitch;
            }
            Assert.AreEqual(GateScale.PitchFor(9), GateScale.PitchFor(20), "beyond the table it stops climbing");
            Assert.AreEqual(1f, GateScale.PitchFor(0), 1e-5f, "a count below one is treated as the first");

            // A pentatonic scale only: every note is 0, 2, 4, 7 or 9 semitones above some octave of the root.
            for (int count = 1; count <= 6; count++)
            {
                float semitones = 12f * Mathf.Log(GateScale.PitchFor(count), 2f);
                float inOctave = Mathf.Repeat(Mathf.Round(semitones), 12f);
                CollectionAssert.Contains(new[] { 0f, 2f, 4f, 7f, 9f }, inOctave, "gate " + count);
            }
        }

        // ---- the win sequence and the fail feedback ----

        [Test]
        public void TheWinSequence_PingsEachGateInTurn_AndStrikesTheStingerAfterTheLast_InsideTheHold()
        {
            for (int gates = 1; gates <= 5; gates++)
            {
                float previous = 0f;
                for (int g = 0; g < gates; g++)
                {
                    Assert.Greater(WinSequence.PingTime(g), previous);
                    previous = WinSequence.PingTime(g);
                }
                Assert.GreaterOrEqual(WinSequence.StingerTime(gates), WinSequence.PingTime(gates - 1) - 1e-5f);
                Assert.Less(WinSequence.StingerTime(gates), GameConfig.WinHoldSeconds, "before the result card appears");
            }
        }

        [Test]
        public void TheMissedGates_AreTheOnesTheFlightDidNotLight()
        {
            Assert.AreEqual(0b100, FailFeedback.MissedMask(3, 0b011));
            Assert.AreEqual(0b111, FailFeedback.MissedMask(3, 0));
            Assert.AreEqual(0, FailFeedback.MissedMask(3, 0b111));
            Assert.AreEqual(0b10, FailFeedback.MissedMask(2, 0b101), "a bit beyond the gate count is ignored");
        }

        [Test]
        public void TheHintPulses_Once_OnTheThirdFailedLaunch()
        {
            Assert.IsFalse(FailFeedback.ShouldPulseHint(1));
            Assert.IsFalse(FailFeedback.ShouldPulseHint(2));
            Assert.IsTrue(FailFeedback.ShouldPulseHint(3));
            Assert.IsFalse(FailFeedback.ShouldPulseHint(4));
            Assert.IsFalse(FailFeedback.ShouldPulseHint(10));
        }

        [Test]
        public void TheWhoosh_FollowsTheShipsSpeed()
        {
            float slow = GameConfig.LaunchSpeed * 0.5f;
            float launch = GameConfig.LaunchSpeed;
            float fast = GameConfig.LaunchSpeed * 3f;
            Assert.Less(WhooshCurve.VolumeFor(slow), WhooshCurve.VolumeFor(launch));
            Assert.Less(WhooshCurve.VolumeFor(launch), WhooshCurve.VolumeFor(fast));
            Assert.Less(WhooshCurve.PitchFor(slow), WhooshCurve.PitchFor(launch));
            Assert.Less(WhooshCurve.PitchFor(launch), WhooshCurve.PitchFor(fast));
            Assert.AreEqual(WhooshCurve.VolumeFor(fast), WhooshCurve.VolumeFor(fast * 4f), "it stops growing");
            Assert.LessOrEqual(WhooshCurve.VolumeFor(fast), 1f);
            Assert.Greater(WhooshCurve.VolumeFor(0f), 0f, "never silent while the ship moves");
        }

        // ---- the slow final approach ----

        private static Trajectory Flight(bool winning, out LevelDefinition level)
        {
            level = LevelGenerator.Generate(DifficultyCurve.SeedForLevel(20), 20);
            var t = new Trajectory();
            FlightSimulator.Simulate(level, winning ? level.SolutionWells : new Well[0], winning ? level.SolutionWells.Length : 0, t);
            return t;
        }

        private static float SecondsToFinish(Trajectory t, float frame)
        {
            var playback = new FlightPlayback();
            playback.Begin(t);
            float seconds = 0f;
            while (!playback.Finished && seconds < 60f)
            {
                playback.Advance(frame);
                seconds += frame;
            }
            return seconds;
        }

        [Test]
        public void AWinningFlight_PlaysItsLastStretchAtHalfSpeed()
        {
            Trajectory t = Flight(winning: true, out LevelDefinition _);
            Assert.AreEqual(FlightOutcome.Won, t.Outcome);

            float real = (t.Count - 1) * GameConfig.TimeStep;
            float extra = GameConfig.FinalApproachSlowSteps * GameConfig.TimeStep
                          * (1f / GameConfig.FinalApproachSpeedFactor - 1f);
            Assert.AreEqual(real + extra, SecondsToFinish(t, 1f / 240f), 0.02f);
            Assert.AreEqual(0.15f, extra, 0.001f, "the slow stretch adds a little over a tenth of a second to the watching");
        }

        [Test]
        public void ADeadFlight_PlaysAtFullSpeedThroughout()
        {
            Trajectory t = Flight(winning: false, out LevelDefinition _);
            Assert.AreNotEqual(FlightOutcome.Won, t.Outcome);
            Assert.AreEqual((t.Count - 1) * GameConfig.TimeStep, SecondsToFinish(t, 1f / 240f), 0.02f);
        }

        [Test]
        public void TheSlowStretch_IsTheSameWhateverTheFrameLength()
        {
            Trajectory t = Flight(winning: true, out LevelDefinition _);
            float fine = SecondsToFinish(t, 1f / 240f);
            Assert.AreEqual(fine, SecondsToFinish(t, 1f / 60f), 0.03f);
            Assert.AreEqual(fine, SecondsToFinish(t, 0.1f), 0.12f, "even a long frame that straddles the boundary lands in the right place");
        }

        [Test]
        public void TheSlowStretch_NeverMovesTheShipOffTheBuffer_AndReportsItself()
        {
            Trajectory t = Flight(winning: true, out LevelDefinition _);
            var playback = new FlightPlayback();
            playback.Begin(t);
            bool sawSlow = false;
            float lastSpeed = float.MaxValue;
            while (!playback.Finished)
            {
                playback.Advance(1f / 60f);
                int step = Mathf.Min(playback.Step, t.Count - 2);
                Assert.Less(GeometryUtils.SqrDistancePointToSegment(playback.Position, t.Points[step], t.Points[step + 1]), 1e-9f);
                sawSlow |= playback.InFinalApproach;
                lastSpeed = playback.Velocity;
            }
            Assert.IsTrue(sawSlow);
            Assert.Greater(lastSpeed, 0f, "the buffer's own speed is reported for the whoosh");
        }

        // ---- the line easing ----

        private static Trajectory Straight(float x, int count)
        {
            var level = new LevelDefinition(
                1, 1, new Vector2(x, 0.2f), new Vector2(0f, GameConfig.LaunchSpeed), null, new Vector2(-9f, -9f), 0.05f, null, 1, 0, null);
            var t = new Trajectory();
            FlightSimulator.Simulate(level, new Well[0], 0, t);
            Assert.GreaterOrEqual(t.Count, count);
            return t;
        }

        [Test]
        public void TheEase_StartsOnTheOldLine_AndLandsExactlyOnTheNewOne()
        {
            Trajectory oldLine = Straight(0.2f, 50);
            var ease = new PathEase();
            ease.Capture(oldLine);

            Trajectory newLine = Straight(0.8f, 50);
            ease.Begin(newLine);

            Assert.IsTrue(ease.Active);
            Assert.AreEqual(0.2f, ease.Points[10].x, 1e-5f, "the first frame is still the old line");

            ease.Tick(GameConfig.PathEaseSeconds * 0.5f);
            float middle = ease.Points[10].x;
            Assert.Greater(middle, 0.2f);
            Assert.Less(middle, 0.8f);
            Assert.Greater(middle, 0.5f, "an ease-out is past halfway at half the time");

            ease.Tick(GameConfig.PathEaseSeconds);
            Assert.IsFalse(ease.Active);
            Assert.AreEqual(0.8f, ease.Points[10].x, 1e-5f);
            Assert.AreEqual(newLine.Count, ease.Count);
        }

        [Test]
        public void TheEase_UsesTheNewLinesLength_WhenItIsLongerOrShorter()
        {
            var ease = new PathEase();
            ease.Capture(Straight(0.2f, 50));
            var shortLevel = new LevelDefinition(
                1, 1, new Vector2(0.5f, 1.4f), new Vector2(0f, GameConfig.LaunchSpeed), null, new Vector2(-9f, -9f), 0.05f, null, 1, 0, null);
            var shortLine = new Trajectory();
            FlightSimulator.Simulate(shortLevel, new Well[0], 0, shortLine);
            ease.Begin(shortLine);
            Assert.AreEqual(shortLine.Count, ease.Count);
            ease.Tick(1f);
            Assert.AreEqual(shortLine.Points[shortLine.Count - 1], ease.Points[ease.Count - 1]);
        }

        [Test]
        public void TheEaseCurve_RunsFromZeroToOne_AndOnlyForward()
        {
            Assert.AreEqual(0f, PathEase.Curve(0f), 1e-6f);
            Assert.AreEqual(1f, PathEase.Curve(1f), 1e-6f);
            float previous = -1f;
            for (float t = 0f; t <= 1f; t += 0.05f)
            {
                Assert.GreaterOrEqual(PathEase.Curve(t), previous);
                previous = PathEase.Curve(t);
            }
        }

        [Test]
        public void TheEase_IsOnlyAPicture_AndCanBeCancelled()
        {
            var ease = new PathEase();
            ease.Capture(Straight(0.2f, 50));
            Trajectory replacement = Straight(0.8f, 50);
            ease.Begin(replacement);
            ease.Cancel();
            Assert.IsFalse(ease.Active);
            Assert.IsFalse(ease.Tick(0.1f));
            Assert.AreEqual(0.8f, replacement.Points[10].x, 1e-5f, "the real buffer was never touched");
        }
    }
}
