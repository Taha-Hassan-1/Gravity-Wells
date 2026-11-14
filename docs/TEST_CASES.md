# Gravity Wells — Test Cases

Last executed: 2026-11-14 · Build: branch `Phase3-Feel` · Unity 6000.3.15f1 · EditMode suite 435/435 passing (headless, including the 5 Play Mode smoke tests).

**How a case is verified** (the *Via* column):

| Via | Meaning |
|---|---|
| **Auto** | Covered by an EditMode test (`Assets/_Project/Scripts/Tests/EditMode/`). Runs through the Unity CLI. |
| **Live** | Driven in the running Editor (Play Mode) through the Unity CLI, with the save redirected by `GRAVITYWELLS_DATA_DIR`. |
| **Device** | Needs a phone or a human (touch feel, audio, framing, Back button). |

**Result:** ✅ pass · ❌ fail (bug logged) · ⏳ not run yet · 🔧 failed once, fixed, re-verified.

Re-run the automated part:

```bash
unity command menu --path "Assets/Refresh" --project-path "D:/Projects/GravityWells"
unity command run_tests --mode EditMode --timeout 150 --project-path "D:/Projects/GravityWells" --no-banner --result-only
```

---

## 0. Setup and shared code (M0)

| ID | Case | Expected | Via | Result |
|---|---|---|---|---|
| M0-01 | Project compiles with the four asmdefs | No compile errors; `Core` has no references | Auto (compile) | ✅ |
| M0-02 | Interstitial pacing rules | First blocker wins, in the documented order | Auto `AdPolicyTests` (11) | ✅ |
| M0-03 | Preloaded ad pool bookkeeping | One load in flight, never past the pool size, backoff after failure | Auto `AdPoolPlannerTests` (11) | ✅ |
| M0-04 | Waterfall over several ad networks | Fallback, runtime re-ordering, most informative failure wins | Auto `WaterfallAdServiceTests` (22) | ✅ |
| M0-05 | Monetization facade | Consent before ads, remove-ads grant and restore, analytics events | Auto `MonetizationControllerTests` (25) | ✅ |
| M0-06 | Rewarded and interstitial flow | Spinner, timeout, Cancel, watchdog, reward only on completion | Auto `AdFlowTests` (14) | ✅ |
| M0-07 | Ad failure wording | Every reason has its own title and a real explanation | Auto `AdFeedbackTests` (2) | ✅ |
| M0-08 | Analytics fan-out, file log, exception reporting | A broken sink never silences the others; the log rotates at its size limit; a repeating exception is reported once | Auto `AnalyticsSinkTests` (11) | ✅ |
| M0-09 | Save service | Atomic write, backup, corrupt-file recovery, migration steps | Auto `SaveServiceTests` (15) | ✅ |
| M0-10 | Themes | Eight themes plus the colour-safe one, unlock levels, legible colours, blending | Auto `ThemeServiceTests` (12) | ✅ |
| M0-11 | UI design tokens and contrast | Spacing scale, minimum touch target, text and surface contrast ratios | Auto `UIDesignSystemTests` (13) | ✅ |
| M0-12 | UI fonts | Both atlases are static and hold every printable ASCII character and all game text | Auto `UIFontTests` (3) | ✅ |
| M0-13 | Daily challenge | Same UTC date gives the same seed; streak counts, resets and best streak | Auto `DailyChallengeServiceTests` (11) | ✅ |
| M0-14 | Coins and daily quests | Payouts, daily reset, all-quests bonus | Auto `EconomyAndQuestTests` (21) | ✅ |
| M0-15 | Vibration plan and permission | Each moment maps to an effect the phone has; Strong is never weaker; the manifest gains `VIBRATE` once | Auto `HapticPlanTests` (11) | ✅ |
| M0-16 | Count-up animation | Lands exactly on the target without overshooting | Auto `CountUpTests` (7) | ✅ |
| M0-17 | Share codes | Round trip, typo detection, `GW-` format | Auto `PuzzleCodeTests` (19) | ✅ |
| M0-18 | Debug APK builds | IL2CPP ARM64, min API 25, target 36, portrait, `VIBRATE` in the manifest | Editor build + `aapt2 dump badging` | ✅ 21.4 MB |
| M0-19 | APK installs and starts on a phone | Installs, opens to an empty scene, no crash | Device | ⏳ |

