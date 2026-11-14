using NUnit.Framework;
using UnityEngine;
using GravityWells.Core;

namespace GravityWells.Tests
{
    public class GeneratorTests
    {
        private const int Seeds = 1000;
        private const int LastLevel = 300;

        private static int LevelFor(int index) => 1 + index * (LastLevel - 1) / (Seeds - 1);

        private static LevelDefinition[] _levels;

        // Generated once and shared: every test below asserts something different about the same
        // thousand levels (levels 1 to 300, a different seed for each).
        private static LevelDefinition[] Levels
        {
            get
            {
                if (_levels == null)
                {
                    _levels = new LevelDefinition[Seeds];
                    for (int i = 0; i < Seeds; i++)
                    {
                        _levels[i] = LevelGenerator.Generate(1000 + i * 7919, LevelFor(i));
                    }
                }
                return _levels;
            }
        }

        private static bool Wins(LevelDefinition level, Well[] wells, int count, Trajectory scratch)
        {
            FlightSimulator.Simulate(level, wells, count, scratch);
            return scratch.Outcome == FlightOutcome.Won;
        }

        [Test]
        public void TheSolutionWellsWin()
        {
            var scratch = new Trajectory();
            foreach (LevelDefinition level in Levels)
            {
                Assert.IsTrue(Wins(level, level.SolutionWells, level.SolutionWells.Length, scratch), Describe(level));
                Assert.AreEqual(level.Gates.Length, scratch.GatesLitCount, Describe(level));
            }
        }

        [Test]
        public void NoWellsDoNotWin()
        {
            var scratch = new Trajectory();
            foreach (LevelDefinition level in Levels)
            {
                Assert.IsFalse(Wins(level, level.SolutionWells, 0, scratch), Describe(level));
            }
        }

        [Test]
        public void RemovingAnyOneSolutionWell_DoesNotWin()
        {
            var scratch = new Trajectory();
            var fewer = new Well[GameConfig.MaxWells];
            foreach (LevelDefinition level in Levels)
            {
                Well[] solution = level.SolutionWells;
                for (int skip = 0; skip < solution.Length; skip++)
                {
                    int count = 0;
                    for (int w = 0; w < solution.Length; w++)
                    {
                        if (w != skip)
                        {
                            fewer[count++] = solution[w];
                        }
                    }
                    Assert.IsFalse(Wins(level, fewer, count, scratch), Describe(level) + " without well " + skip);
                }
            }
        }

        [Test]
        public void EverythingIsInsideTheField()
        {
            foreach (LevelDefinition level in Levels)
            {
                string what = Describe(level);
                AssertInside(level.Start, 0f, what + " start");
                AssertInside(level.Goal, level.GoalRadius, what + " goal");
                foreach (Gate gate in level.Gates)
                {
                    AssertInside(gate.A, 0f, what + " gate post");
                    AssertInside(gate.B, 0f, what + " gate post");
                }
                foreach (Asteroid asteroid in level.Asteroids)
                {
                    AssertInside(asteroid.Centre, asteroid.Radius, what + " asteroid");
                }
                foreach (Well well in level.SolutionWells)
                {
                    AssertInside(well.Position, GameConfig.WellFieldInset - 1e-5f, what + " solution well");
                }
            }
        }

        [Test]
        public void WellClearancesHold()
        {
            foreach (LevelDefinition level in Levels)
            {
                Well[] wells = level.SolutionWells;
                for (int a = 0; a < wells.Length; a++)
                {
                    Assert.IsTrue(WellRules.IsLegal(level, wells[a].Position), Describe(level));
                    for (int b = a + 1; b < wells.Length; b++)
                    {
                        Assert.GreaterOrEqual(
                            Vector2.Distance(wells[a].Position, wells[b].Position), GameConfig.MinSolutionWellSpacing - 1e-5f, Describe(level));
                    }
                    foreach (Gate gate in level.Gates)
                    {
                        float distance = Mathf.Sqrt(GeometryUtils.SqrDistancePointToSegment(wells[a].Position, gate.A, gate.B));
                        Assert.GreaterOrEqual(distance, GameConfig.GateClearance - 1e-5f, Describe(level));
                    }
                }
            }
        }

