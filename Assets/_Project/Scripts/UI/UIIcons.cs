using System.Collections.Generic;
using UnityEngine;
using GravityWells.Core;

namespace GravityWells.UI
{
    // Looks icons up by short name. Most are white glyphs in Resources/UI/Icons (tinted per
    // theme by UIStyle); the few the pack doesn't cover are drawn into a texture at first use.
    // A missing icon returns null and the caller simply skips it, so a bad name never breaks a screen.
    public static class UIIcons
    {
        private const int DrawSize = 128;

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Get(string name)
        {
            if (Cache.TryGetValue(name, out Sprite cached))
            {
                return cached;
            }

            Sprite sprite = Draw(name) ?? Resources.Load<Sprite>("UI/Icons/" + name);
            Cache[name] = sprite;
            return sprite;
        }

        private static Sprite Draw(string name)
        {
            switch (name)
            {
                case "coin": return Paint(CoinPixel);
                case "pin": return Paint(PinPixel);
                case "bridge": return Paint(BridgePixel);
                case "themes": return Paint(ThemesPixel);
                case "haptics": return Paint(HapticsPixel);
                case "timer": return Paint(TimerPixel);
                case "modes": return Paint(ModesPixel);
                case "zen": return Paint(ZenPixel);
                case "perfect": return Paint(PerfectPixel);
                case "share": return Paint(SharePixel);
                case "weekly": return Paint(WeeklyPixel);
                case "endless": return Paint(EndlessPixel);
                case "headstart": return Paint(HeadStartPixel);
                case "restart": return Paint(RestartPixel);
                case "music": return Paint(MusicPixel);
                case "chevron": return Paint(ChevronPixel);
                default: return null;
            }
        }

        // Supersampled so edges stay smooth at any size the UI scales the sprite to.
        private static Sprite Paint(System.Func<float, float, Color> pixel)
        {
            var texture = new Texture2D(DrawSize, DrawSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[DrawSize * DrawSize];
            const int samples = 3;
            for (int y = 0; y < DrawSize; y++)
            {
                for (int x = 0; x < DrawSize; x++)
                {
                    float r = 0f, g = 0f, b = 0f, a = 0f;
                    for (int sy = 0; sy < samples; sy++)
                    {
                        for (int sx = 0; sx < samples; sx++)
                        {
                            Color c = pixel((x + (sx + 0.5f) / samples) / DrawSize, (y + (sy + 0.5f) / samples) / DrawSize);
                            r += c.r * c.a;
                            g += c.g * c.a;
                            b += c.b * c.a;
                            a += c.a;
                        }
                    }
                    float n = samples * samples;
                    pixels[y * DrawSize + x] = a <= 0f
                        ? new Color32(255, 255, 255, 0)
                        : new Color(r / a, g / a, b / a, a / n);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, DrawSize, DrawSize), new Vector2(0.5f, 0.5f), DrawSize);
        }

        // A gold coin: darker rim, lighter face, and a pale inner ring. Fixed colors, not tinted.
        private static Color CoinPixel(float x, float y)
        {
            float d = Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.5f) * (y - 0.5f));
            if (d > 0.47f) return Color.clear;
            if (d > 0.37f) return new Color(0.86f, 0.55f, 0.12f, 1f);
            if (d > 0.31f) return new Color(1f, 0.86f, 0.42f, 1f);
            return new Color(0.98f, 0.74f, 0.22f, 1f);
        }