## 1. Flight core (Phase 1)

Measured 2026-11-14 in the Editor (Mono). "Debug" and "Release" are the Editor's script compilation setting; the spec's budgets are for a phone.

| ID | Case | Expected | Via | Result |
|---|---|---|---|---|
| FLT-01 | No wells | Straight line at constant speed for the whole flight | Auto `FlightSimulatorTests` | ✅ |
| FLT-02 | One attractor / one repulsor beside the path | Path bends toward / away | Auto `FlightSimulatorTests` | ✅ |
| FLT-03 | Mirrored setup | Mirrored trajectory | Auto `FlightSimulatorTests` | ✅ |
| FLT-04 | Energy, path kept more than 0.1 from the well | Kinetic + softened potential drifts under 2% | Auto `FlightSimulatorTests` | ✅ |
| FLT-05 | Gates | Lit from either side; not by a post touch or the post's extension; lit when the ship lands exactly on the gate line; lit at 10× launch speed; any order, stay lit | Auto `FlightSimulatorTests` | ✅ |
| FLT-06 | Goal | Does nothing until every gate is lit; swept at 10× speed | Auto `FlightSimulatorTests` | ✅ |
| FLT-07 | The five outcomes | One hand-built case each | Auto `FlightSimulatorTests` | ✅ |
| FLT-08 | Allocation | Zero bytes in `Simulate` and in `HintPlanner.TryPlan` | Auto `FlightSimulatorTests`, `HintTests` | ✅ |
| FLT-09 | Time for an 8 s flight, 4 wells, 4 asteroids, 5 gates (spec: under 0.25 ms) | Under 0.25 ms | Auto `FlightSimulatorTests` (timing) | 🔧 **Release 0.25–0.26 ms, Debug 0.35 ms: slightly over the spec in the Editor.** The test allows 20% (Release) or 2× (Debug) so noise does not fail it. Not confirmed on a phone; Phase 2 device check |
| GEN-01 | 1,000 seeds over levels 1–300: solution wins, zero wells do not, each one-well-removed layout does not | All hold | Auto `GeneratorTests` | ✅ |
| GEN-02 | Everything inside the field; well, gate and asteroid clearances; `Par` equals the plan | All hold | Auto `GeneratorTests` | ✅ |
| GEN-03 | Levels 1–3 fixed by rule; repulsors only from 35 and never first | Hold | Auto `GeneratorTests` | ✅ |
| GEN-04 | Gate-drop fallback | Under 1% of levels (measured 0.08% over 10,000); `Generate` never throws | Auto `GeneratorTests`, batch | ✅ |
| GEN-05 | `Generate` average (spec: under 30 ms) | Under 30 ms | Auto `GeneratorTests` (timing) | ✅ Release 6.8–7.1 ms, Debug 10.4 ms (levels 1–300) |
| DET-01 | Same `(seed, level)` twice | Bit-identical data | Auto `DeterminismTests` | ✅ |
| DET-02 | Same layout simulated twice | Bit-identical buffer and hash | Auto `DeterminismTests` | ✅ |
| DET-03 | Scan of the simulator and generator source | No `Sin`, `Cos`, `Atan2`, `Pow`, `Exp` | Auto `CoreRulesTests` | ✅ |
| DET-04 | Core's use of Unity, and of Unity's random; who reads the stored solution | Only `Vector2`, `Mathf`; none; only the generator, forgiveness and the level | Auto `CoreRulesTests` | ✅ |
| DET-05 | Combined trajectory hash of the campaign solutions, levels 1–100, in the Editor | Stable run to run | Auto `DeterminismTests` | ✅ `-159271037` (the number the Phase 2 device check compares against) |
| SOL-01 | Solver on campaign levels 1–100 within 20,000 flights (spec: at least 95%) | 95%+ | Auto `SolverTests` | ✅ 100/100, 720 flights on average |
| SOL-02 | Solver on 500 sampled levels from the batch (levels 1–250, 2 seeds each) | — | Batch | 98.6% (seven bands at 95%, none lower) |
| HNT-01 | Hint from 1,000 random non-winning layouts | Never worse; a move found for almost all | Auto `HintTests` | ✅ 999 of 1,000 improved |
| HNT-02 | Repeated hints from an empty field, levels 1–50, up to 12 hints (spec: 80%) | 40 of 50 | Auto `HintTests` | ✅ 42 of 50 (84%). Not won: 12 15 19 30 33 36 39 48 |
| HNT-03 | Hint never lands on the stored solution | None | Auto `HintTests` | ✅ |
| DIF-01 | Curve and scoring boundary values | As specified | Auto `DifficultyCurveTests` | ✅ |
| DIF-02 | Path score | 0 only for a win; 1000 per unlit gate; crash +500 | Auto `DifficultyCurveTests` | ✅ |
| DIF-03 | 10,000-level batch (`LevelBatchTester`) | 0 failures | Editor tool | ✅ 39.2 attempts per level on average, 0.08% gate drops, forgiveness 0.84 (levels 11–20) falling to about 0.48 (levels 170+) |

