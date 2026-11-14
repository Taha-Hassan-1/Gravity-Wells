using UnityEngine;

namespace GravityWells.Core
{
    public enum WellKind
    {
        Attractor,
        Repulsor,
    }

    public readonly struct Well
    {
        public readonly Vector2 Position;
        public readonly WellKind Kind;

        public Well(Vector2 position, WellKind kind = WellKind.Attractor)
        {
            Position = position;
            Kind = kind;
        }
    }
}
