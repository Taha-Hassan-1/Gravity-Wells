using System;

namespace GravityWells.Core
{
    // Eases a displayed number toward its real value so a coin change reads as coins arriving or
    // leaving instead of the figure jumping. Pure: the caller feeds it frame times.
    public sealed class CountUp
    {
        public const float DurationSeconds = 0.55f;
        public const float MinStepsPerSecond = 12f;

        private float _shown;
        private int _target;
        private float _speed; // value units per second

        public int Value => (int)Math.Round(_shown);
        public int Target => _target;
        public bool Animating => (int)Math.Round(_shown) != _target;

        public void SnapTo(int value)
        {
            _shown = value;
            _target = value;
            _speed = 0f;
        }

        // Large jumps take the full duration; small ones still move at a readable pace.
        public void SetTarget(int target)
        {
            _target = target;
            float distance = Math.Abs(target - _shown);
            _speed = Math.Max(distance / DurationSeconds, MinStepsPerSecond);
        }

        // Returns true when the visible number changed this frame.
        public bool Tick(float deltaSeconds)
        {
            if (!Animating)
            {
                _shown = _target;
                return false;
            }

            int before = Value;
            float step = _speed * Math.Max(0f, deltaSeconds);
            if (Math.Abs(_target - _shown) <= step)
            {
                _shown = _target;
            }
            else
            {
                _shown += Math.Sign(_target - _shown) * step;
            }
            return Value != before;
        }
    }
}
