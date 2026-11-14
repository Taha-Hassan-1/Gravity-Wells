using UnityEngine;

namespace GravityWells.Core
{
    // What a level of a given number is made of.
    public readonly struct LevelPlan
    {
        public readonly int Wells;
        public readonly int Gates;
        public readonly float GateWidth;
        public readonly float GoalRadius;
        public readonly int Asteroids;
        public readonly bool ForceRepulsor;   // the level that introduces repulsors must contain one

        public LevelPlan(int wells, int gates, float gateWidth, float goalRadius, int asteroids, bool forceRepulsor = false)
        {
            Wells = wells;
            Gates = gates;
            GateWidth = gateWidth;
            GoalRadius = goalRadius;
            Asteroids = asteroids;
            ForceRepulsor = forceRepulsor;
        }
    }

    // How levels grow. The constants are starting guesses: LevelBatchTester measures them and the
    // owner agrees the values before they are locked (CLAUDE.md, "When to stop and ask").
    public static class DifficultyCurve
    {
        // Four wells is the cap: past that a level is more of the same but slower. Difficulty beyond
        // it comes from gate width, gate count and asteroids.
        public static int WellsForLevel(int level) => Mathf.Clamp(1 + Mathf.FloorToInt(Mathf.Sqrt(level) * 0.35f), 1, 4);

        public static int GatesForLevel(int level) => Mathf.Clamp(1 + level / 8, 1, 5);

        public static float GateWidthForLevel(int level) => Mathf.Lerp(0.16f, 0.07f, Mathf.Clamp01((level - 1) / 150f));

        public static float GoalRadiusForLevel(int level) => Mathf.Lerp(0.07f, 0.04f, Mathf.Clamp01((level - 1) / 150f));

        public static int AsteroidsForLevel(int level) =>
            level < GameConfig.AsteroidFirstLevel ? 0 : Mathf.Clamp(1 + (level - GameConfig.AsteroidFirstLevel) / 15, 1, 4);

        public static int SeedForLevel(int level) => unchecked((int)((uint)level * 2654435761u ^ 0x85EBCA6Bu));

        // The curve, except that the first three levels are fixed by rule so the game teaches one
        // thing at a time: one well and one gate with a wide goal, then a second gate, then a second well.
        public static LevelPlan PlanForLevel(int level)
        {
            int wells = WellsForLevel(level);
            int gates = GatesForLevel(level);
            float goalRadius = GoalRadiusForLevel(level);

            if (level <= 1)
            {
                wells = 1;
                gates = 1;
                goalRadius = GameConfig.FirstLevelGoalRadius;
            }
            else if (level == 2)
            {
                wells = 1;
                gates = 2;
            }
            else if (level == 3)
            {
                wells = 2;
                gates = 2;
            }

            int asteroids = AsteroidsForLevel(level);
            bool forceRepulsor = false;

            // The levels that introduce a new piece teach it and nothing else: level 12 brings the first asteroid
            // on the same wells and gates as level 11, and level 35 brings the first repulsor, certain to be in
            // the level, with a lighter load than the curve would give so the new thing is the only hard thing.
            if (level == GameConfig.RepulsorFirstLevel)
            {
                wells = 2;
                gates = Mathf.Min(gates, 3);
                asteroids = Mathf.Min(asteroids, 1);
                forceRepulsor = true;
            }

            return new LevelPlan(wells, gates, GateWidthForLevel(level), goalRadius, asteroids, forceRepulsor);
        }
    }
}
