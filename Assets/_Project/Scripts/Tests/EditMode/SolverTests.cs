using NUnit.Framework;
using UnityEngine;
using GravityWells.Core;

namespace GravityWells.Tests
{
    public class SolverTests
    {
        private const int MaxEvaluations = 20000;

        [Test]
        [Category("Slow")]
        public void OnLevels1To100_ItWinsAtLeast95Percent_WithoutTheSolution()
        {
            var wells = new Well[GameConfig.MaxWells];
            var scratch = new Trajectory();
            int solved = 0;
            long flights = 0;
            var failed = new System.Text.StringBuilder();

            for (int number = 1; number <= 100; number++)
            {
                LevelDefinition level = LevelGenerator.Generate(DifficultyCurve.SeedForLevel(number), number);
                SolverResult result = Solver.Solve(level, number, MaxEvaluations, wells, scratch);
                flights += result.Evaluations;

                if (!result.Solved)
                {
                    failed.Append(number).Append(' ');
                    continue;
                }
                solved++;

                // The layout it leaves behind really wins, inside the rules a player is held to.
                FlightSimulator.Simulate(level, wells, result.WellCount, scratch);
                Assert.AreEqual(FlightOutcome.Won, scratch.Outcome, "level " + number);
                Assert.LessOrEqual(result.WellCount, level.WellBudget, "level " + number);
                int repulsors = 0;
                for (int w = 0; w < result.WellCount; w++)
                {
                    Assert.IsTrue(WellRules.IsLegal(level, wells[w].Position), "level " + number);
                    repulsors += wells[w].Kind == WellKind.Repulsor ? 1 : 0;
                }
                Assert.LessOrEqual(repulsors, level.RepulsorBudget, "level " + number);
            }

            Debug.Log($"[Solver] campaign levels 1-100: solved {solved}, {flights / 100} flights on average, failed: {failed}");
            Assert.GreaterOrEqual(solved, 95, "unsolved levels: " + failed);
        }

        [Test]
        public void TheSameSeed_SolvesTheSameWay()
        {
            LevelDefinition level = LevelGenerator.Generate(DifficultyCurve.SeedForLevel(20), 20);
            var first = new Well[GameConfig.MaxWells];
            var second = new Well[GameConfig.MaxWells];

            SolverResult a = Solver.Solve(level, 7, MaxEvaluations, first, new Trajectory());
            SolverResult b = Solver.Solve(level, 7, MaxEvaluations, second, new Trajectory());

            Assert.AreEqual(a.Solved, b.Solved);
            Assert.AreEqual(a.Evaluations, b.Evaluations);
            Assert.AreEqual(a.WellCount, b.WellCount);
            for (int w = 0; w < a.WellCount; w++)
            {
                Assert.AreEqual(first[w].Position, second[w].Position);
            }
        }

        [Test]
        public void ItStopsAtTheEvaluationLimit()
        {
            LevelDefinition level = LevelGenerator.Generate(DifficultyCurve.SeedForLevel(90), 90);
            SolverResult result = Solver.Solve(level, 1, 5);

            Assert.LessOrEqual(result.Evaluations, 6);
            Assert.GreaterOrEqual(result.WellCount, 1);
        }

        [Test]
        public void ItDoesNotStartFromTheStoredSolution()
        {
            // Its first layout is one seeded well; if it had peeked it would win on flight one.
            int wonAtOnce = 0;
            for (int number = 20; number < 60; number++)
            {
                LevelDefinition level = LevelGenerator.Generate(DifficultyCurve.SeedForLevel(number), number);
                SolverResult result = Solver.Solve(level, number, 1);
                wonAtOnce += result.Solved ? 1 : 0;
            }
            Assert.AreEqual(0, wonAtOnce);
        }
    }
}
