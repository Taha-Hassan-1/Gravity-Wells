using NUnit.Framework;
using UnityEngine;
using GravityWells.Core;

namespace GravityWells.Tests
{
    public class FlightSimulatorTests
    {
        private static readonly Well[] NoWells = new Well[0];

        // A level with the ship launched straight up the middle unless told otherwise.
        private static LevelDefinition Level(
            Vector2? start = null, Vector2? velocity = null, Gate[] gates = null, Vector2? goal = null,
            float goalRadius = 0.05f, Asteroid[] asteroids = null)
        {
            return new LevelDefinition(
                seed: 1, level: 1,
                start: start ?? new Vector2(0.5f, 0.2f),
                launchVelocity: velocity ?? new Vector2(0f, GameConfig.LaunchSpeed),
                gates: gates,
                goal: goal ?? new Vector2(-10f, -10f), // out of reach unless a test places it
                goalRadius: goalRadius,
                asteroids: asteroids,
                par: 1, repulsorBudget: 0, solutionWells: null);
        }

        private static Trajectory Fly(LevelDefinition level, params Well[] wells)
        {
            var trajectory = new Trajectory();
            FlightSimulator.Simulate(level, wells, wells.Length, trajectory);
            return trajectory;
        }

        // ---- motion ----

        [Test]
        public void NoWells_FliesAStraightLineAtConstantSpeed()
        {
            var velocity = new Vector2(0.12f, 0.3f);
            Trajectory t = Fly(Level(start: new Vector2(0.2f, 0.1f), velocity: velocity));

            Assert.Greater(t.Count, 100);
            for (int i = 0; i < t.Count; i++)
            {
                Vector2 expected = new Vector2(0.2f, 0.1f) + velocity * (i * GameConfig.TimeStep);
                Assert.AreEqual(expected.x, t.Points[i].x, 1e-4f, "x at step " + i);
                Assert.AreEqual(expected.y, t.Points[i].y, 1e-4f, "y at step " + i);
            }
        }

        [Test]
        public void AnAttractorBesideThePath_BendsThePathTowardIt()
        {
            Trajectory t = Fly(Level(), new Well(new Vector2(0.75f, 0.7f)));
            Assert.Greater(t.Points[t.Count - 1].x, 0.55f);
        }

        [Test]
        public void ARepulsorBesideThePath_BendsThePathAway()
        {
            Trajectory t = Fly(Level(), new Well(new Vector2(0.75f, 0.7f), WellKind.Repulsor));
            Assert.Less(t.Points[t.Count - 1].x, 0.45f);
        }

        [Test]
        public void MirroringTheSetup_MirrorsTheTrajectory()
        {
            LevelDefinition right = Level(start: new Vector2(0.4f, 0.2f), velocity: new Vector2(0.05f, 0.34f));
            LevelDefinition left = Level(start: new Vector2(0.6f, 0.2f), velocity: new Vector2(-0.05f, 0.34f));
            Trajectory a = Fly(right, new Well(new Vector2(0.7f, 0.8f)), new Well(new Vector2(0.2f, 1.1f), WellKind.Repulsor));
            Trajectory b = Fly(left, new Well(new Vector2(0.3f, 0.8f)), new Well(new Vector2(0.8f, 1.1f), WellKind.Repulsor));

            Assert.AreEqual(a.Count, b.Count);
            Assert.AreEqual(a.Outcome, b.Outcome);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(1f - a.Points[i].x, b.Points[i].x, 1e-4f, "x at step " + i);
                Assert.AreEqual(a.Points[i].y, b.Points[i].y, 1e-4f, "y at step " + i);
            }
        }

