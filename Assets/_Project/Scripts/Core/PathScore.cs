using UnityEngine;

namespace GravityWells.Core
{
    // How far a flight is from winning, as one number: 0 means won, lower is better. The solver,
    // the hint and the "getting warmer" feedback all steer by it, so it must never read the
    // stored solution of the level.
    public static class PathScore
    {
        public const float UnlitGatePenalty = 1000f;
        public const float CrashPenalty = 500f;

        public static float Evaluate(LevelDefinition level, Trajectory t)
        {
            if (t.Outcome == FlightOutcome.Won)
            {
                return 0f;
            }

            Gate[] gates = level.Gates;
            Vector2[] points = t.Points;
            int count = t.Count;
            float score = 0f;
            bool allLit = t.GatesLitCount == gates.Length;

            // The gate term is so large that lighting one more gate always beats creeping closer to two.
            for (int g = 0; g < gates.Length; g++)
            {
                if ((t.GatesLitMask & (1 << g)) == 0)
                {
                    score += UnlitGatePenalty + Mathf.Sqrt(SmallestSqrDistance(points, 0, count, gates[g].Midpoint));
                }
            }

            // Once every gate is lit only the path after the last one can still reach the goal:
            // passing over the goal earlier did not count then and should not look close now.
            int from = allLit ? t.LastGateLitStep : 0;
            score += Mathf.Sqrt(SmallestSqrDistance(points, from, count, level.Goal));

            if (t.Outcome == FlightOutcome.HitWell || t.Outcome == FlightOutcome.HitAsteroid)
            {
                score += CrashPenalty;
            }
            return score;
        }

        private static float SmallestSqrDistance(Vector2[] points, int from, int count, Vector2 target)
        {
            float best = float.MaxValue;
            for (int i = from; i < count; i++)
            {
                float dx = points[i].x - target.x;
                float dy = points[i].y - target.y;
                float sqr = dx * dx + dy * dy;
                if (sqr < best)
                {
                    best = sqr;
                }
            }
            return best;
        }
    }
}
