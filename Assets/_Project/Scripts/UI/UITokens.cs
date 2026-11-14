namespace GravityWells.UI
{
    // The shared measurements every screen is laid out with, so spacing, sizes and type follow one scale
    // instead of numbers picked per screen. All values are canvas units on the 1080-wide reference canvas;
    // the design brief's "px" scale (a 360-wide phone) maps to them at three units per px.
    public static class UITokens
    {
        public const float Unit = 3f;

        // 4/8-point spacing scale: icon gap, compact, button padding, standard gap, section gap, screen gap.
        public const float SpaceXs = 4f * Unit;
        public const float SpaceS = 8f * Unit;
        public const float SpaceM = 12f * Unit;
        public const float SpaceL = 16f * Unit;
        public const float SpaceXl = 24f * Unit;
        public const float SpaceXxl = 32f * Unit;

        // The edge margin of every full-width element.
        public const float ScreenMargin = SpaceL;
        public const float ContentWidth = 1080f - 2f * 48f; // inside the screen margin on the reference canvas

        // Controls: a standard button, a smaller utility one, and the floor for anything tappable.
        public const float ButtonHeight = 48f * Unit;
        public const float CompactHeight = 42f * Unit;
        public const float MinTouch = 40f * Unit; // 40 px at this scale is about 44 dp on a typical phone

        public const float CornerRadius = 14f * Unit;
        public const float IconSize = 19f * Unit;

        // Type scale. Display (the heavy face) only for the logo and the headline numbers.
        public const float TextScreenTitle = 30f * Unit;
        public const float TextMetric = 28f * Unit;
        public const float TextSection = 16f * Unit;
        public const float TextButton = 15f * Unit;
        public const float TextSupporting = 13f * Unit;
        public const float TextMeta = 11f * Unit;
    }
}
