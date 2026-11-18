// Builds the game for each platform from the command line.
//   Unity -batchmode -quit -projectPath . -executeMethod Zenith.EditorTools.Builder.Android
// Android signing comes from ZENITH_KEYSTORE (a path), ZENITH_KEYSTORE_PASS, ZENITH_KEY_ALIAS and ZENITH_KEY_PASS
// when they're set; otherwise the debug key signs it.
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Zenith.EditorTools
{
    public static class Builder
    {
        static string[] Scenes => new[] { "Assets/Scenes/Main.unity" };

        static string Version()
        {
            var v = Environment.GetEnvironmentVariable("ZENITH_VERSION");
            return string.IsNullOrEmpty(v) ? PlayerSettings.bundleVersion : v;
        }

        static void Run(BuildTarget target, BuildTargetGroup group, string path, BuildOptions options = BuildOptions.None)
        {
            PlayerSettings.bundleVersion = Version();
            var parts = Version().Split('.');
            if (parts.Length == 3 && int.TryParse(parts[2], out int patch))
            {
                PlayerSettings.Android.bundleVersionCode = 1000 + patch;
                PlayerSettings.iOS.buildNumber = (1000 + patch).ToString();
            }
            if (EditorUserBuildSettings.activeBuildTarget != target) EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = path,
                target = target,
                targetGroup = group,
                options = options,
            });
            var s = report.summary;
            Debug.Log($"[build] {target}: {s.result}, {s.totalSize / 1048576f:0.0} MB, {s.totalTime.TotalSeconds:0}s, {s.totalErrors} errors");
            if (s.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        public static void Linux() => Run(BuildTarget.StandaloneLinux64, BuildTargetGroup.Standalone, "Builds/Linux/Zenith.x86_64");

        static void Sign()
        {
            var ks = Environment.GetEnvironmentVariable("ZENITH_KEYSTORE");
            if (string.IsNullOrEmpty(ks) || !File.Exists(ks))
            {
                PlayerSettings.Android.useCustomKeystore = false;
                return;
            }
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = ks;
            PlayerSettings.Android.keystorePass = Environment.GetEnvironmentVariable("ZENITH_KEYSTORE_PASS");
            PlayerSettings.Android.keyaliasName = Environment.GetEnvironmentVariable("ZENITH_KEY_ALIAS");
            PlayerSettings.Android.keyaliasPass = Environment.GetEnvironmentVariable("ZENITH_KEY_PASS");
        }

        public static void Android()
        {
            Sign();
            EditorUserBuildSettings.buildAppBundle = false;
            Run(BuildTarget.Android, BuildTargetGroup.Android, "Builds/Android/Zenith.apk");
        }

        public static void AndroidBundle()
        {
            Sign();
            EditorUserBuildSettings.buildAppBundle = true;
            Run(BuildTarget.Android, BuildTargetGroup.Android, "Builds/Android/Zenith-Play.aab");
            EditorUserBuildSettings.buildAppBundle = false;
        }

        public static void IOS() => Run(BuildTarget.iOS, BuildTargetGroup.iOS, "Builds/iOS");

        public static void Web()
        {
            PlayerSettings.WebGL.template = "PROJECT:Zenith";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;
            Run(BuildTarget.WebGL, BuildTargetGroup.WebGL, "Builds/Web");
        }
    }
}
