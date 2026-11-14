using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using GravityWells.Core;

namespace GravityWells.Editor
{
    // Reproducible Android releases. From the command line (Editor closed):
    //   Unity.exe -batchmode -nographics -projectPath D:/Projects/GravityWells -buildTarget Android
    //             -executeMethod GravityWells.Editor.BuildScript.BuildAab -logFile build.log
    // `-buildTarget Android` matters: switching platform inside a running build method reloads
    // the domain and abandons the method, so the target must already be Android.
    //
    // Signing keys come only from environment variables, never from the repo:
    //   GRAVITYWELLS_KEYSTORE_PATH, GRAVITYWELLS_KEYSTORE_PASS, GRAVITYWELLS_KEY_ALIAS, GRAVITYWELLS_KEY_PASS
    // Without them the build is signed with Unity's debug key: fine for testing on a device,
    // but Google Play will reject it.
    public static class BuildScript
    {
        private const string PackageId = "com.taha.gravitywells";
        private const string CompanyName = "Taha";
        private const string ProductName = "Gravity Wells";
        private const string BootstrapScene = "Assets/_Project/Scenes/Bootstrap.unity";
        private const string VersionFile = "BuildVersion.json"; // project root, committed
        private const string OutputFolder = "Builds";           // git-ignored
        private const long MaxBundleBytes = 40L * 1024 * 1024;  // Phase 6 target

        [Serializable]
        private sealed class BuildVersion
        {
            public int major = 1;
            public int minor = 0;
            public int patch = 0;
            public int versionCode = 0; // last code that shipped in a successful AAB

            public string Name => $"{major}.{minor}.{patch}";
        }

        [MenuItem("GravityWells/Build/Apply Android Settings")]
        public static void ApplyAndroidSettingsMenu()
        {
            ApplyAndroidSettings();
            Debug.Log("[Build] " + DescribeSettings());
        }

        [MenuItem("GravityWells/Build/Build Android AAB")]
        public static void BuildAab() => Run(asBundle: true);

        // A sideloadable APK for quick on-device checks (adb install). Does not use up a versionCode.
        [MenuItem("GravityWells/Build/Build Android APK (for adb)")]
        public static void BuildApk() => Run(asBundle: false);

        // The CLAUDE.md "Build settings" table, applied in one place so it can't drift.
        public static void ApplyAndroidSettings()
        {
            NamedBuildTarget android = NamedBuildTarget.Android;
            BuildVersion version = ReadVersion();

            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(android, PackageId);
            PlayerSettings.bundleVersion = version.Name;

            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64; // ARMv7 off
            PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Medium);

