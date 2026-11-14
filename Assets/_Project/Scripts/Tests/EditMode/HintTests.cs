using NUnit.Framework;
using UnityEngine;
using GravityWells.Core;

namespace GravityWells.Tests
{
    public class HintTests
    {
        private static float Score(LevelDefinition level, Well[] wells, int count, Trajectory scratch)
        {
            FlightSimulator.Simulate(level, wells, count, scratch);
            return PathScore.Evaluate(level, scratch);
        }

        // Applies a planned move the way the game would: move that well, or add an attractor.
        private static int Apply(Well[] wells, int count, int index, Vector2 position)
        {
            if (index == count)
            {
                wells[count] = new Well(position, WellKind.Attractor);
                return count + 1;
            }
            wells[index] = new Well(position, wells[index].Kind);
            return count;
        }

        [Test]
        [Category("Slow")]
        public void From1000RandomNonWinningLayouts_ThePlannedMoveNeverMakesTheScoreWorse()
        {
            var rng = new System.Random(2026);
            var wells = new Well[GameConfig.MaxWells];
            var before = new Well[GameConfig.MaxWells];
            var scratch = new Trajectory();
            int layouts = 0, improved = 0;

            while (layouts < 1000)
            {
                int number = 1 + rng.Next(120);
                LevelDefinition level = LevelGenerator.Generate(rng.Next(), number);
                int count = rng.Next(level.WellBudget + 1);
                for (int w = 0; w < count; w++)
                {
                    wells[w] = new Well(
                        WellRules.Clamp(level, new Vector2(SeededMath.Range(rng, 0f, 1f), SeededMath.Range(rng, 0f, 1.5f))));
                }

                float current = Score(level, wells, count, scratch);
                if (current <= 0f)
                {
                    continue; // already winning: not a layout that needs a hint
                }
                layouts++;
                System.Array.Copy(wells, before, wells.Length);

                bool found = HintPlanner.TryPlan(level, wells, count, rng, scratch, out int index, out Vector2 position);

                for (int w = 0; w < count; w++)
                {
                    Assert.AreEqual(before[w].Position, wells[w].Position, "the planner must leave the layout as it found it");
                }
                if (!found)
                {
                    continue;
                }

                Assert.That(index, Is.InRange(0, count));
                Assert.IsTrue(WellRules.IsLegal(level, position));
                if (index == count)
                {
                    Assert.Less(count, level.WellBudget, "a new well is only suggested when one is left");
                }

                int after = Apply(wells, count, index, position);
                Assert.Less(Score(level, wells, after, scratch), current, $"seed {level.Seed} level {number}");
                improved++;
            }

            Debug.Log($"[Hint] 1000 random layouts: a better move was found for {improved}");
            Assert.Greater(improved, 900, "a hint that rarely finds anything is not a hint");
        }

        [Test]
        [Category("Slow")]
        public void RepeatedHintsFromAnEmptyField_WinMostOfLevels1To50_Within12()
        {
            var wells = new Well[GameConfig.MaxWells];
            var scratch = new Trajectory();
            int won = 0, hintsUsed = 0;
            var failed = new System.Text.StringBuilder();

            for (int number = 1; number <= 50; number++)
            {
                LevelDefinition level = LevelGenerator.Generate(DifficultyCurve.SeedForLevel(number), number);
                var rng = new System.Random(number);
                int count = 0;
                bool solved = false;

                for (int hint = 1; hint <= 12 && !solved; hint++)
                {
                    if (!HintPlanner.TryPlan(level, wells, count, rng, scratch, out int index, out Vector2 position))
                    {
                        break;
                    }
                    count = Apply(wells, count, index, position);
                    FlightSimulator.Simulate(level, wells, count, scratch);
                    solved = scratch.Outcome == FlightOutcome.Won;
                    hintsUsed++;
                }

                if (solved)
                {
                    won++;
                }
                else
                {
                    failed.Append(number).Append(' ');
                }
            }

            Debug.Log($"[Hint] levels 1-50 from an empty field: won {won}, {hintsUsed} hints in all, failed: {failed}");
            Assert.GreaterOrEqual(won, 40, "at least 80% of levels 1-50; failed: " + failed);
        }

        [Test]
        public void AWinningLayout_GetsNoHint()
        {
            LevelDefinition level = LevelGenerator.Generate(DifficultyCurve.SeedForLevel(10), 10);
            var wells = new Well[GameConfig.MaxWells];
            System.Array.Copy(level.SolutionWells, wells, level.SolutionWells.Length);

            bool found = HintPlanner.TryPlan(
                level, wells, level.SolutionWells.Length, new System.Random(1), new Trajectory(), out int _, out Vector2 _);

            Assert.IsFalse(found);
        }

        [Test]
        public void WithTheBudgetFull_ItOnlyMovesWellsThatAreThere()
        {
            LevelDefinition level = LevelGenerator.Generate(DifficultyCurve.SeedForLevel(5), 5);
            var wells = new Well[GameConfig.MaxWells];
            int count = level.WellBudget;
            for (int w = 0; w < count; w++)
            {
                wells[w] = new Well(new Vector2(0.05f + 0.02f * w, 1.45f)); // parked in a corner, doing little
            }

            bool found = HintPlanner.TryPlan(level, wells, count, new System.Random(3), new Trajectory(), out int index, out Vector2 _);

            Assert.IsTrue(found);
            Assert.Less(index, count);
        }

        [Test]
        public void TheHint_DoesNotJumpToTheStoredSolution()
        {
            // From an empty field the first suggestion is a new well. If the planner read the solution
            // it would land on a solution well; a sampled position practically never does.
            int landedOnTheSolution = 0;
            for (int number = 1; number <= 30; number++)
            {
                LevelDefinition level = LevelGenerator.Generate(DifficultyCurve.SeedForLevel(number), number);
                var wells = new Well[GameConfig.MaxWells];
                if (!HintPlanner.TryPlan(level, wells, 0, new System.Random(number), new Trajectory(), out int _, out Vector2 position))
                {
                    continue;
                }
                foreach (Well solutionWell in level.SolutionWells)
                {
                    landedOnTheSolution += Vector2.Distance(solutionWell.Position, position) < 1e-4f ? 1 : 0;
                }
            }
            Assert.AreEqual(0, landedOnTheSolution);
        }

        [Test]
        public void TryPlan_AllocatesNothing()
        {
            LevelDefinition level = LevelGenerator.Generate(DifficultyCurve.SeedForLevel(40), 40);
            var wells = new Well[GameConfig.MaxWells];
            wells[0] = new Well(new Vector2(0.3f, 0.9f));
            wells[1] = new Well(new Vector2(0.7f, 1.1f));
            var rng = new System.Random(1);
            var scratch = new Trajectory();
            HintPlanner.TryPlan(level, wells, 2, rng, scratch, out int _, out Vector2 _); // warm up, and build the spiral table

            long before = System.GC.GetAllocatedBytesForCurrentThread();
            HintPlanner.TryPlan(level, wells, 2, rng, scratch, out int _, out Vector2 _);
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0, allocated);
        }
    }
}
