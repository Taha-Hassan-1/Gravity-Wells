using UnityEngine;

namespace GravityWells.Core
{
    public readonly struct SolverResult
    {
        public readonly bool Solved;
        public readonly int Evaluations;   // flights simulated
        public readonly int WellCount;     // wells in the layout that was left in the caller's array

        public SolverResult(bool solved, int evaluations, int wellCount)
        {
            Solved = solved;
            Evaluations = evaluations;
            WellCount = wellCount;
        }
    }

    // A deterministic stand-in for a player, for tests and tuning. It sees only what a player sees
    // (the level and where its own path goes) and never reads the stored solution. Its job is to
    // show that levels are findable, not merely solvable: a level whose only answer is a needle in
    // a haystack is a bad level even though it is valid.
    //
    // It lives in Core on purpose, so the batch tool and the soak test can run it without a scene.
    public static class Solver
    {
        private const int StallLimit = 150;        // tries without progress before adding a well or starting over
        private const float WidestMove = 0.3f;
        private const float FinestMove = 0.01f;

        public static SolverResult Solve(LevelDefinition level, int seed, int maxEvaluations, Well[] wells, Trajectory scratch)
        {
            var rng = new System.Random(seed);
            int budget = Mathf.Min(level.WellBudget, wells.Length);
            int evaluations = 0;

            int count = StartOver(level, rng, wells);
            float score = Evaluate(level, wells, count, scratch, ref evaluations);
            int stall = 0;

            while (score > 0f && evaluations < maxEvaluations)
            {
                // Move one well. The reach of the move narrows the longer nothing has helped, from
                // a sweep across the field down to a nudge, and widens again after every gain.
                int index = rng.Next(count);
                Well before = wells[index];
                float reach = Mathf.Lerp(WidestMove, FinestMove, (float)stall / StallLimit);
                Vector2 offset = SeededMath.InUnitDisc(rng);
                Vector2 moved = WellRules.Clamp(
                    level, new Vector2(before.Position.x + offset.x * reach, before.Position.y + offset.y * reach));
                wells[index] = new Well(moved, before.Kind);

                float candidate = Evaluate(level, wells, count, scratch, ref evaluations);
                if (candidate < score)
                {
                    score = candidate;
                    stall = 0;
                    continue;
                }

                wells[index] = before;
                stall++;
                if (stall < StallLimit)
                {
                    continue;
                }

                // Stuck. One more well if there is one left, otherwise a fresh start.
                if (count < budget)
                {
                    wells[count] = new Well(RandomLegalPosition(level, rng), NextKind(level, rng, wells, count));
                    count++;
                }
                else
                {
                    count = StartOver(level, rng, wells);
                }
                score = Evaluate(level, wells, count, scratch, ref evaluations);
                stall = 0;
            }

            return new SolverResult(score <= 0f, evaluations, count);
        }

        public static SolverResult Solve(LevelDefinition level, int seed, int maxEvaluations)
        {
            return Solve(level, seed, maxEvaluations, new Well[GameConfig.MaxWells], new Trajectory());
        }

        private static int StartOver(LevelDefinition level, System.Random rng, Well[] wells)
        {
            wells[0] = new Well(RandomLegalPosition(level, rng), WellKind.Attractor);
            return 1;
        }

        private static float Evaluate(LevelDefinition level, Well[] wells, int count, Trajectory scratch, ref int evaluations)
        {
            FlightSimulator.Simulate(level, wells, count, scratch);
            evaluations++;
            return PathScore.Evaluate(level, scratch);
        }

        private static Vector2 RandomLegalPosition(LevelDefinition level, System.Random rng)
        {
            return WellRules.Clamp(
                level,
                new Vector2(SeededMath.Range(rng, 0f, GameConfig.FieldWidth), SeededMath.Range(rng, 0f, GameConfig.FieldHeight)));
        }

        private static WellKind NextKind(LevelDefinition level, System.Random rng, Well[] wells, int count)
        {
            if (level.RepulsorBudget <= 0)
            {
                return WellKind.Attractor;
            }

            int repulsors = 0;
            for (int i = 0; i < count; i++)
            {
                if (wells[i].Kind == WellKind.Repulsor)
                {
                    repulsors++;
                }
            }
            bool wantsRepulsor = rng.NextDouble() < GameConfig.RepulsorChance;
            return wantsRepulsor && repulsors < level.RepulsorBudget ? WellKind.Repulsor : WellKind.Attractor;
        }
    }
}
