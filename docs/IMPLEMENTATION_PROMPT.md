# Claude Code kickoff prompt — Gravity Wells

> Paste everything below into Claude Code as the first message, with `CLAUDE.md` already committed at the repo root and `Frag.Common` already copied in (ROADMAP M0).

> **Decisions inherited from Untangle and Chain Mixer, all binding here:**
> - Package `com.taha.gravitywells`, Unity 6.3 (6000.3.15f1), **min API 25**, target 36; a personal Play account and a **publisher-first** release route.
> - `Frag.Common` is real code: copy the list in `CLAUDE.md` (ad flow from Chain Mixer, the rest from Untangle) and rename the namespace; the AdMob adapter waits for the Services phase and the owner's approval.
> - **No audio files**: sound effects and music are synthesised with the copied `AudioService` and `MusicDsp`. The setting is called **Vibrations**.
> - The UI is built from the copied UI kit. Do not hand-build widgets again.
> - Ads: `AdFlow`, preloaded pool, waterfall over a registry, honest failure messages, stub never on device, test ids only.
> - Verify through the Unity CLI against the open Editor and look at offscreen screenshots of every screen you change.
> - Follow the `game-architecture` skill; where this file and `CLAUDE.md` differ, `CLAUDE.md` wins.

---

You are implementing a complete Android physics-puzzle game in Unity called **Gravity Wells**. Read `CLAUDE.md` at the repo root first. Its architectural rules and game rules are binding for every task.

Work through the phases below **one at a time, in order**. Do not start a phase until the previous phase's acceptance criteria pass. After each phase: run the EditMode tests, report what passed, and commit as `feat:` / `fix:` / `docs:` (written from a file, with the Co-Authored-By trailer) on a branch named `PhaseN-Name`. Tag `phase-N-start` before beginning each phase. If a spec is ambiguous or seems wrong, stop and ask.

---

## The game

A ship launches from a start point with a fixed velocity and no engine. The player places gravity wells on the field; each one bends the ship's path. A dotted line shows the exact path, updated live while a well is dragged. The level is solved when the path crosses every gate and then ends in the goal. The player taps Launch and the ship flies the line. Fewer wells means more stars.

Levels are generated from an integer seed, **backwards**: the generator places wells, flies the ship, puts the gates and the goal on the path that was flown, then removes the wells. So every level has a solution, and nothing ever has to search for one.

---

## Phase 1 — Flight core (no Unity scene)

Everything lives in `Assets/_Project/Scripts/Core/`, pure C# with only `Vector2` and `Mathf`. Write the tests first. In `FlightSimulator` and `LevelGenerator`, use only `+ - * /` and `Mathf.Sqrt` (see "Third rule" in `CLAUDE.md`).

### 1.1 Data types

```csharp
public enum WellKind { Attractor, Repulsor }
public readonly struct Well     { public readonly Vector2 Position; public readonly WellKind Kind; }
public readonly struct Gate     { public readonly Vector2 A, B; }          // the two posts
public readonly struct Asteroid { public readonly Vector2 Centre; public readonly float Radius; }

public sealed class LevelDefinition {
    public int Seed { get; }
    public int Level { get; }
    public Vector2 Start { get; }
    public Vector2 LaunchVelocity { get; }
    public Gate[] Gates { get; }
    public Vector2 Goal { get; }
    public float GoalRadius { get; }
    public Asteroid[] Asteroids { get; }
    public int Par { get; }                  // wells the generator used
    public int AttractorBudget { get; }
    public int RepulsorBudget { get; }
    internal Well[] SolutionWells { get; }   // generator and tests only; never read by Game/
}
```

### 1.2 `Trajectory.cs`

A reusable buffer, allocated once with capacity `MaxFlightSeconds * 120 + 1`.

```csharp
public enum FlightOutcome { Won, LeftField, HitWell, HitAsteroid, TimedOut }

public sealed class Trajectory {
    public Vector2[] Points { get; }         // Points[0] is the start
    public int Count { get; }
    public FlightOutcome Outcome { get; }
    public int GatesLitMask { get; }         // bit i = gate i
    public int GateLitStep(int gateIndex);   // -1 if never lit
    public int GatesLitCount { get; }
}
```

### 1.3 `FlightSimulator.cs`

```csharp
public static void Simulate(LevelDefinition level, Well[] wells, int wellCount, Trajectory into);
```

