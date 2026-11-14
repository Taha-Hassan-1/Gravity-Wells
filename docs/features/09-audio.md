# 09 — Audio: the sounds and the music

There are **no audio files**. Every sound and the music are generated from maths in Core (`SfxLibrary`, `OrbitMusic`, on `MusicDsp`), so there is nothing to license or clear. `AudioService` generates them on a background thread at start-up and turns them into clips as they finish; a sound asked for before it is ready is skipped, never waited for.

## The sounds (`SfxLibrary`)

All in D, so anything played together agrees. One-shots have a 2 ms fade in and a 10 ms fade out (no clicks) and the same peak (0.85).

| Sound | When | What it is |
|---|---|---|
| `WellPickUp` | a well leaves the tray or is picked up | a small rising bloop |
| `WellDrop` | a well is put down | a soft thump and a settling D4 |
| `GateNote` | a gate lights (preview, flight, win pings) | a bell on D5 with a faint octave; played at a rising pitch, one pentatonic step per gate lit |
| `Launch` | the charge begins | a thump, then a swell of air rising |
| `Whoosh` | the flight | looped, softened noise; its end is crossfaded into its start so it joins without a seam; volume and pitch follow the ship's speed |
| `Win` | the win stinger | five rising notes up the scale under a held D chord |
| `Fail` | a flight did not make it | two gentle sagging slides (a miss is free, so it must not sting) |
| `GoalChord` | the line under the finger first reaches the goal | a soft D chord that blooms and rings out |

## The music: "Orbit" (`OrbitMusic`)

Slow, spacious, with one tune: a five-note motif that rises a fifth and then circles back (like a body in orbit), asked as a question a phrase later (it climbs instead of falling), then repeated an octave up with an echo, and resolved. A plucked bell carries it with a long echo that is part of the tune; a drone moves D, G, D, A under it; a whisper of pad; and a slide **falling** out of the high register at the start of each half, the game's own sound. D Dorian, 66 beats a minute, eight bars (29.1 s), written into a circular buffer so the loop has no seam. Levelled to the shared loudness (RMS 0.2, soft-limited under 0.95). It never responds to "pressure": a puzzle with no clock has none. Played from `Bootstrap` through the existing music switch.

## What the tests prove (`AudioSynthesisTests`, 44)

For every one-shot: audible (RMS above a floor), peak exactly at the shared level, never clips, starts and ends at silence, identical every run, no NaN. For the whoosh: it is an even texture (not a tone) and **joins itself without a click**. For Orbit: RMS 0.2 ± 0.03, never above 0.95, exactly eight bars at 66 bpm, seamless join, identical every run, **has a tune in it** (the loudness moves a lot from tenth of a second to tenth, which a steady hum cannot do), has quiet moments between notes, and has no empty second anywhere. `TheSoundsCanBeExportedAsWavFiles_ToBeListenedTo` writes every sound and two passes of the music to `Logs/Audio/*.wav` (git-ignored) with each run.

## Not verified: whether it sounds good

**These tests measure; they cannot listen.** I have not heard any of it. Whether the motif is memorable rather than merely present, whether the echo is lovely or muddy, whether the gate bells are pleasant at the top of the scale, whether the launch swell and the whoosh sit well together and whether the music is too quiet or too loud against the effects (`BaseVolume` 0.55) are the owner's to judge. **Play the WAV files in `Logs/Audio/` first.** Every number involved is a named constant or a few lines in `SfxLibrary` / `OrbitMusic`, so changing the sound is cheap.

Also not checked: how the sounds behave on a phone speaker (the small-speaker range matters: the pop clicks were already tuned for it in Untangle; the new sounds were not), and ducking (the music does not yet lower under the effects).
