using UnityEngine;

namespace GravityWells.Core
{
    // The difficulty number: how much placement error a level tolerates. Every solution well is
    // nudged by a random offset and the flight is flown again; the result is the fraction of those
    // flights that still win. High is generous, low is fiddly. The generator rejects levels below a
    // floor, and gate width is tuned so the average sits in a target band per level range.
    public static class Forgiveness
    {
        public static float Measure(LevelDefinition level, int samples, int seed)
        {
            return Measure(level, samples, seed, new Trajectory(), new Well[level.SolutionWells.Length]);
        }

        // The generator calls this once per candidate level, so it brings its own buffers.
        internal static float Measure(LevelDefinition level, int samples, int seed, Trajectory scratch, Well[] wells)
        {
            Well[] solution = level.SolutionWells;
            if (samples <= 0 || solution.Length == 0)
            {
                return 0f;
            }

            var rng = new System.Random(seed);
            int wins = 0;
            for (int s = 0; s < samples; s++)
            {
                for (int w = 0; w < solution.Length; w++)
                {
                    Vector2 offset = SeededMath.InUnitDisc(rng);
                    wells[w] = new Well(
                        new Vector2(
                            solution[w].Position.x + offset.x * GameConfig.ForgivenessRadius,
                            solution[w].Position.y + offset.y * GameConfig.ForgivenessRadius),
                        solution[w].Kind);
                }

                FlightSimulator.Simulate(level, wells, solution.Length, scratch);
                if (scratch.Outcome == FlightOutcome.Won)
                {
                    wins++;
                }
            }
            return (float)wins / samples;
        }
    }
}