Exact semantics are under "Game rules" in `CLAUDE.md`. Per step, in this order: accumulate acceleration from every well, `v += a * dt`, compute `next = p + v * dt`, then test the step segment `p → next` against: every unlit gate (`GeometryUtils.SegmentsProperlyIntersect`), every asteroid and every well core (`SqrDistancePointToSegment`), the goal if all gates are lit, then the field bounds. Record `next`. The first terminal condition ends the flight.

Starting constants in `GameConfig` (guesses to be tuned in 1.9, not truth):

```csharp
FieldWidth = 1f; FieldHeight = 1.5f; FieldMargin = 0.05f;
StepsPerSecond = 120; MaxFlightSeconds = 8f;
LaunchSpeed = 0.35f;
WellStrength = 0.02f; Softening = 0.03f; WellCoreRadius = 0.035f;
MinWellDistanceFromStart = 0.12f;
```

### 1.4 `PathScore.cs`

```csharp
public static float Evaluate(LevelDefinition level, Trajectory t);   // 0 means won; lower is better
```

`Won` scores 0. Otherwise: `1000 * unlitGates + sum over unlit gates of the smallest distance from the path to the gate's midpoint + the smallest distance from the path to the goal`, plus a fixed penalty of 500 for `HitWell` or `HitAsteroid`. The large gate term makes lighting one more gate always beat getting closer to two. This one number drives the solver, the hint, and the "getting warmer" feedback.

### 1.5 `LevelGenerator.cs`

```csharp
public static LevelDefinition Generate(int seed, int level);
```

All randomness from one `System.Random(seed)`. One attempt:

1. **Start and launch.** Start on the lower third of the field, at least 0.12 from every edge. Launch direction: a seeded unit vector with a positive upward component, rejected if a straight flight would leave the field in under 1.2 seconds.
2. **Solution wells.** Place `DifficultyCurve.WellsForLevel(level)` wells inside the field inset by 0.1, at least `MinWellDistanceFromStart` from the start and at least 0.22 from each other. From level 35 each well after the first has a seeded 30% chance of being a repulsor.
3. **Fly it.** Simulate with no gates and no goal. Find the end step: the last step before the path leaves the field inset by 0.08, hits anything, or reaches the time cap.
4. **Reject** the attempt if any of these hold: flight shorter than 2.5 s or longer than 7 s; the path comes within `3 * WellCoreRadius` of a well; total turning along the path is less than a quarter turn (measure it by summing cross products of successive unit steps, no trig); the end point is within 0.3 of the start; the path crosses itself more than once.
5. **Goal** at the end point. **Gates** at `GatesForLevel(level)` points spread along the path by arc length between 15% and 85%, each at least 0.15 of path length apart, at least 0.1 from every well and from the goal. Each gate is centred on the path, perpendicular to the velocity there, `GateWidthForLevel(level)` long.
6. **Asteroids** (from level 12): `AsteroidsForLevel(level)` circles of radius 0.03–0.05 at seeded positions, each at least 0.07 from the solution path, the start, the goal, every gate and every solution well.
7. **Prove it.** With the gates, goal and asteroids in place: the solution wells must give `Won`; zero wells must not; removing any one solution well must not. Then `Forgiveness.Measure` (1.7) must be at or above `GameConfig.MinForgiveness`. Otherwise reject.
8. `Par = wells used`. Budget is `Par + 2` in total; the repulsor budget is the number of repulsors in the solution plus one from level 35, else zero.

On rejection, continue with the same RNG and try again, up to 300 attempts. If all fail, drop one gate and run another 300. A test asserts the gate-drop fallback happens on fewer than 1% of levels and that `Generate` never throws.

**Levels 1–3 are fixed by rule,** not by the curve: level 1 is one well, one gate and a wide goal; level 2 is one well and two gates; level 3 is two wells and two gates. They still come from the generator with those counts forced.

### 1.6 `Solver.cs` (in Core, deliberately)

A deterministic player for tests and tuning. It must **not** read `SolutionWells`.

Seeded hill climb on `PathScore`: start with one well at a seeded position; each iteration perturb one well (seeded offset, radius shrinking from 0.3 to 0.01), keep the change if the score improves; every 150 iterations without improvement, add a well if the budget allows, else restart from a new seeded layout. Stop at score 0 or after `maxEvaluations`.

Its purpose is to prove levels are findable, not just solvable: a level whose only solution is a needle in a haystack is a bad level even though it is valid.

### 1.7 `Forgiveness.cs`

```csharp
public static float Measure(LevelDefinition level, int samples, int seed);   // 0..1
```

