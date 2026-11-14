using UnityEngine;

namespace GravityWells.Core
{
    // A gate is the segment between its two posts.
    public readonly struct Gate
    {
        public readonly Vector2 A;
        public readonly Vector2 B;

        public Gate(Vector2 a, Vector2 b)
        {
            A = a;
            B = b;
        }

        public Vector2 Midpoint => new Vector2((A.x + B.x) * 0.5f, (A.y + B.y) * 0.5f);
    }
}
