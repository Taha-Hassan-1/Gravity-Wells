using System;
using UnityEngine;

namespace GravityWells.Core
{
    // Builds a level backwards, so it is solvable by construction: place the solution wells, fly
    // the ship through them, and only then put the gates and the goal on the path that was flown.
    // Nothing ever searches for a solution. The wells are kept only as LevelDefinition.SolutionWells,
    // which the game cannot read.
    //
    // Arithmetic and square root only (CLAUDE.md, third rule): no trigonometry in this file, so the
    // same seed gives the same level on every device.
    public static class LevelGenerator
    {
        private const int WellPlacementTries = 30;
        private const int LaunchDirectionTries = 16;
        private const int GateLayoutTries = 6;
        private const int AsteroidPlacementTries = 40;
        private const int CrossingStride = 8;      // the path is checked for self-crossings at this coarseness
        private const int AsteroidPathStride = 2;
        private const float AsteroidSpacing = 0.02f;
        private const float GoalEdgeGap = 0.01f;   // the goal circle stays this far inside the field
        private const float RelaxedMinFlightSeconds = 1.5f;

        // Buffers for one Generate call, shared by all of its attempts.
        private sealed class Workspace
        {
            public readonly Trajectory Path = new Trajectory();
            public readonly Trajectory Scratch = new Trajectory();
            public readonly float[] Arc = new float[GameConfig.TrajectoryCapacity];
            public readonly Well[] Wells = new Well[GameConfig.MaxWells];
            public readonly Well[] Fewer = new Well[GameConfig.MaxWells];
            public readonly float[] Fractions = new float[GameConfig.MaxGates];
            public readonly Gate[] Gates = new Gate[GameConfig.MaxGates];
            public readonly Asteroid[] Asteroids = new Asteroid[8];
        }

        public static LevelDefinition Generate(int seed, int level)
        {
            level = Mathf.Max(1, level);
            var rng = new System.Random(seed);
            LevelPlan plan = DifficultyCurve.PlanForLevel(level);
            var work = new Workspace();
            int attempts = 0;

            // The planned gate count first; if nothing passes, one gate fewer, and so on. Every
            // rejected attempt carries on with the same generator, so the result is still a pure
            // function of the seed.
            for (int gates = Mathf.Min(plan.Gates, GameConfig.MaxGates); gates >= 1; gates--)
            {
                for (int i = 0; i < GameConfig.GeneratorAttemptsPerRound; i++)
                {
                    attempts++;
                    LevelDefinition built = TryBuild(seed, level, plan, gates, strict: true, rng, work, attempts);
                    if (built != null)
                    {
                        return built;
                    }
                }
            }

            // Last resort: one gate, and the quality checks (turning, flight time, forgiveness) eased.
            // The proofs still hold, so the level is solvable and needs its wells.
            for (int i = 0; i < GameConfig.GeneratorRelaxedAttempts; i++)
            {
                attempts++;
                LevelDefinition built = TryBuild(seed, level, plan, 1, strict: false, rng, work, attempts);
                if (built != null)
                {
                    return built;
                }
            }

            throw new InvalidOperationException($"No level could be built for seed {seed}, level {level}.");
        }

