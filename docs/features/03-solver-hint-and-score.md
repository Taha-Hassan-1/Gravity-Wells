# 03 — Path score, solver, hint and forgiveness

Four small pieces that all steer by one number.

## `PathScore` — how far a flight is from winning

`PathScore.Evaluate(level, trajectory)`: 0 means won, lower is better.

`1000 × unlit gates + for each unlit gate the smallest distance from the path to the gate's midpoint + the smallest distance from the path to the goal + 500 if the flight ended in a crash.`

- The thousand per gate means lighting one more gate always beats creeping closer to two.
- Once every gate is lit, only the path **after the last gate lit** counts toward the goal distance. The ship flying over the goal earlier did not count in the simulation, so it must not look close here.
- It never reads the stored solution. The solver, the hint and the "getting warmer" feedback all use it.

## `Solver` — a deterministic stand-in for a player

`Solver.Solve(level, seed, maxEvaluations, wells, scratch)` is a seeded hill climb on `PathScore`. It starts with one well at a seeded legal position, moves one well at a time (the reach shrinks from 0.3 to 0.01 the longer nothing helps, and resets after a gain), and after 150 tries without progress adds a well if the budget allows or starts over. It lives in Core on purpose so the batch tool and later the soak test can run it with no scene. It never reads `SolutionWells`.

Its job is to show levels are findable, not merely solvable.

## `HintPlanner` — the best single move from where the player is

`HintPlanner.TryPlan(level, wells, wellCount, rng, scratch, out wellIndex, out position)`: for every placed well, 120 positions in a golden-angle spiral around it plus a coarse 8×10 jittered grid over the field; for one new attractor (if the budget allows) a 10×20 grid. It returns the single move that most improves `PathScore`, only if strictly better than now. `wellIndex == wellCount` means "place a new well". The array is left as it was found; the scratch buffer is overwritten.

- **It never moves a well to the generator's solution.** The player may be closing in on a different valid layout.
- Zero allocations. The spiral is built once, by rotating a unit vector with a constant (no trigonometry).

## `Forgiveness` — the difficulty number

`Forgiveness.Measure(level, samples, seed)`: nudge every solution well by an independent offset inside a disc of radius `ForgivenessRadius` (0.02), fly again, and return the fraction of samples that still win. High is generous; low is fiddly. The generator rejects a level below `MinForgiveness` (0.2).

## Code

`Core/PathScore.cs`, `Solver.cs`, `HintPlanner.cs`, `Forgiveness.cs`, `WellRules.cs`.

## Tests

- `DifficultyCurveTests`: the score is 0 only for a win; each unlit gate costs 1000 plus its distance; one more gate lit beats closer-to-two; a crash costs 500; flying over the goal before the last gate does not count.
- `SolverTests`: on levels 1–100 it wins at least 95% within 20,000 flights, and the layout it leaves really wins inside the budget and the placement rules; the same seed solves the same way; it respects the flight limit; it does not win on its first flight (so it is not peeking).
- `HintTests`: from 1,000 random non-winning layouts the planned move never makes the score worse (and a move is found for nearly all of them); repeated hints from an empty field win most of levels 1–50 within 12; a winning layout gets no hint; with the budget full it only moves existing wells; it does not land on the stored solution; zero allocations.
- `CoreRulesTests`: only the generator, forgiveness, the level and the determinism check itself may name `SolutionWells`.

## Not verified

- Whether the hint *feels* helpful. It always improves the score, but a hint that fixes the nearest gate may not be the move a person would find instructive. That needs a playable build.
- The solver is a different kind of searcher from a person; "the solver wins 100%" does not mean a casual player will.
