#if UNITY_ANDROID
using System.IO;
using UnityEditor.Android;
using UnityEngine;
using GravityWells.Core;

namespace GravityWells.Editor
{
    // Adds the VIBRATE permission to the generated Android manifest on every build.
    // Unity only adds it by itself when the code calls Handheld.Vibrate, which this game never
    // does; without it the vibrator fallback in HapticService throws and stays silent. (The
    // 2026-11-09 APK shipped without it, which is why vibration could not be felt at all.)
    public sealed class AndroidPermissionPatcher : IPostGenerateGradleAndroidProject
    {
        private const string Permission = "android.permission.VIBRATE";

        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifest = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifest))
            {
                Debug.LogWarning("[Build] Android manifest not found, VIBRATE permission not added: " + manifest);
                return;
            }

            string patched = ManifestPatch.AddPermission(File.ReadAllText(manifest), Permission);
            if (patched != null)
            {
                File.WriteAllText(manifest, patched);
                Debug.Log("[Build] added " + Permission + " to the Android manifest");
            }
        }
    }
}
#endif