Move every solution well by an independent seeded offset inside a disc of radius `GameConfig.ForgivenessRadius` (start at 0.02), re-simulate, and return the fraction of `samples` that still win. This is the difficulty number: high means generous, low means fiddly. Gate width is tuned so the average falls in a target band per level range.

### 1.8 `HintPlanner.cs`

```csharp
public static bool TryPlan(LevelDefinition level, Well[] wells, int wellCount, System.Random rng, Trajectory scratch, out int wellIndex, out Vector2 position);
```

From the player's current layout: for each existing well, and for one new well if the budget allows, sample 200 candidate positions (a spiral around its current position, plus a coarse grid over the field) and evaluate `PathScore`. Return the single move with the best score, only if it is strictly better than the current score. It never reads `SolutionWells`. Zero allocations; `scratch` is the caller's spare buffer.

### 1.9 `DifficultyCurve.cs` and `Scoring.cs`

```csharp
public static int   WellsForLevel(int level)      => Mathf.Clamp(1 + Mathf.FloorToInt(Mathf.Sqrt(level) * 0.35f), 1, 4);
public static int   GatesForLevel(int level)      => Mathf.Clamp(1 + level / 8, 1, 5);
public static float GateWidthForLevel(int level)  => Mathf.Lerp(0.16f, 0.07f, Mathf.Clamp01((level - 1) / 150f));
public static float GoalRadiusForLevel(int level) => Mathf.Lerp(0.07f, 0.04f, Mathf.Clamp01((level - 1) / 150f));
public static int   AsteroidsForLevel(int level)  => level < 12 ? 0 : Mathf.Clamp(1 + (level - 12) / 15, 1, 4);
public static int   SeedForLevel(int level)       => unchecked((int)((uint)level * 2654435761u ^ 0x85EBCA6Bu));
```

Four wells is the cap: past that a level is more of the same but slower. Difficulty beyond it comes from gate width, gate count and asteroids.

**These constants and the physics constants in 1.3 are starting guesses.** Use the batch tool to produce, per band of 10 levels: generator attempts, flight time, forgiveness, solver success rate and solver evaluations. Propose values from that data, report them, and **stop and ask before locking them**.

`Scoring`: stars as in `CLAUDE.md` (3 for `Par` wells or fewer, 2 for `Par + 1`, 1 for `Par + 2` or any win that used Extra Well). A win with fewer than `Par` wells also sets an `UnderPar` flag.

### Phase 1 acceptance criteria (EditMode tests)

- With no wells the ship moves in a straight line at constant speed, to within float error, for the whole flight.
- One attractor beside the path bends the path toward it; one repulsor bends it away; mirroring the whole setup left to right mirrors the trajectory.
- Energy check: for a single well and a path that stays more than 0.1 from it, kinetic plus softened potential energy drifts by less than 2% over the flight.
- A gate is lit when crossed from either side, not lit by a path that touches only a post's extension, and a ship moving at 10× launch speed still lights it and still triggers the goal.
- The goal does nothing until every gate is lit.
- Each of the five outcomes is produced by a hand-built case.
- Determinism: generating the same `(seed, level)` twice gives bit-identical data; simulating the same layout twice gives a bit-identical buffer. Expose a `Trajectory.Hash()` for this; Phase 2 reuses it on a device.
- For 1,000 seeds across levels 1–300: the solution wells win; zero wells do not; each one-well-removed layout does not; every gate, the goal and every asteroid are inside the field; asteroid and well clearances hold; `Par` equals `WellsForLevel` (or the forced value for levels 1–3).
- No source file under `Core/` that contains `FlightSimulator` or `LevelGenerator` calls `Mathf.Sin`, `Cos`, `Atan2`, `Pow` or `Exp` (a test that scans the source text).
- **Solver:** on levels 1–100 it wins ≥ 95% within 20,000 evaluations without reading the solution.
- **Hint:** from 1,000 random non-winning layouts, the planned move never makes `PathScore` worse, and repeatedly applying hints from an empty field wins ≥ 80% of levels 1–50 within 12 hints.
- Performance: one 8-second, 4-well, 4-asteroid simulation under 0.25 ms; zero allocations in `Simulate` and `TryPlan` (assert with `GC.GetAllocatedBytesForCurrentThread`); `Generate` under 30 ms on average.

Also build `Assets/_Project/Scripts/Editor/LevelBatchTester.cs`: a menu item and a `RunFromCommandLine` entry that generates 10,000 levels and logs the table described in 1.9.

