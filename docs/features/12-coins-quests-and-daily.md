# 12 — Coins, quests, hints, the extra well and the daily

## Coins (`Economy`, `EconomyService`, `RewardService`)

- **A campaign level pays in full the first time:** 10 coins plus 5 per star. After that it pays **only for stars gained** (5 each), so replaying a level is never a coin farm.
- **The daily pays double, once a day:** (10 + 5 per star) × 2, on the first completion of the UTC day. A replay of the daily pays nothing.
- The balance can never go negative: every earn and spend goes through `EconomyService`.
- `RewardService.Award` is the one place a win is paid, for both modes, so the campaign and the daily cannot pay by different rules. The result card shows the total, quests included, and it equals what the balance rose by (a test checks this).

## Daily quests (`QuestBoard`, `QuestService`)

Three a day, a pure function of the UTC date (so every install sees the same three), drawn from six kinds: solve N levels (3–5), earn N stars (4–7), solve N under par (1–2), solve N on the first launch (2–3), solve N without a hint (2–4), finish the daily. Each pays 25–40 coins when it completes, with **no claim button**, and finishing all three pays a 50-coin bonus once. Progress survives a restart within the day and resets at UTC midnight. A replayed daily moves no quest.

## Hints (30 coins, or a rewarded ad)

`GameManager.ShowHint` asks `HintPlanner` first. **If it finds nothing it says so and charges nothing.** If it finds a move: 30 coins are spent if the player has them; otherwise a rewarded ad (through `AdFlow`, with its spinner, timeout and honest failure messages) pays for the same hint. The hint is a ghost ring, and a line from the well to move; the player makes the move. The price shows on the hint button (the coins, or AD). Taking a hint marks the level `UsedHint`, which the "without a hint" quest reads.

## The Extra Well (rewarded ad)

After **five failed launches** on a level an "Extra well · AD" button appears. Watching the whole ad adds one attractor to that level's budget (`WellLayout.GrantBonusWell`; it survives Undo and Restart and is gone when the next level begins). **A win that used it earns one star**, whatever the count, and is never "under par". Skipping the ad grants nothing and says why.

## The daily (`DailyMode`)

The seed is the UTC date as a number (`yyyyMMdd`, so 14 Nov 2026 is 20261114), and the puzzle is built at the difficulty of level 40, so the same date gives the same puzzle on every device. The date is read once when the daily begins, so a puzzle started before midnight is finished as the day it began. One completion a day counts toward the **streak** (a missed day breaks it; a held streak freeze, if one exists, forgives one; the best streak is kept). The daily never touches campaign progress.

## Levels that introduce a new piece

- **Level 12** brings the first asteroid on the same wells and gates as level 11.
- **Level 35** brings the first repulsor, and **always contains exactly one**, with a lighter load than the curve would give (two wells, at most three gates, at most one asteroid), so the repulsor is the only hard thing. (From 36 on, each extra well has a 30% chance to be one.) This changed levels' content only at 35, which moved the campaign determinism hash from `-159271037` to `69262906`; no phone had recorded the old value.

## Code

`Core/Economy.cs`, `Core/Quests.cs`, `Meta/EconomyService.cs`, `Meta/QuestService.cs`, `Meta/RewardService.cs`, `Game/Modes/CampaignMode.cs`, `DailyMode.cs`, `IGameMode.cs`, `Core/DifficultyCurve.cs`, `Core/LevelGenerator.cs`, `Core/WellLayout.cs`, `Game/GameManager.cs`.

## Tests

`EconomyAndQuestTests` (20), `CampaignModeTests` (14), `DailyModeTests` (9: same date same puzzle, a different date a different one, difficulty 40, a puzzle begun before midnight, double pay once, a second completion pays nothing, the streak builds and breaks, the daily never touches campaign progress, twenty consecutive days are solvable), the introduction-level tests in `GeneratorTests`, and the Play Mode tests for hints, the extra well and the daily.

## Not verified

- **The real ad.** Every ad flow ran against the stub, which completes at once. A real network's loading, failure, skip and timeout are Phase 5 and device checks. On a device build today there is no ad provider, so a rewarded flow honestly says "no ad right now".
- **The economy's balance.** Whether 30 coins for a hint, the payouts and the quest sizes feel right is a question for play, not for tests.
- **Streak freeze:** the copied streak code supports one, but nothing in this game grants it yet.
