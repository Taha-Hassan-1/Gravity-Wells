using UnityEngine;
using GravityWells.Core;

namespace GravityWells.Game
{
    // The gates: two posts and a thin bar each. An unlit gate is hollow (ring posts, a thin faint bar); a lit
    // one is filled (disc posts, a thicker bright bar). The change of shape, not only of colour, is what says
    // "lit" (CLAUDE.md: never encode meaning in colour alone).
    public sealed class GateViews
    {
        private const int Order = 14;
        private const float PostDiameter = 0.036f;
        private const float UnlitThickness = 0.004f;
        private const float LitThickness = 0.011f;

        private sealed class One
        {
            public GameObject Root;
            public SpriteRenderer Bar;
            public SpriteRenderer PostA;
            public SpriteRenderer PostB;
        }

        private readonly One[] _views = new One[GameConfig.MaxGates];
        private readonly Transform _root;
        private ViewColors _colors;
        private int _count;
        private int _litMask;
        private float _length;

        public GateViews(Transform parent)
        {
            _root = new GameObject("Gates").transform;
            _root.SetParent(parent, false);
            for (int i = 0; i < _views.Length; i++)
            {
                _views[i] = Build(i);
            }
        }

        public void ApplyTheme(ViewColors colors)
        {
            _colors = colors;
            Paint();
        }

        public void ShowLevel(LevelDefinition level)
        {
            _count = level.Gates.Length;
            _litMask = 0;
            for (int i = 0; i < _views.Length; i++)
            {
                One view = _views[i];
                bool used = i < _count;
                view.Root.SetActive(used);
                if (!used)
                {
                    continue;
                }

                Gate gate = level.Gates[i];
                float dx = gate.B.x - gate.A.x;
                float dy = gate.B.y - gate.A.y;
                _length = Mathf.Sqrt(dx * dx + dy * dy);

                view.PostA.transform.localPosition = new Vector3(gate.A.x, gate.A.y, 0f);
                view.PostB.transform.localPosition = new Vector3(gate.B.x, gate.B.y, 0f);
                Vector2 mid = gate.Midpoint;
                view.Bar.transform.localPosition = new Vector3(mid.x, mid.y, 0f);
                view.Bar.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
                view.Bar.transform.localScale = new Vector3(_length, UnlitThickness, 1f);
            }
            Paint();
        }

        // Bit i set = gate i is lit on the flight now shown.
        public void SetLitMask(int mask)
        {
            if (mask == _litMask)
            {
                return;
            }
            _litMask = mask;
            Paint();
        }

        private void Paint()
        {
            for (int i = 0; i < _count; i++)
            {
                One view = _views[i];
                bool lit = (_litMask & (1 << i)) != 0;
                Sprite post = lit ? ShapeSprites.Disc : ShapeSprites.Ring;
                view.PostA.sprite = post;
                view.PostB.sprite = post;

                Color color = lit ? _colors.Lit : ViewColors.WithAlpha(_colors.Neutral, 0.7f);
                view.PostA.color = color;
                view.PostB.color = color;
                view.Bar.color = lit ? _colors.Lit : ViewColors.WithAlpha(_colors.Neutral, 0.35f);

                Vector3 scale = view.Bar.transform.localScale;
                view.Bar.transform.localScale = new Vector3(scale.x, lit ? LitThickness : UnlitThickness, 1f);
            }
        }

        private One Build(int index)
        {
            var one = new One { Root = new GameObject("Gate " + index) };
            one.Root.transform.SetParent(_root, false);
            one.Bar = Sprite("Bar", one.Root.transform, ShapeSprites.Square, Order);
            one.PostA = Sprite("PostA", one.Root.transform, ShapeSprites.Ring, Order + 1);
            one.PostB = Sprite("PostB", one.Root.transform, ShapeSprites.Ring, Order + 1);
            one.PostA.transform.localScale = new Vector3(PostDiameter, PostDiameter, 1f);
            one.PostB.transform.localScale = new Vector3(PostDiameter, PostDiameter, 1f);
            one.Root.SetActive(false);
            return one;
        }

        private static SpriteRenderer Sprite(string name, Transform parent, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }
    }
}
