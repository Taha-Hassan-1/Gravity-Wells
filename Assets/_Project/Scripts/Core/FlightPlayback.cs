using UnityEngine;

namespace GravityWells.Core
{
    // Plays a finished Trajectory back in real time. It only ever reads the buffer the preview drew, so the
    // ship cannot leave the line the player was shown; nothing is simulated here.
    public sealed class FlightPlayback
    {
        private Trajectory _trajectory;
        private float _position;   // in steps; the whole part picks the segment, the fraction is how far along it

        public float Speed { get; private set; } = GameConfig.PlaybackSpeed;
        public bool Finished { get; private set; } = true;
        public Vector2 Position { get; private set; }

        // The direction of the segment the ship is on (unit length; the last direction once it has stopped).
        public Vector2 Heading { get; private set; } = Vector2.up;

        public int Step => (int)_position;
        public FlightOutcome Outcome => _trajectory != null ? _trajectory.Outcome : FlightOutcome.TimedOut;

        public void Begin(Trajectory trajectory)
        {
            _trajectory = trajectory;
            _position = 0f;
            Speed = GameConfig.PlaybackSpeed;
            Finished = trajectory.Count < 2;
            Place();
        }

        // A tap during the flight.
        public void SpeedUp() => Speed = GameConfig.FastPlaybackSpeed;

        public void Advance(float seconds)
        {
            if (Finished)
            {
                return;
            }

            _position += seconds * GameConfig.StepsPerSecond * Speed;
            float last = _trajectory.Count - 1;
            if (_position >= last)
            {
                _position = last;
                Finished = true;
            }
            Place();
        }

        // Gates the ship has passed so far: a gate lights when the ship reaches the end of the step that
        // crossed it, which is when the preview's line crossed it too.
        public int GatesLitSoFar()
        {
            int lit = 0;
            int mask = GatesLitMaskSoFar();
            for (int g = 0; g < GameConfig.MaxGates; g++)
            {
                lit += (mask >> g) & 1;
            }
            return lit;
        }

        // The same as a bit mask (bit g = gate g), for the gate views.
        public int GatesLitMaskSoFar()
        {
            int mask = 0;
            for (int g = 0; g < GameConfig.MaxGates; g++)
            {
                int step = _trajectory.GateLitStep(g);
                if (step >= 0 && _position >= step)
                {
                    mask |= 1 << g;
                }
            }
            return mask;
        }

        private void Place()
        {
            Vector2[] points = _trajectory.Points;
            int count = _trajectory.Count;
            int index = Mathf.Min((int)_position, count - 1);
            if (index >= count - 1)
            {
                Position = points[count - 1];
                if (count >= 2)
                {
                    SetHeading(points[count - 2], points[count - 1]);
                }
                return;
            }

            float t = _position - index;
            Vector2 a = points[index];
            Vector2 b = points[index + 1];
            Position = new Vector2(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t);
            SetHeading(a, b);
        }

        private void SetHeading(Vector2 from, Vector2 to)
        {
            float dx = to.x - from.x;
            float dy = to.y - from.y;
            float length = Mathf.Sqrt(dx * dx + dy * dy);
            if (length > 1e-9f)
            {
                Heading = new Vector2(dx / length, dy / length);
            }
        }
    }
}
