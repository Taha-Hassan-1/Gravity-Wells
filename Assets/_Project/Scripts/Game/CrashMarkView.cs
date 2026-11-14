using UnityEngine;
using GravityWells.Core;

namespace GravityWells.Game
{
    // The mark where a flight ended without winning: a small cross, so the player sees exactly where the ship
    // died or left the field. It is a shape (two bars), not only a colour.
    public sealed class CrashMarkView
    {
        private const int Order = 23;   // above the burst, so the cross is never buried in it
        private const float Length = 0.06f;
        private const float Thickness = 0.011f;
        private const float PopSeconds = 0.25f;

        private readonly Transform _root;
        private readonly SpriteRenderer _a;
        private readonly SpriteRenderer _b;
        private ViewColors _colors;
        private float _age = -1f;

        public CrashMarkView(Transform parent)
        {
            _root = new GameObject("CrashMark").transform;
            _root.SetParent(parent, false);
            _a = Bar("A", 45f);
            _b = Bar("B", -45f);
            Hide();
        }

        public bool Visible => _a.enabled;

        public void ApplyTheme(ViewColors colors)
        {
            _colors = colors;
            Paint(1f);
        }

        public void Show(Vector2 position)
        {
            _root.localPosition = new Vector3(position.x, position.y, 0f);
            _a.enabled = true;
            _b.enabled = true;
            _age = 0f;
            Paint(0f);
        }

        public void Hide()
        {
            _a.enabled = false;
            _b.enabled = false;
            _age = -1f;
        }

        public void Tick(float seconds)
        {
            if (_age < 0f)
            {
                return;
            }
            _age += seconds;
            float u = Mathf.Clamp01(_age / PopSeconds);
            Paint(u);
            if (u >= 1f)
            {
                _age = -1f;
            }
        }

        // Pops in a little large and settles to its size.
        private void Paint(float u)
        {
            float scale = FeelCurves.Settle(u * PopSeconds, PopSeconds);
            _root.localScale = new Vector3(scale, scale, 1f);
            _a.color = _colors.Warning;
            _b.color = _colors.Warning;
        }

        private SpriteRenderer Bar(string name, float degrees)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, degrees);
            go.transform.localScale = new Vector3(Length, Thickness, 1f);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = ShapeSprites.Square;
            renderer.sortingOrder = Order;
            return renderer;
        }
    }
}
