using UnityEngine;
using GravityWells.Core;

namespace GravityWells.Game
{
    // The ship's trail: a short string of small discs left behind it that shrink and fade. A fixed pool,
    // reused as a ring, so a flight allocates nothing.
    public sealed class TrailView
    {
        private const int Order = 19;
        private const float Life = 0.55f;
        private const float StartRadius = 0.0085f;

        private readonly SpriteRenderer[] _dots = new SpriteRenderer[GameConfig.TrailDots];
        private readonly float[] _age = new float[GameConfig.TrailDots];
        private int _next;
        private float _sinceLast;
        private ViewColors _colors;

        public TrailView(Transform parent)
        {
            var root = new GameObject("Trail").transform;
            root.SetParent(parent, false);
            for (int i = 0; i < _dots.Length; i++)
            {
                var go = new GameObject("Dot " + i);
                go.transform.SetParent(root, false);
                SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = ShapeSprites.Disc;
                renderer.sortingOrder = Order;
                renderer.enabled = false;
                _dots[i] = renderer;
                _age[i] = -1f;
            }
        }

        public void ApplyTheme(ViewColors colors) => _colors = colors;

        // Leaves a dot at the ship's position if enough flight time has passed since the last one.
        public void Drop(Vector2 position, float flightSeconds)
        {
            _sinceLast += flightSeconds;
            if (_sinceLast < GameConfig.TrailDotSpacingSeconds)
            {
                return;
            }
            _sinceLast = 0f;

            SpriteRenderer dot = _dots[_next];
            dot.transform.localPosition = new Vector3(position.x, position.y, 0f);
            _age[_next] = 0f;
            dot.enabled = true;
            _next = (_next + 1) % _dots.Length;
        }

        public void Clear()
        {
            for (int i = 0; i < _dots.Length; i++)
            {
                _age[i] = -1f;
                _dots[i].enabled = false;
            }
            _sinceLast = 0f;
        }

        public void Tick(float seconds)
        {
            for (int i = 0; i < _dots.Length; i++)
            {
                if (_age[i] < 0f)
                {
                    continue;
                }
                _age[i] += seconds;
                float u = _age[i] / Life;
                if (u >= 1f)
                {
                    _age[i] = -1f;
                    _dots[i].enabled = false;
                    continue;
                }
                float radius = StartRadius * (1f - 0.7f * u);
                _dots[i].transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
                _dots[i].color = ViewColors.WithAlpha(_colors.Active, 0.75f * (1f - u));
            }
        }
    }
}
