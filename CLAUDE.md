# CLAUDE.md — Gravity Wells

Persistent context for Claude Code. Read this before every task.

## What this project is

A mobile physics-puzzle game for Android. A ship launches from a start point with a fixed velocity. The player places a limited number of gravity wells on the field; each well bends the ship's path. A live preview shows the exact path while a well is being dragged. The level is solved when the path crosses every gate and then reaches the goal. Levels are generated procedurally from a seed. Target: production-ready in 4 working weeks, near-zero art assets.

**Package ID:** `com.taha.gravitywells` (company `Taha`, a personal Play account)
**Engine:** Unity 6.3 (6000.3.15f1), Built-In Render Pipeline, 2D template
**Release route:** the owner will approach a **publisher** rather than self-publish, so Play Console, listing, marketing and the ad / billing / analytics SDKs may be the publisher's. Everything external stays behind an interface (see "External services").
**Branches and commits:** one branch per phase named `PhaseN-Name`; commits `feat:` / `fix:` / `docs:` written from a file (`git commit -F`) with the Co-Authored-By trailer; tag `phase-N-start` before each phase. Never commit the Unity churn in `ProjectSettings/` (connect, resolver and project settings Unity rewrites itself).
**Language:** C# 9, .NET Standard 2.1
**Sibling projects:** Untangle (`D:\Projects\Untangle`, finished) and Chain Mixer (`D:\Projects\ChainMixer`, late in development). Their service layer, UI kit and tooling are real code. Reuse it (see `Frag.Common` below). Do not re-solve ads, saves, audio, vibrations, the UI kit or build scripts.

## General engineering practices

For design, SOLID, patterns, SDK wrapping, testing and the commit/doc workflow, follow the user-level `game-architecture` skill (`~/.claude/skills/game-architecture/SKILL.md` and its `references/`). The rules in this file win where they differ.

## The single most important architectural rule

**`Assets/_Project/Scripts/Core/` must not reference `UnityEngine` at all**, except `Vector2` and `Mathf`. No `MonoBehaviour`, `GameObject`, `Debug.Log`, coroutines, `Resources`, or physics.

The whole flight lives in Core. `FlightSimulator.Simulate(level, wells, trajectory)` integrates the ship from launch to an outcome with a **fixed timestep** (`dt = 1/120`) and fills a preallocated `Trajectory` buffer. It is a pure function of the level and the well positions.

Reasons:
1. It is testable headlessly in EditMode.
2. **The preview and the flight are the same data.** The preview draws the trajectory buffer; pressing Launch plays the ship along that same buffer. Nothing is re-simulated at flight time, so the ship can never leave the line the player was shown. A preview that lies even once destroys trust in the game.
3. A solver and a hint can evaluate thousands of candidate layouts per second.

## Second rule: build the scene in code

Exactly one scene, `Bootstrap.unity`, with exactly one GameObject carrying `Bootstrap.cs`. Camera, canvas, field, wells, gates, ship, UI and services are all constructed at runtime. No Inspector wiring, no prefabs, no drag-and-drop references.

Designer-tunable values live in `Core/GameConfig.cs` as named constants.

## Third rule: arithmetic only in the simulation and the generator

`FlightSimulator` and `LevelGenerator` may use only `+ - * /` and `Mathf.Sqrt`. **No `Sin`, `Cos`, `Atan2`, `Pow` or `Exp`** in either. Square root is correctly rounded on every platform; the others are not, and a level must come out the same on every phone because the daily puzzle and share codes depend on it. Pick a random direction by rejection-sampling a point in the unit disc and normalising it, not from an angle. Trigonometry is fine in the view layer.

Known risk, to be measured not assumed: IL2CPP on ARM64 may fuse a multiply and an add into one instruction, which can change the last bit of a float compared with the Editor. Phase 2 has a device check for this (a trajectory hash over 100 seeds, Editor vs phone). If the hashes differ, stop and ask: the fallback is fixed-point maths in the integrator.

## Directory layout

