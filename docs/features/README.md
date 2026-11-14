# Feature docs

One file per feature, written as context for anyone (human or Claude) changing it. Each lists the behaviour, the numbers, where the code lives, the edge cases, and what is tested. The build spec is [`../IMPLEMENTATION_PROMPT.md`](../IMPLEMENTATION_PROMPT.md), the plan is [`../ROADMAP.md`](../ROADMAP.md), and the verification matrix is [`../TEST_CASES.md`](../TEST_CASES.md).

A feature doc and its test-case rows are written in the same change as the feature.

## Features

| # | Feature | Layer | Phase |
|---|---|---|---|
| [00](00-shared-code.md) | Shared code copied from Untangle and Chain Mixer | all | M0 |
| [01](01-flight-simulation.md) | Flight simulation | Core | 1 |
| [02](02-level-generation.md) | Level generation | Core | 1 |
| [03](03-solver-hint-and-score.md) | Path score, solver, hint, forgiveness | Core | 1 |
| [04](04-difficulty-and-scoring.md) | Difficulty curve and scoring | Core | 1 |
| [05](05-board-and-rendering.md) | Board, views, camera, HUD | Game, UI | 2 |
| [06](06-well-drag-and-launch.md) | Placing wells, preview and launch | Core, Game | 2 |
| [07](07-device-determinism-check.md) | Device determinism check | Core, Editor | 2 |
| [08](08-feel.md) | Feel: line, wells, launch, win and miss | Core, Game | 3 |
| [09](09-audio.md) | Audio: the sounds and the music | Core, Services | 3 |
| [10](10-palette-pass.md) | Palette pass and the sky | Game | 3 |
| [11](11-menu-and-screens.md) | Menu, screens and navigation | UI, Game | 4 |
| [12](12-coins-quests-and-daily.md) | Coins, quests, hints, extra well, the daily | Core, Meta, Game | 4 |
| [13](13-level1-lesson.md) | The level-1 lesson | Core, Game, UI | 4 |
