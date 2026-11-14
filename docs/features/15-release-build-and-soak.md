# 15 — Release build and soak test (Phase 6)

## Build

`GravityWells ▸ Build ▸ Build Android AAB` (or `-executeMethod GravityWells.Editor.BuildScript.BuildAab -buildTarget Android`) produces `Builds/GravityWells-<version>-<code>.aab`; the APK entry is for `adb install`. Version comes from `BuildVersion.json` (a successful bundle uses up one version code). Signing uses only the `GRAVITYWELLS_KEYSTORE_*` environment variables; without them the bundle is debug-signed and says so.

Measured 2026-11-15, built from the open Editor (171 s, IL2CPP, ARM64, API 25 / target 36):

| Check | Spec | Result |
|---|---|---|
| Bundle size | under 40 MB | **28.3 MB** (the "387 MB" Unity prints is the whole build output, not the file) |
| Largest parts | | libil2cpp 34 MB and libunity 18 MB uncompressed, two dex files 16 MB |

## Soak test

`GravityWells ▸ Tuning ▸ Run Soak Test (1,000 levels)` (`Editor/SoakTest.cs`, also `SoakTest.Begin(n)` from the CLI) enters Play Mode, plays the campaign through the real `GameManager` with rendering on at 12× time scale, places each level's generator solution, launches, waits for the result and taps Next level. The save goes to a throwaway folder. Failures (a non-winning solution, a level that stalls 40 s, any logged error or exception) are counted into `Logs/Soak.txt`. Unity must be the foreground window or Play Mode does not advance.

Result: **100 levels: 0 failures, 0 errors (54 s). 1,000 levels: 0 failures, 0 errors (484 s).**

## Not done

* Cold start under 2 s: needs a phone. No device has been connected, so this and the APK install were never tried.
* The soak plays the generator's solution, not the hill-climbing solver; both are covered separately by tests.
* The bundle is debug-signed with Google's TEST ad ids. Release keystore, real ad ids and a privacy policy URL are the owner's.
