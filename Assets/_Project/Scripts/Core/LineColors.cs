using System;

namespace GravityWells.Core
{
    // Colour maths for the line-colour picker, on packed 0xAARRGGBB ints so Core needs no engine
    // colour type and the save can store them as plain numbers. 0 means "use the theme's colour",
    // which is why a real colour always carries a full alpha byte and is never 0.
    public static class LineColors
    {
        public const int Unset = 0;

        // WCAG's guidance for graphical objects is 3:1 against the background.
        public const double MinLineContrast = 3.0;

        // Clear and crossing lines should differ in brightness, not only in hue: a pair of equally
        // bright colours collapses to one shade for many colour-vision types and in grayscale.
        public const double MinSeparation = 1.5;

        [Flags]
        public enum Issue
        {
            None = 0,
            ClearLowContrast = 1,
            CrossingLowContrast = 2,
            SimilarBrightness = 4,
        }

        // A spread that stays distinct for common colour-vision types (the first seven follow the
        // Okabe-Ito set), plus neutrals for a calm look.
        public static readonly int[] Presets =
        {
            Pack(0xE6, 0x9F, 0x00), // orange
            Pack(0x56, 0xB4, 0xE9), // sky blue
            Pack(0x00, 0x9E, 0x73), // green
            Pack(0xF0, 0xE4, 0x42), // yellow
            Pack(0x00, 0x72, 0xB2), // blue
            Pack(0xD5, 0x5E, 0x00), // vermilion
            Pack(0xCC, 0x79, 0xA7), // pink
            Pack(0xF5, 0xF5, 0xF5), // white
            Pack(0xA0, 0xA0, 0xA0), // grey
            Pack(0x15, 0x15, 0x15), // near black
            Pack(0x8E, 0x6B, 0xBF), // purple
            Pack(0xE6, 0x39, 0x46), // red
        };

        public static int Pack(int r, int g, int b) =>
            unchecked((int)(0xFF000000u | ((uint)Clamp(r) << 16) | ((uint)Clamp(g) << 8) | (uint)Clamp(b)));

        public static bool IsSet(int packed) => packed != Unset;

        public static void Unpack(int packed, out int r, out int g, out int b)
        {
            uint v = unchecked((uint)packed);
            r = (int)((v >> 16) & 0xFF);
            g = (int)((v >> 8) & 0xFF);
            b = (int)(v & 0xFF);
        }

        // WCAG relative luminance (0 = black, 1 = white).
        public static double Luminance(int packed)
        {
            Unpack(packed, out int r, out int g, out int b);
            return 0.2126 * Linear(r) + 0.7152 * Linear(g) + 0.0722 * Linear(b);
        }

        // 1 (identical) to 21 (black on white).
        public static double ContrastRatio(int a, int b)
        {
            double la = Luminance(a);
            double lb = Luminance(b);
            double hi = Math.Max(la, lb);
            double lo = Math.Min(la, lb);
            return (hi + 0.05) / (lo + 0.05);
        }

        public static Issue Check(int background, int clear, int crossing)
        {
            Issue issues = Issue.None;
            if (ContrastRatio(background, clear) < MinLineContrast)
            {
                issues |= Issue.ClearLowContrast;
            }
            if (ContrastRatio(background, crossing) < MinLineContrast)
            {
                issues |= Issue.CrossingLowContrast;
            }
            if (ContrastRatio(clear, crossing) < MinSeparation)
            {
                issues |= Issue.SimilarBrightness;
            }
            return issues;
        }

        // One short sentence for the picker; empty when the choice is fine.
        public static string Describe(Issue issues)
        {
            if ((issues & Issue.ClearLowContrast) != 0 && (issues & Issue.CrossingLowContrast) != 0)
            {
                return "Both line colours are hard to see on this background";
            }
            if ((issues & Issue.ClearLowContrast) != 0)
            {
                return "Clear lines are hard to see on this background";
            }
            if ((issues & Issue.CrossingLowContrast) != 0)
            {
                return "Crossing lines are hard to see on this background";
            }
            if ((issues & Issue.SimilarBrightness) != 0)
            {
                return "The two colours are about equally bright - they may look alike";
            }
            return string.Empty;
        }

        // Hue, saturation and value are all 0..1.
        public static int FromHsv(float h, float s, float v)
        {
            h = h - (float)Math.Floor(h);
            s = Math.Max(0f, Math.Min(1f, s));
            v = Math.Max(0f, Math.Min(1f, v));

            float c = v * s;
            float x = c * (1f - Math.Abs(h * 6f % 2f - 1f));
            float m = v - c;
            float r, g, b;
            switch ((int)(h * 6f))
            {
                case 0: r = c; g = x; b = 0f; break;
                case 1: r = x; g = c; b = 0f; break;
                case 2: r = 0f; g = c; b = x; break;
                case 3: r = 0f; g = x; b = c; break;
                case 4: r = x; g = 0f; b = c; break;
                default: r = c; g = 0f; b = x; break;
            }
            return Pack((int)Math.Round((r + m) * 255f), (int)Math.Round((g + m) * 255f), (int)Math.Round((b + m) * 255f));
        }

        public static void ToHsv(int packed, out float h, out float s, out float v)
        {
            Unpack(packed, out int ri, out int gi, out int bi);
            float r = ri / 255f, g = gi / 255f, b = bi / 255f;
            float max = Math.Max(r, Math.Max(g, b));
            float min = Math.Min(r, Math.Min(g, b));
            float d = max - min;

            v = max;
            s = max <= 0f ? 0f : d / max;
            if (d <= 0f)
            {
                h = 0f;
                return;
            }
            if (max == r)
            {
                h = (g - b) / d % 6f;
            }
            else if (max == g)
            {
                h = (b - r) / d + 2f;
            }
            else
            {
                h = (r - g) / d + 4f;
            }
            h /= 6f;
            if (h < 0f)
            {
                h += 1f;
            }
        }

        private static double Linear(int channel)
        {
            double c = channel / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        private static int Clamp(int channel) => Math.Max(0, Math.Min(255, channel));
    }
}