        [Test]
        public void Energy_DriftsLessThanTwoPercent_OnAPathThatKeepsItsDistance()
        {
            var well = new Vector2(0.72f, 0.75f);
            Trajectory t = Fly(Level(), new Well(well));

            float softeningSqr = GameConfig.Softening * GameConfig.Softening;
            float first = 0f, lowest = float.MaxValue, highest = float.MinValue;
            for (int i = 1; i < t.Count; i++)
            {
                Assert.Greater(Vector2.Distance(t.Points[i], well), 0.1f, "the test path must stay clear of the well");

                // Speed from the step just taken; potential at its midpoint.
                Vector2 velocity = (t.Points[i] - t.Points[i - 1]) / GameConfig.TimeStep;
                Vector2 mid = (t.Points[i] + t.Points[i - 1]) * 0.5f;
                float energy = 0.5f * velocity.sqrMagnitude
                    - GameConfig.WellStrength / Mathf.Sqrt((well - mid).sqrMagnitude + softeningSqr);
                if (i == 1)
                {
                    first = energy;
                }
                lowest = Mathf.Min(lowest, energy);
                highest = Mathf.Max(highest, energy);
            }

            // Measured against the kinetic energy at launch, so a total near zero cannot hide a drift.
            float scale = 0.5f * GameConfig.LaunchSpeed * GameConfig.LaunchSpeed;
            Assert.Less((highest - first) / scale, 0.02f);
            Assert.Less((first - lowest) / scale, 0.02f);
        }

        // ---- gates ----

        private static readonly Gate GateAcrossThePath = new Gate(new Vector2(0.4f, 0.6f), new Vector2(0.6f, 0.6f));

        [Test]
        public void AGate_IsLitWhenCrossedFromEitherSide()
        {
            Trajectory up = Fly(Level(gates: new[] { GateAcrossThePath }));
            Assert.AreEqual(1, up.GatesLitCount);
            Assert.AreEqual(1, up.GatesLitMask);

            LevelDefinition downward = Level(
                start: new Vector2(0.5f, 1.2f), velocity: new Vector2(0f, -GameConfig.LaunchSpeed), gates: new[] { GateAcrossThePath });
            Assert.AreEqual(1, Fly(downward).GatesLitCount);
        }

        [Test]
        public void AGate_RecordsTheStepThatCrossedIt()
        {
            Trajectory t = Fly(Level(gates: new[] { GateAcrossThePath }));
            int step = t.GateLitStep(0);

            Assert.Greater(step, 0);
            Assert.LessOrEqual(t.Points[step - 1].y, 0.6f);
            Assert.GreaterOrEqual(t.Points[step].y, 0.6f);
            Assert.AreEqual(step, t.LastGateLitStep);
        }

        [Test]
        public void AGate_IsNotLitByAPathBesideIt_OnThePostsExtension()
        {
            var beside = new Gate(new Vector2(0.6f, 0.6f), new Vector2(0.8f, 0.6f));
            Trajectory t = Fly(Level(gates: new[] { beside }));

            Assert.AreEqual(0, t.GatesLitCount);
            Assert.AreEqual(-1, t.GateLitStep(0));
        }

        [Test]
        public void AGate_IsNotLitByAPathThatOnlyTouchesAPost()
        {
            var postOnThePath = new Gate(new Vector2(0.5f, 0.6f), new Vector2(0.7f, 0.6f));
            Assert.AreEqual(0, Fly(Level(gates: new[] { postOnThePath })).GatesLitCount);
        }

        [Test]
        public void AShipLandingExactlyOnTheGateLine_StillLightsIt()
        {
            // Steps of exactly 1/64 land on y = 0.5 precisely; a test with a tolerance would miss this.
            var gate = new Gate(new Vector2(0.4f, 0.5f), new Vector2(0.6f, 0.5f));
            LevelDefinition level = Level(
                start: new Vector2(0.5f, 0.25f), velocity: new Vector2(0f, GameConfig.StepsPerSecond / 64f), gates: new[] { gate });

            Trajectory t = Fly(level);

            bool landedOnTheLine = false;
            for (int i = 0; i < t.Count; i++)
            {
                landedOnTheLine |= t.Points[i].y == 0.5f;
            }
            Assert.IsTrue(landedOnTheLine, "the setup must put a point exactly on the gate line");
            Assert.AreEqual(1, t.GatesLitCount);
        }