        [Test]
        public void AsteroidClearancesHold()
        {
            var scratch = new Trajectory();
            int asteroidLevels = 0;
            foreach (LevelDefinition level in Levels)
            {
                Assert.AreEqual(DifficultyCurve.PlanForLevel(level.Level).Asteroids, level.Asteroids.Length, Describe(level));
                if (level.Asteroids.Length == 0)
                {
                    continue;
                }
                asteroidLevels++;
                FlightSimulator.Simulate(level, level.SolutionWells, level.SolutionWells.Length, scratch);

                for (int a = 0; a < level.Asteroids.Length; a++)
                {
                    Asteroid asteroid = level.Asteroids[a];
                    float clear = asteroid.Radius + GameConfig.AsteroidClearance - 1e-4f;
                    string what = Describe(level) + " asteroid " + a;

                    Assert.GreaterOrEqual(Vector2.Distance(asteroid.Centre, level.Start), clear, what);
                    Assert.GreaterOrEqual(Vector2.Distance(asteroid.Centre, level.Goal), clear + level.GoalRadius, what);
                    foreach (Well well in level.SolutionWells)
                    {
                        Assert.GreaterOrEqual(Vector2.Distance(asteroid.Centre, well.Position), clear, what);
                    }
                    foreach (Gate gate in level.Gates)
                    {
                        Assert.GreaterOrEqual(Mathf.Sqrt(GeometryUtils.SqrDistancePointToSegment(asteroid.Centre, gate.A, gate.B)), clear, what);
                    }
                    for (int i = 0; i < scratch.Count; i += 2)
                    {
                        Assert.GreaterOrEqual(Vector2.Distance(asteroid.Centre, scratch.Points[i]), clear, what + " vs the solution path");
                    }
                    for (int b = a + 1; b < level.Asteroids.Length; b++)
                    {
                        Assert.Greater(
                            Vector2.Distance(asteroid.Centre, level.Asteroids[b].Centre), asteroid.Radius + level.Asteroids[b].Radius, what);
                    }
                }
            }
            Assert.Greater(asteroidLevels, 900, "most of the thousand levels are past level 12");
        }

        [Test]
        public void Par_IsTheCurvesWellCount_OrTheFixedOneForTheFirstThreeLevels()
        {
            foreach (LevelDefinition level in Levels)
            {
                Assert.AreEqual(DifficultyCurve.PlanForLevel(level.Level).Wells, level.Par, Describe(level));
                Assert.AreEqual(level.Par, level.SolutionWells.Length, Describe(level));
                Assert.AreEqual(level.Par + 2, level.WellBudget, Describe(level));
            }
        }

        [TestCase(1, 1, 1)]
        [TestCase(2, 1, 2)]
        [TestCase(3, 2, 2)]
        public void TheFirstThreeLevels_AreFixedByRule(int number, int wells, int gates)
        {
            for (int seed = 0; seed < 25; seed++)
            {
                LevelDefinition level = LevelGenerator.Generate(seed * 131 + 7, number);
                Assert.AreEqual(wells, level.Par);
                Assert.AreEqual(gates, level.Gates.Length + level.GatesDropped);
                Assert.AreEqual(0, level.Asteroids.Length);
                Assert.AreEqual(0, level.RepulsorBudget);
            }
            Assert.AreEqual(GameConfig.FirstLevelGoalRadius, LevelGenerator.Generate(5, 1).GoalRadius);
            Assert.Greater(GameConfig.FirstLevelGoalRadius, DifficultyCurve.GoalRadiusForLevel(1), "level 1 has the widest goal");
        }

        [Test]
        public void Repulsors_AppearOnlyFromTheirLevel_NeverAsTheFirstWell_AndTheBudgetCoversThem()
        {
            int withRepulsors = 0;
            foreach (LevelDefinition level in Levels)
            {
                int repulsors = 0;
                foreach (Well well in level.SolutionWells)
                {
                    repulsors += well.Kind == WellKind.Repulsor ? 1 : 0;
                }

                if (level.Level < GameConfig.RepulsorFirstLevel)
                {
                    Assert.AreEqual(0, repulsors, Describe(level));
                    Assert.AreEqual(0, level.RepulsorBudget, Describe(level));
                    continue;
                }

                Assert.AreEqual(WellKind.Attractor, level.SolutionWells[0].Kind, Describe(level));
                Assert.AreEqual(repulsors + 1, level.RepulsorBudget, Describe(level));
                withRepulsors += repulsors > 0 ? 1 : 0;
            }
            Assert.Greater(withRepulsors, 100, "repulsors do turn up");
        }

