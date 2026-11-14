using UnityEngine;

namespace GravityWells.Core
{
    // The whole flight: a fixed-step integration from launch to an outcome, a pure function of the
    // level and the well positions. It fills a caller-owned Trajectory and allocates nothing, because
    // the preview calls it every frame of a drag and the solver and hint call it thousands of times.
    //
    // Arithmetic and square root only (CLAUDE.md, third rule): those are rounded the same way on
    // every device, which the daily puzzle and share codes rely on. No trigonometry in this file.
    public static class FlightSimulator
    {
        public static void Simulate(LevelDefinition level, Well[] wells, int wellCount, Trajectory into)
        {
            Run(
                level.Start, level.LaunchVelocity, level.Gates, level.Gates.Length, level.Goal, level.GoalRadius,
                hasGoal: true, level.Asteroids, level.Asteroids.Length, wells, wellCount, into);
        }

        // A flight with nothing to hit but the wells and the edge: the generator flies this first and
        // only then puts the gates and the goal on the path that came out.
        internal static void SimulateFree(Vector2 start, Vector2 velocity, Well[] wells, int wellCount, Trajectory into)
        {
            Run(start, velocity, null, 0, default, 0f, hasGoal: false, null, 0, wells, wellCount, into);
        }

        // Written with plain floats and locals rather than Vector2 operators and Trajectory calls:
        // this loop runs 960 times per flight and the flight runs every frame of a drag.
        private static void Run(
            Vector2 start, Vector2 velocity, Gate[] gates, int gateCount, Vector2 goal, float goalRadius, bool hasGoal,
            Asteroid[] asteroids, int asteroidCount, Well[] wells, int wellCount, Trajectory into)
        {
            const float dt = GameConfig.TimeStep;
            const float softeningSqr = GameConfig.Softening * GameConfig.Softening;
            const float coreSqr = GameConfig.WellCoreRadius * GameConfig.WellCoreRadius;
            const float minX = -GameConfig.FieldMargin;
            const float maxX = GameConfig.FieldWidth + GameConfig.FieldMargin;
            const float minY = -GameConfig.FieldMargin;
            const float maxY = GameConfig.FieldHeight + GameConfig.FieldMargin;

            float px = start.x, py = start.y;
            float vx = velocity.x, vy = velocity.y;
            float goalX = goal.x, goalY = goal.y;
            float goalSqr = goalRadius * goalRadius;
            int allGatesMask = (1 << gateCount) - 1;
            int litMask = 0;

            float[] wellX = into.WellX, wellY = into.WellY, wellSign = into.WellSign;
            float[] rockX = into.RockX, rockY = into.RockY, rockRadiusSqr = into.RockRadiusSqr;
            for (int w = 0; w < wellCount; w++)
            {
                wellX[w] = wells[w].Position.x;
                wellY[w] = wells[w].Position.y;
                wellSign[w] = wells[w].Kind == WellKind.Repulsor ? -1f : 1f;
            }
            for (int a = 0; a < asteroidCount; a++)
            {
                rockX[a] = asteroids[a].Centre.x;
                rockY[a] = asteroids[a].Centre.y;
                rockRadiusSqr[a] = asteroids[a].Radius * asteroids[a].Radius;
            }

            into.Begin(start);
            Vector2[] points = into.Points;
            int maxSteps = points.Length - 1;

            for (int step = 1; step <= maxSteps; step++)
            {
                float ax = 0f, ay = 0f;
                for (int w = 0; w < wellCount; w++)
                {
                    float dx = wellX[w] - px;
                    float dy = wellY[w] - py;
                    float r2 = dx * dx + dy * dy + softeningSqr;
                    float pull = wellSign[w] * GameConfig.WellStrength / (r2 * Mathf.Sqrt(r2));
                    ax += dx * pull;
                    ay += dy * pull;
                }

                // Semi-implicit Euler: the new velocity moves the ship. It keeps orbits from gaining energy.
                vx += ax * dt;
                vy += ay * dt;
                float sx = vx * dt;
                float sy = vy * dt;
                float nx = px + sx;
                float ny = py + sy;
                float stepSqr = sx * sx + sy * sy;

                points[step].x = nx;
                points[step].y = ny;

                if (litMask != allGatesMask)
                {
                    for (int g = 0; g < gateCount; g++)
                    {
                        if ((litMask & (1 << g)) == 0 && StepCrossesGate(px, py, nx, ny, sx, sy, in gates[g]))
                        {
                            litMask |= 1 << g;
                            into.LightGate(g, step);
                        }
                    }
                }

                for (int a = 0; a < asteroidCount; a++)
                {
                    float cx = rockX[a];
                    float cy = rockY[a];
                    float radiusSqr = rockRadiusSqr[a];
                    if (MayTouch(cx - nx, cy - ny, radiusSqr, stepSqr)
                        && SqrDistanceToStep(cx, cy, px, py, sx, sy, stepSqr) <= radiusSqr)
                    {
                        into.Complete(step + 1, FlightOutcome.HitAsteroid);
                        return;
                    }
                }

                for (int w = 0; w < wellCount; w++)
                {
                    float cx = wellX[w];
                    float cy = wellY[w];
                    if (MayTouch(cx - nx, cy - ny, coreSqr, stepSqr)
                        && SqrDistanceToStep(cx, cy, px, py, sx, sy, stepSqr) <= coreSqr)
                    {
                        into.Complete(step + 1, FlightOutcome.HitWell);
                        return;
                    }
                }

                // Swept, so a fast ship cannot step over the goal; and only once every gate is lit.
                if (hasGoal && litMask == allGatesMask
                    && MayTouch(goalX - nx, goalY - ny, goalSqr, stepSqr)
                    && SqrDistanceToStep(goalX, goalY, px, py, sx, sy, stepSqr) <= goalSqr)
                {
                    into.Complete(step + 1, FlightOutcome.Won);
                    return;
                }

                if (nx < minX || nx > maxX || ny < minY || ny > maxY)
                {
                    into.Complete(step + 1, FlightOutcome.LeftField);
                    return;
                }

                px = nx;
                py = ny;
            }

            into.Complete(maxSteps + 1, FlightOutcome.TimedOut);
        }

