// One-time project setup, run from the command line: the text resources, font atlases, materials, the scene,
// the app's identity and icons. Safe to run again; it rebuilds what it makes.
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Zenith.EditorTools
{
    public static class Setup
    {
        const string Root = "Assets/Zenith";
        public const string Id = "com.neomaverick.zenithunity";

        /// <summary>Step 1: bring in TextMeshPro's shaders and settings.</summary>
        public static void Import()
        {
            if (!Directory.Exists("Assets/TextMesh Pro"))
            {
                var pkg = Directory.GetFiles("Library/PackageCache", "TMP Essential Resources.unitypackage", SearchOption.AllDirectories);
                if (pkg.Length == 0) throw new System.Exception("TextMeshPro resources not found");
                AssetDatabase.ImportPackage(pkg[0], false);
            }
            AssetDatabase.Refresh();
            Debug.Log("[setup] TextMeshPro resources imported");
        }

        /// <summary>Step 2: everything else.</summary>
        public static void Run()
        {
            Fonts();
            Materials();
            Scene();
            Player();
            AssetDatabase.SaveAssets();
            Debug.Log("[setup] done");
        }

        static void FontAsset(string ttf, string name, string chars)
        {
            string path = $"{Root}/Resources/Fonts/{name}.asset";
            AssetDatabase.DeleteAsset(path);
            var font = AssetDatabase.LoadAssetAtPath<Font>($"{Root}/Resources/Fonts/{ttf}");
            var fa = TMP_FontAsset.CreateFontAsset(font, 96, 10, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            fa.name = name;
            fa.TryAddCharacters(chars, out string missing);
            if (!string.IsNullOrEmpty(missing)) Debug.LogWarning($"[setup] {name} is missing: {missing}");
            AssetDatabase.CreateAsset(fa, path);
            for (int i = 0; i < fa.atlasTextures.Length; i++)
            {
                fa.atlasTextures[i].name = $"{name} Atlas {i}";
                AssetDatabase.AddObjectToAsset(fa.atlasTextures[i], fa);
            }
            fa.material.name = $"{name} Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            Debug.Log($"[setup] font {name}: {fa.characterTable.Count} characters");
        }

        static void Fonts()
        {
            const string digits = "0123456789,+×·";
            const string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz ’'-";
            FontAsset("Unbounded-Bold.ttf", "Unbounded-Bold SDF", digits + letters);
            FontAsset("Manrope-ExtraBold.ttf", "Manrope-ExtraBold SDF", digits + letters);
            FontAsset("ZenithMarks.ttf", "ZenithMarks SDF", "·△▲◇◆○●□■✦✧◉✺❂");
        }

        static void Materials()
        {
            foreach (var (shader, name) in new[] { ("Zenith/Tile", "Tile"), ("Zenith/Board", "Board"), ("Zenith/Nebula", "Nebula"), ("Zenith/Backdrop", "Backdrop"), ("Zenith/Spark", "Spark") })
            {
                string path = $"{Root}/Resources/Materials/{name}.mat";
                var s = Shader.Find(shader);
                if (!s) throw new System.Exception($"Shader {shader} didn't compile");
                var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (existing)
                {
                    existing.shader = s;
                    EditorUtility.SetDirty(existing);
                }
                else AssetDatabase.CreateAsset(new Material(s) { name = name }, path);
            }
        }

        static void Scene()
        {
            // the app builds itself from code, so the scene is empty
            const string path = "Assets/Scenes/Main.unity";
            if (!File.Exists(path))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, path);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(path, true) };
        }

        static void Player()
        {
            PlayerSettings.companyName = "Randy Kundinger";
            PlayerSettings.productName = "Zenith";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, Id);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, Id);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, Id);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.runInBackground = true;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 1170;
            PlayerSettings.visibleInBackground = true;

            // the splash: Unity's logo on the game's own night sky
            PlayerSettings.SplashScreen.backgroundColor = new Color32(7, 8, 28, 255);
            PlayerSettings.SplashScreen.unityLogoStyle = PlayerSettings.SplashScreen.UnityLogoStyle.LightOnDark;
            PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Dolly;

            // icons
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Art/AppIcon.png");
            var fg = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Art/AppIconForeground.png");
            if (icon)
            {
                PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
                // Android's adaptive icon, when Android build support is installed
                var kind = System.Type.GetType("UnityEditor.Android.AndroidPlatformIconKind, UnityEditor.Android.Extensions")
                    ?.GetProperty("Adaptive", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                    ?.GetValue(null) as PlatformIconKind;
                if (kind != null)
                {
                    var adaptive = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                    foreach (var a in adaptive) a.SetTextures(icon, fg ? fg : icon);
                    PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, adaptive);
                }
            }

            // Android: 64-bit, compiled ahead of time, as Google Play requires
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.preferredInstallLocation = AndroidPreferredInstallLocation.Auto;
            PlayerSettings.Android.renderOutsideSafeArea = true;

            // iPhone
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.iOS.hideHomeButton = false;
            PlayerSettings.iOS.appleEnableAutomaticSigning = false;
            PlayerSettings.statusBarHidden = true;

            // the browser: small downloads that GitHub Pages can serve
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
            if (Directory.Exists("Assets/WebGLTemplates/Zenith")) PlayerSettings.WebGL.template = "PROJECT:Zenith";

            // smaller, faster native code: strip what the game never calls
            foreach (var t in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS, NamedBuildTarget.WebGL })
            {
                PlayerSettings.SetManagedStrippingLevel(t, ManagedStrippingLevel.High);
                PlayerSettings.SetIl2CppCodeGeneration(t, Il2CppCodeGeneration.OptimizeSize);
            }

            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.iOS.buildNumber = "1";
        }
    }
}
