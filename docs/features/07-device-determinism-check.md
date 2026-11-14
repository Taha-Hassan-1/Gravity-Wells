# 07 — Device determinism check

The daily puzzle and share codes need a seed to mean the same level on every phone. Core avoids trigonometry for that (only `+ - * /` and square root, which are rounded identically everywhere), but IL2CPP on ARM64 may fuse a multiply and an add into one instruction, which can change the last bit of a float compared with the Editor. This check measures it instead of assuming.

## What it computes

`DeterminismCheck.CombinedHash()` generates campaign levels 1 to 100 from their campaign seeds, flies each level's own solution, takes `Trajectory.Hash()` of each flight (every point, the outcome and the lit gates, bit for bit) and folds the 100 hashes into one 32-bit number. It covers the generator as well as the simulator: a level that comes out differently flies differently.

## How to run it

| Where | How | Result |
|---|---|---|
| Editor | menu `GravityWells ▸ Tuning ▸ Print Determinism Hash` | logged as `[Determinism] ...` and written to `Logs/determinism-editor.txt` |
| Phone | a **development build** only: tap the level title seven times, quickly | a toast, a `[Determinism]` line in `adb logcat`, and `determinism.txt` in the app's data folder |

Compare the two numbers. **If they differ, stop and ask the owner** (`CLAUDE.md`): the fallback is fixed-point arithmetic in the integrator. A release build has no such action.

## Recorded results

| Where | Hash | Date |
|---|---|---|
| Editor (Windows, Mono) | `-159271037` | 2026-11-14 |
| Phone | **not yet run** (no device was connected) | |

`DeterminismTests.TheCombinedHashOfTheCampaign_IsStableFromRunToRun` pins the Editor value (`ExpectedEditorHash`), so any change to the generator or the simulator that moves the campaign fails a test and forces a conscious decision: record the new value and re-run the device check.

## Code

`Core/DeterminismCheck.cs`, `Editor/DeterminismTool.cs`, the hidden tap counter in `UI/GameHUD.cs`, `GameManager.RunDeterminismCheck`.

## Not verified

Everything about the phone: the result above is only the Editor's half.
