using System.Collections.Generic;
using UnityEngine;
using GravityWells.Core;

namespace GravityWells.Game
{
    // The wells on the field: a pool of small views, one per placed well, reused from level to level.
    // Each is a faint pull ring, an outline (whole for an attractor, broken for a repulsor, so the two
    // differ by shape and not only colour) and a core disc of exactly WellCoreRadius, so the crash zone the
    // player sees is the crash zone the simulator uses.
    public sealed class WellViews
    {
        private const int Order = 16;
        private const float BodyDiameter = 0.15f;

        private sealed class One
        {
            public GameObject Root;
            public SpriteRenderer Pull;
            public SpriteRenderer Body;
            public SpriteRenderer Core;
        }

        private readonly List<One> _views = new List<One>();
        private readonly Transform _root;
        private ViewColors _colors;

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
            for (int i = 0; i < _views.Count; i++)
            {
                One view = _views[i];
                bool shown = i < layout.Count;
                view.Root.SetActive(shown);
                if (!shown)
                {
                    continue;
                }

                Well well = layout[i];
                bool held = layout.IsDragging && layout.DraggedIndex == i;
                bool fading = held && layout.DragWillRemove;
                float alpha = fading ? 0.3f : 1f;

                view.Root.transform.localPosition = new Vector3(well.Position.x, well.Position.y, 0f);
                view.Body.sprite = well.Kind == WellKind.Repulsor ? ShapeSprites.BrokenRing : ShapeSprites.Ring;

                Color body = held ? _colors.Active : _colors.Neutral;
                view.Body.color = ViewColors.WithAlpha(body, alpha);
                view.Core.color = ViewColors.WithAlpha(body, alpha);
                view.Pull.color = ViewColors.WithAlpha(body, (held ? 0.22f : 0.12f) * alpha);
                view.Body.transform.localScale = Vector3.one * (BodyDiameter * (held ? 1.15f : 1f));
            }
        }

        private One Build(int index)
        {
            var one = new One { Root = new GameObject("Well " + index) };
            one.Root.transform.SetParent(_root, false);
            one.Pull = Sprite("Pull", one.Root.transform, ShapeSprites.Ring, GameConfig.WellViewRadius * 2f, Order);
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
