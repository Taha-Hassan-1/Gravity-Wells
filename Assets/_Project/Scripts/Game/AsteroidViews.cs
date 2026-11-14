using UnityEngine;
using GravityWells.Core;

namespace GravityWells.Game
{
    // The asteroids: filled lumpy rocks in the warning colour, one per asteroid of the level, sized to the
    // collision radius the simulator uses so what is drawn is what is hit. Each is turned a fixed, different
    // amount so a field of them does not look stamped.
    public sealed class AsteroidViews
    {
        private const int Order = 11;
        private const int Capacity = 8;
        private const float RockScale = 1.08f;   // the rock's average radius is 0.91 of its sprite, so this makes it match the collision circle

        private readonly SpriteRenderer[] _views = new SpriteRenderer[Capacity];
        private int _count;
        private ViewColors _colors;

        public AsteroidViews(Transform parent)
        {
            var root = new GameObject("Asteroids").transform;
            root.SetParent(parent, false);
            for (int i = 0; i < Capacity; i++)
            {
                var go = new GameObject("Asteroid " + i);
                go.transform.SetParent(root, false);
                SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = ShapeSprites.Rock;
                renderer.sortingOrder = Order;
                go.SetActive(false);
                _views[i] = renderer;
            }
        }

        public int ActiveCount => _count;

        public void ApplyTheme(ViewColors colors)
        {
            _colors = colors;
            Paint();
        }

        public void ShowLevel(LevelDefinition level)
        {
            _count = Mathf.Min(level.Asteroids.Length, Capacity);
            for (int i = 0; i < Capacity; i++)
            {
                bool used = i < _count;
                _views[i].gameObject.SetActive(used);
                if (!used)
                {
                    continue;
                }

                Asteroid asteroid = level.Asteroids[i];
                Transform t = _views[i].transform;
                t.localPosition = new Vector3(asteroid.Centre.x, asteroid.Centre.y, 0f);
                float diameter = asteroid.Radius * 2f * RockScale;
                t.localScale = new Vector3(diameter, diameter, 1f);
                t.localRotation = Quaternion.Euler(0f, 0f, 47f * i + 13f);
            }
            Paint();
        }

        private void Paint()
        {
            for (int i = 0; i < _count; i++)
            {
                _views[i].color = ViewColors.WithAlpha(_colors.Warning, 0.9f);
            }
        }
    }
}
