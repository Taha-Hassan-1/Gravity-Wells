using UnityEngine;
using GravityWells.Core;

namespace GravityWells.Game
{
    // A burst of small discs flying out of a point and fading: the goal when a level is won, the place a
    // flight ended when it failed. A fixed pool, and a fixed seed, so every burst looks the same every time
    // and nothing is allocated.
    public sealed class BurstView
    {
        private const int Order = 22;
        private const int Capacity = 56;

        private readonly SpriteRenderer[] _bits = new SpriteRenderer[Capacity];
        private readonly Vector2[] _position = new Vector2[Capacity];
        private readonly Vector2[] _velocity = new Vector2[Capacity];
        private readonly float[] _age = new float[Capacity];
        private readonly Color[] _colour = new Color[Capacity];
        private readonly float[] _size = new float[Capacity];
        private readonly System.Random _random = new System.Random(20261114);
        private int _next;

        public BurstView(Transform parent)
        {
            var root = new GameObject("Burst").transform;
            root.SetParent(parent, false);
            for (int i = 0; i < Capacity; i++)
            {
                var go = new GameObject("Bit " + i);
                go.transform.SetParent(root, false);
                SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = ShapeSprites.Disc;
                renderer.sortingOrder = Order;
                renderer.enabled = false;
                _bits[i] = renderer;
                _age[i] = -1f;
            }
        }

        // count bits fly out at up to `speed` field widths a second. Two colours alternate, so a burst has depth.
        public void Emit(Vector2 position, Color first, Color second, int count, float speed)
        {
            for (int n = 0; n < count; n++)
            {
                int i = _next;
                _next = (_next + 1) % Capacity;

                float angle = (float)_random.NextDouble() * Mathf.PI * 2f;
                float pace = speed * Mathf.Lerp(0.35f, 1f, (float)_random.NextDouble());
                _position[i] = position;
                _velocity[i] = new Vector2(Mathf.Cos(angle) * pace, Mathf.Sin(angle) * pace);
                _age[i] = 0f;
                _colour[i] = n % 2 == 0 ? first : second;
                _size[i] = Mathf.Lerp(0.007f, 0.015f, (float)_random.NextDouble());
                _bits[i].enabled = true;
                _bits[i].transform.localPosition = new Vector3(position.x, position.y, 0f);
            }
        }

        public void Tick(float seconds)
        {
            float drag = Mathf.Exp(-3.2f * seconds);
            for (int i = 0; i < Capacity; i++)
            {
                if (_age[i] < 0f)
                {
                    continue;
                }
                _age[i] += seconds;
                float u = _age[i] / GameConfig.BurstSeconds;
                if (u >= 1f)
                {
                    _age[i] = -1f;
                    _bits[i].enabled = false;
                    continue;
                }

                _velocity[i] *= drag;
                _position[i] += _velocity[i] * seconds;
                float size = _size[i] * (1f - 0.6f * u);
                _bits[i].transform.localPosition = new Vector3(_position[i].x, _position[i].y, 0f);
                _bits[i].transform.localScale = new Vector3(size * 2f, size * 2f, 1f);
                _bits[i].color = ViewColors.WithAlpha(_colour[i], 1f - u * u);
            }
        }
    }
}