## 2. Playable board (Phase 2)

Measured 2026-11-14 in the Editor. "Play" rows run the real `Bootstrap.unity` in Play Mode through `GameFlowSmokeTests`, which must be run **headlessly with the Editor closed** (see `CLAUDE.md`, Verification). Screens were checked in offscreen 1080×1920 renders (`Logs/Screens/`, git-ignored).

| ID | Case | Expected | Via | Result |
|---|---|---|---|---|
| BRD-01 | Camera fit at 4:3, 16:9, 19.5:9 and 21:9 (upright) | Whole field visible, between the HUD and the tray reserve; safe-area insets push it inward | Auto `CameraFitTests` (4) | ✅ |
| BRD-02 | The board, HUD and result card in a portrait render | Field, gates, wells, ship, path, tray and buttons laid out without overlap | Offscreen render | ✅ after fixes (below) |
| BRD-03 | Asteroids, repulsor outline, lit and unlit gates, filled goal | Drawn, and told apart by shape as well as colour | Offscreen render (levels 1 and 60) | ✅ asteroids were missing at first; added |
| LAY-01 | Budget (par + 2), repulsor cap, legal positions, clamp | As specified | Auto `WellLayoutTests` (15) | ✅ |
| LAY-02 | Undo of place, move and remove; restart; one drag is one edit; tray drop never happened; cancel | As specified | Auto `WellLayoutTests` | ✅ |
| FLY-01 | The ship stays on the line that was shown | Every frame, the ship view equals the playback position, which lies on the copied preview line; the buffer's hash is unchanged after the flight | Play `GameFlowSmokeTests` | ✅ |
| FLY-02 | Playback timing and the 3× tap | Takes the flight's length; 3× after a tap; gates light as the ship reaches them | Auto `FlightPlaybackTests` (6) | ✅ |
| FLY-03 | Solve level 1 and level 60 (five gates, asteroids) with their own wells through the real `GameManager` | Wins, result card, 3 stars at par, next level starts empty | Play `GameFlowSmokeTests` | ✅ |
| FLY-04 | A failed launch | Wells stay exactly where they were, launch counted, back to planning | Play `GameFlowSmokeTests` | ✅ |
| FLY-05 | Undo, restart, pause (Back resumes), hint | As specified; the game is never stranded | Play `GameFlowSmokeTests` | ✅ |
| CMP-01 | Campaign: stars by wells used, stars only rise, progress and unlocks saved and reloaded, extra well, under par | As specified | Auto `CampaignModeTests` (12) | ✅ |
| DET-06 | Device determinism hash, Editor half | `-159271037` | Editor menu / Auto `DeterminismTests` | ✅ |
| DET-07 | Device determinism hash, phone half | Same number as the Editor | Device | ⏳ **not run: no phone connected** |
| INP-01 | Press, drag, drop and tray-removal by a finger | Works with one finger, ignores a second | Device | ⏳ not run. The controller reads `Input`, which no test can inject; the rules behind it are covered by LAY-01/02 |
| PERF-01 | 60 fps and zero GC per frame while dragging a well on a 4-well, 5-gate, 4-asteroid level | Zero allocation; 60 fps on a mid-range phone | Auto for allocation (`Simulate` and `TryPlan` allocate 0 bytes); Device for fps | allocation ✅; **fps and Profiler number ⏳ not measured** |

Fixes found by looking at the renders: all text was missing (TextMesh Pro essentials were not imported), asteroids were not drawn, the hint ghost used the repulsor shape for an attractor, the tray count overlapped its glyph, and the result card hid its "New best" line behind a button.

## 3. Feel (Phase 3)

