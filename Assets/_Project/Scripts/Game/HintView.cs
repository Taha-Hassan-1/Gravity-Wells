using UnityEngine;
using GravityWells.Core;

namespace GravityWells.Game
{
    // The hint, as a ghost: a ring where a well would help, and, when it is a placed well that should move, a
    // line from where it stands now. The player makes the move; nothing is applied for them. (Charging for a
    // hint comes with the meta phase.)
    public sealed class HintView
    {
        private const int Order = 15;
        private const float GhostDiameter = 0.15f;
        private const float LineThickness = 0.006f;

        private readonly SpriteRenderer _ring;
        private readonly SpriteRenderer _line;
        private ViewColors _colors;

        public HintView(Transform parent)
        {
            var root = new GameObject("Hint").transform;
            root.SetParent(parent, false);
            _ring = Make("Ring", root, ShapeSprites.Ring);
            _ring.transform.localScale = new Vector3(GhostDiameter, GhostDiameter, 1f);
            _line = Make("Line", root, ShapeSprites.Square);
            Hide();
        }

        public bool Visible => _ring.enabled;

        public void ApplyTheme(ViewColors colors)
        {
            _colors = colors;
            _ring.color = colors.Lit;
            _line.color = ViewColors.WithAlpha(colors.Lit, 0.7f);
        }

        // from is where the well to move stands now; pass hasFrom = false for a well that is not placed yet.
        public void Show(Vector2 target, WellKind kind, bool hasFrom, Vector2 from)
        {
            _ring.sprite = kind == WellKind.Repulsor ? ShapeSprites.BrokenRing : ShapeSprites.Ring;
            _ring.transform.localPosition = new Vector3(target.x, target.y, 0f);
            _ring.enabled = true;

            _line.enabled = hasFrom;
            if (!hasFrom)
            {
                return;
            }

            float dx = target.x - from.x;
            float dy = target.y - from.y;
            float length = Mathf.Sqrt(dx * dx + dy * dy);
            _line.transform.localPosition = new Vector3((target.x + from.x) * 0.5f, (target.y + from.y) * 0.5f, 0f);
            _line.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
            _line.transform.localScale = new Vector3(length, LineThickness, 1f);
        }

        public void Hide()
        {
            _ring.enabled = false;
            _line.enabled = false;
        }

        private static SpriteRenderer Make(string name, Transform parent, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = Order;
            return renderer;
        }
    }
}
