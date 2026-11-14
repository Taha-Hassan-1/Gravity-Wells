# Building Gravity Wells for Android

Last verified: 2026-11-14 — debug APK 21.4 MB (M0, no game code yet; first IL2CPP build took 189 s), built on Unity 6000.3.15f1 with the SDK/NDK/JDK that ship inside the Unity install (no separate Android Studio needed). No AAB has been built yet.

## One-time state

- The Editor build target is **Android** (the project was created with it). If it ever flips back: File ▸ Build Profiles ▸ Android ▸ Switch Platform (about 40 s on this project), or pass `-buildTarget Android` in batch mode.
- All settings below are applied by code (`GravityWells ▸ Build ▸ Apply Android Settings`, also run at the start of every build), so they can't drift from `CLAUDE.md`.

## Settings the build enforces

| Setting | Value | Note |
|---|---|---|
| Package ID / company / product | `com.taha.gravitywells` / `Taha` / `Gravity Wells` | personal Play account |
| Scripting backend | IL2CPP | |
| Architectures | ARM64 only (ARMv7 off) | confirmed: the AAB contains only `arm64-v8a` |
| Min API level | **25** | `CLAUDE.md` says 24, but Unity 6.3's own floor is 25 and it clamps lower values back up, so 24 cannot be built |
| Target API level | 36 | required by Play for new apps |
| Output | Android App Bundle (`.aab`) | `Build Android APK (for adb)` makes a sideloadable `.apk` instead |
| Managed stripping | Medium | `SaveData` is marked `[Preserve]` so JsonUtility never loses a field |
| Graphics APIs | OpenGLES3, Vulkan | auto-API off; listed in this order per `CLAUDE.md`, so GLES3 is tried first |
| Orientation | Portrait, all auto-rotate off | |
| Scenes | `Bootstrap.unity` only | |
| Frame rate | 60 fps cap, vsync off | set at runtime in `Bootstrap` (Android defaults to 30) |

Permissions in the built manifest: `VIBRATE` only so far (`INTERNET` arrives with the ad package). Verified with `aapt2 dump badging` on the M0 APK: target 36, versionName 1.0.0, versionCode 1, `arm64-v8a`; the build log reports min 25 and portrait.

## Version numbers

`BuildVersion.json` at the repo root holds `major.minor.patch` and the last shipped `versionCode`. Each **AAB** build uses `versionCode + 1` and writes it back **only if the build succeeds**, so a failed build never burns a code. APK builds reuse the current code. Bump `major/minor/patch` by editing the file.

## Building

From inside the Editor: `GravityWells ▸ Build ▸ Build Android AAB` (or `... APK (for adb)`). The Editor is unresponsive while it runs (about 4 minutes the first time with IL2CPP, about 15 s when cached); the result is logged as `[Build] OK Builds\GravityWells-<version>-<code>.aab (...)`.

From the command line, with the Editor **closed**:

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.15f1/Editor/Unity.exe" -batchmode -nographics \
  -projectPath "D:/Projects/GravityWells" -buildTarget Android \
  -executeMethod GravityWells.Editor.BuildScript.BuildAab -logFile build.log
```

The process exit code is 0 on success, 1 on failure. `-buildTarget Android` is required: switching platform from inside the build method reloads scripts and abandons the method.

Output goes to `Builds/` (git-ignored). Don't read the build summary's "total size" as the bundle size — it includes Unity's staging data; look at the file itself.

## Signing (do this before uploading to Play)

Builds are signed with Unity's **debug key** unless signing variables are set. A debug-signed bundle is fine for a device but **Google Play rejects it**.

1. Create an upload key **outside the repo** and back it up somewhere safe (losing it means a support request to reset it). Use Unity's bundled JDK:
   ```bash
   "C:/Program Files/Unity/Hub/Editor/6000.3.15f1/Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK/bin/keytool.exe" -genkeypair -v \
     -keystore "D:/keys/gravitywells-upload.keystore" -alias gravitywells-upload -keyalg RSA -keysize 2048 -validity 10000
   ```
2. In Play Console, enrol in **Play App Signing** (default for new apps). Google then holds the real signing key and this is only the upload key.
3. Before building, set these in the same shell (or system environment) that launches Unity. The script reads them and never writes them anywhere:
   `GRAVITYWELLS_KEYSTORE_PATH`, `GRAVITYWELLS_KEYSTORE_PASS`, `GRAVITYWELLS_KEY_ALIAS`, `GRAVITYWELLS_KEY_PASS`.

`*.keystore` and `*.jks` are git-ignored. Never commit a key or a password.

## Installing on a phone

1. Phone: Settings ▸ About ▸ tap Build number 7 times ▸ Developer options ▸ enable **USB debugging**; plug in, accept the prompt.
2. `"…/AndroidPlayer/SDK/platform-tools/adb.exe" devices` should list it, then:
   `adb install -r Builds/GravityWells-1.0.0-1.apk`
   (or copy the APK to the phone and open it, allowing "install unknown apps").

## What a release build does differently

- Ads: only the stub exists so far, and it is used in the Editor only. The device ad order (`GameConfig.AdProviderOrder`) names `admob`, whose adapter is added at the Services phase after the owner approves the Google Mobile Ads package; until then a device build simply shows no ads.
- `StubPurchaseService` returns `Unavailable` outside the Editor/dev builds, so **Remove ads cannot be bought** yet.
- `LogAnalyticsService` records nothing outside the Editor/dev builds.
- The launcher icon comes from `Assets/_Project/AppIcon/` (`icon.png`, `adaptive_background.png`, `adaptive_foreground.png`); `BuildScript` applies it to the legacy, round and adaptive slots on every build. The folder does not exist yet (the owner supplies the files), so the build warns and uses Unity's default icon.

## Ad libraries (AdMob), for the Services phase

Nothing is installed yet. When the package is approved, follow section 7 of `ADS_IMPLEMENTATION.md` in the planning folder: the OpenUPM scoped registry, the External Dependency Manager prompt switched off in `ProjectSettings/GvhProjectSettings.xml`, `Assets/GoogleMobileAds/link.xml` kept committed, and the app id written by `BuildScript.ApplyAdMobSettings()`.

## Not yet verified

Everything about running on a real device: install, cold start under 2 s, 60 fps, touch feel, Back button, safe-area insets, audio, vibrations. Also the Play pre-launch report and 16 KB page-size compatibility (Unity 6.3 should satisfy it; confirm in the pre-launch report).
