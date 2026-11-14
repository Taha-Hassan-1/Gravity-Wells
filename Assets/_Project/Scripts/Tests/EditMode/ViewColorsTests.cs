using NUnit.Framework;
using UnityEngine;
using GravityWells.Game;
using GravityWells.Meta;
using GravityWells.UI;

namespace GravityWells.Tests
{
    // The board has to be readable in every palette, not just the one it was first looked at in.
    public class ViewColorsTests
    {
        private static readonly ThemePalette[] Palettes = ThemeService.Palettes;

        private static Color Over(Color foreground, Color background)
        {
            float a = foreground.a;
            return new Color(
                foreground.r * a + background.r * (1f - a),
                foreground.g * a + background.g * (1f - a),
                foreground.b * a + background.b * (1f - a), 1f);
        }

        private static float Contrast(Color foreground, Color field) => UIStyle.ContrastRatio(Over(foreground, field), field);

        [Test]
        public void EveryRole_KeepsTheMinimumContrast_AgainstThePlayfield_InEveryPalette()
        {
            Assert.GreaterOrEqual(Palettes.Length, 9, "eight themes and the colour-safe one");
            foreach (ThemePalette palette in Palettes)
            {
                var c = new ViewColors(palette);
                string name = palette.Name;
                Assert.GreaterOrEqual(Contrast(c.Neutral, c.Field), ViewColors.MinContrast, name + " neutral (wells, the path)");
                Assert.GreaterOrEqual(Contrast(c.Active, c.Field), ViewColors.MinContrast, name + " active (the ship, the well in the finger)");
                Assert.GreaterOrEqual(Contrast(c.Lit, c.Field), ViewColors.MinContrast, name + " lit (gates, the goal)");
                Assert.GreaterOrEqual(Contrast(c.Good, c.Field), ViewColors.MinContrast, name + " good (the winning path)");
                Assert.GreaterOrEqual(Contrast(c.Warning, c.Field), ViewColors.MinContrast, name + " warning (crashes, asteroids)");
            }
        }

        [Test]
        public void TheThingsDrawnFaint_AreStillSeen()
        {
            // The dotted line is drawn at 80% and an unlit gate post at 70%; asteroids are solid; faint must not mean invisible.
            foreach (ThemePalette palette in Palettes)
            {
                var c = new ViewColors(palette);
                Assert.GreaterOrEqual(Contrast(ViewColors.WithAlpha(c.Neutral, 0.8f), c.Field), ViewColors.MinContrast, palette.Name + " path");
                Assert.GreaterOrEqual(Contrast(ViewColors.WithAlpha(c.Neutral, 0.7f), c.Field), ViewColors.MinContrast, palette.Name + " unlit gate post");
                Assert.GreaterOrEqual(Contrast(c.Warning, c.Field), ViewColors.MinContrast, palette.Name + " asteroid");
            }
        }

        [Test]
        public void ALitGateAndAnUnlitOne_DifferInColourToo_NotOnlyInShape()
        {
            foreach (ThemePalette palette in Palettes)
            {
                var c = new ViewColors(palette);
                float distance = Mathf.Abs(c.Lit.r - c.Neutral.r) + Mathf.Abs(c.Lit.g - c.Neutral.g) + Mathf.Abs(c.Lit.b - c.Neutral.b);
                Assert.Greater(distance, 0.2f, palette.Name + ": lit and unlit gates are nearly the same colour");
            }
        }

        [Test]
        public void ThePathWhileWinning_DiffersFromThePathWhileNot()
        {
            foreach (ThemePalette palette in Palettes)
            {
                var c = new ViewColors(palette);
                float distance = Mathf.Abs(c.Good.r - c.Neutral.r) + Mathf.Abs(c.Good.g - c.Neutral.g) + Mathf.Abs(c.Good.b - c.Neutral.b);
                Assert.Greater(distance, 0.2f, palette.Name + ": the winning path looks like any other");
            }
        }

        [Test]
        public void Readable_LeavesAGoodColourAlone_AndRescuesAFaintOne()
        {
            Color field = new Color(0.9f, 0.9f, 0.9f);
            Color text = Color.black;
            Color dark = new Color(0.1f, 0.2f, 0.4f);
            Assert.AreEqual(dark, ViewColors.Readable(dark, field, text));

            Color faint = new Color(0.88f, 0.86f, 0.9f);
            Color rescued = ViewColors.Readable(faint, field, text);
            Assert.GreaterOrEqual(UIStyle.ContrastRatio(rescued, field), ViewColors.MinContrast);
            Assert.AreNotEqual(faint, rescued);
        }
    }
}
