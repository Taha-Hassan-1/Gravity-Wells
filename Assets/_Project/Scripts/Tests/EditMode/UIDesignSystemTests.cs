using NUnit.Framework;
using UnityEngine;
using GravityWells.Game;
using GravityWells.Meta;
using GravityWells.UI;

namespace GravityWells.Tests
{
    // The shared design tokens and the two redesigned themes (the brief's light and dark versions): the rules a
    // later edit must not quietly break, such as text that stops being readable or a spacing scale that drifts.
    public class UIDesignSystemTests
    {
        private static ThemePalette Light => ThemeService.Palettes[0]; // Warm Paper
        private static ThemePalette Dark => ThemeService.Palettes[1];  // Deep Navy

        private static readonly string[] Themes = { "Light", "Dark" };

        private static ThemePalette Pick(string which) => which == "Light" ? Light : Dark;

        // ---- tokens ----

        [Test]
        public void TheSpacingScale_IsBuiltFromTheFourPointStep()
        {
            float[] steps =
            {
                UITokens.SpaceXs, UITokens.SpaceS, UITokens.SpaceM, UITokens.SpaceL, UITokens.SpaceXl, UITokens.SpaceXxl,
            };
            foreach (float step in steps)
            {
                Assert.AreEqual(0f, step % (4f * UITokens.Unit), 1e-4f, $"{step} is not a multiple of the 4-point step");
            }
            for (int i = 1; i < steps.Length; i++)
            {
                Assert.Greater(steps[i], steps[i - 1], "the scale must increase");
            }
        }

        [Test]
        public void EveryButtonHeight_MeetsTheMinimumTouchTarget()
        {
            Assert.GreaterOrEqual(UITokens.ButtonHeight, UITokens.MinTouch);
            Assert.GreaterOrEqual(UITokens.CompactHeight, UITokens.MinTouch);
        }

        [Test]
        public void TheContentWidth_IsTheCanvasMinusBothScreenMargins()
        {
            Assert.AreEqual(1080f - 2f * UITokens.ScreenMargin, UITokens.ContentWidth, 1e-3f);
        }

        // ---- contrast ----

        [Test]
        public void ContrastRatio_MatchesTheKnownExtremes()
        {
            Assert.AreEqual(21f, UIStyle.ContrastRatio(Color.black, Color.white), 0.01f);
            Assert.AreEqual(1f, UIStyle.ContrastRatio(Color.gray, Color.gray), 1e-4f);
        }

        [TestCaseSource(nameof(Themes))]
        public void Text_IsReadableOnTheBackgroundAndOnASurface(string which)
        {
            ThemePalette p = Pick(which);
            Assert.GreaterOrEqual(UIStyle.ContrastRatio(p.Text, p.Background), 4.5f, "text on background");
            Assert.GreaterOrEqual(UIStyle.ContrastRatio(p.Text, p.Surface), 4.5f, "text on surface");
            Assert.GreaterOrEqual(UIStyle.ContrastRatio(p.Text, p.SurfaceElevated), 4.5f, "text on elevated surface");
        }

        [TestCaseSource(nameof(Themes))]
        public void SupportingText_IsStillReadable(string which)
        {
            ThemePalette p = Pick(which);
            Assert.GreaterOrEqual(UIStyle.ContrastRatio(p.TextSecondary, p.Background), 4.5f, "supporting text on background");
            Assert.GreaterOrEqual(UIStyle.ContrastRatio(p.TextSecondary, p.Surface), 4.5f, "supporting text on surface");
        }

        [TestCaseSource(nameof(Themes))]
        public void TheMainButtonLabel_IsLegibleOnTheAccentFill(string which)
        {
            ThemePalette p = Pick(which);
            Color label = UIStyle.ReadableOn(p.Accent, p);
            // Button labels are large text, for which the bar is 3:1.
            Assert.GreaterOrEqual(UIStyle.ContrastRatio(label, p.Accent), 3f);
        }

        [TestCaseSource(nameof(Themes))]
        public void TheLinesAndTheSelectedNode_StandOutFromTheBackground(string which)
        {
            ThemePalette p = Pick(which);
            Assert.GreaterOrEqual(UIStyle.ContrastRatio(p.EdgeClear, p.Background), 3f, "clear edge");
            Assert.GreaterOrEqual(UIStyle.ContrastRatio(p.EdgeCrossing, p.Background), 3f, "crossing edge");
            Assert.GreaterOrEqual(UIStyle.ContrastRatio(p.Node, p.Background), 4.5f, "node");
            Assert.GreaterOrEqual(UIStyle.ContrastRatio(p.NodeGrabbed, p.Background), 3f, "selected node");
        }

        [TestCaseSource(nameof(Themes))]
        public void ClearAndCrossingEdges_AreClearlyDifferentColours(string which)
        {
            ThemePalette p = Pick(which);
            Vector3 a = new Vector3(p.EdgeClear.r, p.EdgeClear.g, p.EdgeClear.b);
            Vector3 b = new Vector3(p.EdgeCrossing.r, p.EdgeCrossing.g, p.EdgeCrossing.b);
            Assert.GreaterOrEqual(Vector3.Distance(a, b), 0.35f);
        }

        // ---- palette roles ----

        [Test]
        public void ASurfaceIsDistinctFromTheBackgroundAndTheElevatedOneFromTheSurface()
        {
            foreach (ThemePalette p in ThemeService.Palettes)
            {
                Assert.AreNotEqual(p.Background, p.Surface, p.Name);
                Assert.AreNotEqual(p.Surface, p.SurfaceElevated, p.Name);
            }
        }

        [Test]
        public void APaletteThatNamesNoSurface_GetsTheBlendedDefaultsItAlwaysHad()
        {
            ThemePalette forest = ThemeService.Palettes[2];
            Assert.AreEqual(Color.Lerp(forest.Background, forest.Text, 0.12f), forest.Surface);
        }

        [Test]
        public void ChangingTheLineColours_KeepsTheSurfaceRoles()
        {
            ThemePalette changed = Dark.WithEdgeColors(Color.red, Color.blue);
            Assert.AreEqual(Dark.Surface, changed.Surface);
            Assert.AreEqual(Dark.SurfaceElevated, changed.SurfaceElevated);
            Assert.AreEqual(Dark.TextSecondary, changed.TextSecondary);
            Assert.AreEqual(Color.red, changed.EdgeClear);
        }

        [Test]
        public void BlendingTwoPalettes_BlendsTheSurfaceRolesToo()
        {
            ThemePalette half = ThemePalette.Lerp(Light, Dark, 0.5f);
            Assert.AreEqual(Color.Lerp(Light.Surface, Dark.Surface, 0.5f), half.Surface);
            Assert.AreEqual(Color.Lerp(Light.TextSecondary, Dark.TextSecondary, 0.5f), half.TextSecondary);
        }
    }
}
