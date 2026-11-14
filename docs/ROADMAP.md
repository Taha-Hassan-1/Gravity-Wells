# Gravity Wells — Development Pathway

**Studio:** Taha (personal Play account)
**Engine:** Unity 6.3 (6000.3.15f1), Built-In Render Pipeline, 2D template
**Package:** `com.taha.gravitywells`
**Platform:** Android first (Google Play)
**Release route [decided on Untangle]:** approach a **publisher**; keep every external SDK behind an interface so the publisher's SDKs drop in as adapters. Self-publishing stays the fallback (section 4).
**Target ship date:** 4 working weeks to a production-ready build
**Purpose:** the third game (backlog shortlist #5). Chosen because it fits the pipeline exactly as it stands: zero art, a pure deterministic simulation, and levels generated backwards.

**Status (2026-11-14):** planning docs written. Nothing built.

---

## 1. Product definition

**One-line pitch:** Bend the path. Place gravity to fly through every gate.

**Core loop (target 30–90s per level):**
1. A ship sits on a launch pad with a dotted line showing where it will fly: straight off the screen.
2. The player drags a gravity well onto the field. The line bends, live, as the well moves.
3. Each gate the line passes through lights up with a rising note.
4. When every gate is lit and the line ends in the goal, the player taps Launch and watches the ship fly it.
5. Fewer wells used means more stars.

**Why it should work:** one mechanic with a very high skill ceiling; the bending line is satisfying to drag even before it solves anything; "can you do it with one less well" is replay value that costs nothing to build. The clip reads muted: a line bends through three gates, a ship flies it.

**The risk to be honest about:** the hook is less instant than Chain Mixer's. Level 1 and the first clip must show a line bending under a finger within two seconds, or the game has a marketing problem. M0 tests exactly that.

**Non-goals for v1.0** (do not build):
- Multiplayer or online leaderboards
- Cloud save, iOS build
- Any illustrated art, story or characters
- IAP beyond a single "remove ads"
- Moving gates, moving wells, wells of different strength, thrust or any control during flight
- Modes beyond Campaign and Daily

---

## 2. Scope lock

| Area | v1.0 scope |
|---|---|
| Levels | Infinite, seeded procedural, generated backwards from a solved flight |
| Game modes | Campaign + Daily Challenge, both as `IGameMode` classes |
| Pieces | Attractor wells; static asteroids from level 12; repulsor wells from level 35 (first to be cut to an update if M4 runs late) |
| Meta | 8 unlockable themes plus a colour-safe one, star ratings by wells used, stats, coins and three daily quests (reused) |
| Monetization | Interstitial every 3 levels (45s cooldown); rewarded **Hint** (shows the best next move for one well) and **Extra Well**; $2.99 remove-ads IAP. AdMob behind `IAdService`; the publisher may substitute its SDK |
| Accessibility | Lit gates change shape as well as colour; repulsors have a distinct outline; colour-safe theme from level 1 |
| Screens | Main Menu → Game → Level Complete → Settings → Stats → Themes |
| Build size | Under 40 MB AAB |
| Perf target | 60fps on a 2019 mid-range device while dragging a well (one full trajectory re-simulated per frame) |

---

## 3. Milestones

### M0 — Validate and set up (Days 1–2)
**Validate before building:**
- [ ] Three 15-second mockup clips: a line bending as a well is dragged; a full solve and flight; a near miss that clips a gate post. Post from a throwaway account and compare with the Chain Mixer clips.
- [ ] Add this game to the publisher pitch alongside Untangle and Chain Mixer. Ask what SDKs they require.

**Setup:**
- [ ] Unity 6.3 project (2D Built-In), repo at `D:\Projects\GravityWells`, `.gitignore` (no LFS), platform switched to Android on day one, `com.unity.pipeline` added, Visual Scripting and Timeline removed, EDM "prompt before auto-resolution" off
- [ ] Copy `Frag.Common` per the table in `CLAUDE.md` (ad flow from Chain Mixer, the rest from Untangle), rename the namespace, set up the four asmdefs, the tests folder, `docs/features/` and `docs/TEST_CASES.md`
- [ ] **Decision for the owner:** the playbook says to extract `Frag.Common` into a local UPM package when a third game needs it. This is the third game. Recommendation: copy once more now and extract after Chain Mixer ships, because extracting while Chain Mixer is still changing means doing the work twice.

**Exit criteria:** an empty project builds an APK that installs on a device, with Frag.Common compiling inside it and its copied tests passing.

### M1 — Flight core, headless (Days 3–7)
The most important milestone.
- [ ] `FlightSimulator`: fixed-step integrator, gate crossing, swept goal test, all five outcomes, zero allocation
- [ ] `PathScore`: one number for how far a trajectory is from winning
- [ ] `LevelGenerator`: backwards generation with the "wells were needed" proof
- [ ] `Solver`: a hill-climb player that does not know the solution
- [ ] `HintPlanner` and `Forgiveness`
- [ ] `DifficultyCurve`, `Scoring`
- [ ] Batch tool: generate 10,000 levels across the curve; report retries, flight time, forgiveness, solver success and solver evaluations per level

**Exit criteria:** every generated level is won by its own solution wells and not won without them. The solver, starting from nothing, solves ≥ 95% of levels 1–100. Same seed gives a bit-identical level and trajectory. One 8-second trajectory simulates in under 0.25 ms with zero allocations. Difficulty constants proposed from the batch data and **agreed with the owner before they are locked**.

### M2 — Playable board (Days 8–11)
- [ ] Bootstrap scene, camera fit for 4:3 to 21:9
- [ ] Field, gates, goal, start pad, wells, and the preview line as one mesh
- [ ] Drag a well from the tray, move it, drag it back to remove it; Launch; flight playback; fail returns to planning with wells kept
- [ ] HUD: level, wells left, gates lit, undo, restart, hint
- [ ] **Device determinism check:** trajectory hash over 100 seeds, Editor vs phone

**Exit criteria:** a level is playable and winnable on a real phone, and the determinism check has a recorded result. Spend the first day of this milestone on drag feel alone.

### M3 — Feel and juice (Days 12–14)
- [ ] The line: animated dots that flow along it, gates that chime in rising pitch the moment the preview crosses them
- [ ] Wells: pulsing rings, a soft pull-in when dropped, a visible core
- [ ] Flight: trail, gate flashes, a brief slow-down through the last gate, burst at the goal
- [ ] Fail states that explain themselves: the ship marks where it died and the missed gates blink
- [ ] Vibrations, generated sound effects, one generated music track with its own identity
- [ ] Full palette pass and a procedural star background

**Exit criteria:** dragging a well around an unsolved level is pleasant by itself, and you want to watch the flight again after solving. Record the real ad clip here.

### M4 — Meta and screens (Days 15–18)
- [ ] Save, stars, stats, 8 themes plus colour-safe, coins and daily quests (all reused)
- [ ] Daily Challenge with the date seed and a streak
- [ ] Asteroids from level 12, repulsors from level 35
- [ ] Rewarded Hint and Extra Well (stub ads), paid in coins or an ad
- [ ] Wordless level 1 demo; illustrated how-to-play only if playtesters need it
- [ ] Menu, Settings, Stats, Themes, Level Complete

**Exit criteria:** progress survives a restart, every screen is reachable and exitable, the Back button never dead-ends.

### M5 — Services and release build (Days 19–20)
- [ ] Ask the owner, then add the Google Mobile Ads package and copy the AdMob adapter, consent and probe; test ids only
- [ ] Analytics: the standard set plus `launch` (level, wells, gates_lit, outcome), `hint_used`, `extra_well_used`
- [ ] `BuildScript`, soak test (1,000 levels solved by the solver through the real `GameManager` with rendering on)
- [ ] Icon, feature graphic, screenshots, privacy policy: the owner's or the publisher's

**Exit criteria:** a release AAB under 40 MB with ads live in test mode, cold start under 2 seconds, soak test clean.

### M6 — Test and launch
Same as the sibling games: internal test on 5 devices, closed test only if self-publishing, pre-launch report, vitals, localised listing, staged rollout.

---

## 4. Critical path warning

Unchanged from Untangle and Chain Mixer: on a personal Play account created after 13 Nov 2023 the 12-tester / 14-day closed test is a hard gate **if the game is self-published**. With a publisher it does not apply.

## 5. Risks

| Risk | Mitigation |
|---|---|
| The hook does not read in 3 seconds | M0 clips. Level 1 is one well and one gate, with a ghost-hand demo. The line reacts from the first pixel of drag. |
| Too hard for a casual player | Budget is always par + 2 wells, failing is free, the preview is exact, and the hint always improves the current layout. Gate width is tuned from the forgiveness metric, not by eye. |
| Chaotic paths: a 1-pixel move swings the line wildly | Softened gravity, a ban on solution paths that pass close to a well, a flight-time cap, and a generator check that rejects levels whose solution is too sensitive (`Forgiveness` below the floor). |
| Floats differ between the Editor and a phone, so the daily puzzle differs between devices | Arithmetic and square root only in Core; the device hash check in M2; fixed-point integrator as the fallback. Solvability is safe either way, because gates are placed on the path flown on that same device. |
| A finger hides the well being dragged | The thing to watch is the line, which is elsewhere. Test on a phone on day one of M2; add a drag offset only if needed. |
| Levels solvable in a trivial way the generator did not intend | Allowed: fewer wells is the 3-star route and a "Under par" badge. The generator only has to prove zero wells cannot win. |

## 6. Post-launch

- [ ] D1 / D7 against the benchmarks (35–45% / 12–18%) before any paid UA
- [ ] Modes, each one class: **Minimal** (budget equals par), **Blind** (the preview shows only the first second), Weekly, share codes via `PuzzleCodes`
- [ ] Second content update: binary wells or a one-use slingshot gate, only if retention data asks for more variety

## 7. Definition of done for v1.0

- 100 consecutive levels, no crashes (solver soak test and a manual run)
- Cold start under 2 seconds, fully offline
- Correct from 4.5" screens to tablets, 4:3 through 21:9; Back works on every screen
- Zero allocations per frame while dragging a well
- The ship always flies exactly the line that was shown
- Ads never appear mid-drag, during a flight, or within 3 seconds of a level start
- A new player solves level 1 with no text instruction
- No unlicensed audio; music audible and not clipping (tested); the setting says "Vibrations"
- The stub is not in the device ad order; ads are preloaded
- Every screen looked at in an offscreen portrait render; anything not checked on a device is listed honestly in `docs/TEST_CASES.md`
