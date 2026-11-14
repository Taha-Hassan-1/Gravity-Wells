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

        public ThemePalette(
            string name, int unlockLevel, string background, string node, string nodeGrabbed,
            string edgeClear, string edgeCrossing, string accent, string text)
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
        }

        private ThemePalette(
            string name, int unlockLevel, Color background, Color node, Color nodeGrabbed,
            Color edgeClear, Color edgeCrossing, Color accent, Color text)
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
        }

        // The same palette with its two line colors replaced (the player's picks).
        public ThemePalette WithEdgeColors(Color edgeClear, Color edgeCrossing)
        {
            return new ThemePalette(
                Name, UnlockLevel, Background, Node, NodeGrabbed, edgeClear, edgeCrossing, Accent, Text);
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
                Color.Lerp(from.Text, to.Text, t));
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
            new ThemePalette("Warm Paper", 1,
                background: "#F2E8D5", node: "#3A2E22", nodeGrabbed: "#C9A876",
                edgeClear: "#6E9A6A", edgeCrossing: "#C14E3A", accent: "#B4552F", text: "#2B1F15"),
            new ThemePalette("Deep Navy", 10,
                background: "#0B1930", node: "#E8EEF7", nodeGrabbed: "#5C9DD6",
                edgeClear: "#4FA6A0", edgeCrossing: "#D65A5A", accent: "#5C9DD6", text: "#E8EEF7"),
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
