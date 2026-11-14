using UnityEngine;
using GravityWells.Core;

namespace GravityWells.Game
{
    // The static parts of the board: a faint star background (one mesh, one draw call), the playfield
    // panel, the start pad and the goal. The goal is a ring that fills when every gate is lit.
    public sealed class FieldView
    {
        private const int StarCount = 140;
        private const int StarSeed = 20261114;   // the same sky on every launch
        private const int OrderStars = 0;
        private const int OrderPanel = 1;
        private const int OrderPad = 12;
        private const int OrderGoal = 13;

        private readonly Mesh _starMesh;
        private readonly Color32[] _starColors = new Color32[StarCount * 4];
        private readonly float[] _starBrightness = new float[StarCount];
        private readonly SpriteRenderer _panel;
        private readonly SpriteRenderer _padRing;
        private readonly SpriteRenderer _padDot;
        private readonly SpriteRenderer _goalRing;
        private readonly SpriteRenderer _goalFill;
        private readonly Transform _root;

        private ViewColors _colors;
        private bool _goalReached;
        private float _goalPopAge = -1f;
        private float _goalFillScale;
        private float _clock;
        private float _twinkleTimer;
        private readonly float[] _twinkleSpeed = new float[StarCount];
        private readonly float[] _twinklePhase = new float[StarCount];

        public FieldView(Transform parent)
        {
            _root = new GameObject("Field").transform;
            _root.SetParent(parent, false);

            _starMesh = BuildStars(_root);

            _panel = Make("Panel", ShapeSprites.Square, OrderPanel);
            _panel.transform.localPosition = new Vector3(GameConfig.FieldWidth * 0.5f, GameConfig.FieldHeight * 0.5f, 0f);
            _panel.transform.localScale = new Vector3(GameConfig.FieldWidth, GameConfig.FieldHeight, 1f);

            _padRing = Make("StartPadRing", ShapeSprites.Ring, OrderPad);
            _padDot = Make("StartPadDot", ShapeSprites.Disc, OrderPad);
            _goalRing = Make("GoalRing", ShapeSprites.Ring, OrderGoal);
            _goalFill = Make("GoalFill", ShapeSprites.Disc, OrderGoal);
        }

        public void ShowLevel(LevelDefinition level)
        {
            float pad = GameConfig.MinWellDistanceFromStart;
            Place(_padRing, level.Start, pad * 2f);
            Place(_padDot, level.Start, 0.022f);
            Place(_goalRing, level.Goal, level.GoalRadius * 2f);
            _goalFillScale = level.GoalRadius * 2f * 0.62f;
            _goalPopAge = -1f;
            Place(_goalFill, level.Goal, _goalFillScale);
            SetGoalReached(false);
        }

        // The goal fills when the flight on screen has lit every gate and ends in it.
        public void SetGoalReached(bool reached)
        {
            if (reached && !_goalReached)
            {
                _goalPopAge = 0f; // it fills with a pop
            }
            _goalReached = reached;
            PaintGoal();
        }

        // Breathing goal, popping fill and twinkling stars. Call every frame (a decoration, so any time scale).
        public void Tick(float seconds)
        {
            _clock += seconds;

            if (_goalReached)
            {
                float scale = 1f + 0.05f * Mathf.Sin(_clock * 3.2f);
                if (_goalPopAge >= 0f)
                {
                    _goalPopAge += seconds;
                    scale *= FeelCurves.Settle(_goalPopAge, 0.45f);
                    if (_goalPopAge >= 0.45f)
                    {
                        _goalPopAge = -1f;
                    }
                }
                float diameter = _goalFillScale * scale;
                _goalFill.transform.localScale = new Vector3(diameter, diameter, 1f);
            }

            // The stars twinkle gently, a few times a second: slow enough to be a mood and not a distraction.
            _twinkleTimer += seconds;
            if (_twinkleTimer >= 0.05f)
            {
                _twinkleTimer = 0f;
                for (int i = 0; i < StarCount; i++)
                {
                    float shimmer = 0.78f + 0.22f * Mathf.Sin(_clock * _twinkleSpeed[i] + _twinklePhase[i]);
                    Color32 c = ViewColors.ToVertexColor(ViewColors.WithAlpha(_colors.Neutral, _starBrightness[i] * shimmer));
                    int v = i * 4;
                    _starColors[v] = c;
                    _starColors[v + 1] = c;
                    _starColors[v + 2] = c;
                    _starColors[v + 3] = c;
                }
                _starMesh.SetColors(_starColors);
            }
        }

