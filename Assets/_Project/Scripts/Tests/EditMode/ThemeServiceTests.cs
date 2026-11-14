using NUnit.Framework;
using UnityEngine;
using GravityWells.Meta;

namespace GravityWells.Tests
{
    public class ThemeServiceTests
    {
        [Test]
        public void Palettes_HasEightProgressionThemesPlusTheColorSafeOne()
        {
            Assert.AreEqual(9, ThemeService.Palettes.Length);
            Assert.AreEqual("Color Safe", ThemeService.Palettes[ThemeService.ColorSafeIndex].Name);
        }

        [Test]
        public void Palettes_UnlockLevelsMatchSpec()
        {
            // Index 8 is the accessibility palette: available immediately, and last so saved
            // theme indices from earlier builds keep pointing at the same palettes.
            int[] expected = { 1, 10, 25, 50, 80, 120, 175, 250, 1 };
            Assert.AreEqual(expected.Length, ThemeService.Palettes.Length);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], ThemeService.Palettes[i].UnlockLevel, $"theme index {i}");
            }
        }

        [Test]
        public void ColorSafe_IsUnlockedForANewPlayer()
        {
            Assert.IsTrue(ThemeService.IsUnlocked(ThemeService.ColorSafeIndex, highestLevelUnlocked: 1));
        }

        [Test]
        public void ColorSafe_ClearAndCrossingEdgesDifferInBrightnessNotJustHue()
        {
            // Red/green deficiencies keep blue vs amber apart, and brightness separates them
            // for every other kind of color vision (and in grayscale).
            ThemePalette p = ThemeService.Palettes[ThemeService.ColorSafeIndex];
            Assert.GreaterOrEqual(ContrastRatio(p.EdgeClear, p.EdgeCrossing), 1.8f);
        }

        [Test]
        public void ColorSafe_AvoidsRedAndGreenForTheEdgeStates()
        {
            ThemePalette p = ThemeService.Palettes[ThemeService.ColorSafeIndex];
            Assert.Less(p.EdgeClear.g, p.EdgeClear.b, "clear edge should lean blue, not green");
            Assert.Greater(p.EdgeCrossing.r, p.EdgeCrossing.b, "crossing edge should lean amber, not blue");
            Assert.Greater(p.EdgeCrossing.r, p.EdgeCrossing.g, "crossing edge is orange-amber, which keeps r > g");
        }

        [Test]
        public void Palettes_AllHexColorsParseSuccessfully()
        {
            foreach (ThemePalette palette in ThemeService.Palettes)
            {
                Assert.AreNotEqual(Color.magenta, palette.Background, $"{palette.Name}: Background failed to parse");
                Assert.AreNotEqual(Color.magenta, palette.Node, $"{palette.Name}: Node failed to parse");
                Assert.AreNotEqual(Color.magenta, palette.NodeGrabbed, $"{palette.Name}: NodeGrabbed failed to parse");
                Assert.AreNotEqual(Color.magenta, palette.EdgeClear, $"{palette.Name}: EdgeClear failed to parse");
                Assert.AreNotEqual(Color.magenta, palette.EdgeCrossing, $"{palette.Name}: EdgeCrossing failed to parse");
                Assert.AreNotEqual(Color.magenta, palette.Accent, $"{palette.Name}: Accent failed to parse");
                Assert.AreNotEqual(Color.magenta, palette.Text, $"{palette.Name}: Text failed to parse");
            }
        }

        // WCAG relative-luminance contrast ratio, computed in linear space.
        private static float ContrastRatio(Color a, Color b)
        {
            float la = Luminance(a);
            float lb = Luminance(b);
            return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
        }

        private static float Luminance(Color color)
        {
            Color l = color.linear;
            return 0.2126f * l.r + 0.7152f * l.g + 0.0722f * l.b;
        }

        [Test]
        public void Palettes_EveryGameplayColorIsLegibleAgainstItsBackground()
        {
            // Nodes and text are the things you must see; edges are thinner lines, so they get
            // a lower (but still real) bar. Pastel originally shipped at 1.2:1 and was invisible.
            foreach (ThemePalette palette in ThemeService.Palettes)
            {
                Assert.GreaterOrEqual(ContrastRatio(palette.Node, palette.Background), 4.5f, palette.Name + " node");
                Assert.GreaterOrEqual(ContrastRatio(palette.Text, palette.Background), 4.5f, palette.Name + " text");
                Assert.GreaterOrEqual(ContrastRatio(palette.EdgeClear, palette.Background), 2.2f, palette.Name + " clear edge");
                Assert.GreaterOrEqual(ContrastRatio(palette.EdgeCrossing, palette.Background), 2.5f, palette.Name + " crossing edge");
            }
        }

        [Test]
        public void Lerp_EndpointsReturnTheSourceAndTargetColors()
        {
            ThemePalette a = ThemeService.Palettes[0];
            ThemePalette b = ThemeService.Palettes[1];

            Assert.AreEqual(a.Background, ThemePalette.Lerp(a, b, 0f).Background);
            Assert.AreEqual(b.Background, ThemePalette.Lerp(a, b, 1f).Background);
            Assert.AreEqual(b.Name, ThemePalette.Lerp(a, b, 0.5f).Name);
        }

        [Test]
        public void Lerp_Midpoint_BlendsColors()
        {
            ThemePalette a = ThemeService.Palettes[0];
            ThemePalette b = ThemeService.Palettes[1];

            Color mid = ThemePalette.Lerp(a, b, 0.5f).Node;
            Color expected = Color.Lerp(a.Node, b.Node, 0.5f);

            Assert.AreEqual(expected.r, mid.r, 1e-5f);
            Assert.AreEqual(expected.g, mid.g, 1e-5f);
            Assert.AreEqual(expected.b, mid.b, 1e-5f);
        }

        [Test]
        public void IsUnlocked_AtExactUnlockLevel_IsTrue()
        {
            Assert.IsTrue(ThemeService.IsUnlocked(7, highestLevelUnlocked: 250));
        }

        [Test]
        public void IsUnlocked_OneBelowUnlockLevel_IsFalse()
        {
            Assert.IsFalse(ThemeService.IsUnlocked(7, highestLevelUnlocked: 249));
        }

        [Test]
        public void IsUnlocked_FirstTheme_AlwaysUnlockedFromLevelOne()
        {
            Assert.IsTrue(ThemeService.IsUnlocked(0, highestLevelUnlocked: 1));
        }
    }
}