```
Assets/_Project/
  Scripts/
    Core/                    # pure C#. Vector2/Mathf only.
      GameConfig.cs
      GeometryUtils.cs       # copied from Untangle (segment intersection, point-segment distance)
      SeededMath.cs          # unit vectors and ranges from System.Random, no trig
      Well.cs                # struct: Position, Kind (Attractor, Repulsor)
      Gate.cs                # struct: two end points
      Asteroid.cs            # struct: centre, radius
      LevelDefinition.cs
      Trajectory.cs          # preallocated point buffer + outcome + gate-hit steps
      FlightSimulator.cs     # the fixed-step integrator and all collision tests
      PathScore.cs           # how far a trajectory is from winning (lower is better)
      LevelGenerator.cs      # generates backwards from a solved flight
      Solver.cs              # hill-climb player, used by tests and tuning
      HintPlanner.cs         # best single improvement from the player's current wells
      Forgiveness.cs         # difficulty metric: how much error a level tolerates
      DifficultyCurve.cs
      Scoring.cs
    Game/
      Bootstrap.cs
      GameManager.cs
      FieldView.cs           # background stars, start pad, goal
      PathView.cs            # one mesh for the preview line
      WellView.cs            # pooled
      GateView.cs
      ShipView.cs
      WellDragController.cs
      EffectsController.cs
      CameraFitter.cs
      Modes/                 # IGameMode seam: CampaignMode, DailyMode
    Meta/                    # from Frag.Common
    UI/                      # from Frag.Common UI kit + this game's screens
    Services/                # from Frag.Common
  Editor/
    LevelBatchTester.cs
    SoakTest.cs
    LevelDebugWindow.cs
  Tests/EditMode/
    FlightSimulatorTests.cs
    DeterminismTests.cs
    GeneratorTests.cs
    SolverTests.cs
    HintTests.cs
    DifficultyCurveTests.cs
  Scenes/Bootstrap.unity
```

Assembly definitions (same layout as Untangle and Chain Mixer): `GravityWells.Core` at `Scripts/Core/` (no references); `GravityWells.Game` at the `Scripts/` root, covering `Game/`, `Meta/`, `UI/` and `Services/` because nested asmdefs win (references Core and Unity.TextMeshPro); `GravityWells.Editor` (Editor platform only); `GravityWells.Tests.EditMode` (Core, Game, Unity.TextMeshPro, TestRunner, nunit). Do not add a fifth.

## Game rules (binding spec for `FlightSimulator`)

- **Field:** normalised, 1 wide by 1.5 tall (`GameConfig.FieldWidth`, `FieldHeight`), portrait.
- **Ship:** a point. Starts at `level.Start` with velocity `level.LaunchVelocity`. It has no thrust and no drag.
- **Step:** semi-implicit Euler, `dt = 1/120`: sum the acceleration from every well, `v += a * dt`, then `p += v * dt`.
- **Attractor:** with `d = well - p` and `r2 = d.sqrMagnitude + Softening²`, the acceleration is `d * (WellStrength / (r2 * sqrt(r2)))`. A **repulsor** is the same with the sign flipped. All wells of a kind have the same strength.
- **Gates:** a gate is a line segment. It is **lit** when the segment the ship travelled in one step properly intersects it, from either side. Gates can be lit in any order and stay lit.
- **Goal:** a circle. It only counts once **every** gate is lit; before that the ship flies over it. The test is swept (distance from the goal centre to the step segment), so a fast ship cannot skip it.
- **Flight ends** with the first of: `Won` (goal reached, all gates lit), `HitWell` (within `WellCoreRadius` of any well, either kind), `HitAsteroid`, `LeftField` (outside the field by more than `FieldMargin`), `TimedOut` (`MaxFlightSeconds`).
- **Wells:** the player has a budget of `par + 2` wells, where `par` is the number the generator used. Wells may be placed anywhere inside the field except within `MinWellDistanceFromStart` of the start. Wells may be moved or removed freely before launch; there is no cost to trying.
- **Stars:** 3 if the winning layout uses `par` wells or fewer, 2 for `par + 1`, 1 for `par + 2`. A level won with the rewarded Extra Well earns 1 star.
- A failed launch costs nothing but a counted attempt: the wells stay where they are and the player adjusts.

