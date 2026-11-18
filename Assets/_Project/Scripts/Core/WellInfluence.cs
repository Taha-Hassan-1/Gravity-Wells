using UnityEngine;

namespace GravityWells.Core
{
    // Where one well bends the line most, and how hard. The tutorial draws a thin link from each well to that
    // point so that with several wells on the field the player can see which one is doing what: the link flows
    // toward an attractor and away from a repulsor, and is bright when the well matters and faint when it barely
    // does. Pure arithmetic on the trajectory the preview already drew; it never simulates.
    public struct Influence
    {
        public bool Valid;
        public Vector2 Point;     // the spot on the line nearest the well, where its pull is strongest
        public float Distance;
        public float Strength;    // 0..1 against a well InfluenceReferenceDistance away; never below InfluenceFloor
    }

    public static class WellInfluence
    {
        public static void Compute(Trajectory trajectory, Well[] wells, int wellCount, Influence[] into)
        {
            const float soft = GameConfig.Softening * GameConfig.Softening;
            float reference = Pull(GameConfig.InfluenceReferenceDistance, soft);

            for (int w = 0; w < wellCount; w++)
            {
                Vector2 centre = wells[w].Position;
                int best = -1;
                float bestSqr = float.MaxValue;
                for (int i = 0; i < trajectory.Count; i++)
                {
                    float dx = trajectory.Points[i].x - centre.x;
                    float dy = trajectory.Points[i].y - centre.y;
                    float sqr = dx * dx + dy * dy;
                    if (sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        best = i;
                    }
                }

                if (best < 0)
                {
                    into[w] = default;
                    continue;
                }

                float distance = Mathf.Sqrt(bestSqr);
                float strength = Mathf.Clamp01(Pull(distance, soft) / reference);
                into[w] = new Influence
                {
                    Valid = true,
                    Point = trajectory.Points[best],
                    Distance = distance,
                    Strength = Mathf.Max(GameConfig.InfluenceFloor, strength),
                };
            }
        }

        // The size of the pull at a distance: the same law the simulator uses.
        private static float Pull(float distance, float softeningSqr)
        {
            float r2 = distance * distance + softeningSqr;
            return GameConfig.WellStrength * distance / (r2 * Mathf.Sqrt(r2));
        }
    }
}
