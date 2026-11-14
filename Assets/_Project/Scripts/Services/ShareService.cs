using UnityEngine;

namespace GravityWells.Services
{
    // The Android share sheet through a plain intent (no plugin); anywhere else it copies to the clipboard.
    public sealed class ShareService : IShareService
    {
        public bool Share(string text)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var intent = new AndroidJavaObject("android.content.Intent"))
                {
                    intent.Call<AndroidJavaObject>("setAction", "android.intent.action.SEND");
                    intent.Call<AndroidJavaObject>("setType", "text/plain");
                    intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.TEXT", text);
                    using (var intentClass = new AndroidJavaClass("android.content.Intent"))
                    using (var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "Share puzzle"))
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    {
                        activity.Call("startActivity", chooser);
                    }
                }
                return true;
            }
            catch (System.Exception)
            {
                // No share sheet available: fall through to the clipboard.
            }
#endif
            GUIUtility.systemCopyBuffer = text;
            return false;
        }

        public string Paste() => GUIUtility.systemCopyBuffer ?? string.Empty;
    }
}