        [Test]
        public void AtTenTimesLaunchSpeed_TheGateStillLights_AndTheGoalStillCounts()
        {
            LevelDefinition level = Level(
                velocity: new Vector2(0f, GameConfig.LaunchSpeed * 10f),
                gates: new[] { GateAcrossThePath }, goal: new Vector2(0.5f, 1.0f), goalRadius: 0.01f);

            Trajectory t = Fly(level);

            Assert.AreEqual(1, t.GatesLitCount);
            Assert.AreEqual(FlightOutcome.Won, t.Outcome);
        }

        [Test]
        public void GatesCanBeLitInAnyOrder_AndStayLit()
        {
            var far = new Gate(new Vector2(0.4f, 1.0f), new Vector2(0.6f, 1.0f));
            Trajectory t = Fly(Level(gates: new[] { far, GateAcrossThePath }));

            Assert.AreEqual(2, t.GatesLitCount);
            Assert.AreEqual(3, t.GatesLitMask);
            Assert.Less(t.GateLitStep(1), t.GateLitStep(0), "the nearer gate was listed second but lit first");
        }

        // ---- goal ----

        [Test]
        public void TheGoal_DoesNothingUntilEveryGateIsLit()
        {
            // The goal lies on the path before the gate, so the ship flies over it unlit and never returns.
            var gateAfterGoal = new Gate(new Vector2(0.4f, 1.0f), new Vector2(0.6f, 1.0f));
            Trajectory t = Fly(Level(gates: new[] { gateAfterGoal }, goal: new Vector2(0.5f, 0.6f)));

            Assert.AreEqual(1, t.GatesLitCount);
            Assert.AreNotEqual(FlightOutcome.Won, t.Outcome);
        }

        [Test]
        public void TheGoal_EndsTheFlightWhereTheShipReachesIt()
        {
            Trajectory t = Fly(Level(gates: new[] { GateAcrossThePath }, goal: new Vector2(0.5f, 1.0f), goalRadius: 0.05f));

            Assert.AreEqual(FlightOutcome.Won, t.Outcome);
            Assert.AreEqual(0.95f, t.Points[t.Count - 1].y, 0.01f);
        }

        // ---- the five outcomes ----

        [Test]
        public void Outcome_Won()
        {
            Assert.AreEqual(FlightOutcome.Won, Fly(Level(goal: new Vector2(0.5f, 1.0f))).Outcome);
        }

        [Test]
        public void Outcome_LeftField_OnlyPastTheMargin()
        {
            Trajectory t = Fly(Level());

            Assert.AreEqual(FlightOutcome.LeftField, t.Outcome);
            Assert.Greater(t.Points[t.Count - 1].y, GameConfig.FieldHeight + GameConfig.FieldMargin);
            Assert.LessOrEqual(t.Points[t.Count - 2].y, GameConfig.FieldHeight + GameConfig.FieldMargin);
        }

        [Test]
        public void Outcome_HitWell_ForEitherKind()
        {
            Assert.AreEqual(FlightOutcome.HitWell, Fly(Level(), new Well(new Vector2(0.5f, 0.8f))).Outcome);

            // Too close to be pushed aside in time: the first step already sweeps through the core.
            Vector2 justAhead = new Vector2(0.5f, 0.2f + GameConfig.WellCoreRadius);
            Assert.AreEqual(FlightOutcome.HitWell, Fly(Level(), new Well(justAhead, WellKind.Repulsor)).Outcome);
        }

        [Test]
        public void Outcome_HitAsteroid()
        {
            Trajectory t = Fly(Level(asteroids: new[] { new Asteroid(new Vector2(0.52f, 0.8f), 0.04f) }));

            Assert.AreEqual(FlightOutcome.HitAsteroid, t.Outcome);
            Assert.Less(t.Points[t.Count - 1].y, 0.8f);
        }

        [Test]
        public void Outcome_TimedOut_FillsTheBufferExactly()
        {
            // Too slow to reach an edge in the time allowed.
            Trajectory t = Fly(Level(velocity: new Vector2(0f, 0.01f)));

            Assert.AreEqual(FlightOutcome.TimedOut, t.Outcome);
            Assert.AreEqual(GameConfig.TrajectoryCapacity, t.Count);
            Assert.AreEqual(961, GameConfig.TrajectoryCapacity);
        }