## Levels are solvable by construction

`LevelGenerator` works backwards. It places the solution wells first, flies the ship through them, and only then drops the gates and the goal **onto the path that was actually flown**. Then it throws the wells away. The solution is never searched for and never stored in the save; it is regenerated with the level. The generator must also prove the wells were needed: the flight with no wells must not win, and the flight with any one solution well removed must not win.

The hint never moves a well to the generator's solution. The player may be converging on a different valid layout, so `HintPlanner` samples candidate positions for one well and picks the one that most improves `PathScore` from where the player is now.

## Coding conventions

- `PascalCase` for types and methods, `_camelCase` for private fields, `camelCase` for locals and parameters.
- No `public` fields. Properties or `[SerializeField] private`.
- No singletons except one `ServiceLocator` created by `Bootstrap`. Dependencies via constructor or `Init(...)`.
- No `FindObjectOfType`, `GameObject.Find`, `SendMessage`.
- No allocations in `Update`, `FlightSimulator.Simulate`, `HintPlanner`, or the drag hot path. The `Trajectory` buffer is allocated once and reused.
- `System.Random` seeded explicitly, never `UnityEngine.Random`, anywhere in Core. Write `System.Random` in full in any file that also has `using UnityEngine;` (bare `Random` is ambiguous).
- Never use Unity physics or `Time.deltaTime` inside Core.
- Built-In render pipeline: `Sprites/Default` or an unlit vertex-colour material. No URP or HDRP types. A Linear colour space means mesh vertex colours are not converted for you; convert them yourself.
- Legacy Input Manager. Do not add the Input System package.
- Comment *why*, not *what*.

## Verification

Preferred: drive the already-open Editor through the Unity CLI (`com.unity.pipeline`):

```bash
unity command menu --path "Assets/Refresh" --project-path "D:/Projects/GravityWells"
unity command run_tests --mode EditMode --timeout 150 --project-path "D:/Projects/GravityWells" --no-banner --result-only
```

Refresh first after editing files on disk, and confirm compilation with `EditorUtility.scriptCompilationFailed`. Put C# snippets in a file and use `eval_file`. Render screenshots **offscreen** (main camera into a 1080x1920 RenderTexture, then the canvas through a temporary ScreenSpaceCamera whose culling mask is the canvas layer) and look at every screen you change. Set the `GRAVITYWELLS_DATA_DIR` environment variable (Editor only, read by `Bootstrap.DataDirectory`, the same idea as Untangle's `UNTANGLE_DATA_DIR`) for scripted play sessions so the real save is not touched. An unfocused Editor stops advancing frames: pause, set `Time.captureDeltaTime` and call `EditorApplication.Step()`.

