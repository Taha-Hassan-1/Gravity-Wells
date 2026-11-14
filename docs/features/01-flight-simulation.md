# 01 — Flight simulation

The whole flight is one pure function in Core. The preview draws its output and (from Phase 2) the ship plays the same output back, so the ship can never leave the line the player was shown.

## Behaviour

`FlightSimulator.Simulate(level, wells, wellCount, into)` integrates the ship from the start to an outcome and fills a caller-owned `Trajectory`.

- **Step:** semi-implicit Euler at `dt = 1/120`. Sum the pull of every well, `v += a * dt`, then `p += v * dt`.
- **Pull of a well:** with `d = well - p` and `r2 = d² + Softening²`, the acceleration is `d * WellStrength / (r2 * sqrt(r2))`. A repulsor is the same with the sign flipped.
- **Per step, in this order:** light any unlit gate the step crossed; asteroids; well cores; the goal (only once every gate is lit); the field bounds. The step's end point is always recorded, so the path draws to where it ended.
- **Outcomes:** `Won`, `HitAsteroid`, `HitWell` (either kind), `LeftField` (past the field by more than `FieldMargin`), `TimedOut` (8 seconds, which fills the buffer exactly: 961 points).
- **Gates** light from either side, in any order, and stay lit. A path that only touches a post, or passes beside the gate on the line through its posts, does not light it.
- **Goal** is swept (distance from the goal centre to the step segment), so a fast ship cannot step over it. Before every gate is lit the ship flies over it.

## Numbers (`Core/GameConfig.cs`, starting guesses until the owner locks them)

| Constant | Value |
|---|---|
| `FieldWidth` × `FieldHeight`, `FieldMargin` | 1 × 1.5, 0.05 (origin at the bottom-left corner) |
| `StepsPerSecond`, `MaxFlightSeconds` | 120, 8 |
| `LaunchSpeed` | 0.35 field widths per second |
| `WellStrength`, `Softening`, `WellCoreRadius` | 0.02, 0.03, 0.035 |
| `MinWellDistanceFromStart` | 0.12 |
| `ExtraWellBudget` | 2 (the player may use par + 2 wells) |

## Code

- `Core/FlightSimulator.cs`: the integrator and every collision test. Plain floats and locals in the loop, no Vector2 operators.
- `Core/Trajectory.cs`: the reusable buffer, outcome, lit-gate mask, the step each gate lit on, and `Hash()`.
- `Core/Well.cs`, `Gate.cs`, `Asteroid.cs`, `LevelDefinition.cs`: the data.
- `Core/WellRules.cs`: where a well may go (`IsLegal`, `Clamp`), shared by the solver, the hint and later the drag.
- `Core/PathScore.cs`: how far a flight is from winning (see doc 03).

## Decisions and edge cases

- **Gate crossing is not `GeometryUtils.SegmentsProperlyIntersect`**, although the spec names it. That test has a tolerance, and a ship landing within the tolerance of the gate line would be seen as "touching" by the step that arrives and again by the step that leaves, so the gate would never light (about 1 crossing in 150 at these step sizes). The simulator instead sorts the two ends of the step onto the two sides of the line, with "exactly on the line" counted as one fixed side, so consecutive steps tile the path with no gap. The posts are still tested strictly. `AShipLandingExactlyOnTheGateLine_StillLightsIt` pins this.
- **No trigonometry, powers or exponentials** in the simulator or the generator: only `+ - * /` and square root, which round the same on every device. `CoreRulesTests` scans the source for it.
- **A cheap bound skips most distance tests:** a circle of radius r can only touch a step of length L if its centre is within r + L of the step's end, and (r + L)² ≤ 2(r² + L²).
- **Zero gates** means the goal counts from the first step.
- The simulator does not check that wells are legally placed; `WellRules` does, and the caller is responsible.
- `SimulateFree` (internal) is a flight with no gates, goal or asteroids, used by the generator.

## Tests

`FlightSimulatorTests` (23): straight line at constant speed with no wells; an attractor bends the path toward it and a repulsor away; a mirrored setup gives a mirrored trajectory; energy drift under 2% on a path that stays 0.1 clear of the well; gates from both sides, post and extension cases, the exact-on-the-line case, 10× launch speed; the goal waits for the gates; one hand-built case per outcome; buffer reuse; zero allocation; the time budget. `DeterminismTests`: the same layout simulated twice is bit-identical, and the hash changes when a well moves by 0.00001.

## Not verified

- **Speed on a phone.** The 0.25 ms budget is measured in the Editor only (see `docs/TEST_CASES.md` for the Debug and Release numbers).
- **Float results on a phone.** The Editor-versus-device hash comparison is Phase 2's device check. `DeterminismTests.CombinedHashOfLevels1To100` is the number it will compare.
