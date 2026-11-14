using NUnit.Framework;
using UnityEngine;
using GravityWells.Core;

namespace GravityWells.Tests
{
    public class FlightPlaybackTests
    {
        private static Trajectory Solved(out LevelDefinition level)
        {
            level = LevelGenerator.Generate(DifficultyCurve.SeedForLevel(40), 40);
            var trajectory = new Trajectory();
            FlightSimulator.Simulate(level, level.SolutionWells, level.SolutionWells.Length, trajectory);
            Assert.AreEqual(FlightOutcome.Won, trajectory.Outcome);
            return trajectory;
        }

        // The ship is on the buffer segment its step number names: "every ship position came from the buffer".
        private static void AssertOnTheBuffer(Trajectory t, FlightPlayback playback)
        {
            int step = Mathf.Min(playback.Step, t.Count - 2);
            Vector2 a = t.Points[step];
            Vector2 b = t.Points[step + 1];
            float sqr = GeometryUtils.SqrDistancePointToSegment(playback.Position, a, b);
            Assert.Less(sqr, 1e-10f, $"step {step}");
        }

        [Test]
        public void TheShip_AlwaysStaysOnTheBufferTheShownLineCameFrom()
        {
            Trajectory t = Solved(out LevelDefinition _);
            var playback = new FlightPlayback();
            playback.Begin(t);

            var rng = new System.Random(3);
            int frames = 0;
            while (!playback.Finished)
            {
                playback.Advance((float)rng.NextDouble() * 0.05f); // frame times from 0 to 50 ms
                AssertOnTheBuffer(t, playback);
                frames++;
                if (frames == 40)
                {
                    playback.SpeedUp();
                }
            }

            Assert.AreEqual(t.Points[t.Count - 1], playback.Position);
            Assert.AreEqual(t.Outcome, playback.Outcome);
        }

        [Test]
        public void PlaybackStartsAtTheStart_AndTakesAsLongAsTheFlight()
        {
            Trajectory t = Solved(out LevelDefinition level);
            var playback = new FlightPlayback();
            playback.Begin(t);

            Assert.AreEqual(level.Start, playback.Position);
            Assert.IsFalse(playback.Finished);

            float seconds = (t.Count - 1) * GameConfig.TimeStep;
            playback.Advance(seconds * 0.5f);
            Assert.IsFalse(playback.Finished);
            Assert.AreEqual((t.Count - 1) / 2, playback.Step, 1);
            playback.Advance(seconds * 0.5f + 0.2f); // a winning flight also plays its last stretch slowly (+0.15 s)
            Assert.IsTrue(playback.Finished);
        }

        [Test]
        public void ATap_SpeedsTheRestOfTheFlightUpToThreeTimes()
        {
            Trajectory t = Solved(out LevelDefinition _);
            var slow = new FlightPlayback();
            var fast = new FlightPlayback();
            slow.Begin(t);
            fast.Begin(t);
            fast.SpeedUp();

            slow.Advance(0.5f);
            fast.Advance(0.5f);

            Assert.AreEqual(GameConfig.FastPlaybackSpeed, fast.Speed);
            Assert.AreEqual(slow.Step * 3f, fast.Step, 3f);
        }

        [Test]
        public void TheHeading_FollowsTheLine_AndIsAUnitVector()
        {
            Trajectory t = Solved(out LevelDefinition level);
            var playback = new FlightPlayback();
            playback.Begin(t);
            playback.Advance(0.001f);

            Vector2 launch = level.LaunchVelocity.normalized;
            Assert.AreEqual(1f, playback.Heading.magnitude, 1e-4f);
            Assert.Greater(Vector2.Dot(playback.Heading, launch), 0.99f);
        }

        [Test]
        public void GatesLight_AsTheShipReachesThem_NotBefore()
        {
            Trajectory t = Solved(out LevelDefinition level);
            var playback = new FlightPlayback();
            playback.Begin(t);
            Assert.AreEqual(0, playback.GatesLitSoFar());

            int previous = 0;
            while (!playback.Finished)
            {
                playback.Advance(0.02f);
                int lit = playback.GatesLitSoFar();
                Assert.GreaterOrEqual(lit, previous, "a lit gate stays lit");
                previous = lit;
            }
            Assert.AreEqual(level.Gates.Length, playback.GatesLitSoFar());
        }

        [Test]
        public void ADeadFlight_StopsWhereItEnded()
        {
            LevelDefinition level = LevelGenerator.Generate(DifficultyCurve.SeedForLevel(40), 40);
            var t = new Trajectory();
            FlightSimulator.Simulate(level, level.SolutionWells, 0, t);
            Assert.AreNotEqual(FlightOutcome.Won, t.Outcome);

            var playback = new FlightPlayback();
            playback.Begin(t);
            playback.Advance(60f);

            Assert.IsTrue(playback.Finished);
            Assert.AreEqual(t.Points[t.Count - 1], playback.Position);
        }
    }
}
