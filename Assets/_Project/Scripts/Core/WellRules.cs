using UnityEngine;

namespace GravityWells.Core
{
    // Where a well may be put. One place, so the drag, the solver and the hint cannot disagree.
    public static class WellRules
    {
        public static bool IsLegal(LevelDefinition level, Vector2 position)
        {
            if (position.x < 0f || position.x > GameConfig.FieldWidth || position.y < 0f || position.y > GameConfig.FieldHeight)
            {
                return false;
            }
            float dx = position.x - level.Start.x;
            float dy = position.y - level.Start.y;
            return dx * dx + dy * dy >= GameConfig.MinWellDistanceFromStart * GameConfig.MinWellDistanceFromStart;
        }

        // The nearest legal position: pulled inside the field, then pushed out of the circle around the start.
        public static Vector2 Clamp(LevelDefinition level, Vector2 position)
        {
            float x = Mathf.Clamp(position.x, 0f, GameConfig.FieldWidth);
            float y = Mathf.Clamp(position.y, 0f, GameConfig.FieldHeight);

            float dx = x - level.Start.x;
            float dy = y - level.Start.y;
            float sqr = dx * dx + dy * dy;
            float min = GameConfig.MinWellDistanceFromStart;
            if (sqr >= min * min)
            {
                return new Vector2(x, y);
            }

            // Straight up when the well sits exactly on the start, where there is no direction to push along.
            float distance = Mathf.Sqrt(sqr);
            float ux = distance > 1e-6f ? dx / distance : 0f;
            float uy = distance > 1e-6f ? dy / distance : 1f;
            const float nudge = 1.001f; // lands just outside, so IsLegal agrees despite rounding
            return new Vector2(level.Start.x + ux * min * nudge, level.Start.y + uy * min * nudge);
        }
    }
}
