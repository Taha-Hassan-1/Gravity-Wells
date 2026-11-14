using UnityEngine;
using GravityWells.Meta;

namespace GravityWells.Game
{
    // The roles the board draws in, taken from the active palette. Phase 4 gives the palettes roles of their
    // own for wells, gates and the path (and re-checks their contrast); until then the roles borrow the
    // closest existing ones.
    public readonly struct ViewColors
    {
        public readonly Color Background;
        public readonly Color Field;        // the playfield panel, a little apart from the page
        public readonly Color Neutral;      // wells, unlit gates, the path while it is only a path
        public readonly Color Active;       // the well in the finger, the ship
        public readonly Color Lit;          // a lit gate, the filled goal
        public readonly Color Good;         // the path once every gate is lit and it ends in the goal
        public readonly Color Warning;      // where a flight ends in a crash

        public ViewColors(ThemePalette palette)
        {
            Background = palette.Background;
            Field = Color.Lerp(palette.Background, palette.Text, 0.05f);
            Neutral = palette.Text;
            Active = palette.NodeGrabbed;
            Lit = palette.Accent;
            Good = palette.EdgeClear;
            Warning = palette.EdgeCrossing;
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
