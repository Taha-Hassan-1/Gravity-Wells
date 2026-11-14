using UnityEngine;

namespace GravityWells.Meta
{
    public readonly struct ThemePalette
    {
        public readonly string Name;
        public readonly int UnlockLevel;
        public readonly Color Background;
        public readonly Color Node;
        public readonly Color NodeGrabbed;
        public readonly Color EdgeClear;
        public readonly Color EdgeCrossing;
        public readonly Color Accent;
        public readonly Color Text;

        // Semantic roles beyond the core colours: a panel/control surface, a raised one for dialogs, and
        // supporting text. A palette that does not name them gets values blended from its own background and
        // text, so every theme has the full set and the older palettes look as they did.
        public readonly Color Surface;
        public readonly Color SurfaceElevated;
        public readonly Color TextSecondary;

        public ThemePalette(
            string name, int unlockLevel, string background, string node, string nodeGrabbed,
            string edgeClear, string edgeCrossing, string accent, string text,
            string surface = null, string surfaceElevated = null, string textSecondary = null)
        {
            Name = name;
            UnlockLevel = unlockLevel;
            Background = ParseHex(background);
            Node = ParseHex(node);
            NodeGrabbed = ParseHex(nodeGrabbed);
            EdgeClear = ParseHex(edgeClear);
            EdgeCrossing = ParseHex(edgeCrossing);
            Accent = ParseHex(accent);
            Text = ParseHex(text);
            Surface = surface != null ? ParseHex(surface) : Color.Lerp(Background, Text, SurfaceBlend);
            SurfaceElevated = surfaceElevated != null ? ParseHex(surfaceElevated) : Color.Lerp(Background, Text, ElevatedBlend);
            TextSecondary = textSecondary != null ? ParseHex(textSecondary) : Color.Lerp(Background, Text, SecondaryTextBlend);
        }

        private ThemePalette(
            string name, int unlockLevel, Color background, Color node, Color nodeGrabbed,
            Color edgeClear, Color edgeCrossing, Color accent, Color text,
            Color surface, Color surfaceElevated, Color textSecondary)
        {
            Name = name;
            UnlockLevel = unlockLevel;
            Background = background;
            Node = node;
            NodeGrabbed = nodeGrabbed;
            EdgeClear = edgeClear;
            EdgeCrossing = edgeCrossing;
            Accent = accent;
            Text = text;
            Surface = surface;
            SurfaceElevated = surfaceElevated;
            TextSecondary = textSecondary;
        }

        private const float SurfaceBlend = 0.12f;
        private const float ElevatedBlend = 0.18f;
        private const float SecondaryTextBlend = 0.66f;

        // The same palette with its two line colors replaced (the player's picks).
        public ThemePalette WithEdgeColors(Color edgeClear, Color edgeCrossing)
        {
            return new ThemePalette(
                Name, UnlockLevel, Background, Node, NodeGrabbed, edgeClear, edgeCrossing, Accent, Text,
                Surface, SurfaceElevated, TextSecondary);
        }

        // Blends every color; identity (name/unlock level) comes from the target.
        public static ThemePalette Lerp(ThemePalette from, ThemePalette to, float t)
        {
            return new ThemePalette(
                to.Name, to.UnlockLevel,
                Color.Lerp(from.Background, to.Background, t),
                Color.Lerp(from.Node, to.Node, t),
                Color.Lerp(from.NodeGrabbed, to.NodeGrabbed, t),
                Color.Lerp(from.EdgeClear, to.EdgeClear, t),
                Color.Lerp(from.EdgeCrossing, to.EdgeCrossing, t),
                Color.Lerp(from.Accent, to.Accent, t),
                Color.Lerp(from.Text, to.Text, t),
                Color.Lerp(from.Surface, to.Surface, t),
                Color.Lerp(from.SurfaceElevated, to.SurfaceElevated, t),
                Color.Lerp(from.TextSecondary, to.TextSecondary, t));
        }

        private static Color ParseHex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out Color color) ? color : Color.magenta;
        }
    }

    // Nine palettes, zero art cost. The first eight are the progression meta; the last is an
    // accessibility palette that is available from level 1 (it sits at the end so saved theme
    // indices never shift).
    public static class ThemeService
    {
        public static readonly ThemePalette[] Palettes =
        {
            // Warm Paper and Deep Navy follow the UI design brief's semantic tokens (light and dark versions of
            // one system): ivory/stone with a terracotta action (the brief's blue read as generic on paper, so the
            // original accent was kept), navy/blue-grey with a cool blue one, and a
            // coral/green edge pair tuned separately for each so both stay legible. Three light-theme values are
            // a shade deeper than the brief's (selected node, clear edge, supporting text) because the brief's
            // measured under 3:1 / 4.5:1 against the ivory background; UIDesignSystemTests pins the ratios.
            new ThemePalette("Warm Paper", 1,
                background: "#F3E9D5", node: "#382F25", nodeGrabbed: "#BF6F0F",
                edgeClear: "#4A9167", edgeCrossing: "#C95545", accent: "#B4552F", text: "#30291F",
                surface: "#E8DDC8", surfaceElevated: "#FFF8EB", textSecondary: "#655B4E"),
            new ThemePalette("Deep Navy", 10,
                background: "#0D1730", node: "#E9EDF5", nodeGrabbed: "#FFD166",
                edgeClear: "#69B88A", edgeCrossing: "#E46A62", accent: "#63A9E8", text: "#F4F6FA",
                surface: "#1B2944", surfaceElevated: "#263752", textSecondary: "#AAB6C8"),
            new ThemePalette("Forest", 25,
                background: "#1B2A1E", node: "#E8F0E0", nodeGrabbed: "#7BAE6F",
                edgeClear: "#6FAE8F", edgeCrossing: "#C97A4A", accent: "#8FC97A", text: "#E8F0E0"),
            new ThemePalette("Monochrome", 50,
                background: "#1A1A1A", node: "#F0F0F0", nodeGrabbed: "#B0B0B0",
                edgeClear: "#808080", edgeCrossing: "#FFFFFF", accent: "#CCCCCC", text: "#F0F0F0"),
            new ThemePalette("Sunset", 80,
                background: "#2E1A3A", node: "#FFE8D6", nodeGrabbed: "#FFB088",
                edgeClear: "#FF9D6C", edgeCrossing: "#FF5C5C", accent: "#FF8C42", text: "#FFE8D6"),
            new ThemePalette("Neon", 120,
                background: "#000000", node: "#00FFD0", nodeGrabbed: "#FF00E6",
                edgeClear: "#00FFD0", edgeCrossing: "#FF3366", accent: "#FF00E6", text: "#00FFD0"),
            new ThemePalette("Pastel", 175,
                background: "#FDF4F9", node: "#5A4A5C", nodeGrabbed: "#F6C6D9",
                edgeClear: "#5FA896", edgeCrossing: "#E0587F", accent: "#C9A0DC", text: "#5A4A5C"),
            new ThemePalette("High Contrast", 250,
                background: "#000000", node: "#FFFFFF", nodeGrabbed: "#FFFF00",
                edgeClear: "#00FF00", edgeCrossing: "#FF0000", accent: "#FFFF00", text: "#FFFFFF"),
            // Color-vision friendly: "ok" vs "crossing" differ in hue (blue vs amber, which
            // red-green deficiencies preserve) AND in brightness, so they stay distinct even
            // in grayscale.
            new ThemePalette("Color Safe", 1,
                background: "#12161F", node: "#F4F4F4", nodeGrabbed: "#56B4E9",
                edgeClear: "#2E7FD0", edgeCrossing: "#FFB000", accent: "#FFB000", text: "#F4F4F4"),
        };

        // Index of the accessibility palette; always unlocked.
        public const int ColorSafeIndex = 8;

        public static bool IsUnlocked(int themeIndex, int highestLevelUnlocked)
        {
            return highestLevelUnlocked >= Palettes[themeIndex].UnlockLevel;
        }
    }
}
