using UnityEngine;
using GravityWells.Core;

namespace GravityWells.Game
{
    // The ship: a small triangle that points along its heading. On the pad it points along the launch
    // direction; in flight it is moved by FlightPlayback and nothing else.
    public sealed class ShipView
    {
        private const int Order = 20;
        private const float Size = 0.055f;

        private readonly SpriteRenderer _renderer;

        public ShipView(Transform parent)
        {
            var go = new GameObject("Ship");
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(Size, Size, 1f);
            _renderer = go.AddComponent<SpriteRenderer>();
            _renderer.sprite = ShapeSprites.Triangle;
            _renderer.sortingOrder = Order;
        }

        public Vector2 Position => _renderer.transform.localPosition;

        public void ApplyTheme(ViewColors colors) => _renderer.color = colors.Active;

        public void Place(Vector2 position, Vector2 heading)
        {
            Transform t = _renderer.transform;
            t.localPosition = new Vector3(position.x, position.y, 0f);
            // The triangle points up at zero rotation; turn it to the heading.
            t.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg - 90f);
        }

        public void PlaceOnPad(LevelDefinition level)
        {
            Vector2 v = level.LaunchVelocity;
            float length = Mathf.Sqrt(v.x * v.x + v.y * v.y);
            Place(level.Start, length > 0f ? new Vector2(v.x / length, v.y / length) : Vector2.up);
        }
    }
}
