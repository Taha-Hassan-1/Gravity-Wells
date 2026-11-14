using UnityEngine;

namespace GravityWells.Core
{
    // The little animation curves the views share, as pure functions of elapsed time, so how they behave
    // (starts at rest, ends at rest, how many pulses) is tested and the views only apply them.
    public static class FeelCurves
    {
        // 0 at the start, rising to 1 in the middle and back to 0 at the end; 0 outside [0, duration].
        public static float Bump(float t, float duration)
        {
            if (duration <= 0f || t <= 0f || t >= duration)
            {
                return 0f;
            }
            float s = Mathf.Sin(Mathf.PI * t / duration);
            return s * s;
        }

        // `count` separate bumps between 0 and duration, back to zero between them: "blinks twice".
        public static float Blink(float t, float duration, int count)
        {
            if (count <= 0 || t <= 0f || t >= duration)
            {
                return 0f;
            }
            float each = duration / count;
            return Bump(t % each, each);
        }

        // A thing dropped into place: it starts large and settles to its size with a small overshoot.
        // Returns a scale: 1 once it has settled (t >= duration), above 1 at the start.
        public static float Settle(float t, float duration)
        {
            if (duration <= 0f || t >= duration)
            {
                return 1f;
            }
            float u = Mathf.Clamp01(t / duration);
            float decay = Mathf.Exp(-5f * u);
            return 1f + 0.4f * decay * Mathf.Cos(u * Mathf.PI * 3f);
        }

        // A ring travelling toward (or away from) the centre: 1 at the outside, 0 at the centre, phase 0..1.
        // The ring fades in at its start and out at its end, so it never pops.
        public static float RingRadius(float phase, bool inward)
        {
            float p = phase - Mathf.Floor(phase);
            return inward ? 1f - p : p;
        }

        public static float RingAlpha(float phase)
        {
            float p = phase - Mathf.Floor(phase);
            return Mathf.Sin(Mathf.PI * p);
        }

        // The launch charge: the ship swells as the ring draws in, then pops. 0..1 in, a scale out.
        public static float ChargeScale(float t)
        {
            float u = Mathf.Clamp01(t);
            return 1f + 0.35f * u * u - (u > 0.85f ? (u - 0.85f) * 2f : 0f);
        }
    }
}