        private static LevelDefinition TryBuild(
            int seed, int level, LevelPlan plan, int gateCount, bool strict, System.Random rng, Workspace work, int attempts)
        {
            // 1. Start and launch.
            var start = new Vector2(
                SeededMath.Range(rng, GameConfig.StartEdgeInset, GameConfig.FieldWidth - GameConfig.StartEdgeInset),
                SeededMath.Range(rng, GameConfig.StartEdgeInset, GameConfig.FieldHeight * GameConfig.StartMaxHeightFraction));
            if (!TryPickLaunchDirection(rng, start, out Vector2 direction))
            {
                return null;
            }
            var velocity = new Vector2(direction.x * GameConfig.LaunchSpeed, direction.y * GameConfig.LaunchSpeed);

            // 2. Solution wells.
            int wellCount = plan.Wells;
            int repulsors = 0;
            for (int w = 0; w < wellCount; w++)
            {
                if (!TryPlaceWell(rng, start, work.Wells, w, out Vector2 position))
                {
                    return null;
                }
                WellKind kind = WellKind.Attractor;
                if (level >= GameConfig.RepulsorFirstLevel && w > 0 && rng.NextDouble() < GameConfig.RepulsorChance)
                {
                    kind = WellKind.Repulsor;
                    repulsors++;
                }
                work.Wells[w] = new Well(position, kind);
            }

            // 3. Fly it, with nothing on the field but the wells.
            Trajectory path = work.Path;
            FlightSimulator.SimulateFree(start, velocity, work.Wells, wellCount, path);
            if (path.Outcome == FlightOutcome.HitWell)
            {
                return null;
            }

            float endInset = Mathf.Max(GameConfig.PathEndInset, plan.GoalRadius + GoalEdgeGap);
            int end = LastStepInside(path, endInset);

            // 4. Is it a path worth playing?
            float flightSeconds = end * GameConfig.TimeStep;
            float minSeconds = strict ? GameConfig.MinSolutionFlightSeconds : RelaxedMinFlightSeconds;
            if (flightSeconds < minSeconds || flightSeconds > GameConfig.MaxSolutionFlightSeconds)
            {
                return null;
            }

            Vector2[] points = path.Points;
            Vector2 goal = points[end];
            if (SqrDistance(goal, start) < GameConfig.MinGoalDistanceFromStart * GameConfig.MinGoalDistanceFromStart)
            {
                return null;
            }
            if (PassesTooCloseToAWell(points, end, work.Wells, wellCount))
            {
                return null;
            }
            if (strict && TotalTurning(points, end) < GameConfig.MinTotalTurning)
            {
                return null;
            }
            if (SelfCrossings(points, end) > GameConfig.MaxPathSelfCrossings)
            {
                return null;
            }

            // 5. Gates on the path.
            float pathLength = FillArcLengths(points, end, work.Arc);
            bool gatesPlaced = false;
            for (int attempt = 0; attempt < GateLayoutTries && !gatesPlaced; attempt++)
            {
                gatesPlaced = TryPlaceGates(rng, points, end, work.Arc, pathLength, gateCount, plan.GateWidth, goal, work, wellCount);
            }
            if (!gatesPlaced)
            {
                return null;
            }

            // 6. Asteroids, clear of everything the solution needs.
            for (int a = 0; a < plan.Asteroids; a++)
            {
                if (!TryPlaceAsteroid(rng, points, end, start, goal, plan.GoalRadius, work, gateCount, wellCount, a))
                {
                    return null;
                }
            }

            // 7. Prove it: the wells win, and every one of them is needed.
            var solution = new Well[wellCount];
            Array.Copy(work.Wells, solution, wellCount);
            var gates = new Gate[gateCount];
            Array.Copy(work.Gates, gates, gateCount);
            var asteroids = new Asteroid[plan.Asteroids];
            Array.Copy(work.Asteroids, asteroids, plan.Asteroids);
            int repulsorBudget = level >= GameConfig.RepulsorFirstLevel ? repulsors + 1 : 0;

            var candidate = new LevelDefinition(
                seed, level, start, velocity, gates, goal, plan.GoalRadius, asteroids, wellCount, repulsorBudget, solution);
            if (!SolutionWinsAndEveryWellIsNeeded(candidate, work))
            {
                return null;
            }

            float forgiveness = Forgiveness.Measure(
                candidate, GameConfig.ForgivenessSamples, unchecked(seed * 31 + attempts), work.Scratch, work.Fewer);
            if (strict && forgiveness < GameConfig.MinForgiveness)
            {
                return null;
            }

            return new LevelDefinition(
                seed, level, start, velocity, gates, goal, plan.GoalRadius, asteroids, wellCount, repulsorBudget, solution,
                attempts, plan.Gates - gateCount, forgiveness, flightSeconds);
        }

