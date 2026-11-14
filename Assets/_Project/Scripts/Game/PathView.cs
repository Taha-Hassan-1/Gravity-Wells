using UnityEngine;
using UnityEngine.Rendering;
using GravityWells.Core;

namespace GravityWells.Game
{
    // The dotted line: the exact flight, drawn as one mesh of small discs. Its vertices are rewritten in
    // place whenever the trajectory changes, from arrays allocated once, so dragging a well allocates nothing.
    public sealed class PathView
    {
        public enum PathState
        {
            Neutral,   // just a path
            Winning,   // every gate lit and it ends in the goal
        }

        private const int OrderPath = 10;
        private const int CrashDots = 7;                  // how many dots at the end turn to the warning colour
        private static readonly int MaxDots = GameConfig.TrajectoryCapacity / GameConfig.PathDotStride + 2;

        private readonly Mesh _mesh;
        private readonly Vector3[] _vertices = new Vector3[MaxDots * 4];
        private readonly Color32[] _colors = new Color32[MaxDots * 4];
        private readonly int[] _indices = new int[MaxDots * 6];
        private readonly MeshRenderer _renderer;

        private ViewColors _palette;
        private Trajectory _last;
        private PathState _state;
        private int _dots;
        private int _crashFrom = int.MaxValue;

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

        public void ApplyTheme(ViewColors colors)
        {
            _palette = colors;
            if (_last != null)
            {
                Show(_last, _state);
            }
        }

        // Draws the buffer. Everything up to the outcome point is in it and nothing past it, so the line
        // ends exactly where the flight does.
        public void Show(Trajectory trajectory, PathState state)
        {
            _last = trajectory;
            _state = state;

            bool crash = trajectory.Outcome == FlightOutcome.HitWell || trajectory.Outcome == FlightOutcome.HitAsteroid;
            int stride = GameConfig.PathDotStride;
            int lastIndex = trajectory.Count - 1;

            int dots = 0;
            for (int i = 0; i <= lastIndex; i += stride)
            {
                SetDot(dots, trajectory.Points[i], GameConfig.PathDotRadius);
                dots++;
            }
            if ((lastIndex % stride) != 0)
            {
                SetDot(dots, trajectory.Points[lastIndex], GameConfig.PathDotRadius * 1.6f); // the end is always marked
                dots++;
            }

            _crashFrom = crash ? dots - CrashDots : int.MaxValue;
            Color body = state == PathState.Winning ? _palette.Good : ViewColors.WithAlpha(_palette.Neutral, 0.8f);
            Color32 body32 = ViewColors.ToVertexColor(body);
            Color32 warn32 = ViewColors.ToVertexColor(_palette.Warning);
            for (int d = 0; d < dots; d++)
            {
                Color32 c = d >= _crashFrom ? warn32 : body32;
                int v = d * 4;
                _colors[v] = c;
                _colors[v + 1] = c;
                _colors[v + 2] = c;
                _colors[v + 3] = c;
            }

            _dots = dots;
            // Whole arrays, so the vertex count never changes; only the dots in use are drawn, by cutting the
            // index range (a few hundred vertices cost nothing, and a fixed layout keeps this allocation-free).
            _mesh.SetVertices(_vertices);
            _mesh.SetColors(_colors);
            _mesh.SetIndices(_indices, 0, dots * 6, MeshTopology.Triangles, 0, false);
        }

        public int DotCount => _dots;

        private void SetDot(int index, Vector2 centre, float radius)
        {
            int v = index * 4;
            _vertices[v + 0] = new Vector3(centre.x - radius, centre.y - radius, 0f);
            _vertices[v + 1] = new Vector3(centre.x - radius, centre.y + radius, 0f);
            _vertices[v + 2] = new Vector3(centre.x + radius, centre.y + radius, 0f);
            _vertices[v + 3] = new Vector3(centre.x + radius, centre.y - radius, 0f);
        }
    }
}
