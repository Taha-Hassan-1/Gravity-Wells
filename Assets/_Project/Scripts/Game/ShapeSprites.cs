using UnityEngine;

namespace GravityWells.Game
{
    // Every shape the game draws, made once in code and tinted by the theme: there are no art assets
    // (CLAUDE.md). Each sprite is exactly one world unit across at scale 1, so a view sets its size by scale.
    // Trigonometry is fine here: this is the view layer, not the simulation.
    public static class ShapeSprites
    {
        private const int Samples = 3;   // 3x3 supersampling per pixel keeps the curved edges smooth

        private static Sprite _disc;
        private static Sprite _ring;
        private static Sprite _brokenRing;
        private static Sprite _triangle;
        private static Sprite _square;
        private static Sprite _rock;

        public static Sprite Disc => _disc != null ? _disc : _disc = CircleSprite.Create(128);

        // A hollow circle: full outline, used for attractors and for unlit gate posts.
        public static Sprite Ring => _ring != null ? _ring : _ring = Draw(128, (x, y) => RingCoverage(x, y, 0.80f));

        // The repulsor's outline: three arcs with gaps, so it differs from an attractor by shape and not only colour.
        public static Sprite BrokenRing => _brokenRing != null ? _brokenRing : _brokenRing = Draw(128, BrokenRingCoverage);

        // Points up (+y); rotate it to point along a heading.
        public static Sprite Triangle => _triangle != null ? _triangle : _triangle = Draw(96, TriangleCoverage);

        public static Sprite Square => _square != null ? _square : _square = MakeSquare();

        // An irregular rock, the same one every time. Filled, with a lumpy edge, so a hazard is not mistaken
        // for a well (a ring with a core) or a gate post.
        public static Sprite Rock => _rock != null ? _rock : _rock = Draw(128, RockCoverage);

        private static readonly float[] RockRadii = { 1.00f, 0.84f, 0.96f, 0.80f, 1.00f, 0.86f, 0.94f, 0.82f, 0.98f };

        private static float RockCoverage(float x, float y)
        {
            // Even-odd test against a polygon with one vertex per 40 degrees at the radii above.
            int n = RockRadii.Length;
            bool inside = false;
            float step = 2f * Mathf.PI / n;
            float pj = RockRadii[n - 1] * Mathf.Cos((n - 1) * step), qj = RockRadii[n - 1] * Mathf.Sin((n - 1) * step);
            for (int i = 0; i < n; i++)
            {
                float pi = RockRadii[i] * Mathf.Cos(i * step), qi = RockRadii[i] * Mathf.Sin(i * step);
                if ((qi > y) != (qj > y) && x < (pj - pi) * (y - qi) / (qj - qi) + pi)
                {
                    inside = !inside;
                }
                pj = pi;
                qj = qi;
            }
            return inside ? 1f : 0f;
        }

        private static float RingCoverage(float x, float y, float innerFraction)
        {
            // x, y in -1..1 from the centre
            float d = Mathf.Sqrt(x * x + y * y);
            return d <= 1f && d >= innerFraction ? 1f : 0f;
        }

        private static float BrokenRingCoverage(float x, float y)
        {
            if (RingCoverage(x, y, 0.80f) <= 0f)
            {
                return 0f;
            }
            const float gapDegrees = 26f;
            float angle = Mathf.Atan2(y, x) * Mathf.Rad2Deg + 90f;           // gaps centred at the top, and 120 and 240 round
            float within = Mathf.Repeat(angle + 360f, 120f);
            bool inGap = within < gapDegrees * 0.5f || within > 120f - gapDegrees * 0.5f;
            return inGap ? 0f : 1f;
        }

        private static float TriangleCoverage(float x, float y)
        {
            // Tip at the top, a flat base below the middle: x, y in -1..1.
            const float tipY = 0.95f, baseY = -0.8f, halfBase = 0.72f;
            if (y > tipY || y < baseY)
            {
                return 0f;
            }
            float t = (tipY - y) / (tipY - baseY);   // 0 at the tip, 1 at the base
            return Mathf.Abs(x) <= halfBase * t ? 1f : 0f;
        }

        private static Sprite Draw(int size, System.Func<float, float, float> coverage)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[size * size];
            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    float sum = 0f;
                    for (int sy = 0; sy < Samples; sy++)
                    {
                        for (int sx = 0; sx < Samples; sx++)
                        {
                            float x = ((px + (sx + 0.5f) / Samples) / size) * 2f - 1f;
                            float y = ((py + (sy + 0.5f) / Samples) / size) * 2f - 1f;
                            sum += coverage(x, y);
                        }
                    }
                    byte alpha = (byte)Mathf.RoundToInt(255f * sum / (Samples * Samples));
                    pixels[py * size + px] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite MakeSquare()
        {
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 255);
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        }
    }
}
