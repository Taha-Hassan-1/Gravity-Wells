# 04 — Difficulty curve and scoring

## What a level is made of (`DifficultyCurve`)

| Quantity | Rule | Reaches |
|---|---|---|
| Wells (par) | `clamp(1 + floor(sqrt(level) × 0.35), 1, 4)` | 2 at level 9, 3 at 33, 4 at 74 |
| Gates | `clamp(1 + level / 8, 1, 5)` | 5 at level 32 |
| Gate width | lerp 0.16 → 0.07 over levels 1–151, then flat | |
| Goal radius | lerp 0.07 → 0.04 over levels 1–151, then flat | |
| Asteroids | none before 12, then `clamp(1 + (level − 12) / 15, 1, 4)` | 4 at level 57 |
| Repulsors | from level 35 (generator, doc 02) | |
| Seed | `SeedForLevel(level) = (level × 2654435761) xor 0x85EBCA6B` | |

Four wells is the cap: past that a level is more of the same but slower. Difficulty beyond it comes from gate width, gate count and asteroids.

`PlanForLevel` applies the curve with levels 1–3 fixed by rule (level 1: one well, one gate, goal radius 0.09; level 2: one well, two gates; level 3: two wells, two gates).

## Scoring (`Scoring`)

- 3 stars for par wells or fewer, 2 for par + 1, 1 for par + 2.
- A win using the rewarded Extra Well is 1 star, however few wells.
- `IsUnderPar` is true when the winning layout used fewer wells than par (feeds the "Under par" badge and a quest in Phase 4).

## Status of the constants

**All of these, and the physics constants in doc 01, are still the spec's starting guesses.** `LevelBatchTester` measures them and the owner agrees values before they are locked; see the proposal in the Phase 1 report and `docs/TEST_CASES.md`. Until then the tests pin the curve's shape, not its tuning.

**Decision (owner, 2026-11-14): lock none of them now; decide after Phase 2**, when the board can be played and felt on a phone. Open questions for that review: whether to raise `MinForgiveness` (the 0.2 floor never triggers; the lowest band average is about 0.47), whether gate width should keep narrowing past level 151, and the 0.25 ms flight budget on a device.

## Code and tests

`Core/DifficultyCurve.cs`, `Core/Scoring.cs`; `DifficultyCurveTests` (well, gate, width, goal and asteroid values at the boundary levels, monotonic growth, the fixed first three levels, buffer limits, stars).