        [Test]
        public void TheLevelThatIntroducesRepulsors_HasExactlyOne_AndNothingElseHard()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                LevelDefinition level = LevelGenerator.Generate(seed * 211 + 5, GameConfig.RepulsorFirstLevel);
                int repulsors = 0;
                foreach (Well well in level.SolutionWells)
                {
                    repulsors += well.Kind == WellKind.Repulsor ? 1 : 0;
                }
                Assert.AreEqual(1, repulsors, "seed " + seed);
                Assert.AreEqual(2, level.Par, "one attractor and the repulsor");
                Assert.LessOrEqual(level.Gates.Length, 3);
                Assert.LessOrEqual(level.Asteroids.Length, 1);
                Assert.AreEqual(2, level.RepulsorBudget);
            }
        }

        [Test]
        public void TheLevelThatIntroducesAsteroids_BringsOne_OnTheWellsAndGatesOfTheLevelBefore()
        {
            LevelPlan before = DifficultyCurve.PlanForLevel(GameConfig.AsteroidFirstLevel - 1);
            LevelPlan first = DifficultyCurve.PlanForLevel(GameConfig.AsteroidFirstLevel);
            Assert.AreEqual(0, before.Asteroids);
            Assert.AreEqual(1, first.Asteroids);
            Assert.AreEqual(before.Wells, first.Wells);
            Assert.AreEqual(before.Gates, first.Gates);
        }

        [Test]
        public void TheGateDropFallback_IsRare_AndGenerateNeverThrows()
        {
            int dropped = 0;
            foreach (LevelDefinition level in Levels) // building Levels is the "never throws" half
            {
                dropped += level.GatesDropped > 0 ? 1 : 0;
                Assert.GreaterOrEqual(level.Gates.Length, 1, Describe(level));
            }
            Assert.Less(dropped, Seeds / 100, "fewer than 1% of levels may lose a gate");
        }

        [Test]
        public void GeneratedLevels_MeetTheForgivenessFloor()
        {
            foreach (LevelDefinition level in Levels)
            {
                Assert.GreaterOrEqual(level.Forgiveness, GameConfig.MinForgiveness, Describe(level));
                Assert.GreaterOrEqual(level.SolutionFlightSeconds, GameConfig.MinSolutionFlightSeconds, Describe(level));
                Assert.LessOrEqual(level.SolutionFlightSeconds, GameConfig.MaxSolutionFlightSeconds, Describe(level));
            }
        }

        [Test]
        public void Forgiveness_IsRepeatable_AndLowerForATighterLevel()
        {
            LevelDefinition level = Levels[500];
            float first = Forgiveness.Measure(level, 200, 99);
            Assert.AreEqual(first, Forgiveness.Measure(level, 200, 99));
            Assert.That(first, Is.InRange(0f, 1f));

            // The same level with a pinhole goal tolerates less error.
            var tight = new LevelDefinition(
                level.Seed, level.Level, level.Start, level.LaunchVelocity, level.Gates, level.Goal, level.GoalRadius * 0.1f,
                level.Asteroids, level.Par, level.RepulsorBudget, level.SolutionWells);
            Assert.Less(Forgiveness.Measure(tight, 200, 99), first);
        }

        [Test]
        public void ALevelNumberBelowOne_IsTreatedAsLevelOne()
        {
            Assert.AreEqual(1, LevelGenerator.Generate(3, 0).Level);
            Assert.AreEqual(1, LevelGenerator.Generate(3, -5).Level);
        }

        [Test]
        public void Generate_AveragesUnderThirtyMilliseconds()
        {
            LevelGenerator.Generate(1, 50); // warm up the JIT
            var watch = System.Diagnostics.Stopwatch.StartNew();
            const int count = 200;
            for (int i = 0; i < count; i++)
            {
                LevelGenerator.Generate(50000 + i * 31, 1 + i * (LastLevel - 1) / (count - 1));
            }
            watch.Stop();

            double milliseconds = watch.Elapsed.TotalMilliseconds / count;
            Debug.Log("[Perf] Generate, levels 1-300: " + TestTiming.Describe(milliseconds));
            Assert.Less(milliseconds, TestTiming.Budget(30.0));
        }

        private static void AssertInside(Vector2 point, float inset, string what)
        {
            Assert.That(point.x, Is.InRange(inset, GameConfig.FieldWidth - inset), what);
            Assert.That(point.y, Is.InRange(inset, GameConfig.FieldHeight - inset), what);
        }

        private static string Describe(LevelDefinition level) => $"seed {level.Seed} level {level.Level}";
    }
}
