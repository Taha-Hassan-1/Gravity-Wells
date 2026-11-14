using UnityEngine;

namespace GravityWells.Core
{
    // Random values built from System.Random with arithmetic and square root only, so the
    // generator gives the same level on every device (no trigonometry: see CLAUDE.md, third rule).
    public static class SeededMath
    {
        public static float Range(System.Random rng, float min, float max)
        {
            return min + (float)rng.NextDouble() * (max - min);
        }

        // A point spread evenly over the unit disc, by rejection from the square around it.
        public static Vector2 InUnitDisc(System.Random rng)
        {
            while (true)
            {
                float x = (float)rng.NextDouble() * 2f - 1f;
                float y = (float)rng.NextDouble() * 2f - 1f;
                if (x * x + y * y <= 1f)
                {
                    return new Vector2(x, y);
                }
            }
        }

        // A direction spread evenly over the circle: a disc point pushed out to the rim. Points
        // too near the centre are thrown away because normalising them magnifies rounding.
        public static Vector2 UnitVector(System.Random rng)
        {
            while (true)
            {
                Vector2 p = InUnitDisc(rng);
                float sqr = p.x * p.x + p.y * p.y;
                if (sqr >= 0.01f)
                {
                    float inv = 1f / Mathf.Sqrt(sqr);
                    return new Vector2(p.x * inv, p.y * inv);
                }
            }
        }
    }
}