**Do not write a single line of scene code until all of this passes.**

---

## Phase 2 — Playable board

One scene `Bootstrap.unity`, one GameObject with `Bootstrap.cs`, everything else built at runtime. The rules of play sit in `CampaignMode : IGameMode`, not in `GameManager`.

### Rendering

- **Path:** one procedural mesh of dots (a quad every N buffer points) with an unlit vertex-colour material. Rewrite vertices in place when the trajectory changes; cached arrays, no allocation. The part of the path after the outcome point is not drawn. Colour the path by state: neutral, brighter once all gates are lit, warning where it ends in a crash.
- **Wells:** pooled sprites from a generated circle texture, with a core disc of exactly `WellCoreRadius` so the crash zone is honest. Repulsors get a different outline (a broken ring), not just a different colour.
- **Gates:** two posts and a thin bar. Unlit is hollow, lit is filled.
- **Ship:** a small procedural triangle on the start pad, pointing along the launch velocity. **Goal:** a ring that fills when every gate is lit.
- **Background:** a static, seeded star mesh. One draw call.

### Input (`WellDragController.cs`)

- Single pointer only. A tray at the bottom shows the wells left of each kind.
- Drag from the tray to place a well. Drag a placed well to move it (nearest well within a 48dp touch radius, minimum 0.5 world units). Drag a well back onto the tray, or drop it on an illegal spot, to remove it.
- While dragging: clamp to the field, keep `MinWellDistanceFromStart`, and call `FlightSimulator.Simulate` every frame the position changed. That is the whole "preview"; there is no cheaper approximate path.
- Undo pops the last place, move or remove. Restart clears all wells with no confirmation.
- **Launch** is always enabled. It plays the ship along the current buffer at 1× (a tap during flight speeds it to 3×). `Won` leads to the win sequence; anything else returns to planning with the wells exactly where they were.
- Input is ignored during a flight and during the win sequence.

### Camera, HUD

Orthographic; fit the 1 × 1.5 field to the screen with a margin, reserving safe-area room for the HUD above and the tray and Launch button below. Correct from 4:3 to 21:9 and on tablets. HUD: level number, gates lit out of total, wells used against par, undo, restart, hint, pause.

### Device determinism check

Add an Editor menu item and a hidden debug action in a development build that simulate the solution of levels 1–100 and print one combined hash. Run both and compare. Record the result in `docs/TEST_CASES.md`. **If they differ, stop and ask** (see `CLAUDE.md`).

### Phase 2 acceptance criteria

- A level loads and can be solved and failed on a device.
- The ship's flight follows the previewed line exactly: a test plays a flight through the real `GameManager` and asserts every ship position came from the buffer.
- 60fps while dragging a well on a 4-well, 5-gate, 4-asteroid level on a mid-range phone; zero GC allocations per frame during a drag (report the Profiler number).
- Framed correctly on 4:3, 16:9, 19.5:9 and 21:9 (offscreen renders).
- The determinism check has a recorded result.

---

## Phase 3 — Feel

Do not rush this phase. The ad clip comes from it.

- **The line is the toy.** Dots flow along it in the direction of travel. It must feel good to wave a well around before anything is solved.
- **Gates light in the preview**, live, while dragging: a pop, a note that rises with each gate lit, a light vibration. Un-lighting plays nothing. When the last gate lights and the path reaches the goal, the goal fills and a soft chord holds: the "you have it" moment happens under the finger. Let the drag finish; never auto-launch.
- **Wells:** concentric rings that pulse inward (outward for repulsors); a short settle when dropped; the path eases to its new shape over 0.05s rather than snapping on drop.
- **Launch:** a short charge on the pad, then the flight with a trail. Each gate flashes as the ship passes. A brief slow-down (about 0.3s at half speed) through the final approach to the goal. Then a burst at the goal, the gates ping in sequence, and the result card.
- **Failing explains itself:** a mark where the ship died or left, the missed gates blink twice, and the hint button pulses once after the third failed launch on a level.
- **Audio (all synthesised, no files):** well pick-up and drop, gate notes on a scale, launch, flight whoosh that follows the ship's speed, win and fail stingers, and one music track with its own identity (slow, spacious, a clear motif; not a generic pad). Normalise to the shared loudness; test that it is audible, never clips and loops without a click. Export a WAV and listen before moving on.
- **Vibrations** through `HapticService`: light on gate lit, medium on launch, strong on win.