Fallback, with the Editor closed (do **not** add `-quit`, it can quit before the tests start):

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.15f1/Editor/Unity.exe" -batchmode -nographics -projectPath "D:/Projects/GravityWells" -runTests -testPlatform EditMode -testResults ./TestResults.xml -logFile <path>
```

Never report a phase complete without running the tests and reading the result.

## Build settings (do not change without asking)

Same as the sibling games: IL2CPP, ARM64 only, **min API 25** (Unity 6.3 clamps anything lower), target API 36, AAB, Medium stripping, OpenGLES3 + Vulkan, **portrait only**, auto-rotate off. The build is produced by the copied `BuildScript`; signing keys come only from the owner's `GRAVITYWELLS_KEYSTORE_*` environment variables, never from the repo or chat.

## Shared code: `Frag.Common`

Copy at M0, renaming the namespace to `GravityWells.*`, and do not rewrite. **Take each file from whichever sibling has the newer version**: Chain Mixer for the ad flow, Untangle for the modes seam and the economy.

| Copy at M0 | From |
|---|---|
| Data: `SaveService`, `SaveData` (shared fields only, plus this game's), `ThemeService` + `ThemeController`, `DailyChallengeService`, `EconomyService`, `QuestService`, `LineColors` (contrast maths) | Untangle |
| Ads and money: `AdPolicy`, `AdPoolPlanner`, `AdProviderOrder`, `IAdService`, `StubAdService`, `AdProviderRegistry`, `WaterfallAdService`, `IConsentService` + stub, `IPurchaseService` + stub, `IAnalyticsService` + log implementation, `MonetizationController`, `AdMessages`, `AdOverlay`, **`AdFlow`** + `IAdPresenter`, and their tests | Chain Mixer (it pulled the rewarded flow, interstitial gate and watchdog out of `GameManager` into `AdFlow`) |
| Added at the Services phase, after the owner approves the Google Mobile Ads package: `AdMobAdService`, `AdMobConsentService`, `AdNetworkProbe`, `AdMobConfigTests` | Untangle |
| Audio and feel: `AudioService`, `MusicDsp`, `MusicLibrary`, `HapticService`, `CountUp`, `ShareService` | Untangle |
| UI kit: `UIFactory`, `UIStyle`, `UIIcons`, `UIFonts`, `PressFeedback`, `UIScreen` / `UIRoot`, the importers and `UIFontBuilder`, `Resources/UI/Icons/`, `Resources/UI/Fonts/` with their licence texts | Untangle |
| Modes seam: `IGameMode`, `GameModeRegistry`, `GameModeCatalog`, `PuzzleCodes` | Untangle |
| Tooling: `BuildScript`, `BuildVersion.json`, `docs/BUILD.md`, the `GameFlowSmokeTests` pattern | Untangle |

The ads guide is `ADS_IMPLEMENTATION.md` in the planning folder; follow its section 8.

## External services (decided on Untangle; apply here)

- Ads, billing, consent, analytics, sharing, audio and vibration all sit behind interfaces; concrete adapters are registered in `Bootstrap` only.
- **Ads:** one adapter per network behind `IAdService`; `WaterfallAdService` combines them by priority with fallback; the order comes from `GameConfig`. The stub is for the Editor and tests **only and must never be in the device order**. A preloaded pool of two interstitials and two rewarded ads. A mediation or publisher SDK is later one more adapter.
- Test ad ids stay in `GameConfig` until launch. Real ids, keystores and passwords are the owner's to supply and never go in the repo or chat.
- **No audio files.** Sound effects and music are synthesised in code, levelled to the same loudness, with a test that fails if a track is quiet or clips.
- The player-facing word is **Vibrations** (Off / On / Strong). Never call `Handheld.Vibrate()`.

## Scope: modes

v1.0 ships Campaign and Daily, both written as `IGameMode` classes from the start (Untangle's lesson: start with the seam, do not let `GameManager` absorb the rules). Other modes are post-launch; do not build them early.

## Things to never do in this repo

- Never add an art asset, texture, model or audio file, with one exception: the white UI icons and the two OFL fonts (Varela Round, Lilita One) copied from Untangle. Wells, gates, the ship, the stars and the path are procedural sprites and meshes tinted by code.
- Never add a third-party package without asking. TextMeshPro is pre-approved.
- Never re-simulate at flight time. The ship plays back the trajectory buffer that the preview drew.
- Never use trigonometry in `FlightSimulator` or `LevelGenerator`.
- Never store or snap to the generator's solution wells outside the generator and its tests.
- Never call an ad, billing or analytics SDK from gameplay code; go through the interface. Never integrate a real ad SDK before the Services phase, and never ship the stub as a device ad provider.
- Never put a secret (keystore, password, key) in the repo or in chat.
- Never encode meaning in colour alone: a lit gate changes shape (hollow to filled) as well as colour, and a repulsor has a different outline from an attractor.
- Never write level data to disk. Levels regenerate from a seed.
- Never commit `Library/`, `Temp/`, `Logs/`, `Builds/`, `*.csproj`.

## When to stop and ask

Ask the human before: adding a dependency, changing the save schema after v1.0 ships, locking or changing the difficulty-curve or physics constants, changing the rules in "Game rules" above, if the device determinism check fails, or if a phase's exit criteria look unachievable as specified. Otherwise proceed without checking in.
