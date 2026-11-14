using UnityEngine;

namespace GravityWells.Core
{
    public readonly struct CameraFitResult
    {
        public readonly float OrthographicSize; // half the visible world height
        public readonly Vector2 Center;         // world position the camera looks at

        public CameraFitResult(float orthographicSize, Vector2 center)
        {
            OrthographicSize = orthographicSize;
            Center = center;
        }
    }

    // Where to put an orthographic camera so a content rectangle is fully visible, centred in the part of the
    // screen left over once the HUD (top) and the tray strip (bottom) are reserved. Pure maths so every aspect
    // ratio can be tested without a screen.
    public static class CameraFit
    {
        // aspect = screen width / height. sideMargin is a fraction of the width kept free on each side;
        // topReserve and bottomReserve are fractions of the height.
        public static CameraFitResult Compute(
            float aspect, Vector2 contentMin, Vector2 contentMax, float sideMargin, float topReserve, float bottomReserve)
        {
            float contentWidth = contentMax.x - contentMin.x;
            float contentHeight = contentMax.y - contentMin.y;
            float usableWidth = Mathf.Max(0.05f, 1f - 2f * sideMargin);
            float usableHeight = Mathf.Max(0.05f, 1f - topReserve - bottomReserve);

            // Whichever dimension is tighter decides the zoom; the other one just gets spare room.
            float visibleHeight = Mathf.Max(contentWidth / (usableWidth * aspect), contentHeight / usableHeight);

            // The content centre lands in the middle of the usable band (screen fractions from the bottom).
            float bandCentre = (bottomReserve + 1f - topReserve) * 0.5f;
            Vector2 contentCentre = (contentMin + contentMax) * 0.5f;
            var center = new Vector2(contentCentre.x, contentCentre.y - (bandCentre - 0.5f) * visibleHeight);
            return new CameraFitResult(visibleHeight * 0.5f, center);
        }

        // The field with its margin. extraTopReserve and extraBottomReserve are the device safe-area insets
        // (notch, gesture bar), as fractions of the height.
        public static CameraFitResult ComputeForField(float aspect, float extraTopReserve = 0f, float extraBottomReserve = 0f)
        {
            return Compute(
                aspect,
                new Vector2(-GameConfig.ViewMargin, -GameConfig.ViewMargin),
                new Vector2(GameConfig.FieldWidth + GameConfig.ViewMargin, GameConfig.FieldHeight + GameConfig.ViewMargin),
                GameConfig.ViewSideMargin,
                GameConfig.ViewTopReserve + extraTopReserve,
                GameConfig.ViewBottomReserve + extraBottomReserve);
        }
    }
}