        [Test]
        public void TheLastPointIsAlwaysRecorded_SoThePathDrawsToWhereItEnded()
        {
            Trajectory t = Fly(Level(), new Well(new Vector2(0.5f, 0.8f)));
            float distance = Vector2.Distance(t.Points[t.Count - 1], new Vector2(0.5f, 0.8f));
            Assert.LessOrEqual(distance, GameConfig.WellCoreRadius + 0.01f);
        }

        [Test]
        public void ABufferIsReusable_AndForgetsThePreviousFlight()
        {
            var trajectory = new Trajectory();
            LevelDefinition withGate = Level(gates: new[] { GateAcrossThePath }, goal: new Vector2(0.5f, 1.0f));
            FlightSimulator.Simulate(withGate, NoWells, 0, trajectory);
            Assert.AreEqual(FlightOutcome.Won, trajectory.Outcome);

            FlightSimulator.Simulate(Level(), NoWells, 0, trajectory);
            Assert.AreEqual(FlightOutcome.LeftField, trajectory.Outcome);
            Assert.AreEqual(0, trajectory.GatesLitMask);
            Assert.AreEqual(0, trajectory.GatesLitCount);
            Assert.AreEqual(-1, trajectory.GateLitStep(0));
        }

        // ---- cost ----

        [Test]
        public void Simulate_AllocatesNothing()
        {
            LevelDefinition level = HeavyLevel(out Well[] wells);
            var trajectory = new Trajectory();
            FlightSimulator.Simulate(level, wells, wells.Length, trajectory); // warm up the JIT

            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 50; i++)
            {
                FlightSimulator.Simulate(level, wells, wells.Length, trajectory);
            }
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0, allocated);
        }

        [Test]
        public void AnEightSecondFlight_WithFourWellsAndFourAsteroids_TakesUnderAQuarterMillisecond()
        {
            LevelDefinition level = HeavyLevel(out Well[] wells);
            var trajectory = new Trajectory();
            FlightSimulator.Simulate(level, wells, wells.Length, trajectory);
            Assert.AreEqual(FlightOutcome.TimedOut, trajectory.Outcome, "the benchmark flight must last the full eight seconds");

            const int runs = 400;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < runs; i++)
            {
                FlightSimulator.Simulate(level, wells, wells.Length, trajectory);
            }
            watch.Stop();

            double milliseconds = watch.Elapsed.TotalMilliseconds / runs;
            UnityEngine.Debug.Log("[Perf] 8 s flight, 4 wells, 4 asteroids, 5 gates: " + TestTiming.Describe(milliseconds));
            Assert.Less(milliseconds, TestTiming.Budget(0.25), "average per flight");
        }

        // A slow ship drifting in the balanced middle of four far-off wells: it never ends early, so every
        // step pays for four wells, four asteroids and five gates.
        private static LevelDefinition HeavyLevel(out Well[] wells)
        {
            wells = new[]
            {
                new Well(new Vector2(0.1f, 0.1f)), new Well(new Vector2(0.9f, 0.1f)),
                new Well(new Vector2(0.1f, 1.4f)), new Well(new Vector2(0.9f, 1.4f)),
            };
            var asteroids = new[]
            {
                new Asteroid(new Vector2(0.15f, 0.75f), 0.04f), new Asteroid(new Vector2(0.85f, 0.75f), 0.04f),
                new Asteroid(new Vector2(0.5f, 0.2f), 0.04f), new Asteroid(new Vector2(0.5f, 1.3f), 0.04f),
            };
            var gates = new Gate[5];
            for (int i = 0; i < gates.Length; i++)
            {
                float y = 1.0f + 0.05f * i;
                gates[i] = new Gate(new Vector2(0.7f, y), new Vector2(0.8f, y));
            }
            return new LevelDefinition(
                1, 1, new Vector2(0.5f, 0.75f), new Vector2(0.002f, 0.001f), gates, new Vector2(0.5f, 1.45f), 0.04f,
                asteroids, par: 4, repulsorBudget: 0, solutionWells: null);
        }
    }
}
