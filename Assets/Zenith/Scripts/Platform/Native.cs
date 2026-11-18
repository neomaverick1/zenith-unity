// The phone: haptics, sharing and the screen's pixel density. One haptic per moment, tuned so the phone feels
// like part of the game rather than buzzing at everything.
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Zenith
{
    public enum Feel { Tap, Move, Merge, Big, Huge, Blocked, Win, Lose }

    public static class Native
    {
        public const string Url = "https://neomaverick1.github.io/zenith-unity/";

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void ZenithHaptic(int kind);
        [DllImport("__Internal")] static extern void ZenithShare(string text, string url);
#elif UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void ZenithVibrate(string pattern);
        [DllImport("__Internal")] static extern void ZenithShare(string title, string text, string url);
        [DllImport("__Internal")] static extern float ZenithPixelRatio();
        [DllImport("__Internal")] static extern int ZenithIsMobile();
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject vibrator;
        static int sdk;
        // Referencing Handheld.Vibrate makes the build ask for the vibrate permission.
        static bool never;
#endif

        public static bool IsMobileWeb
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                try { return ZenithIsMobile() != 0; } catch { return false; }
#else
                return false;
#endif
            }
        }

        /// <summary>How many screen pixels make one layout point, so the interface is the same size everywhere.</summary>
        public static float PixelRatio()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try { return Mathf.Max(1, ZenithPixelRatio()); } catch { return 1; }
#else
            float dpi = Screen.dpi > 0 ? Screen.dpi : 160;
            return Application.isMobilePlatform ? Mathf.Max(1, dpi / 160f) : Mathf.Max(1, dpi / 120f);
#endif
        }

        public static void Haptic(Feel kind)
        {
            if (!Store.settings.haptics) return;
            try
            {
#if UNITY_IOS && !UNITY_EDITOR
                ZenithHaptic((int)kind);
#elif UNITY_ANDROID && !UNITY_EDITOR
                Android(kind);
#elif UNITY_WEBGL && !UNITY_EDITOR
                string p = kind switch
                {
                    Feel.Tap => "6",
                    Feel.Move => "8",
                    Feel.Merge => "14",
                    Feel.Big => "28",
                    Feel.Huge => "40,60,70",
                    Feel.Blocked => "8,40,8",
                    Feel.Win => "30,60,30,60,90",
                    _ => "60,80,120",
                };
                ZenithVibrate(p);
#endif
            }
            catch (Exception)
            {
                // no haptics here
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static void Android(Feel kind)
        {
            if (never) Handheld.Vibrate();
            if (vibrator == null)
            {
                using var version = new AndroidJavaClass("android.os.Build$VERSION");
                sdk = version.GetStatic<int>("SDK_INT");
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            }
            if (vibrator == null || !vibrator.Call<bool>("hasVibrator")) return;
            if (sdk >= 29 && kind is Feel.Tap or Feel.Move or Feel.Merge or Feel.Blocked or Feel.Big)
            {
                // the phone's own tuned clicks feel crisper than a raw buzz
                int effect = kind switch { Feel.Tap => 2, Feel.Move => 2, Feel.Merge => 0, Feel.Blocked => 2, _ => 5 };
                using var fx = new AndroidJavaClass("android.os.VibrationEffect");
                using var e = fx.CallStatic<AndroidJavaObject>("createPredefined", effect);
                vibrator.Call("vibrate", e);
                return;
            }
            long[] timings;
            int[] amps;
            switch (kind)
            {
                case Feel.Huge:
                    timings = new long[] { 0, 40, 60, 70 };
                    amps = new[] { 0, 255, 0, 200 };
                    break;
                case Feel.Win:
                    timings = new long[] { 0, 30, 60, 30, 60, 90 };
                    amps = new[] { 0, 180, 0, 200, 0, 255 };
                    break;
                case Feel.Lose:
                    timings = new long[] { 0, 60, 80, 120 };
                    amps = new[] { 0, 160, 0, 120 };
                    break;
                case Feel.Big:
                    timings = new long[] { 0, 28 };
                    amps = new[] { 0, 230 };
                    break;
                default:
                    timings = new long[] { 0, kind == Feel.Merge ? 14 : 8 };
                    amps = new[] { 0, kind == Feel.Merge ? 160 : 90 };
                    break;
            }
            if (sdk >= 26)
            {
                using var fx = new AndroidJavaClass("android.os.VibrationEffect");
                using var e = fx.CallStatic<AndroidJavaObject>("createWaveform", timings, amps, -1);
                vibrator.Call("vibrate", e);
            }
            else vibrator.Call("vibrate", timings, -1);
        }
#endif

        /// <summary>The back button on the title screen sends the app to the background, like other Android apps.</summary>
        public static void Minimize()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                activity.Call<bool>("moveTaskToBack", true);
            }
            catch (Exception)
            {
                // nothing to do
            }
#endif
        }

        /// <summary>Opens the phone's share sheet, or copies the text in a browser that has none.</summary>
        public static void Share(string text)
        {
            try
            {
#if UNITY_IOS && !UNITY_EDITOR
                ZenithShare(text, Url);
#elif UNITY_ANDROID && !UNITY_EDITOR
                using var intentClass = new AndroidJavaClass("android.content.Intent");
                using var intent = new AndroidJavaObject("android.content.Intent");
                intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));
                intent.Call<AndroidJavaObject>("setType", "text/plain");
                intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), $"{text} {Url}");
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "Share Zenith");
                activity.Call("startActivity", chooser);
#elif UNITY_WEBGL && !UNITY_EDITOR
                ZenithShare("Zenith", text, Url);
#else
                GUIUtility.systemCopyBuffer = $"{text} {Url}";
#endif
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Share failed: {e.Message}");
            }
        }
    }
}
