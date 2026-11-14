# Gravity Wells — Test Cases

Last executed: 2026-11-14 · Build: branch `main` (M0 setup) · Unity 6000.3.15f1 · EditMode suite 224/224 passing.

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

## Not yet verified on a device

Everything. No phone was connected during M0.