        // An upward direction that would keep an unbent ship on the field for a while, so the first
        // thing the player sees is a line with room to bend.
        private static bool TryPickLaunchDirection(System.Random rng, Vector2 start, out Vector2 direction)
        {
            for (int i = 0; i < LaunchDirectionTries; i++)
            {
                Vector2 d = SeededMath.UnitVector(rng);
                if (d.y < 0f)
                {
                    d = new Vector2(d.x, -d.y);
                }
                if (d.y <= 0f)
                {
                    continue;
                }

                float seconds = (GameConfig.FieldHeight - start.y) / (d.y * GameConfig.LaunchSpeed);
                if (d.x > 0f)
                {
                    seconds = Mathf.Min(seconds, (GameConfig.FieldWidth - start.x) / (d.x * GameConfig.LaunchSpeed));
                }
                else if (d.x < 0f)
                {
                    seconds = Mathf.Min(seconds, start.x / (-d.x * GameConfig.LaunchSpeed));
                }

                if (seconds >= GameConfig.MinStraightFlightSeconds)
                {
                    direction = d;
                    return true;
                }
            }
            direction = default;
            return false;
        }

        private static bool TryPlaceWell(System.Random rng, Vector2 start, Well[] placed, int placedCount, out Vector2 position)
        {
            float inset = GameConfig.WellFieldInset;
            float minFromStartSqr = GameConfig.MinWellDistanceFromStart * GameConfig.MinWellDistanceFromStart;
            float minApartSqr = GameConfig.MinSolutionWellSpacing * GameConfig.MinSolutionWellSpacing;

            for (int i = 0; i < WellPlacementTries; i++)
            {
                var p = new Vector2(
                    SeededMath.Range(rng, inset, GameConfig.FieldWidth - inset),
                    SeededMath.Range(rng, inset, GameConfig.FieldHeight - inset));
                if (SqrDistance(p, start) < minFromStartSqr)
                {
                    continue;
                }

                bool crowded = false;
                for (int w = 0; w < placedCount && !crowded; w++)
                {
                    crowded = SqrDistance(p, placed[w].Position) < minApartSqr;
                }
                if (!crowded)
                {
                    position = p;
                    return true;
                }
            }
            position = default;
            return false;
        }

        // The last step before the path first leaves the field shrunk by inset. The flight itself
        // ended (edge or time cap) no earlier than that.
        private static int LastStepInside(Trajectory path, float inset)
        {
            Vector2[] points = path.Points;
            float maxX = GameConfig.FieldWidth - inset;
            float maxY = GameConfig.FieldHeight - inset;
            for (int i = 0; i < path.Count; i++)
            {
                if (points[i].x < inset || points[i].x > maxX || points[i].y < inset || points[i].y > maxY)
                {
                    return i - 1;
                }
            }
            return path.Count - 1;
        }

