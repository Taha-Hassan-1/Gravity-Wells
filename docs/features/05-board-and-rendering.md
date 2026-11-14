# 05 — Board, views and camera

Everything the player sees on the field is built in code at launch by `Bootstrap`: one scene, one GameObject, no prefabs, no art files. The views only draw; the rules are in Core.

## The views (`Game/`)

| View | What it draws |
|---|---|
| `FieldView` | A faint star sky (one mesh, one draw call, the same stars every launch), the playfield panel, the start pad (a ring the size of the no-well zone), the goal. The goal is a ring that **fills when the flight on screen has lit every gate and ends in it**. |
| `PathView` | The dotted line: one mesh of small discs, a dot every 6 steps (0.05 s) and a larger one at the end. Its vertex arrays are allocated once and rewritten in place, and only the dots in use are drawn (the index range is cut), so dragging allocates nothing. Neutral colour normally, green when the flight wins, and the last seven dots turn to the warning colour when it ends in a crash. Nothing is drawn after the outcome point because the buffer ends there. |
| `WellViews` | A pool of eight. Each is a faint pull ring, an outline and a core disc of **exactly `WellCoreRadius`**, so the crash zone drawn is the crash zone simulated. An attractor's outline is whole; a repulsor's is broken into three arcs, so they differ by shape and not only colour. The well in the finger is brighter and larger; one held where letting go would remove it fades. |
| `GateViews` | Two posts and a bar per gate. **Unlit is hollow** (ring posts, a thin faint bar); **lit is filled** (disc posts, a thicker bright bar). |
| `ShipView` | A small triangle. On the pad it points along the launch direction; in flight it follows `FlightPlayback`. |
| `HintView` | A broken-ring ghost where a well would help, and a line from the well that should move. Nothing is applied for the player. |
| `ShapeSprites` | The shared sprites (disc, ring, broken ring, triangle, square), drawn into textures once with 3×3 supersampling, each one world unit across at scale 1. |
| `ViewColors` | The roles the board draws in, taken from the active palette. |

`ThemeController.Changed` reaches every view through `GameManager.ApplyTheme`. Vertex colours go through `ViewColors.ToVertexColor`, which converts to linear only in a Linear colour space (this project is Gamma, so it is a no-op today).

## The camera (`CameraFit`, `CameraFitter`)

The orthographic camera frames the field plus a 0.04 margin, centred in the space left after a top reserve for the HUD (13% of the height), a bottom reserve for the tray and Launch button (21%) and the device safe area. `CameraFit` is pure and tested at 4:3, 16:9, 19.5:9 and 21:9 (upright): the whole field is always visible and always between the HUD and the tray. `CameraFitter` reapplies it when the screen or safe area changes.

## The HUD (`UI/GameHUD`)

Level and pause at the top, hint at the right, "Gates lit/total" and "Wells used (par N)" beneath; the well tray, Undo, Restart and Launch at the bottom. It implements `IWellTray` for the drag controller. Text and the counters only change when a value changes, so a drag allocates no strings. `PausePanel` and `LevelCompletePanel` are plain placeholders for Phase 4's screens.

## Code

`Game/FieldView.cs`, `PathView.cs`, `WellViews.cs`, `GateViews.cs`, `ShipView.cs`, `HintView.cs`, `ShapeSprites.cs`, `ViewColors.cs`, `CameraFitter.cs`; `Core/CameraFit.cs`; `UI/GameHUD.cs`, `PausePanel.cs`, `LevelCompletePanel.cs`.

## Decisions and edge cases

- **TextMeshPro essentials were missing** from the project (the copied UI kit needs them to draw any text). They were imported from the installed package (`Assets/TextMesh Pro`), TMP being the one pre-approved package.
- The stars are circles, not squares: the star mesh uses the disc texture.
- Level 1's launch is not always visibly "bendable": the line leaves the field until a well bends it. That is by design (the preview must react from the first pixel of a drag) and is Phase 3's demo hand's job to teach.

## Not verified

- On a phone: framing under a real notch, the look of the dotted line at phone density, and touch size of the tray slots. Everything was checked only in offscreen 1080×1920 renders (`Logs/Screens/`, git-ignored).
- The tray slot shows only the count of wells left. Whether that is clear enough needs playtesters.
