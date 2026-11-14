using UnityEngine;

namespace GravityWells.Core
{
    // The hint: the single best move of one well from where the player is now. It never looks at
    // the stored solution of the level, because the player may be closing in on a different valid
    // layout and a hint that drags them toward the generator's would undo their work. Instead it
    // tries a spread of positions for each well (and for one new well, if any are left) and keeps
    // whichever most improves PathScore.
    public static class HintPlanner
    {
        public const int CandidatesPerWell = 200;

        private const int SpiralPoints = 120;            // close-in moves around the current position
        private const int GridColumns = 8;               // the rest of the 200: a coarse sweep of the whole field
        private const int GridRows = 10;
        private const float SpiralReach = 0.35f;
        private const int NewWellGridColumns = 10;       // a well that is not placed yet has no "around": sweep only
        private const int NewWellGridRows = 20;

        // One step of the golden angle as a rotation, so the spiral is built by multiplying rather
        // than by calling trigonometric functions.
        private const float GoldenCos = -0.7373688f;
        private const float GoldenSin = 0.6754903f;

        private static readonly Vector2[] Spiral = BuildSpiral();

        // False when nothing tried beats the current layout (or it already wins). On true, wellIndex is
        // the well to move, or wellCount when the move is to place a new attractor; position is where.
        // wells is used as working space and left as it was; scratch is overwritten.
        public static bool TryPlan(
            LevelDefinition level, Well[] wells, int wellCount, System.Random rng, Trajectory scratch,
            out int wellIndex, out Vector2 position, int bonusWells = 0)
        {
            wellIndex = -1;
            position = default;

            FlightSimulator.Simulate(level, wells, wellCount, scratch);
            if (scratch.Outcome == FlightOutcome.Won)
            {
                return false;
            }
            float current = PathScore.Evaluate(level, scratch);
            float best = current;

            for (int w = 0; w < wellCount; w++)
            {
                Well original = wells[w];

                for (int s = 0; s < SpiralPoints; s++)
                {
                    var candidate = new Vector2(original.Position.x + Spiral[s].x, original.Position.y + Spiral[s].y);
                    Try(level, wells, wellCount, w, original.Kind, candidate, scratch, ref best, ref wellIndex, ref position);
                }
                SweepField(level, wells, wellCount, w, original.Kind, GridColumns, GridRows, rng, scratch, ref best, ref wellIndex, ref position);

                wells[w] = original;
            }

            if (wellCount < level.WellBudget + bonusWells && wellCount < wells.Length)
            {
                Well slot = wells[wellCount];
                SweepField(
                    level, wells, wellCount + 1, wellCount, WellKind.Attractor, NewWellGridColumns, NewWellGridRows, rng, scratch,
                    ref best, ref wellIndex, ref position);
                wells[wellCount] = slot;
            }

            return best < current;
        }

        // One position in every cell of a grid over the field, jittered inside its cell so repeated
        // hints do not keep testing the same points.
        private static void SweepField(
            LevelDefinition level, Well[] wells, int count, int index, WellKind kind, int columns, int rows, System.Random rng,
            Trajectory scratch, ref float best, ref int bestIndex, ref Vector2 bestPosition)
        {
            float cellWidth = GameConfig.FieldWidth / columns;
            float cellHeight = GameConfig.FieldHeight / rows;
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    var candidate = new Vector2(
                        (column + (float)rng.NextDouble()) * cellWidth,
                        (row + (float)rng.NextDouble()) * cellHeight);
                    Try(level, wells, count, index, kind, candidate, scratch, ref best, ref bestIndex, ref bestPosition);
                }
            }
        }

        private static void Try(
            LevelDefinition level, Well[] wells, int count, int index, WellKind kind, Vector2 candidate, Trajectory scratch,
            ref float best, ref int bestIndex, ref Vector2 bestPosition)
        {
            if (!WellRules.IsLegal(level, candidate))
            {
                return;
            }

            wells[index] = new Well(candidate, kind);
            FlightSimulator.Simulate(level, wells, count, scratch);
            float score = PathScore.Evaluate(level, scratch);
            if (score < best)
            {
                best = score;
                bestIndex = index;
                bestPosition = candidate;
            }
        }

        // A sunflower: each point a golden angle on from the last and a little further out, which
        // covers the disc evenly with more points close in, where a fine adjustment is likeliest.
        private static Vector2[] BuildSpiral()
        {
            var points = new Vector2[SpiralPoints];
            float dx = 1f, dy = 0f;
            for (int i = 0; i < SpiralPoints; i++)
            {
                float fraction = (i + 1f) / SpiralPoints;
                float radius = SpiralReach * fraction * fraction + 0.004f;
                points[i] = new Vector2(dx * radius, dy * radius);

                float rotatedX = dx * GoldenCos - dy * GoldenSin;
                float rotatedY = dx * GoldenSin + dy * GoldenCos;
                float inv = 1f / Mathf.Sqrt(rotatedX * rotatedX + rotatedY * rotatedY); // stops rounding from shrinking it
                dx = rotatedX * inv;
                dy = rotatedY * inv;
            }
            return points;
        }
    }
}
