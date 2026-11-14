namespace GravityWells.Core
{
    // What the game wants the player to feel, named by the moment rather than by a motor setting.
    public enum HapticKind
    {
        Tap,   // a button was pressed
        Grab,  // a dot was picked up
        Cross, // a line of the dragged dot crossed, or stopped crossing, another line
        Solve, // the board came clean
    }

    // Chooses the Android effect for each moment. The first choice is the system's own touch
    // feedback (android.view.HapticFeedbackConstants, played through View.performHapticFeedback):
    // the effects the keyboard and system buttons use, tuned by the phone maker for its motor, and
    // needing no permission. The second is a predefined VibrationEffect, for a phone where the
    // first is unavailable. Pure numbers, so the choice per Android version is tested.
    public static class HapticPlan
    {
        // android.view.HapticFeedbackConstants
        public const int LongPress = 0;
        public const int VirtualKey = 1;
        public const int KeyboardTap = 3;
        public const int ClockTick = 4;
        public const int ContextClick = 6;   // API 23
        public const int GestureStart = 12;  // API 30
        public const int Confirm = 16;       // API 30
        public const int DragStart = 25;     // API 34

        // android.os.VibrationEffect predefined ids (API 29)
        public const int EffectClick = 0;
        public const int EffectTick = 2;
        public const int EffectHeavyClick = 5;

        // Sweeping a dot across a fan of lines must feel like distinct ticks, not one long buzz.
        public const float CrossMinIntervalSeconds = 0.045f;

        public static int FeedbackConstant(HapticKind kind, int sdk, bool strong)
        {
            switch (kind)
            {
                case HapticKind.Tap:
                    return strong ? VirtualKey : KeyboardTap;
                case HapticKind.Grab:
                    if (strong)
                    {
                        return LongPress;
                    }
                    return sdk >= 34 ? DragStart : sdk >= 30 ? GestureStart : VirtualKey;
                case HapticKind.Cross:
                    return strong ? ContextClick : ClockTick;
                default:
                    return sdk >= 30 ? Confirm : LongPress;
            }
        }

        public static int FallbackEffect(HapticKind kind, bool strong)
        {
            switch (kind)
            {
                case HapticKind.Tap:
                case HapticKind.Cross:
                    return strong ? EffectClick : EffectTick;
                case HapticKind.Grab:
                    return strong ? EffectHeavyClick : EffectClick;
                default:
                    return EffectHeavyClick;
            }
        }

        // Before API 29 there are no predefined effects: a short one-shot of this length...
        public static long FallbackMillis(HapticKind kind, bool strong)
        {
            long millis = kind == HapticKind.Solve ? 45 : kind == HapticKind.Grab ? 22 : 14;
            return strong ? millis * 8 / 5 : millis;
        }

        // ...and strength (1..255).
        public static int FallbackAmplitude(HapticKind kind, bool strong)
        {
            if (strong || kind == HapticKind.Solve)
            {
                return 255;
            }
            return kind == HapticKind.Grab ? 200 : 150;
        }

        // Only crossings are paced; every other moment is a single deliberate act.
        public static bool IsDue(HapticKind kind, float now, float lastCrossTime)
        {
            return kind != HapticKind.Cross || now - lastCrossTime >= CrossMinIntervalSeconds;
        }
    }
}