        // A target ring with a dot: the same ring the board draws around a pinned node.
        private static Color PinPixel(float x, float y)
        {
            float d = Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.5f) * (y - 0.5f));
            bool ring = d <= 0.44f && d >= 0.30f;
            bool dot = d <= 0.14f;
            return ring || dot ? Color.white : Color.clear;
        }

        // One line passing over another by hopping across it.
        private static Color BridgePixel(float x, float y)
        {
            const float half = 0.07f;
            // The lower (vertical) line is cut either side of the hop so the crossing reads as over/under.
            bool vertical = Mathf.Abs(x - 0.5f) <= half && y >= 0.06f && y <= 0.94f && (y < 0.47f || y > 0.77f);
            bool leftArm = y >= 0.42f - half && y <= 0.42f + half && x >= 0.04f && x <= 0.30f;
            bool rightArm = y >= 0.42f - half && y <= 0.42f + half && x >= 0.70f && x <= 0.96f;
            float arch = Mathf.Abs(Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.42f) * (y - 0.42f)) - 0.2f);
            bool hop = y >= 0.42f && arch <= half;
            return vertical || leftArm || rightArm || hop ? Color.white : Color.clear;
        }

        // Distance from (x,y) to the segment a-b.
        private static float SegmentDistance(float x, float y, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay;
            float t = Mathf.Clamp01(((x - ax) * dx + (y - ay) * dy) / (dx * dx + dy * dy));
            float px = ax + t * dx - x, py = ay + t * dy - y;
            return Mathf.Sqrt(px * px + py * py);
        }

        // A ring with a gap and an arrowhead: start again. (The pack's glyph was a "redo" arrow, which
        // promised the opposite of Undo rather than a fresh start.)
        private static Color RestartPixel(float x, float y)
        {
            float dx = x - 0.5f, dy = y - 0.5f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            bool gap = angle > 14f && angle < 74f;
            bool ring = d <= 0.40f && d >= 0.29f && !gap;

            const float baseAngle = 14f * Mathf.Deg2Rad;
            const float tipAngle = 56f * Mathf.Deg2Rad;
            bool head = InTriangle(
                x, y,
                0.5f + 0.17f * Mathf.Cos(baseAngle), 0.5f + 0.17f * Mathf.Sin(baseAngle),
                0.5f + 0.52f * Mathf.Cos(baseAngle), 0.5f + 0.52f * Mathf.Sin(baseAngle),
                0.5f + 0.345f * Mathf.Cos(tipAngle), 0.5f + 0.345f * Mathf.Sin(tipAngle));
            return ring || head ? Color.white : Color.clear;
        }

        private static bool InTriangle(float x, float y, float ax, float ay, float bx, float by, float cx, float cy)
        {
            float d1 = (x - bx) * (ay - by) - (ax - bx) * (y - by);
            float d2 = (x - cx) * (by - cy) - (bx - cx) * (y - cy);
            float d3 = (x - ax) * (cy - ay) - (cx - ax) * (y - ay);
            bool negative = d1 < 0f || d2 < 0f || d3 < 0f;
            bool positive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(negative && positive);
        }

        // A single note, so Music no longer shares the speaker with Sound.
        private static Color MusicPixel(float x, float y)
        {
            float hx = (x - 0.40f) / 0.19f, hy = (y - 0.24f) / 0.14f;
            bool head = hx * hx + hy * hy <= 1f;
            bool stem = x >= 0.52f && x <= 0.60f && y >= 0.24f && y <= 0.86f;
            bool flag = SegmentDistance(x, y, 0.58f, 0.84f, 0.82f, 0.70f) <= 0.055f;
            return head || stem || flag ? Color.white : Color.clear;
        }

        // A ">" for rows that open another page.
        private static Color ChevronPixel(float x, float y)
        {
            bool stroke = SegmentDistance(x, y, 0.36f, 0.20f, 0.66f, 0.50f) <= 0.065f
                || SegmentDistance(x, y, 0.66f, 0.50f, 0.36f, 0.80f) <= 0.065f;
            return stroke ? Color.white : Color.clear;
        }

        // A flag on a pole: a head start.
        private static Color HeadStartPixel(float x, float y)
        {
            bool pole = x >= 0.22f && x <= 0.30f && y >= 0.08f && y <= 0.92f;
            bool flag = x >= 0.30f && x <= 0.86f && y >= 0.52f && y <= 0.92f
                && (y - 0.52f) / 0.40f <= 1.0f - Mathf.Abs((x - 0.30f) / 0.56f) * 0.55f;
            return pole || flag ? Color.white : Color.clear;
        }

        // An infinity sign: two rings side by side.
        private static Color EndlessPixel(float x, float y)
        {
            float left = Mathf.Sqrt((x - 0.30f) * (x - 0.30f) + (y - 0.5f) * (y - 0.5f));
            float right = Mathf.Sqrt((x - 0.70f) * (x - 0.70f) + (y - 0.5f) * (y - 0.5f));
            bool ring = (left <= 0.26f && left >= 0.16f) || (right <= 0.26f && right >= 0.16f);
            return ring ? Color.white : Color.clear;
        }

        // A calendar page: a frame, a header bar, two binder rings and a grid of days.
        private static Color WeeklyPixel(float x, float y)
        {
            bool frame = x >= 0.12f && x <= 0.88f && y >= 0.10f && y <= 0.82f
                && !(x > 0.19f && x < 0.81f && y > 0.17f && y < 0.60f);
            bool header = x >= 0.12f && x <= 0.88f && y >= 0.60f && y <= 0.82f;
            bool rings = (Mathf.Abs(x - 0.32f) <= 0.04f || Mathf.Abs(x - 0.68f) <= 0.04f) && y >= 0.74f && y <= 0.94f;
            bool days = y >= 0.24f && y <= 0.52f && x >= 0.26f && x <= 0.74f
                && Mathf.Repeat((x - 0.26f) / 0.16f, 1f) < 0.62f && Mathf.Repeat((y - 0.24f) / 0.15f, 1f) < 0.62f;
            return frame || header || rings || days ? Color.white : Color.clear;
        }

        // Three dots joined by two lines: the usual "share" glyph.
        private static Color SharePixel(float x, float y)
        {
            bool dots = Mathf.Sqrt((x - 0.26f) * (x - 0.26f) + (y - 0.5f) * (y - 0.5f)) <= 0.15f
                || Mathf.Sqrt((x - 0.74f) * (x - 0.74f) + (y - 0.22f) * (y - 0.22f)) <= 0.15f
                || Mathf.Sqrt((x - 0.74f) * (x - 0.74f) + (y - 0.78f) * (y - 0.78f)) <= 0.15f;
            bool lines = SegmentDistance(x, y, 0.26f, 0.5f, 0.74f, 0.22f) <= 0.045f
                || SegmentDistance(x, y, 0.26f, 0.5f, 0.74f, 0.78f) <= 0.045f;
            return dots || lines ? Color.white : Color.clear;
        }

        // A cut gem: a diamond outline with a smaller diamond inside.
        private static Color PerfectPixel(float x, float y)
        {
            float d = Mathf.Abs(x - 0.5f) + Mathf.Abs(y - 0.5f);
            bool outline = d <= 0.46f && d >= 0.34f;
            bool core = d <= 0.14f;
            return outline || core ? Color.white : Color.clear;
        }

        // An enso: a brushed ring left open at the top right, with a dot of stillness in the middle.
        private static Color ZenPixel(float x, float y)
        {
            float dx = x - 0.5f, dy = y - 0.5f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            bool gap = angle > 5f && angle < 60f;
            bool ring = d <= 0.42f && d >= 0.31f && !gap;
            bool dot = d <= 0.07f;
            return ring || dot ? Color.white : Color.clear;
        }

        // Four tiles: a launcher for several ways to play.
        private static Color ModesPixel(float x, float y)
        {
            bool left = x >= 0.10f && x <= 0.46f;
            bool right = x >= 0.54f && x <= 0.90f;
            bool low = y >= 0.10f && y <= 0.46f;
            bool high = y >= 0.54f && y <= 0.90f;
            return (left || right) && (low || high) ? Color.white : Color.clear;
        }

        // A stopwatch: ring, two hands, and the crown on top.
        private static Color TimerPixel(float x, float y)
        {
            float d = Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.44f) * (y - 0.44f));
            bool ring = d <= 0.42f && d >= 0.33f;
            bool hands = SegmentDistance(x, y, 0.5f, 0.44f, 0.5f, 0.67f) <= 0.04f
                || SegmentDistance(x, y, 0.5f, 0.44f, 0.64f, 0.36f) <= 0.04f;
            bool crown = x >= 0.42f && x <= 0.58f && y >= 0.90f && y <= 0.97f;
            bool stem = x >= 0.46f && x <= 0.54f && y >= 0.85f && y <= 0.91f;
            return ring || hands || crown || stem ? Color.white : Color.clear;
        }

        // A phone outline with a pulse either side: the usual "vibration" glyph.
        private static Color HapticsPixel(float x, float y)
        {
            bool outer = x >= 0.33f && x <= 0.67f && y >= 0.10f && y <= 0.90f;
            bool inner = x >= 0.40f && x <= 0.60f && y >= 0.18f && y <= 0.82f;
            bool pulses = SegmentDistance(x, y, 0.20f, 0.38f, 0.20f, 0.62f) <= 0.035f
                || SegmentDistance(x, y, 0.07f, 0.30f, 0.07f, 0.70f) <= 0.035f
                || SegmentDistance(x, y, 0.80f, 0.38f, 0.80f, 0.62f) <= 0.035f
                || SegmentDistance(x, y, 0.93f, 0.30f, 0.93f, 0.70f) <= 0.035f;
            return (outer && !inner) || pulses ? Color.white : Color.clear;
        }

        // Half-filled disc inside a ring: the usual "appearance" glyph.
        private static Color ThemesPixel(float x, float y)
        {
            float d = Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.5f) * (y - 0.5f));
            bool ring = d <= 0.46f && d >= 0.38f;
            bool half = d <= 0.30f && x <= 0.5f;
            return ring || half ? Color.white : Color.clear;
        }
    }
}
