# 10 — Palette pass and the sky

## The sky

The star background is still procedural and still one mesh in one draw call (140 small discs, the same sky on every launch). It now twinkles: each star shimmers slowly (its own speed and phase, 22% either way, refreshed 20 times a second), so the field is never dead still. It is decoration only and runs on any time scale.

## The palette pass

The board borrows its colours from the nine palettes the menus were written for (eight themes and the colour-safe one, from Untangle). Looking at the board, the risk is a role colour that is fine on a button but too close to the playfield to see as a dotted line. `ViewColors` therefore **pushes any role colour toward the palette's text colour until it keeps at least 3:1 contrast against the playfield** (the WCAG bar for graphics that must be seen), and `ViewColorsTests` checks it for every palette:

- every role (neutral, active, lit, good, warning) against the playfield, at the alphas they are actually drawn: the dotted line at 80%, an unlit gate post at 70%, asteroids solid;
- a lit gate and an unlit one differ in colour as well as shape;
- the winning path differs from the ordinary path.

Two real problems were found and fixed this way: **Neon** gives its "clear" colour exactly the value of its text colour, so the winning path looked like any other line (it now leans toward the lit colour); and a warm-paper asteroid at 90% opacity fell to 2.9:1 (asteroids are drawn solid).

Meaning is never carried by colour alone: lit gates are filled, unlit hollow; repulsors have a broken outline; the miss is a cross; asteroids are filled lumps.

## Code and tests

`Game/ViewColors.cs`, `Game/FieldView.cs` (twinkle); `ViewColorsTests` (5).

## Not verified

The contrast is measured, not judged: whether each of the nine palettes looks good (not merely legible) with the board needs eyes. The offscreen renders in `Logs/Screens/` were taken in the default palette only. Phase 4 defines the palettes' own roles and re-checks them (`LineColors.ContrastRatio`).
