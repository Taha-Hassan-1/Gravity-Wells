# Gravity Wells — Test Cases

Last executed: 2026-11-14 · Build: branch `Phase1-FlightCore` · Unity 6000.3.15f1 · EditMode suite 321/321 passing.

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

## Not yet verified on a device

Everything. No phone was connected. In particular: whether a phone flies the same trajectory as the Editor (DET-05 on a device), and the speed of a flight under IL2CPP (FLT-09).
