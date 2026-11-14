# 06 — Placing wells, preview and launch

## What the player does

1. **Drag a well from the tray** onto the field. It counts against the budget from the moment it leaves the tray.
2. **Drag a placed well** to move it (a press within the touch radius picks the nearest one).
3. **Drag a well back onto the tray, or off the field,** to remove it. While the finger is there the well fades and the preview is drawn without it; letting go removes it.
4. **Undo** reverses the last place, move or remove. **Restart** clears every well, with no confirmation.
5. **Launch** is always available. The ship flies the line on screen. A tap during the flight speeds it to 3×.
6. A win leads to the result card. A miss returns to planning after a short beat, with every well exactly where it was; it costs only the launch count.
7. **Hint** shows the best single move from where the layout is now (a ghost ring, and a line from the well to move). The player makes the move.

## How it is built

| Piece | Job |
|---|---|
| `Core/WellLayout` | The wells, the budget, the undo history. A drag is **one** undoable edit however many times it moves; a new well dropped on the tray never happened; a placed well dropped there is a removal Undo restores at its old place in the list. Pure and tested without a scene. |
| `Core/WellRules` | Where a well may go: inside the field, outside the 0.12 circle around the start (`IsLegal`, `Clamp`). |
| `Game/WellDragController` | Finger to layout calls, with the legacy Input Manager only. One pointer: a drag belongs to the finger that started it and ends when that finger goes; a second finger neither takes it over nor starts anything. It re-flies the preview only when the finger actually moved. Allocates nothing. |
| `Game/GameManager` | The states (Planning, Flying, FailHold, WinHold, Result, Paused), the single place the simulator runs for the player (`Resimulate`), and Launch. |
| `Core/FlightPlayback` | Plays a finished buffer back in real time. It only reads the buffer. |

## The preview and the flight are the same data

`GameManager.Resimulate` flies the level with the current wells on **every** change to the layout (a drag step, Undo, Restart) and draws the buffer. `Launch` does not simulate: it starts `FlightPlayback` on that same buffer. `GameFlowSmokeTests.Level1_SolvedWithItsWells_FliesExactlyTheShownLine_AndWins` copies the buffer before launch, then checks on every frame that the ship view is where the playback says and that the playback is on the copied line's segment, and that the buffer's hash is unchanged at the end.

## Numbers (`GameConfig`)

Touch radius 48 dp (at least 0.07 world units); playback 1× and 3×; win beat 0.9 s; fail beat 0.6 s; drag lift 0 (see below).

## Decisions and deviations

- **The 0.5 world-unit minimum touch radius in the spec cannot apply here.** It comes from Untangle, whose board was several units wide; here the whole field is 1 unit wide, so 0.5 would let a press grab a well from across half the field. The minimum is 0.07 (7% of the field width, about 76 px on the reference canvas).
- **"Illegal spot" for a drop** is taken to mean off the field or over the tray. A drop inside the field but too near the start is not an error: the well is moved to the nearest legal position, as the drag already shows.
- **No drag lift yet.** A finger hides the well it holds, but the line the player watches is elsewhere. `GameConfig.DragLiftWorldUnits` is 0 and should be judged on a phone (the spec: spend the first day of this milestone on drag feel alone); if the finger gets in the way, raise it.
- The well tray shows two slots (attractor, and repulsor only on levels with a repulsor budget). An attractor's count is the total wells left, because an attractor can stand in for any well; the repulsor's is also capped by the level's repulsor budget.
- Hint costs nothing in this phase; the coin or ad price arrives with Phase 4.
- Input is ignored during a flight and during the win beat; Launch, Undo, Restart and Hint are disabled then.

## Tests

`WellLayoutTests` (15): budget, repulsor cap, legal positions, undo of place, move and remove in order, restart, one drag one edit, tray drop, cancel, one drag at a time, picking. `FlightPlaybackTests` (6). `CampaignModeTests` (12). `GameFlowSmokeTests` (4, Play Mode): level 1 solved and flown, a failed launch, undo/restart/pause/hint, and level 60 (five gates, asteroids) solved and flown.

## Not verified

- **Touch on a real screen.** The drag controller reads `Input`; no test can inject touches, so the press, drag, drop and tray-removal paths are covered through `WellLayout` (pure) but the controller itself has not been used by a finger.
- The feel of dragging (the first-day check) and whether the 48 dp radius picks the right well when two sit close together.
