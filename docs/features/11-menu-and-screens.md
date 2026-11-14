# 11 — Menu, screens and navigation

The app opens on the **main menu**. From it the player plays, opens the daily, or goes to Themes, Stats or Settings. A level is left through its pause card (Menu) or its result card (Menu). Navigation lives in `AppFlow` (a component beside `GameManager`); the screens are built from the shared UI kit and ask `AppFlow` for what they show and do.

## The screens

| Screen | What it shows and does |
|---|---|
| **Main menu** | Title; the coin balance; **Play** with the level the player has reached; **Daily challenge** with "A new puzzle every day" or "Done today" and the streak; **today's three quests** with progress or "Done"; the way to Themes, Stats, Settings. |
| **HUD** (Phase 2, extended) | The hint button carries its price on a corner chip: the coins, or **AD** when the player is short. After five misses an **Extra well · AD** button appears above Launch. The level-1 lesson's ghost finger draws above everything. |
| **Pause** | Resume, Restart level, **Menu**. |
| **Result card** | Stars, wells against par, launches, a badge (under par / extra well used / streak / new best), **coins paid**, any completed quest, then **Next level** (or **Menu** after the daily) and Replay, Menu. |
| **Settings** | Sound and Music switches, Vibrations (Off → On → Strong, with a sample buzz), Remove ads, Restore purchase, Privacy policy, Privacy options (only where consent rules require it), the version. |
| **Stats** | Levels solved, stars earned, under par, launches, wells placed, best daily streak, time played. |
| **Themes** | The nine palettes as a list: four swatches of the palette's own colours, its name, and "In use", or "Unlocks at level N" with a lock. Tapping an unlocked one switches at once (everything cross-fades); a locked one says when it opens. |

## Back never dead-ends

| Where | Back does |
|---|---|
| Settings, Stats, Themes | back to the menu |
| In a level | pause; Back again resumes |
| Pause card | resume |
| Result card | replay the level (never advances, so a stray press cannot skip one) |
| Ad overlay | dismisses it (except while the ad is opening) |
| Main menu | the first press shows "Press back again to exit"; a second within two seconds exits |

`GameFlowSmokeTests.EveryScreen_Opens_AndBackAlwaysLeadsSomewhere` walks all of this in the real scene.

## Settings

`AppFlow.SetSoundOn / SetMusicOn / CycleVibrations / SelectTheme` change the service and the save together and save at once; a restart reads them back (`TheSettings_Stick_AndSurviveARestartOfTheApp`). The purchase rows go through `MonetizationController` and the stub purchase service, which in a release build says "not available yet": **Remove ads cannot actually be bought until billing is wired** (Phase 5/6). The privacy-policy row says the policy "will be published with the release" until `GameConfig.PrivacyPolicyUrl` is set.

## Code

`Game/AppFlow.cs`; `UI/MainMenuScreen.cs`, `SettingsScreen.cs`, `StatsScreen.cs`, `ThemesScreen.cs`, `PausePanel.cs`, `LevelCompletePanel.cs`, `GameHUD.cs`; all text in `Core/Strings.cs`.

## Decisions and edge cases

- The menu is a screen, not a scene: there is still one scene and one GameObject.
- A level in progress is **never resumed**: going to the menu drops it (a level is regenerated from its seed and takes a minute to play).
- No separate Quests screen (it is not in the spec's screen list): the quests are a card on the menu.
- No level-select screen: Play continues from the level reached. (A level the player wants again is reached by replaying from the result card.)

## Not verified

- **On a phone:** the layout under a real notch and gesture bar, touch targets of the new buttons, the menu's double-Back exit (`Application.Quit` does nothing in the Editor, so only the first press was exercised).
- **The Themes list on a short screen:** nine rows of 148 px from 330 px down need about 1,660 px of canvas height; on a 16:9 phone that is nearly all of it. If a device is shorter the last row will clip (the list does not scroll yet).
- Whether every palette looks good on every screen (the renders are in the default palette).
