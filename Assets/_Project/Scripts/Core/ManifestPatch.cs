using System;

namespace GravityWells.Core
{
    // Text edits the build makes to the generated Android manifest. Kept here, away from the
    // Editor-only build hook that uses it, so the edit itself is covered by a test.
    public static class ManifestPatch
    {
        // Returns the manifest with a <uses-permission> for `permission` inserted before
        // <application>, or null when it is already declared or there is nothing to anchor on.
        public static string AddPermission(string manifestXml, string permission)
        {
            if (string.IsNullOrEmpty(manifestXml) || manifestXml.Contains("\"" + permission + "\""))
            {
                return null;
            }
            int at = manifestXml.IndexOf("<application", StringComparison.Ordinal);
            if (at < 0)
            {
                return null;
            }
            return manifestXml.Insert(at, "<uses-permission android:name=\"" + permission + "\" />\n  ");
        }
    }
}
