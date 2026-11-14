# 08 — Feel: the line, the wells, the launch, the win and the miss

The rule that keeps this safe: **the decisions are pure, the performing is thin.** Which moment deserves a sound, how time bends, when the hint pulses and how a curve moves are in Core with tests; `FeelController` performs them with the views, the audio and the vibration. `GameManager` never plays a sound or starts an animation.

## Under the finger

| What | How |
|---|---|
| **The line is the toy** | `PathView` drifts its dots along the line in the direction the ship will travel (`PathFlowStepsPerSecond`, 36 steps a second), fading in at the start and out before the end; the end is always marked by a larger dot. The vertex arrays are reused, so a drag still allocates nothing. |
| **Gates light live** | `PreviewTracker` turns the stream of preview lines into moments: a gate that has *just* lit pops and sings its note (`GateScale`: each gate lit goes one step up a major pentatonic scale, so any set of gates sounds right) with a light vibration; a gate going dark, or the line ceasing to win, makes **no** sound. Undo, Restart and a new level adopt their state silently. |
| **The goal** | When the line under the finger first reaches the goal with every gate lit, the goal fills with a pop, breathes, and a soft chord rings. It never launches by itself. |
| **Wells** | Two rings travel **inward** for an attractor and **outward** for a repulsor, half a cycle apart, so pull and push read at a glance. A dropped well settles (`FeelCurves.Settle`: large, then in with one small overshoot). A held well is brighter; one held where letting go removes it fades. |
| **The glide** | On a drop, Undo or Restart the line glides to its new shape over 0.05 s (`PathEase`); while a well is held there is no easing, the line follows the finger exactly. It is only a picture: the buffer is untouched and a test asserts it. |

## The launch

1. **Charge** (0.32 s): the ship swells and a ring draws in on the pad (`ShipView.SetCharge`); the launch thump and rising swell play. The line cannot change during it.
2. **Flight:** the ship follows the buffer (never re-simulated) with a trail of fading dots (`TrailView`), a looped whoosh whose volume and pitch follow the ship's real speed on the buffer (`WhooshCurve`), and for each gate it passes a ring flash (`GateViews.Flash`), a note and a light vibration. A tap speeds the flight up 3×.
3. **The last stretch of a winning flight plays at half speed** (`FlightPlayback`: the last 18 steps, 0.15 s of flight, take about 0.3 s to watch). It only slows time; the ship never leaves the buffer.
4. **The win** (`WinSequence`): a burst at the goal in two colours and the strong vibration; the gates ping one after another, 0.12 s apart, each with a quiet note; the win stinger lands after the last ping; then, after 1.3 s, the result card.

## When it does not work

`FeelController.Failed`: a **cross** where the ship ended (a shape, not only a colour), a small burst there, the **gates it missed blink twice** in the warning colour (`FeelCurves.Blink`), a sagging two-note sound, and **the hint button pulses once, on the third failed launch of a level** (`FailFeedback`). A failed launch costs nothing but the count.

## Vibrations

All through `HapticService` (the player-facing word is *Vibrations*): **light** (`Cross`, paced so a sweep across several gates is ticks and not a buzz) when a gate lights, in the preview and in flight; **medium** (`Grab`) when the ship is released; **strong** (`Solve`, the system's Confirm) on a win; a tap when a well is picked up or put down. The `HapticKind` names now describe this game's moments; the Android effect choices are Untangle's, unchanged.

## Code

`Core/PreviewTracker.cs` (with `GateScale`, `WinSequence`, `FailFeedback`, `WhooshCurve`), `Core/PathEase.cs`, `Core/FeelCurves.cs`, `Core/FlightPlayback.cs`; `Game/FeelController.cs`, `PathView.cs`, `WellViews.cs`, `GateViews.cs`, `ShipView.cs`, `TrailView.cs`, `BurstView.cs`, `CrashMarkView.cs`, `FieldView.cs`; `UI/GameHUD.cs` (the hint pulse). All timings are named constants in `GameConfig` under "Feel".

## Tests

`FeelLogicTests` (18) and `FeelCurvesTests` (5) cover every decision above; `GameFlowSmokeTests.TheFeel_AnswersEachMoment...` plays each moment in the real scene and saves a render of it (`Logs/Screens/09`–`16`).

## Not verified

- **How any of it feels.** Whether the drift speed, the pop size, the 0.3 s charge and the slow-down are right needs a phone and a hand, and the first day of Phase 2's device work (drag feel) comes before any of this can be judged.
- **Vibration strength on a real motor.** The mapping is tested; how it feels is not.
- **Frame rate.** The line now relays its dots every frame (about 160 quads), the rings and stars animate every frame; nothing allocates, but the cost on a mid-range phone is unmeasured.
