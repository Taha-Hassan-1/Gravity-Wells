using NUnit.Framework;
using UnityEngine;
using GravityWells.Core;

namespace GravityWells.Tests
{
    public class CameraFitTests
    {
        // Aspect ratios from 4:3 (a tablet held upright) to 21:9 turned upright.
        private static readonly float[] Aspects = { 3f / 4f, 9f / 16f, 9f / 19.5f, 9f / 21f };

        private static Rect VisibleRect(float aspect, CameraFitResult fit)
        {
            float height = fit.OrthographicSize * 2f;
            float width = height * aspect;
            return new Rect(fit.Center.x - width / 2f, fit.Center.y - height / 2f, width, height);
        }

        [Test]
        public void TheWholeField_IsVisible_AtEveryAspectRatio()
        {
            foreach (float aspect in Aspects)
            {
                Rect visible = VisibleRect(aspect, CameraFit.ComputeForField(aspect));
                Assert.LessOrEqual(visible.xMin, -GameConfig.ViewMargin + 1e-4f, "left, aspect " + aspect);
                Assert.GreaterOrEqual(visible.xMax, GameConfig.FieldWidth + GameConfig.ViewMargin - 1e-4f, "right, aspect " + aspect);
                Assert.LessOrEqual(visible.yMin, -GameConfig.ViewMargin + 1e-4f, "bottom, aspect " + aspect);
                Assert.GreaterOrEqual(visible.yMax, GameConfig.FieldHeight + GameConfig.ViewMargin - 1e-4f, "top, aspect " + aspect);
            }
        }

        [Test]
        public void TheField_StaysInsideTheBandBetweenTheHudAndTheTray()
        {
            foreach (float aspect in Aspects)
            {
                CameraFitResult fit = CameraFit.ComputeForField(aspect);
                Rect visible = VisibleRect(aspect, fit);

                // Where the field's top and bottom edges land on screen, as fractions of the height from the bottom.
                float top = (GameConfig.FieldHeight - visible.yMin) / visible.height;
                float bottom = (0f - visible.yMin) / visible.height;
                Assert.LessOrEqual(top, 1f - GameConfig.ViewTopReserve + 1e-3f, "under the HUD, aspect " + aspect);
                Assert.GreaterOrEqual(bottom, GameConfig.ViewBottomReserve - 1e-3f, "above the tray, aspect " + aspect);
            }
        }

        [Test]
        public void ASafeAreaInset_PushesTheFieldInwards()
        {
            CameraFitResult plain = CameraFit.ComputeForField(9f / 19.5f);
            CameraFitResult notched = CameraFit.ComputeForField(9f / 19.5f, extraTopReserve: 0.05f, extraBottomReserve: 0.03f);

            Assert.GreaterOrEqual(notched.OrthographicSize, plain.OrthographicSize);
        }

        [Test]
        public void AWiderScreen_NeverCropsTheSides()
        {
            // 4:3 upright is wide for a portrait game; the height decides, and the sides have room to spare.
            CameraFitResult fit = CameraFit.ComputeForField(3f / 4f);
            Rect visible = VisibleRect(3f / 4f, fit);
            Assert.Greater(visible.width, GameConfig.FieldWidth);
        }
    }
}
