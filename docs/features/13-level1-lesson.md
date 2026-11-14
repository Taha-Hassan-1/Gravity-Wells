# 13 — The level-1 lesson

A new player is taught with no words. On level 1 a ghost finger (a ring and a dot, in the text colour, above everything) rises on the well tray, **carries a well across the field to a marked spot**, lets go, and the dotted line bends through the gate and into the goal. It rests for a moment, fades, the well is taken back, and the whole thing begins again, until the player touches the screen.

## How it works

- `TutorialDemo` (Core) is a pure function of time: `Sample(seconds, trayPosition)` returns where the hand is, how visible, whether a well is on the field and whether the finger is holding it. One cycle is 4.3 s: 0.5 s appearing, 1.5 s carrying (smoothstep), 1.5 s resting with the line bent, 0.8 s leaving.
- **The spot is found by the same `Solver` the tests use**, which does not read the level's stored solution. So the demonstration always ends in a win, and the rule that nothing outside the generator reads the solution still holds (a test enforces it).
- `GameManager` drives the layout through the same calls a finger would (`BeginDragNew`, `DragTo`, `EndDrag`), so the preview line bends live, the gate pops and sings, and the goal fills, exactly as the player's own drag would. The hand is drawn by the HUD at the screen position of the world point.
- Shown only on **campaign level 1** and only while `SaveData.TutorialSeen` is false. It ends when the player touches the field or the tray (the touch is then theirs), when they ask for a hint, or when they launch. It ends **for good** when the player's own first drop has been made (`TutorialSeen` is saved then); if they only touch and walk away, it shows again next launch.

## Code

`Core/TutorialDemo.cs`; `Game/GameManager.cs` (`BeginDemoIfNeeded`, `TickDemo`, `StopDemo`); `UI/GameHUD.cs` (`ShowHand`, `HideHand`, `TrayScreenCenter`).

## Tests

`TutorialDemoTests` (6): the dropped well really wins level 1; the same every time; no demo where more than one well is needed; the timeline order; the hand moves smoothly and never backwards; it repeats. `GameFlowSmokeTests.TheLevel1Lesson_PlaysByItself_ThenStopsForGood` runs it in the real scene: the well appears, the line wins, it empties and starts again, and Launch ends it.

## Not verified

- **A finger ending it.** The touch check reads `Input`, which no test can inject; the logic after it is the same code Launch exercises.
- **Whether it teaches.** That a person who has never seen the game understands "drag this ring onto the field" from a ring and a dot is the question the spec asks ("a new player solves level 1 with no text instruction"). It needs a new player: the owner's first playtesters. If they do not understand it, the spec's fallback is an illustrated how-to-play.
- The hand is a plain ring and dot, not a drawn finger: the project has no art, and a more finger-like glyph would be a small change in `GameHUD`.
