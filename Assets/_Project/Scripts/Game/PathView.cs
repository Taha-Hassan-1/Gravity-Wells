using UnityEngine;
using UnityEngine.Rendering;
using GravityWells.Core;

namespace GravityWells.Game
{
    // The dotted line: the exact flight, drawn as one mesh of small discs. The dots drift along it, in the
    // direction the ship will travel, so even before anything is solved the line feels alive under the
    // finger. Its vertices are rewritten in place from arrays allocated once, so dragging allocates nothing.
    public sealed class PathView
    {
        public enum PathState
        {
            Neutral,   // just a path
            Winning,   // every gate lit and it ends in the goal
        }

        private const int OrderPath = 10;
        private const int CrashDots = 7;                  // how many dots at the end turn to the warning colour
        private const float EdgeFadeStrides = 1.6f;       // dots fade in at the start and out before the end over this many dot spacings
        private static readonly int MaxDots = GameConfig.TrajectoryCapacity / GameConfig.PathDotStride + 3;

        private readonly Mesh _mesh;
        private readonly Vector3[] _vertices = new Vector3[MaxDots * 4];
        private readonly Color32[] _colors = new Color32[MaxDots * 4];
        private readonly int[] _indices = new int[MaxDots * 6];
        private readonly MeshRenderer _renderer;

        private ViewColors _palette;
        private Vector2[] _points;
        private int _count;
        private FlightOutcome _outcome;
        private PathState _state;
        private float _phase;          // how far the dots have drifted along, in steps (0 to the dot spacing)
        private bool _hasPath;

        public PathView(Transform parent)
        {
            var go = new GameObject("Path");
            go.transform.SetParent(parent, false);

            var uvs = new Vector2[MaxDots * 4];
            for (int i = 0; i < MaxDots; i++)
            {
                int v = i * 4;
                uvs[v + 0] = new Vector2(0f, 0f);
                uvs[v + 1] = new Vector2(0f, 1f);
                uvs[v + 2] = new Vector2(1f, 1f);
                uvs[v + 3] = new Vector2(1f, 0f);
                int t = i * 6;
                _indices[t + 0] = v;
                _indices[t + 1] = v + 1;
                _indices[t + 2] = v + 2;
                _indices[t + 3] = v;
                _indices[t + 4] = v + 2;
                _indices[t + 5] = v + 3;
            }

            _mesh = new Mesh { name = "Path" };
            _mesh.MarkDynamic();
            _mesh.vertices = _vertices;
            _mesh.uv = uvs;
            _mesh.colors32 = _colors;
            _mesh.SetIndices(_indices, 0, 0, MeshTopology.Triangles, 0, false);
            _mesh.bounds = new Bounds(
                new Vector3(GameConfig.FieldWidth * 0.5f, GameConfig.FieldHeight * 0.5f, 0f), new Vector3(3f, 4f, 1f));

            go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = go.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = ShapeSprites.Disc.texture };
            _renderer.sortingOrder = OrderPath;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
        }

        public bool Visible
        {
            get => _renderer.enabled;
            set => _renderer.enabled = value;
        }

        public int DotCount { get; private set; }

        public void ApplyTheme(ViewColors colors)
        {
            _palette = colors;
            if (_hasPath)
            {
                Layout();
            }
        }

        // Draws a flight buffer. Everything up to the outcome point is in it and nothing past it, so the line
        // ends exactly where the flight does.
        public void Show(Trajectory trajectory, PathState state)
        {
            Show(trajectory.Points, trajectory.Count, trajectory.Outcome, state);
        }

        // The same, from any array of points (the eased line during a drop's glide).
        public void Show(Vector2[] points, int count, FlightOutcome outcome, PathState state)
        {
            _points = points;
            _count = count;
            _outcome = outcome;
            _state = state;
            _hasPath = count > 1;
            Layout();
        }

        // Drifts the dots along the line. Call every frame (any time scale: it is a decoration).
        public void Tick(float seconds)
        {
            if (!_hasPath)
            {
                return;
            }
            _phase += GameConfig.PathFlowStepsPerSecond * seconds;
            float stride = GameConfig.PathDotStride;
            if (_phase >= stride)
            {
                _phase -= stride * Mathf.Floor(_phase / stride);
            }
            Layout();
        }

        // Dots sit every PathDotStride steps along the buffer, shifted by the drift; the end of the line is
        // always marked by a larger dot, which does not drift.
        private void Layout()
        {
            if (!_hasPath)
            {
                _mesh.SetIndices(_indices, 0, 0, MeshTopology.Triangles, 0, false);
                DotCount = 0;
                return;
            }

            float stride = GameConfig.PathDotStride;
            float last = _count - 1;
            float fade = stride * EdgeFadeStrides;
            bool crash = _outcome == FlightOutcome.HitWell || _outcome == FlightOutcome.HitAsteroid;
            float crashFrom = last - CrashDots * stride;

            Color body = _state == PathState.Winning ? _palette.Good : ViewColors.WithAlpha(_palette.Neutral, 0.8f);
            Color warn = _palette.Warning;

            int dots = 0;
            for (float position = _phase; position < last - stride * 0.5f && dots < MaxDots - 1; position += stride)
            {
                Vector2 centre = PointAt(position);
                float alpha = Mathf.Clamp01(position / fade) * Mathf.Clamp01((last - position) / fade);
                bool warning = crash && position >= crashFrom;
                SetDot(dots, centre, GameConfig.PathDotRadius, ViewColors.ToVertexColor(Faded(warning ? warn : body, alpha)));
                dots++;
            }

            // The end marker, always whole.
            Vector2 end = _points[_count - 1];
            SetDot(dots, end, GameConfig.PathDotRadius * 1.6f, ViewColors.ToVertexColor(crash ? warn : body));
            dots++;

            DotCount = dots;
            _mesh.SetVertices(_vertices);
            _mesh.SetColors(_colors);
            _mesh.SetIndices(_indices, 0, dots * 6, MeshTopology.Triangles, 0, false);
        }

        private Vector2 PointAt(float position)
        {
            int index = (int)position;
            if (index >= _count - 1)
            {
                return _points[_count - 1];
            }
            float t = position - index;
            Vector2 a = _points[index];
            Vector2 b = _points[index + 1];
            return new Vector2(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t);
        }

        private static Color Faded(Color color, float alpha)
        {
            color.a *= alpha;
            return color;
        }

        private void SetDot(int index, Vector2 centre, float radius, Color32 color)
        {
            int v = index * 4;
            _vertices[v + 0] = new Vector3(centre.x - radius, centre.y - radius, 0f);
            _vertices[v + 1] = new Vector3(centre.x - radius, centre.y + radius, 0f);
            _vertices[v + 2] = new Vector3(centre.x + radius, centre.y + radius, 0f);
            _vertices[v + 3] = new Vector3(centre.x + radius, centre.y - radius, 0f);
            _colors[v] = color;
            _colors[v + 1] = color;
            _colors[v + 2] = color;
            _colors[v + 3] = color;
        }
    }
}
