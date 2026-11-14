# 00 — Shared code (`Frag.Common`) copied at M0

The service layer, UI kit and tooling were copied from the two sibling games on 2026-11-14 and renamed to `GravityWells.*`. Nothing here was rewritten. When a fix is made in one game, bring it to the others by hand; the owner's decision is to extract a shared package only after Chain Mixer ships.

## What came from where

| Group | Files | Source |
|---|---|---|
| Ad rules (pure) | `Core/AdPolicy`, `AdPoolPlanner`, `AdProviderOrder` | identical in both siblings |
| Ad services | `Services/IAdService`, `StubAdService`, `AdProviderRegistry`, `WaterfallAdService`, `IConsentService` + `StubConsentService`, `IPurchaseService` + `StubPurchaseService` | identical in both siblings |
| Ad flow | `Game/AdFlow` (+ `IAdPresenter`), `Game/AdMessages`, `UI/AdOverlay`, `Game/MonetizationController`, the ad parts of `UI/UIRoot` | Chain Mixer |
| Analytics | `IAnalyticsService`, `LogAnalyticsService`, `CompositeAnalyticsService`, `FileAnalyticsService`, `ExceptionReporter` | Untangle |
| Save | `Meta/SaveService` (atomic write, backup, corrupt-file recovery), `SaveMigrator`, `SaveData` | Untangle; `SaveData` holds the shared fields plus `UnderParCount`, `TotalLaunches`, `TotalWellsPlaced` |
| Themes and colour | `Meta/ThemeService`, `Game/ThemeController`, `Core/LineColors`, `Meta/LineStyleService` | Untangle |
| Daily, coins, quests | `Meta/DailyChallengeService`, `Core/Economy`, `Meta/EconomyService`, `Core/Quests`, `Meta/QuestService` | Untangle |
| Audio and feel | `Services/AudioService`, `Core/MusicDsp`, `Core/MusicLibrary`, `Services/HapticService`, `Core/HapticPlan`, `Core/CountUp`, `Services/IShareService` + `ShareService` | Untangle |
| UI kit | `UI/UIFactory`, `UIStyle`, `UITokens`, `UISwitch`, `UIIcons`, `UIFonts`, `PressFeedback`, `UIScreen`, `UIRoot`; `Resources/UI/Icons/`, `Resources/UI/Fonts/` with both OFL licence texts | Untangle (after its UI redesign) |
| Share codes | `Core/PuzzleCode` (prefix changed from `UT` to `GW`) | Untangle |
| Tooling | `Editor/BuildScript`, `AndroidPermissionPatcher` + `Core/ManifestPatch`, `UIFontBuilder`, `UIIconImporter`, `BootstrapSceneBuilder`, `BuildVersion.json`, `docs/BUILD.md` | Untangle |
| Sprites | `Game/CircleSprite` | Chain Mixer |

New for this game, written at M0: `Core/GameConfig` (ad constants only so far), `Core/Strings` (only the `Common` and `Ads` sections the shared code reads), a `Bootstrap` that only sets the frame rate.

## Deliberately not copied yet

| Item | Why | When |
|---|---|---|
| `IGameMode`, `GameModeRegistry`, `GameModeCatalog` | Untangle's interface is written against its own `LevelDefinition`, moves and par; it cannot compile before this game's level type exists, and its members need re-shaping for wells and launches | Phase 2, with `CampaignMode` |
| `AdMobAdService`, `AdMobConsentService`, `AdNetworkProbe`, `AdMobConfigTests`, the probe half of `AdFeedbackTests` | They need the Google Mobile Ads package, which needs the owner's approval | Phase 5 |
| `GameFlowSmokeTests` | It drives a real game; there is nothing to boot yet | Phase 4 |
| `MusicLibrary` tracks | Untangle's three tracks belong to its modes; this game gets its own track | Phase 3 |
| App icon files | The owner supplies them; `BuildScript` warns and uses the default icon until `Assets/_Project/AppIcon/` exists | before release |

## Copied as-is but still carrying Untangle's wording (to adapt, not rewrite)

- `ThemePalette` names its colours `Node`, `NodeGrabbed`, `EdgeClear`, `EdgeCrossing`, and the palettes (eight plus the colour-safe one) are Untangle's. Phase 4 renames the roles for wells, gates and the path and re-checks the contrast.
- `Core/Quests` counts Untangle's events (undo, power-ups, moves). Phase 4 swaps the quest kinds for this game's (solve levels, earn stars, solve under par).
- `PuzzleCodes.ShareMessage` talks about moves and a "Puzzle Challenge" screen. Share codes are post-launch here.
- `SaveData` keeps `LineColorClear`, `LineColorCrossing` and `ThickCrossings` because `LineStyleService` reads them.

## Tests

224 EditMode tests, all copied with the code: ad policy, pool planner, waterfall, monetization controller, ad flow, ad messages, analytics sinks, save service and migration, themes, design tokens and contrast, fonts, daily challenge, economy and quests, haptic plan, count-up, puzzle codes. Tests that needed Untangle-only types were trimmed, not rewritten: the ring-sprite check, `VertexColorTests`, the challenge-mode half of `PuzzleCodeTests`, and the network-probe half of `AdFeedbackTests`.
