using UnityEngine;

namespace GravityWells.Core
{
    public readonly struct Asteroid
    {
        public readonly Vector2 Centre;
        public readonly float Radius;

        public Asteroid(Vector2 centre, float radius)
        {
            Centre = centre;
            Radius = radius;
        }
    }
}