        // A solution that skims a well is one a small nudge turns into a crash: too twitchy to be fair.
        private static bool PassesTooCloseToAWell(Vector2[] points, int end, Well[] wells, int wellCount)
        {
            float clearance = GameConfig.SolutionPathWellClearanceCores * GameConfig.WellCoreRadius;
            float clearanceSqr = clearance * clearance;
            for (int w = 0; w < wellCount; w++)
            {
                Vector2 well = wells[w].Position;
                for (int i = 0; i <= end; i++)
                {
                    if (SqrDistance(points[i], well) < clearanceSqr)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        // How much the path bends in total, in radians, left and right both counting. Each step turns
        // by a tiny angle, and for a tiny angle the cross product of the two unit directions is the
        // angle itself, so the sum needs no trigonometry.
        private static float TotalTurning(Vector2[] points, int end)
        {
            float total = 0f;
            float prevX = 0f, prevY = 0f;
            bool hasPrev = false;
            for (int i = 1; i <= end; i++)
            {
                float dx = points[i].x - points[i - 1].x;
                float dy = points[i].y - points[i - 1].y;
                float sqr = dx * dx + dy * dy;
                if (sqr <= 0f)
                {
                    continue;
                }
                float inv = 1f / Mathf.Sqrt(sqr);
                dx *= inv;
                dy *= inv;
                if (hasPrev)
                {
                    total += Mathf.Abs(prevX * dy - prevY * dx);
                }
                prevX = dx;
                prevY = dy;
                hasPrev = true;
            }
            return total;
        }

        // A path that loops over itself again and again reads as a scribble. One crossing is allowed.
        private static int SelfCrossings(Vector2[] points, int end)
        {
            int crossings = 0;
            for (int a = 0; a + CrossingStride <= end; a += CrossingStride)
            {
                // Start two pieces on: neighbouring pieces share an end point and cannot cross.
                for (int b = a + 2 * CrossingStride; b + CrossingStride <= end; b += CrossingStride)
                {
                    if (GeometryUtils.SegmentsProperlyIntersect(
                        points[a], points[a + CrossingStride], points[b], points[b + CrossingStride]))
                    {
                        crossings++;
                    }
                }
            }
            return crossings;
        }

        private static float FillArcLengths(Vector2[] points, int end, float[] arc)
        {
            arc[0] = 0f;
            for (int i = 1; i <= end; i++)
            {
                arc[i] = arc[i - 1] + Mathf.Sqrt(SqrDistance(points[i], points[i - 1]));
            }
            return arc[end];
        }

        // Gates spread along the path by distance travelled, each centred on the path and square
        // to the direction of flight there.
        private static bool TryPlaceGates(
            System.Random rng, Vector2[] points, int end, float[] arc, float pathLength, int gateCount, float gateWidth,
            Vector2 goal, Workspace work, int wellCount)
        {
            // The spare room beyond the minimum spacing is shared out at random, which always gives
            // a legal spread (drawing each gate freely and rejecting clashes would rarely fit five).
            float spare = GameConfig.GateArcMax - GameConfig.GateArcMin - (gateCount - 1) * GameConfig.MinGateArcSpacing;
            if (spare < 0f)
            {
                return false;
            }

            float[] fractions = work.Fractions;
            for (int g = 0; g < gateCount; g++)
            {
                float value = SeededMath.Range(rng, 0f, spare);
                int slot = g;
                while (slot > 0 && fractions[slot - 1] > value)
                {
                    fractions[slot] = fractions[slot - 1];
                    slot--;
                }
                fractions[slot] = value;
            }

            float clearanceSqr = GameConfig.GateClearance * GameConfig.GateClearance;
            float half = gateWidth * 0.5f;
            bool allFit = true; // keep drawing even after a failure, so the random stream does not depend on where it failed

            for (int g = 0; g < gateCount; g++)
            {
                float target = (GameConfig.GateArcMin + fractions[g] + g * GameConfig.MinGateArcSpacing) * pathLength;
                int index = 1;
                while (index < end - 1 && arc[index] < target)
                {
                    index++;
                }

                Vector2 centre = points[index];
                float tx = points[index + 1].x - points[index - 1].x;
                float ty = points[index + 1].y - points[index - 1].y;
                float length = Mathf.Sqrt(tx * tx + ty * ty);
                if (length <= 0f)
                {
                    allFit = false;
                    continue;
                }
                float nx = -ty / length * half;
                float ny = tx / length * half;
                var a = new Vector2(centre.x - nx, centre.y - ny);
                var b = new Vector2(centre.x + nx, centre.y + ny);
                work.Gates[g] = new Gate(a, b);

                if (!InsideField(a, 0f) || !InsideField(b, 0f))
                {
                    allFit = false;
                }
                if (GeometryUtils.SqrDistancePointToSegment(goal, a, b) < clearanceSqr)
                {
                    allFit = false;
                }
                for (int w = 0; w < wellCount; w++)
                {
                    if (GeometryUtils.SqrDistancePointToSegment(work.Wells[w].Position, a, b) < clearanceSqr)
                    {
                        allFit = false;
                    }
                }
            }
            return allFit;
        }

        private static bool TryPlaceAsteroid(
            System.Random rng, Vector2[] points, int end, Vector2 start, Vector2 goal, float goalRadius, Workspace work,
            int gateCount, int wellCount, int index)
        {
            for (int attempt = 0; attempt < AsteroidPlacementTries; attempt++)
            {
                float radius = SeededMath.Range(rng, GameConfig.AsteroidMinRadius, GameConfig.AsteroidMaxRadius);
                var centre = new Vector2(
                    SeededMath.Range(rng, radius, GameConfig.FieldWidth - radius),
                    SeededMath.Range(rng, radius, GameConfig.FieldHeight - radius));
                float clear = radius + GameConfig.AsteroidClearance;
                float clearSqr = clear * clear;

                if (SqrDistance(centre, start) < clearSqr)
                {
                    continue;
                }
                float fromGoal = clear + goalRadius;
                if (SqrDistance(centre, goal) < fromGoal * fromGoal)
                {
                    continue;
                }

                bool blocked = false;
                for (int w = 0; w < wellCount && !blocked; w++)
                {
                    blocked = SqrDistance(centre, work.Wells[w].Position) < clearSqr;
                }
                for (int g = 0; g < gateCount && !blocked; g++)
                {
                    blocked = GeometryUtils.SqrDistancePointToSegment(centre, work.Gates[g].A, work.Gates[g].B) < clearSqr;
                }
                for (int other = 0; other < index && !blocked; other++)
                {
                    float apart = radius + work.Asteroids[other].Radius + AsteroidSpacing;
                    blocked = SqrDistance(centre, work.Asteroids[other].Centre) < apart * apart;
                }
                for (int i = 0; i <= end && !blocked; i += AsteroidPathStride)
                {
                    blocked = SqrDistance(centre, points[i]) < clearSqr;
                }
                if (blocked)
                {
                    continue;
                }

                work.Asteroids[index] = new Asteroid(centre, radius);
                return true;
            }
            return false;
        }

        private static bool SolutionWinsAndEveryWellIsNeeded(LevelDefinition candidate, Workspace work)
        {
            Well[] solution = candidate.SolutionWells;
            Trajectory scratch = work.Scratch;

            FlightSimulator.Simulate(candidate, solution, solution.Length, scratch);
            if (scratch.Outcome != FlightOutcome.Won)
            {
                return false;
            }

            FlightSimulator.Simulate(candidate, solution, 0, scratch);
            if (scratch.Outcome == FlightOutcome.Won)
            {
                return false;
            }

            for (int skip = 0; skip < solution.Length && solution.Length > 1; skip++)
            {
                int count = 0;
                for (int w = 0; w < solution.Length; w++)
                {
                    if (w != skip)
                    {
                        work.Fewer[count++] = solution[w];
                    }
                }
                FlightSimulator.Simulate(candidate, work.Fewer, count, scratch);
                if (scratch.Outcome == FlightOutcome.Won)
                {
                    return false;
                }
            }
            return true;
        }

        private static bool InsideField(Vector2 p, float inset)
        {
            return p.x >= inset && p.x <= GameConfig.FieldWidth - inset && p.y >= inset && p.y <= GameConfig.FieldHeight - inset;
        }

        private static float SqrDistance(Vector2 a, Vector2 b)
        {
            float dx = a.x - b.x;
            float dy = a.y - b.y;
            return dx * dx + dy * dy;
        }
    }
}
