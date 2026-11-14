using UnityEngine;
using GravityWells.Core;

namespace GravityWells.Game
{
    // The ship: a small triangle that points along its heading. On the pad it points along the launch
    // direction and, just before it goes, gathers: a ring draws in and the ship swells. In flight it is moved
    // by FlightPlayback and nothing else.
    public sealed class ShipView
    {
        private const int Order = 20;
        private const float Size = 0.055f;

        private readonly SpriteRenderer _renderer;
        private readonly SpriteRenderer _chargeRing;
        private ViewColors _colors;

        public ShipView(Transform parent)
        {
            var go = new GameObject("Ship");
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(Size, Size, 1f);
            _renderer = go.AddComponent<SpriteRenderer>();
            _renderer.sprite = ShapeSprites.Triangle;
            _renderer.sortingOrder = Order;

            var ring = new GameObject("ChargeRing");
            ring.transform.SetParent(parent, false);
            _chargeRing = ring.AddComponent<SpriteRenderer>();
            _chargeRing.sprite = ShapeSprites.Ring;
            _chargeRing.sortingOrder = Order - 1;
            _chargeRing.enabled = false;
        }

        public Vector2 Position => _renderer.transform.localPosition;

        public void ApplyTheme(ViewColors colors)
        {
            _colors = colors;
            _renderer.color = colors.Active;
        }

        public void Place(Vector2 position, Vector2 heading)
        {
            Transform t = _renderer.transform;
            t.localPosition = new Vector3(position.x, position.y, 0f);
            // The triangle points up at zero rotation; turn it to the heading.
            t.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg - 90f);
        }

        public void PlaceOnPad(LevelDefinition level)
        {
            SetCharge(0f);
            Vector2 v = level.LaunchVelocity;
            float length = Mathf.Sqrt(v.x * v.x + v.y * v.y);
            Place(level.Start, length > 0f ? new Vector2(v.x / length, v.y / length) : Vector2.up);
        }

        // 0 = at rest, 1 = about to go. The ship swells and a ring closes in on it.
        public void SetCharge(float progress)
        {
            float u = Mathf.Clamp01(progress);
            float scale = Size * FeelCurves.ChargeScale(u);
            _renderer.transform.localScale = new Vector3(scale, scale, 1f);

            bool charging = u > 0f && u < 1f;
            _chargeRing.enabled = charging;
            if (charging)
            {
                float diameter = Mathf.Lerp(0.30f, Size * 0.9f, u);
                _chargeRing.transform.localPosition = _renderer.transform.localPosition;
                _chargeRing.transform.localScale = new Vector3(diameter, diameter, 1f);
                _chargeRing.color = ViewColors.WithAlpha(_colors.Active, 0.2f + 0.7f * u);
            }
        }
    }
}
