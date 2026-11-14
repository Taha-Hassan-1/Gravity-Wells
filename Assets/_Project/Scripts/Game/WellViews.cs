using System.Collections.Generic;
using UnityEngine;
using GravityWells.Core;

namespace GravityWells.Game
{
    // The wells on the field: a pool of small views, one per placed well, reused from level to level.
    // Each is a faint pull ring, two rings that travel inward for an attractor (and outward for a repulsor, so
    // the pull and the push can be read at a glance), an outline (whole for an attractor, broken for a repulsor,
    // so the two differ by shape and not only colour) and a core disc of exactly WellCoreRadius, so the crash
    // zone the player sees is the crash zone the simulator uses. A well that has just been dropped settles.
    public sealed class WellViews
    {
        private const int Order = 16;
        private const float BodyDiameter = 0.15f;
        private const int Rings = 2;

        private sealed class One
        {
            public GameObject Root;
            public SpriteRenderer Pull;
            public SpriteRenderer Body;
            public SpriteRenderer Core;
            public SpriteRenderer[] Ring = new SpriteRenderer[Rings];
            public bool Inward = true;
            public float Alpha = 1f;
            public Color Colour;
            public float SettleAge = -1f;     // seconds since it was dropped; negative = not settling
            public bool Held;
        }

        private readonly List<One> _views = new List<One>();
        private readonly Transform _root;
        private ViewColors _colors;
        private int _shown;
        private float _clock;

        public WellViews(Transform parent)
        {
            _root = new GameObject("Wells").transform;
            _root.SetParent(parent, false);
            for (int i = 0; i < GameConfig.MaxWells; i++)
            {
                _views.Add(Build(i));
            }
        }

        public void ApplyTheme(ViewColors colors) => _colors = colors;

        // Shows every well in the layout, and hides the spare views. Called whenever the layout changes
        // and on a theme change; it only sets fields on views that already exist.
        public void Sync(WellLayout layout)
        {
            _shown = layout.Count;
            for (int i = 0; i < _views.Count; i++)
            {
                One view = _views[i];
                bool shown = i < layout.Count;
                view.Root.SetActive(shown);
                if (!shown)
                {
                    view.SettleAge = -1f;
                    continue;
                }

                Well well = layout[i];
                bool held = layout.IsDragging && layout.DraggedIndex == i;
                bool fading = held && layout.DragWillRemove;
                view.Alpha = fading ? 0.3f : 1f;
                view.Held = held;
                view.Inward = well.Kind != WellKind.Repulsor;

                view.Root.transform.localPosition = new Vector3(well.Position.x, well.Position.y, 0f);
                view.Body.sprite = well.Kind == WellKind.Repulsor ? ShapeSprites.BrokenRing : ShapeSprites.Ring;

                Color body = held ? _colors.Active : _colors.Neutral;
                view.Colour = body;
                view.Body.color = ViewColors.WithAlpha(body, view.Alpha);
                view.Core.color = ViewColors.WithAlpha(body, view.Alpha);
                view.Pull.color = ViewColors.WithAlpha(body, (held ? 0.2f : 0.1f) * view.Alpha);
                view.Body.transform.localScale = Vector3.one * (BodyDiameter * (held ? 1.15f : 1f));
            }
        }

        // The well at this index has just been put down: it settles into place.
        public void Settle(int index)
        {
            if (index >= 0 && index < _views.Count)
            {
                _views[index].SettleAge = 0f;
            }
        }

        // Moves the rings and the settle on. Call every frame.
        public void Tick(float seconds)
        {
            _clock += seconds;
            for (int i = 0; i < _shown; i++)
            {
                One view = _views[i];

                // The rings: each is a copy of the same cycle, half a cycle apart.
                for (int r = 0; r < Rings; r++)
                {
                    float phase = _clock / GameConfig.WellRingPeriodSeconds + r * (1f / Rings);
                    float radius = Mathf.Lerp(GameConfig.WellCoreRadius * 1.6f, GameConfig.WellViewRadius, FeelCurves.RingRadius(phase, view.Inward));
                    float alpha = FeelCurves.RingAlpha(phase) * (view.Held ? 0.55f : 0.32f) * view.Alpha;
                    SpriteRenderer ring = view.Ring[r];
                    ring.transform.localScale = Vector3.one * (radius * 2f);
                    ring.color = ViewColors.WithAlpha(view.Colour, alpha);
                }

                // The settle: large, then in, with a small overshoot.
                float scale = 1f;
                if (view.SettleAge >= 0f)
                {
                    view.SettleAge += seconds;
                    scale = FeelCurves.Settle(view.SettleAge, GameConfig.WellSettleSeconds);
                    if (view.SettleAge >= GameConfig.WellSettleSeconds)
                    {
                        view.SettleAge = -1f;
                    }
                }
                view.Root.transform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        private One Build(int index)
        {
            var one = new One { Root = new GameObject("Well " + index) };
            one.Root.transform.SetParent(_root, false);
            one.Pull = Sprite("Pull", one.Root.transform, ShapeSprites.Ring, GameConfig.WellViewRadius * 2f, Order);
            for (int r = 0; r < Rings; r++)
            {
                one.Ring[r] = Sprite("Ring" + r, one.Root.transform, ShapeSprites.Ring, GameConfig.WellViewRadius * 2f, Order);
                one.Ring[r].sortingOrder = Order;
            }
            one.Body = Sprite("Body", one.Root.transform, ShapeSprites.Ring, BodyDiameter, Order + 1);
            one.Core = Sprite("Core", one.Root.transform, ShapeSprites.Disc, GameConfig.WellCoreRadius * 2f, Order + 2);
            one.Root.SetActive(false);
            return one;
        }

        private static SpriteRenderer Sprite(string name, Transform parent, Sprite sprite, float diameter, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(diameter, diameter, 1f);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }
    }
}