Acceptance: dragging a well on an unsolved level is pleasant on its own, and you want to watch the flight again after solving. Record a clean 15-second clip from the real build.

---

## Phase 4 — Meta and screens

Reuse the `Frag.Common` save, themes, daily, economy, quests and UI kit. Game-specific:

- **Save fields** (additive, defaults, `Version = 1`): `HighestLevelUnlocked`, `StarsByLevel`, `UnderParCount`, `TotalLaunches`, `TotalWellsPlaced`, plus the standard fields.
- **Level Complete:** stars, wells used against par, launches taken, an "Under par" badge when earned. Next / Replay / Menu.
- **Themes:** 8 palettes unlocked at levels 1, 10, 25, 50, 80, 120, 175, 250, plus a colour-safe palette from level 1. Test every palette for ≥ 3:1 contrast between path and background, lit and unlit gate, attractor and repulsor (`LineColors.ContrastRatio`).
- **Daily Challenge** as `DailyMode`: seed `int.Parse(DateTime.UtcNow.ToString("yyyyMMdd"))`, generated at the difficulty of level 40. Streak, one completion a day.
- **Asteroids** from level 12 and **repulsors** from level 35, each introduced by a level with that one new thing and nothing else hard.
- **Hint** (30 coins, or a rewarded ad when short): shows a ghost well at `HintPlanner`'s position with an arrow from the well to move; the player makes the move. Charge only when a hint was actually found.
- **Extra Well** (rewarded ad): +1 attractor for this level, offered after 5 failed launches. A win using it earns 1 star.
- **Coins and quests:** first-solve payouts, a daily double, three daily quests (examples: solve 3 levels, earn 5 stars, solve one under par).
- **Level 1 demo:** wordless. A ghost hand drags the well from the tray toward a marked spot and the line bends through the gate; it repeats until the player touches the screen.
- **Screens:** Main Menu, HUD, Level Complete, Settings (sound, music, vibrations, restore purchase, privacy policy), Stats, Themes.

### Phase 4 acceptance

- Progress survives a force-quit; a corrupted save is handled without a crash; Back works everywhere.
- The daily gives the same level for the same UTC date.
- A `GameFlowSmokeTests` equivalent boots `Bootstrap.unity`, plays levels 1–5 and the daily with the solver, and opens every screen, with the save redirected by `GRAVITYWELLS_DATA_DIR`.
- Every screen has been looked at in an offscreen 1080x1920 render.

---

## Phase 5 — Services

Do not start until Phases 1–4 are done. Follow `ADS_IMPLEMENTATION.md` section 8. Ask the owner before adding the Google Mobile Ads package. Use `AdFlow`; the game supplies only the reward callbacks.

- Interstitial only at the "Next level" tap, when `levelsSinceLastAd >= 3` AND `secondsSinceLastAd >= 45` AND `currentLevel >= 5` AND `!removeAdsPurchased`.
- Never during a drag or a flight, never within 3 seconds of a level starting, never before level 3 in the first session.
- Rewarded: Hint, Extra Well, double coins on the result card.
- **Analytics** (through `IAnalyticsService`): `level_start`, `level_complete` (level, wells, par, launches, seconds, stars), `level_abandon` (level, launches, seconds, best_gates_lit), `launch` (level, wells, gates_lit, outcome), `hint_used`, `extra_well_used`, `ad_shown`, `ad_failed`, `theme_unlocked`, `daily_started`, `daily_completed`, `iap_purchased`.

---

## Phase 6 — Release build

Reuse `BuildScript` (AAB, an APK entry for `adb install`, version from `BuildVersion.json`). Signing passwords come only from the owner's environment variables. Add a **soak test**: an Editor menu item that solves 1,000 levels with the solver through the real `GameManager` with rendering on. Verify: AAB under 40 MB, cold start under 2 seconds, 100 consecutive levels with no crash.

---

## How to work

- One phase at a time. No scaffolding of later phases.
- Core tests before Core implementation.
- Paste the actual test output at the end of each phase. Never report completion from reading code alone.
- At the end of each phase, report: what you built, what the tests showed, deviations from this spec and why, and any decision you need from the human.
- Write a short feature doc in `docs/features/` and a row in `docs/TEST_CASES.md` in the same change as each feature, and say honestly what was only checked in the Editor and not on a device.
- If a rule or constant looks wrong, say so rather than working around it silently.
- Ask the owner before adding any dependency, downloading a file, or locking tuned constants.

Start with Phase 1.1.
