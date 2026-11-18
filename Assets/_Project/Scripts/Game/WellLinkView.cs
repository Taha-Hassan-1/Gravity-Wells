using UnityEngine;
using GravityWells.Core;

namespace GravityWells.Game
{
    // The tutorial's "who bends what": from every placed well a thin run of dots flows to the spot on the line
    // where that well pulls hardest. The dots flow toward an attractor and away from a repulsor (the same
    // direction as the force), and the link is bright when the well matters and faint when it barely does. The
    // well in the finger is drawn at full strength and the others dimmer, so while dragging one of several
    // wells the player sees exactly what that one is doing to the line.
    public sealed class WellLinkView
    {
        private const int Order = 11;          // above the path, below the wells
        private const int DotsPerLink = 5;
        private const float DotDiameter = 0.015f;
        private const float MarkerDiameter = 0.032f;
        private const float FlowSeconds = 1.4f;

        private sealed class One
        {
            public GameObject Root;
            public SpriteRenderer Marker;
            public SpriteRenderer[] Dots = new SpriteRenderer[DotsPerLink];
            public Vector2 From;
            public Vector2 To;
            public float Strength;
            public bool Held;
            public Color Colour;
        }

        private readonly One[] _links = new One[GameConfig.MaxWells];
        private readonly Influence[] _influence = new Influence[GameConfig.MaxWells];
        private readonly Well[] _scratch = new Well[GameConfig.MaxWells];
        private readonly Transform _root;
        private ViewColors _colors;
        private int _shown;
        private bool _visible = true;
        private float _clock;

        public WellLinkView(Transform parent)
        {
            _root = new GameObject("WellLinks").transform;
            _root.SetParent(parent, false);
            for (int i = 0; i < _links.Length; i++)
            {
                _links[i] = Build(i);
            }
        }

        public int Shown => _shown;

        public void ApplyTheme(ViewColors colors) => _colors = colors;

        // Called whenever the layout or the line changes: works out each link's two ends and strength.
        public void Sync(WellLayout layout, Trajectory trajectory)
        {
            // Indexed like the layout (the simulation list skips a well that is being dragged off the field).
            for (int i = 0; i < layout.Count; i++)
            {
                _scratch[i] = layout[i];
            }
            WellInfluence.Compute(trajectory, _scratch, layout.Count, _influence);
            _shown = Mathf.Min(layout.Count, _links.Length);
            for (int i = 0; i < _links.Length; i++)
            {
                One link = _links[i];
                bool leaving = layout.IsDragging && layout.DraggedIndex == i && layout.DragWillRemove;
                if (i >= _shown || leaving || !_influence[i].Valid)
                {
                    link.Root.SetActive(false);
                    continue;
                }

                Well well = layout[i];
                bool inward = well.Kind != WellKind.Repulsor;
                link.Held = layout.IsDragging && layout.DraggedIndex == i;
                link.Strength = _influence[i].Strength;
                link.Colour = _colors.Active;
                // The dots travel the way the force acts on the ship.
                link.From = inward ? _influence[i].Point : well.Position;
                link.To = inward ? well.Position : _influence[i].Point;
                link.Root.SetActive(_visible);
                link.Marker.transform.localPosition = new Vector3(_influence[i].Point.x, _influence[i].Point.y, 0f);
                Paint(link);
            }
            Place(0f);
        }

        // Links belong to planning; while the ship flies or a card is up they are hidden.
        public void SetVisible(bool visible)
        {
            _visible = visible;
            for (int i = 0; i < _shown; i++)
            {
                _links[i].Root.SetActive(visible);
            }
        }

        public void Tick(float seconds)
        {
            if (!_visible || _shown == 0)
            {
                return;
            }
            _clock += seconds;
            Place(_clock / FlowSeconds);
        }

        private void Place(float phase)
        {
            for (int i = 0; i < _shown; i++)
            {
                One link = _links[i];
                for (int d = 0; d < DotsPerLink; d++)
                {
                    float t = Mathf.Repeat(phase + d / (float)DotsPerLink, 1f);
                    Vector2 p = Vector2.Lerp(link.From, link.To, t);
                    SpriteRenderer dot = link.Dots[d];
                    dot.transform.localPosition = new Vector3(p.x, p.y, 0f);
                    // Fade in at the start and out at the end so the flow reads as a direction.
                    float edge = Mathf.Sin(t * Mathf.PI);
                    dot.color = ViewColors.WithAlpha(link.Colour, AlphaFor(link) * edge);
                }
            }
        }

        private static float AlphaFor(One link) => Mathf.Lerp(0.35f, 0.95f, link.Strength) * (link.Held ? 1f : 0.75f);

        private void Paint(One link)
        {
            link.Marker.color = ViewColors.WithAlpha(link.Colour, AlphaFor(link));
        }

        private One Build(int index)
        {
            var one = new One { Root = new GameObject("Link " + index) };
            one.Root.transform.SetParent(_root, false);
            one.Marker = Sprite("Marker", one.Root.transform, ShapeSprites.Ring, MarkerDiameter);
            for (int d = 0; d < DotsPerLink; d++)
            {
                one.Dots[d] = Sprite("Dot" + d, one.Root.transform, ShapeSprites.Disc, DotDiameter);
            }
            one.Root.SetActive(false);
            return one;
        }

        private static SpriteRenderer Sprite(string name, Transform parent, Sprite sprite, float diameter)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(diameter, diameter, 1f);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = Order;
            return renderer;
        }
    }
}