            // CLAUDE.md asked for min API 24, but Unity 6.3's own floor is 25 and it clamps anything
            // lower back up, so state the real value instead of requesting one that is ignored.
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel36;

            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3, GraphicsDeviceType.Vulkan });

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            EditorUserBuildSettings.buildAppBundle = true;
            PlayerSettings.Android.splitApplicationBinary = false;

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(BootstrapScene, true) };

            ApplyAdMobSettings();
        }

        // The Google Mobile Ads plugin refuses to build without an Android app id in its settings
        // asset, and that asset's class is internal to the plugin, so it is set through reflection.
        // The id lives in GameConfig (with the ad unit ids) so there is a single place to change.
        private static void ApplyAdMobSettings()
        {
            Type settingsType = Type.GetType("GoogleMobileAds.Editor.GoogleMobileAdsSettings, GoogleMobileAds.Editor");
            if (settingsType == null)
            {
                Debug.LogWarning("[Build] Google Mobile Ads editor assembly not found; skipping the AdMob app id.");
                return;
            }

            const BindingFlags any = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            object instance = settingsType.GetMethod("LoadInstance", any).Invoke(null, null);
            settingsType.GetProperty("GoogleMobileAdsAndroidAppId").SetValue(instance, GameConfig.AdMobAndroidAppId);
            EditorUtility.SetDirty((UnityEngine.Object)instance);
            AssetDatabase.SaveAssets();
        }

        public static bool UsingAdMobTestIds()
        {
            return GameConfig.IsAdMobTestId(GameConfig.AdMobAndroidAppId)
                || GameConfig.IsAdMobTestId(GameConfig.AdMobInterstitialUnitId)
                || GameConfig.IsAdMobTestId(GameConfig.AdMobRewardedUnitId);
        }

        public static string DescribeSettings()
        {
            return $"target={EditorUserBuildSettings.activeBuildTarget} id={PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android)} "
                + $"version={PlayerSettings.bundleVersion} code={PlayerSettings.Android.bundleVersionCode} "
                + $"backend={PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android)} arch={PlayerSettings.Android.targetArchitectures} "
                + $"minSdk={(int)PlayerSettings.Android.minSdkVersion} targetSdk={(int)PlayerSettings.Android.targetSdkVersion} "
                + $"strip={PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.Android)} aab={EditorUserBuildSettings.buildAppBundle} "
                + $"orientation={PlayerSettings.defaultInterfaceOrientation} scenes={EditorBuildSettings.scenes.Length}";
        }

        private static void Run(bool asBundle)
        {
            int exitCode = 1;
            try
            {
                exitCode = Build(asBundle) ? 0 : 1;
            }
            catch (Exception e)
            {
                Debug.LogError("[Build] failed: " + e);
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
        }

        private static bool Build(bool asBundle)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.LogError("[Build] Active build target is " + EditorUserBuildSettings.activeBuildTarget
                    + ". Switch to Android first (File > Build Profiles, or pass -buildTarget Android in batch mode).");
                return false;
            }

            ApplyAndroidSettings();

            BuildVersion version = ReadVersion();
            int versionCode = asBundle ? version.versionCode + 1 : Math.Max(1, version.versionCode);
            PlayerSettings.Android.bundleVersionCode = versionCode;
            EditorUserBuildSettings.buildAppBundle = asBundle;
            bool signedWithRealKey = ApplySigningFromEnvironment();

            Directory.CreateDirectory(OutputFolder);
            string extension = asBundle ? "aab" : "apk";
            string path = Path.Combine(OutputFolder, $"GravityWells-{version.Name}-{versionCode}.{extension}");

            var options = new BuildPlayerOptions
            {
                scenes = new[] { BootstrapScene },
                locationPathName = path,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            };

            Debug.Log("[Build] " + DescribeSettings() + " signedWithReleaseKey=" + signedWithRealKey);
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[Build] {summary.result}: {summary.totalErrors} error(s). See the log above.");
                return false;
            }

            double megabytes = new FileInfo(path).Length / (1024.0 * 1024.0); // the artifact itself, not the build's intermediate output
            Debug.Log($"[Build] OK {path} ({megabytes:F1} MB, {summary.totalTime.TotalSeconds:F0}s)");
            if (asBundle && new FileInfo(path).Length > MaxBundleBytes)
            {
                Debug.LogWarning($"[Build] Bundle is over the {MaxBundleBytes / (1024 * 1024)} MB target.");
            }
            if (!signedWithRealKey)
            {
                Debug.LogWarning("[Build] Signed with the DEBUG key. Set the GRAVITYWELLS_KEYSTORE_* variables for a Play-ready bundle.");
            }
            if (UsingAdMobTestIds())
            {
                Debug.LogWarning("[Build] AdMob is still using Google's TEST ids (GameConfig.AdMob*): ads will be marked test ads and earn nothing.");
            }

            if (asBundle)
            {
                version.versionCode = versionCode; // only a successful bundle uses up the code
                WriteVersion(version);
            }
            return true;
        }

        private static bool ApplySigningFromEnvironment()
        {
            string keystore = Environment.GetEnvironmentVariable("GRAVITYWELLS_KEYSTORE_PATH");
            string keystorePass = Environment.GetEnvironmentVariable("GRAVITYWELLS_KEYSTORE_PASS");
            string alias = Environment.GetEnvironmentVariable("GRAVITYWELLS_KEY_ALIAS");
            string keyPass = Environment.GetEnvironmentVariable("GRAVITYWELLS_KEY_PASS");

            bool complete = !string.IsNullOrEmpty(keystore) && File.Exists(keystore)
                && !string.IsNullOrEmpty(keystorePass) && !string.IsNullOrEmpty(alias) && !string.IsNullOrEmpty(keyPass);

            PlayerSettings.Android.useCustomKeystore = complete;
            if (complete)
            {
                PlayerSettings.Android.keystoreName = keystore;
                PlayerSettings.Android.keystorePass = keystorePass;
                PlayerSettings.Android.keyaliasName = alias;
                PlayerSettings.Android.keyaliasPass = keyPass;
            }
            return complete;
        }

        private static BuildVersion ReadVersion()
        {
            try
            {
                if (File.Exists(VersionFile))
                {
                    var parsed = JsonUtility.FromJson<BuildVersion>(File.ReadAllText(VersionFile));
                    if (parsed != null)
                    {
                        return parsed;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Build] could not read " + VersionFile + ": " + e.Message);
            }
            return new BuildVersion();
        }

        private static void WriteVersion(BuildVersion version)
        {
            File.WriteAllText(VersionFile, JsonUtility.ToJson(version, true) + "\n");
        }
    }
}
