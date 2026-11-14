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
