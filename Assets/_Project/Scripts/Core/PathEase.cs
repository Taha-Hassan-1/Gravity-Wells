using UnityEngine;

namespace GravityWells.Core
{
    // A very short glide from the line that was on screen to the line that replaces it, used when the layout
    // changes in one jump (a drop, an undo, a restart) so the line settles instead of snapping. It is only
    // ever a picture: the flight that plays is always the real buffer, and while a well is held the line
    // follows the finger exactly, with no easing at all.
    public sealed class PathEase
    {
        private readonly Vector2[] _from = new Vector2[GameConfig.TrajectoryCapacity];
        private readonly Vector2[] _shown = new Vector2[GameConfig.TrajectoryCapacity];
        private int _fromCount;
        private float _elapsed;
        private Trajectory _to;

        public bool Active { get; private set; }

        // The points to draw this frame, and how many.
        public Vector2[] Points => _shown;
        public int Count { get; private set; }

        // Remembers the line now on screen, before it is replaced. Call it just before the buffer is overwritten.
        public void Capture(Trajectory current)
        {
            _fromCount = current.Count;
            System.Array.Copy(current.Points, _from, _fromCount);
        }

        // Starts the glide from the captured line to the line now in the buffer.
        public void Begin(Trajectory replacement)
        {
            _to = replacement;
            _elapsed = 0f;
            Active = _fromCount > 1;
            Fill(0f);
        }

        public void Cancel() => Active = false;

        // Advances the glide; returns false once it has arrived (the real buffer is then drawn as it is).
        public bool Tick(float seconds)
        {
            if (!Active)
            {
                return false;
            }

            _elapsed += seconds;
            float t = Mathf.Clamp01(_elapsed / GameConfig.PathEaseSeconds);
            Fill(t);
            if (t >= 1f)
            {
                Active = false;
            }
            return true;
        }

        // Ease-out: fast at first, settling at the end.
        public static float Curve(float t)
        {
            float inverse = 1f - Mathf.Clamp01(t);
            return 1f - inverse * inverse;
        }

        private void Fill(float t)
        {
            float k = Curve(t);
            Vector2[] target = _to.Points;
            int count = _to.Count;
            int shared = Mathf.Min(_fromCount, count);
            for (int i = 0; i < count; i++)
            {
                if (i < shared)
                {
                    _shown[i] = new Vector2(
                        _from[i].x + (target[i].x - _from[i].x) * k,
                        _from[i].y + (target[i].y - _from[i].y) * k);
                }
                else
                {
                    _shown[i] = target[i];
                }
            }
            Count = count;
        }
    }
}
