# 02 — Level generation

Levels are built backwards from a solved flight, so every level has a solution by construction and nothing ever searches for one. A level is a pure function of `(seed, level)` and is never written to disk.

## How one attempt goes (`LevelGenerator.TryBuild`)

1. **Start and launch.** The start is in the lower third, at least 0.12 from every edge. The launch direction is a seeded unit vector pointing upward, rejected if an unbent flight would leave the field in under 1.2 s.
2. **Solution wells.** `plan.Wells` wells inside the field inset by 0.1, at least 0.12 from the start and 0.22 from each other. From level 35 each well after the first has a 30% chance of being a repulsor.
3. **Fly it** with only the wells on the field. The path ends at the last step before it leaves the field inset by 0.08 (or by the goal radius plus 0.01 if that is larger, so the goal circle stays on the field).
4. **Reject** the attempt if: the flight hit a well; it lasted under 2.5 s or over 7 s; the end is within 0.3 of the start; the path comes within three core radii of a well; it turns less than a quarter turn in total; it crosses itself more than once.
5. **Goal** at the end point. **Gates** spread along the path by distance travelled between 15% and 85%, at least 15% apart, each centred on the path and square to the direction of flight. A gate is rejected if a post leaves the field or the gate comes within 0.1 of a well or of the goal.
6. **Asteroids** (from level 12): radius 0.03 to 0.05, at least 0.07 clear of the solution path, the start, the goal, every gate and every solution well, and not overlapping each other.
7. **Prove it.** The solution wells must win. No wells must not win. Removing any one solution well must not win. Then `Forgiveness` (doc 03) must be at least `MinForgiveness`.
8. `Par` is the number of wells used. The player's budget is `Par + 2` wells in total; up to `repulsors in the solution + 1` of them may be repulsors from level 35, none before.

A rejected attempt carries on with the same random generator. After 300 failed attempts the generator drops one gate and tries another 300, down to one gate. If even that fails it runs a last-resort round with one gate and the quality checks of step 4 and the forgiveness floor eased (the proofs of step 7 still apply). That round has never been reached in testing.

**Levels 1 to 3 are fixed by rule** (`DifficultyCurve.PlanForLevel`): level 1 is one well, one gate and a wide goal (radius 0.09); level 2 is one well and two gates; level 3 is two wells and two gates.

## Code

- `Core/LevelGenerator.cs`: `Generate(seed, level)`; one `Workspace` of buffers per call.
- `Core/SeededMath.cs`: ranges, disc points and unit vectors from `System.Random` with no trigonometry.
- `Core/DifficultyCurve.cs`: the per-level plan (doc 04).
- `Core/GameConfig.cs`: every number above, under "Level generation".
- `Editor/LevelBatchTester.cs`: menu `GravityWells ▸ Tuning ▸ Run Level Batch (10,000 levels)`, or `-executeMethod GravityWells.Editor.LevelBatchTester.RunFromCommandLine`. Writes `Logs/LevelBatchReport.txt`.

## Decisions and edge cases

- **Total turning without trigonometry:** each step turns by a tiny angle, and for a tiny angle the cross product of the two unit directions equals the angle, so the sum of their absolute values is the turning in radians. Left and right turns both count.
- **Gate spacing is shared out, not rejected:** the spare room beyond the minimum spacing is split at random between the gates. Drawing each gate freely and rejecting clashes would rarely fit five gates into 70% of a path.
- **Self-crossings** are counted on the path sampled every 8 steps, which is far finer than any loop the ship can fly.
- **Budgets:** the spec lists an attractor budget and a repulsor budget. Here the total is `WellBudget = Par + 2`, `RepulsorBudget` caps the repulsors inside it, and `AttractorBudget` equals the total, because an attractor may stand in for any well.
- **`SolutionWells` is internal.** Only the generator, `Forgiveness`, the tests and the Editor tools can read it; `CoreRulesTests` fails if the solver, the hint or any game code names it.
- `LevelDefinition` also carries `Attempts`, `GatesDropped`, `Forgiveness` and `SolutionFlightSeconds`. They exist for the batch tool; play never depends on them.
- A level number below 1 is treated as 1.
- Easier solutions than the generator's are allowed (fewer wells is the three-star route). The generator only proves that zero wells cannot win and that its own solution has no spare well.

## Tests

`GeneratorTests` on 1,000 seeds across levels 1 to 300: the solution wins; no wells do not; each one-well-removed layout does not; everything is inside the field; well, gate and asteroid clearances hold; `Par` matches the plan; repulsors appear only from level 35, never as the first well; the gate-drop fallback happens on fewer than 1% of levels and `Generate` never throws; forgiveness and flight time are inside their limits; the time budget. `DeterminismTests`: the same `(seed, level)` twice gives bit-identical data.

## Not verified

- Whether the levels are fun, fair or readable. That needs a playable board (Phase 2) and a person.
- That a phone generates the same level from the same seed (Phase 2 device check).