        // True when the step passes between the posts, from either side.
        //
        // The two ends of the step are sorted onto the two sides of the gate line, with "on the line"
        // counted as one fixed side. That makes consecutive steps tile the path exactly: a ship that
        // lands precisely on the line is caught by the step that leaves it, never by neither. The posts
        // are tested strictly, so a path that only grazes a post, or passes beside the gate, does not
        // light it.
        private static bool StepCrossesGate(float px, float py, float nx, float ny, float sx, float sy, in Gate gate)
        {
            float ax = gate.A.x, ay = gate.A.y;
            float gx = gate.B.x - ax;
            float gy = gate.B.y - ay;
            bool startSide = gx * (py - ay) - gy * (px - ax) >= 0f;
            bool endSide = gx * (ny - ay) - gy * (nx - ax) >= 0f;
            if (startSide == endSide)
            {
                return false;
            }

            float postA = sx * (ay - py) - sy * (ax - px);
            float postB = sx * (gate.B.y - py) - sy * (gate.B.x - px);
            return (postA > 0f && postB < 0f) || (postA < 0f && postB > 0f);
        }

        // A cheap bound that is never wrong in the "no" direction: a circle of radius r can only touch
        // a step of length L if its centre is within r + L of the far end of the step, and
        // (r + L)^2 <= 2 (r^2 + L^2). Almost every step fails this and skips the exact test.
        private static bool MayTouch(float toEndX, float toEndY, float radiusSqr, float stepSqr)
        {
            return toEndX * toEndX + toEndY * toEndY <= 2f * (radiusSqr + stepSqr);
        }

        private static float SqrDistanceToStep(float cx, float cy, float px, float py, float sx, float sy, float stepSqr)
        {
            float t = 0f;
            if (stepSqr > 0f)
            {
                t = ((cx - px) * sx + (cy - py) * sy) / stepSqr;
                t = t < 0f ? 0f : (t > 1f ? 1f : t);
            }
            float ex = px + sx * t - cx;
            float ey = py + sy * t - cy;
            return ex * ex + ey * ey;
        }
    }
}
