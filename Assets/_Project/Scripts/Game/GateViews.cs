using UnityEngine;
using GravityWells.Core;

namespace GravityWells.Game
{
    // The gates: two posts and a thin bar each. An unlit gate is hollow (ring posts, a thin faint bar); a lit
    // one is filled (disc posts, a thicker bright bar). The change of shape, not only of colour, is what says
    // "lit" (CLAUDE.md: never encode meaning in colour alone). A gate that lights pops; one the ship passes
    // flashes a ring; a gate the flight missed blinks twice in the warning colour.
    public sealed class GateViews
    {
        private const int Order = 14;
        private const float PostDiameter = 0.036f;
        private const float UnlitThickness = 0.004f;
        private const float LitThickness = 0.011f;
        private const float BlinkSeconds = 0.9f;

        private sealed class One
        {
            public GameObject Root;
            public SpriteRenderer Bar;
            public SpriteRenderer PostA;
            public SpriteRenderer PostB;
            public SpriteRenderer Flash;
            public float Length;
            public Vector2 Middle;
            public float PopAge = -1f;
            public float FlashAge = -1f;
            public float BlinkAge = -1f;
            public bool Animating;
        }

        private readonly One[] _views = new One[GameConfig.MaxGates];
        private readonly Transform _root;
        private ViewColors _colors;
        private int _count;
        private int _litMask;

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
                view.PopAge = view.FlashAge = view.BlinkAge = -1f;
                bool used = i < _count;
                view.Root.SetActive(used);
                if (!used)
                {
                    continue;
                }

                Gate gate = level.Gates[i];
                float dx = gate.B.x - gate.A.x;
                float dy = gate.B.y - gate.A.y;
                view.Length = Mathf.Sqrt(dx * dx + dy * dy);
                view.Middle = gate.Midpoint;

                view.PostA.transform.localPosition = new Vector3(gate.A.x, gate.A.y, 0f);
                view.PostB.transform.localPosition = new Vector3(gate.B.x, gate.B.y, 0f);
                view.Bar.transform.localPosition = new Vector3(view.Middle.x, view.Middle.y, 0f);
                view.Bar.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
                view.Flash.transform.localPosition = new Vector3(view.Middle.x, view.Middle.y, 0f);
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

        // A gate has just lit in the preview: it pops.
        public void Pop(int index)
        {
            if (index >= 0 && index < _count)
            {
                _views[index].PopAge = 0f;
            }
        }

        // The ship has just passed through a gate: a ring spreads from it.
        public void Flash(int index)
        {
            if (index >= 0 && index < _count)
            {
                _views[index].FlashAge = 0f;
            }
        }

        // The flight missed these gates: they blink twice in the warning colour.
        public void Blink(int mask)
        {
            for (int i = 0; i < _count; i++)
            {
                if ((mask & (1 << i)) != 0)
                {
                    _views[i].BlinkAge = 0f;
                }
            }
        }

        // Moves the pops, flashes and blinks on. Call every frame.
        public void Tick(float seconds)
        {
            for (int i = 0; i < _count; i++)
            {
                One view = _views[i];
                bool lit = (_litMask & (1 << i)) != 0;
                bool animating = false;

                float pop = 0f;
                if (view.PopAge >= 0f)
                {
                    view.PopAge += seconds;
                    pop = FeelCurves.Bump(view.PopAge, GameConfig.GatePopSeconds);
                    animating = true;
                    if (view.PopAge >= GameConfig.GatePopSeconds)
                    {
                        view.PopAge = -1f;
                        pop = 0f;
                    }
                }

                float blink = 0f;
                if (view.BlinkAge >= 0f)
                {
                    view.BlinkAge += seconds;
                    blink = FeelCurves.Blink(view.BlinkAge, BlinkSeconds, 2);
                    animating = true;
                    if (view.BlinkAge >= BlinkSeconds)
                    {
                        view.BlinkAge = -1f;
                        blink = 0f;
                    }
                }

                if (view.FlashAge >= 0f)
                {
                    view.FlashAge += seconds;
                    float u = view.FlashAge / GameConfig.GateFlashSeconds;
                    if (u >= 1f)
                    {
                        view.FlashAge = -1f;
                        view.Flash.enabled = false;
                    }
                    else
                    {
                        view.Flash.enabled = true;
                        float diameter = Mathf.Lerp(view.Length * 0.35f, view.Length * 1.5f, FeelCurves.RingRadius(u, inward: false));
                        view.Flash.transform.localScale = new Vector3(diameter, diameter, 1f);
                        view.Flash.color = ViewColors.WithAlpha(_colors.Lit, 0.85f * (1f - u));
                    }
                }

                if (animating || view.Animating)
                {
                    Apply(view, lit, pop, blink);
                }
                view.Animating = animating;
            }
        }

        private void Paint()
        {
            for (int i = 0; i < _count; i++)
            {
                Apply(_views[i], (_litMask & (1 << i)) != 0, 0f, 0f);
            }
        }

        private void Apply(One view, bool lit, float pop, float blink)
        {
            Sprite post = lit ? ShapeSprites.Disc : ShapeSprites.Ring;
            view.PostA.sprite = post;
            view.PostB.sprite = post;

            Color unlit = ViewColors.WithAlpha(_colors.Neutral, 0.7f);
            Color unlitBar = ViewColors.WithAlpha(_colors.Neutral, 0.35f);
            Color postColor = lit ? _colors.Lit : unlit;
            Color barColor = lit ? _colors.Lit : unlitBar;
            if (blink > 0f && !lit)
            {
                postColor = Color.Lerp(postColor, _colors.Warning, blink);
                barColor = Color.Lerp(barColor, ViewColors.WithAlpha(_colors.Warning, 0.9f), blink);
            }
            view.PostA.color = postColor;
            view.PostB.color = postColor;
            view.Bar.color = barColor;

            float diameter = PostDiameter * (1f + 0.7f * pop + 0.35f * blink);
            view.PostA.transform.localScale = new Vector3(diameter, diameter, 1f);
            view.PostB.transform.localScale = new Vector3(diameter, diameter, 1f);
            float thickness = (lit ? LitThickness : UnlitThickness) * (1f + 1.1f * pop + 1.4f * blink);
            view.Bar.transform.localScale = new Vector3(view.Length, thickness, 1f);
        }

        private One Build(int index)
        {
            var one = new One { Root = new GameObject("Gate " + index) };
            one.Root.transform.SetParent(_root, false);
            one.Bar = Sprite("Bar", one.Root.transform, ShapeSprites.Square, Order);
            one.PostA = Sprite("PostA", one.Root.transform, ShapeSprites.Ring, Order + 1);
            one.PostB = Sprite("PostB", one.Root.transform, ShapeSprites.Ring, Order + 1);
            one.Flash = Sprite("Flash", one.Root.transform, ShapeSprites.Ring, Order + 2);
            one.Flash.enabled = false;
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
