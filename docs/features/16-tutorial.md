# 16 — Tutorial (Phase 7)

Added after the first phone test: the music, sounds and vibrations landed, but the gravity concept was hard to read, especially with two or three pullers or a pusher, and a well placed on the line looked like the line "merged into the hole".

## What it does

* **Crash mark in the preview.** When the line on screen ends in a well or a rock, a red cross is drawn where it ends, before launching. A line that runs into a well also says so in the banner ("Too close: the ship would crash into that well. Move it off the line."); the level's lesson line returns when the crash is gone. (`GameManager.ShowPreviewCrash`, `CrashMarkView`.)
* **Well links.** Each placed well has a thin run of dots flowing to the spot on the line where it pulls hardest: toward an attractor, away from a repulsor. Strong wells are bright, weak ones faint; the well in the finger is brightest, so with several wells the player can see what each one does. Strength comes from `Core/WellInfluence` (the simulator's own pull law, evaluated at the line's nearest point; never re-simulates). Hidden while the ship flies. (`WellLinkView`.)
* **Lesson banner** on campaign levels 1 to 3, one idea each: drag a well next to the line; closer pulls harder; every well adds its own pull. It steps aside at launch. (`GameHUD.SetLesson`, `Strings.Tutorial`.)
* **How to play**: five short pages, opened from a button on the menu, and shown once before a brand-new player's first level (`HowToPlayScreen`, `SaveData.HowToPlaySeen`). Pusher wells have a page, but no campaign level teaches them until level 35, where they first appear.

## Not done

* The links tint the connection, not the stretch of line a dragged well bends.
* No dedicated pusher lesson level (pushers are text only).
* All of this is checked in the Editor (renders `Logs/Screens/30`–`34`); whether it makes the concept clear to a new player is for the phone and for a person who has not seen the game.
