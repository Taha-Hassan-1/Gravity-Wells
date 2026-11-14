using UnityEngine;
using GravityWells.Meta;
using GravityWells.UI;

namespace GravityWells.Game
{
    // The roles the board draws in, taken from the active palette. Every palette is written for the menus
    // first, so a role colour that happens to sit too close to the playfield (a soft accent on a pale panel)
    // is pushed toward the palette's text colour until it can be seen, rather than trusting each of the nine
    // palettes to have been tuned for a dotted line over a field. The result is tested for every palette.
    // Phase 4 may give the palettes roles of their own for wells, gates and the path.
    public readonly struct ViewColors
    {
        // The least contrast (WCAG ratio) any role keeps against the playfield: the bar for graphics that must be seen.
        public const float MinContrast = 3f;

        public readonly Color Background;
        public readonly Color Field;        // the playfield panel, a little apart from the page
        public readonly Color Neutral;      // wells, unlit gates, the path while it is only a path
        public readonly Color Active;       // the well in the finger, the ship
        public readonly Color Lit;          // a lit gate, the filled goal
        public readonly Color Good;         // the path once every gate is lit and it ends in the goal
        public readonly Color Warning;      // where a flight ends in a crash, an asteroid, a missed gate

        public ViewColors(ThemePalette palette)
        {
            Background = palette.Background;
            Field = Color.Lerp(palette.Background, palette.Text, 0.05f);
            Neutral = palette.Text;
            Active = Readable(palette.NodeGrabbed, Field, palette.Text);
            Lit = Readable(palette.Accent, Field, palette.Text);
            Warning = Readable(palette.EdgeCrossing, Field, palette.Text);

            // A palette may give its "clear" colour the same value as its text (Neon does), which would make the
            // winning line look like every other line. Then it leans toward the lit colour until it can be told apart.
            Color good = Readable(palette.EdgeClear, Field, palette.Text);
            if (Distance(good, Neutral) < MinRoleDistance)
            {
                good = Readable(Color.Lerp(good, Lit, 0.6f), Field, palette.Text);
            }
            Good = good;
        }

        // The least difference (sum of the channel differences) between roles that must not be mistaken for each other.
        public const float MinRoleDistance = 0.25f;

        public static float Distance(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);

        // The colour itself if it already contrasts enough with what it sits on; otherwise the nearest step toward
        // the fallback (the palette's text colour) that does.
        public static Color Readable(Color color, Color against, Color fallback)
        {
            if (UIStyle.ContrastRatio(color, against) >= MinContrast)
            {
                return color;
            }
            for (int step = 1; step <= 20; step++)
            {
                Color blended = Color.Lerp(color, fallback, step / 20f);
                if (UIStyle.ContrastRatio(blended, against) >= MinContrast)
                {
                    return blended;
                }
            }
            return fallback;
        }

        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        // Unity converts SpriteRenderer colours from sRGB to linear in a Linear colour space but does not
        // convert mesh vertex colours, so a mesh does it itself (CLAUDE.md).
        public static Color32 ToVertexColor(Color srgbColor)
        {
            return QualitySettings.activeColorSpace == ColorSpace.Linear ? srgbColor.linear : srgbColor;
        }
    }
}