        public void ApplyTheme(ViewColors colors)
        {
            _colors = colors;
            _panel.color = colors.Field;
            _padRing.color = ViewColors.WithAlpha(colors.Neutral, 0.30f);
            _padDot.color = ViewColors.WithAlpha(colors.Neutral, 0.55f);
            PaintGoal();

            for (int i = 0; i < StarCount; i++)
            {
                Color32 c = ViewColors.ToVertexColor(ViewColors.WithAlpha(colors.Neutral, _starBrightness[i]));
                for (int v = 0; v < 4; v++)
                {
                    _starColors[i * 4 + v] = c;
                }
            }
            _starMesh.SetColors(_starColors);
        }

        private void PaintGoal()
        {
            _goalRing.color = ViewColors.WithAlpha(_goalReached ? _colors.Lit : _colors.Neutral, _goalReached ? 1f : 0.75f);
            _goalFill.color = ViewColors.WithAlpha(_colors.Lit, 1f);
            _goalFill.enabled = _goalReached;
        }

        private SpriteRenderer Make(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        private static void Place(SpriteRenderer renderer, Vector2 position, float diameter)
        {
            renderer.transform.localPosition = new Vector3(position.x, position.y, 0f);
            renderer.transform.localScale = new Vector3(diameter, diameter, 1f);
        }

        // Stars are small squares scattered well beyond the field, so the sky fills a tall phone screen too.
        private Mesh BuildStars(Transform root)
        {
            var rng = new System.Random(StarSeed);
            var vertices = new Vector3[StarCount * 4];
            var uvs = new Vector2[StarCount * 4];
            var triangles = new int[StarCount * 6];

            for (int i = 0; i < StarCount; i++)
            {
                float x = Mathf.Lerp(-1.2f, 2.2f, (float)rng.NextDouble());
                float y = Mathf.Lerp(-1.0f, 2.5f, (float)rng.NextDouble());
                float half = Mathf.Lerp(0.0025f, 0.0075f, (float)rng.NextDouble());
                _starBrightness[i] = Mathf.Lerp(0.06f, 0.26f, (float)rng.NextDouble());
                _twinkleSpeed[i] = Mathf.Lerp(0.4f, 1.6f, (float)rng.NextDouble());
                _twinklePhase[i] = (float)rng.NextDouble() * 6.2832f;

                int v = i * 4;
                vertices[v + 0] = new Vector3(x - half, y - half, 0f);
                vertices[v + 1] = new Vector3(x - half, y + half, 0f);
                vertices[v + 2] = new Vector3(x + half, y + half, 0f);
                vertices[v + 3] = new Vector3(x + half, y - half, 0f);
                uvs[v + 0] = new Vector2(0f, 0f);
                uvs[v + 1] = new Vector2(0f, 1f);
                uvs[v + 2] = new Vector2(1f, 1f);
                uvs[v + 3] = new Vector2(1f, 0f);

                int t = i * 6;
                triangles[t + 0] = v;
                triangles[t + 1] = v + 1;
                triangles[t + 2] = v + 2;
                triangles[t + 3] = v;
                triangles[t + 4] = v + 2;
                triangles[t + 5] = v + 3;
            }

            var mesh = new Mesh { name = "Stars" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            var go = new GameObject("Stars");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = ShapeSprites.Disc.texture };
            meshRenderer.sortingOrder = OrderStars;
            return mesh;
        }
    }
}
