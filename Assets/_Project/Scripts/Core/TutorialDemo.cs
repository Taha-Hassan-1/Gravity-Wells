using UnityEngine;

namespace GravityWells.Core
{
    // What a frame of the level-1 demonstration looks like: a ghost finger drags a well from the tray to a
    // marked spot, the line bends through the gate, and the picture repeats until the player touches the screen.
    public readonly struct DemoFrame
    {
        public readonly float HandAlpha;      // 0 = not shown
        public readonly Vector2 HandPosition;
        public readonly bool WellOnField;     // the well exists on the field (carried, then dropped)
        public readonly bool Carrying;        // the finger is holding it (false once it has been let go)

        public DemoFrame(float handAlpha, Vector2 handPosition, bool wellOnField, bool carrying)
        {
            HandAlpha = handAlpha;
            HandPosition = handPosition;
            WellOnField = wellOnField;
            Carrying = carrying;
        }
    }

    // A wordless lesson, as a pure function of time so its timeline is tested without a scene. The spot the well
    // is dragged to is found by the same hill-climbing Solver the tests use (it does not read the level's stored
    // solution), so the demonstration always ends with the line through the gate and into the goal.
    public sealed class TutorialDemo
    {
        public const float AppearSeconds = 0.5f;   // the hand fades in on the tray
        public const float DragSeconds = 1.5f;     // and carries the well across
        public const float HoldSeconds = 1.5f;     // the line is left bent, and the hand rests
        public const float LeaveSeconds = 0.8f;    // the hand fades out, then it all begins again
        public const float CycleSeconds = AppearSeconds + DragSeconds + HoldSeconds + LeaveSeconds;

        private const int SolverSeed = 1;
        private const int SolverEvaluations = 20000;

        private TutorialDemo(Vector2 target)
        {
            Target = target;
        }

        // Where the well is dropped.
        public Vector2 Target { get; }

        // The demonstration for a level, or false if the level needs more than one well or the solver did not
        // find a layout (then the game simply has no demo for it).
        public static bool TryCreate(LevelDefinition level, out TutorialDemo demo)
        {
            demo = null;
            if (level.Par != 1)
            {
                return false;
            }

            var wells = new Well[GameConfig.MaxWells];
            SolverResult result = Solver.Solve(level, SolverSeed, SolverEvaluations, wells, new Trajectory());
            if (!result.Solved || result.WellCount != 1)
            {
                return false;
            }
            demo = new TutorialDemo(wells[0].Position);
            return true;
        }

        // The frame at a moment since the demonstration began; it repeats every CycleSeconds. from is where the
        // hand starts (the tray slot, in world units).
        public DemoFrame Sample(float seconds, Vector2 from)
        {
            float t = seconds % CycleSeconds;
            if (t < 0f)
            {
                t += CycleSeconds;
            }

            if (t < AppearSeconds)
            {
                return new DemoFrame(t / AppearSeconds, from, wellOnField: false, carrying: false);
            }
            t -= AppearSeconds;

            if (t < DragSeconds)
            {
                float u = t / DragSeconds;
                float eased = u * u * (3f - 2f * u);
                Vector2 hand = Vector2.Lerp(from, Target, eased);
                return new DemoFrame(1f, hand, wellOnField: true, carrying: true);
            }
            t -= DragSeconds;

            if (t < HoldSeconds)
            {
                return new DemoFrame(1f, Target, wellOnField: true, carrying: false);
            }
            t -= HoldSeconds;

            return new DemoFrame(1f - t / LeaveSeconds, Target, wellOnField: t < LeaveSeconds, carrying: false);
        }
    }
}