Measured 2026-11-14 in the Editor. Visual rows are offscreen 1080×1920 renders from `GameFlowSmokeTests.TheFeel_AnswersEachMoment...` (`Logs/Screens/09`–`16`). **No audio row can say "sounds good": nothing here has been heard.**

| ID | Case | Expected | Via | Result |
|---|---|---|---|---|
| FEL-01 | A gate lighting in the preview is reported once; going dark is silent; lighting again is new; two at once; reset is silent | As specified | Auto `FeelLogicTests` | ✅ |
| FEL-02 | The goal chord fires on the frame the line starts to win, not while it keeps winning, not when losing it | As specified | Auto `FeelLogicTests` | ✅ |
| FEL-03 | Gate notes rise up a pentatonic scale and stop climbing past the table | Strictly rising; only scale degrees | Auto `FeelLogicTests` | ✅ |
| FEL-04 | Win sequence: gates ping in turn, stinger after the last, inside the hold | As specified | Auto `FeelLogicTests` | ✅ |
| FEL-05 | Missed gates; the hint pulses once, on the third failed launch only | As specified | Auto `FeelLogicTests` | ✅ |
| FEL-06 | The last stretch of a winning flight plays at half speed (+0.15 s); a dead flight at full speed; the same for any frame length; the ship never leaves the buffer | As specified | Auto `FeelLogicTests`, `FlightPlaybackTests` | ✅ |
| FEL-07 | The line eases over 0.05 s from the old shape to exactly the new; the real buffer is never touched; cancellable | As specified | Auto `FeelLogicTests` | ✅ |
| FEL-08 | Animation curves: bump, blink twice, settle with one small overshoot, rings, charge | As specified | Auto `FeelCurvesTests` (5) | ✅ |
| FEL-09 | The whoosh's volume and pitch follow the ship's speed, never silent, capped | As specified | Auto `FeelLogicTests` | ✅ |
| FEL-10 | The line while the ship charges does not change; flight, win and result still hold | As specified | Play `GameFlowSmokeTests` | ✅ |
| FEL-11 | Held well, gate pop and filled goal, drop and glide, charge ring, flight with trail, win burst with a ring through the gate, miss cross and blinking gate, a fresh level carries nothing over | Each looks as designed | Offscreen renders | ✅ after one fix (the miss cross sat under the HUD; now clamped to the field edge) |
| AUD-01 | Every one-shot is audible, peaks at the shared level, never clips, starts and ends at silence, is identical every run, has no NaN | As specified | Auto `AudioSynthesisTests` (44) | ✅ |
| AUD-02 | The whoosh loop joins without a click and is an even texture, not a tone | As specified | Auto `AudioSynthesisTests` | ✅ |
| AUD-03 | The music: RMS 0.2 ± 0.03, never above 0.95, exactly 8 bars at 66 bpm, seamless join, identical every run, has a tune (not flat), has space, no empty second | As specified | Auto `AudioSynthesisTests` | ✅ |
| AUD-04 | WAV export of every sound and two passes of the music | Files written to `Logs/Audio/` | Auto `AudioSynthesisTests` | ✅ written; **not listened to** |
| AUD-05 | The sounds and the music actually sound good; the music sits right behind the effects; the sounds on a phone speaker | Pleasant, balanced | Human + Device | ⏳ **not heard by anyone** |
| PAL-01 | Every role keeps at least 3:1 contrast against the playfield in all nine palettes, at the alphas drawn; lit and unlit gates differ in colour as well as shape; the winning path differs from the ordinary one | As specified | Auto `ViewColorsTests` (5) | ✅ after two fixes (Neon's winning path matched its normal one; asteroids were drawn at 90%) |
| PAL-02 | Each palette looks good, not merely legible, with the board | Looks right | Eyes | ⏳ renders were taken in the default palette only |
| HAP-01 | Vibrations: light on a gate, medium on launch, strong on a win; a tap on pick-up and drop | Right motor effect | Device | ⏳ mapping written; **not felt** |
| PERF-02 | Frame rate on a phone with the line, rings, stars and burst animating every frame | 60 fps | Device | ⏳ not measured; nothing allocates in the animations |

## Not yet verified on a device

Everything. No phone was connected. In particular: whether a phone flies the same trajectory as the Editor (DET-05 on a device), and the speed of a flight under IL2CPP (FLT-09).
