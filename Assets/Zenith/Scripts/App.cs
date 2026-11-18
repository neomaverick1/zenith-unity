// The app: starts everything, shows the splash, moves between the title screen and a game, opens the sheets,
// and handles the phone going to the background and the back button.
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Zenith
{
    public class App : MonoBehaviour
    {
        public static App I { get; private set; }

        enum Screen { Splash, Title, Play }

        Screen screen = Screen.Splash;
        VisualElement splash;
        Title title;
        Play play;
        Layer sheet;
        Mode? pendingMode;
        bool started, leaving;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (I) return;
            var go = new GameObject("Zenith");
            DontDestroyOnLoad(go);
            go.AddComponent<App>();
        }

        void Awake()
        {
            I = this;
            var rate = UnityEngine.Screen.currentResolution.refreshRateRatio.value;
            Application.targetFrameRate = Mathf.Clamp(Mathf.RoundToInt((float)rate), 60, 120);
            QualitySettings.vSyncCount = 0;
            Store.Load();
            Kit.LoadFonts();
            Stage.Create();
            Sound.Create();
            UI.Create();
            Sound.I.SetVolumes(Store.settings.music, Store.settings.sfx);
            ApplyLook();
            UI.I.Resized += () => play?.OnResize();
            ShowSplash();
            gameObject.AddComponent<AutoTest>();
        }

        void ApplyLook()
        {
            var s = Store.settings;
            Stage.I.SetQuality(s.quality, s.reduceMotion);
            if (s.quality == Quality.Saver) Application.targetFrameRate = 30;
            else
            {
                var rate = UnityEngine.Screen.currentResolution.refreshRateRatio.value;
                Application.targetFrameRate = Mathf.Clamp(Mathf.RoundToInt((float)rate), 60, 120);
            }
        }

        // ---- splash ---------------------------------------------------------------------------------------------------

        void ShowSplash()
        {
            var logo = Title.Logo("A 2048 merge puzzle with a living soundtrack", out var mark);
            var hint = Kit.Txt("Tap anywhere to begin · sound on", "hint", Face.Bold);
            splash = Kit.El("layer splash", logo, hint);
            splash.pickingMode = PickingMode.Position;
            splash.RegisterCallback<PointerDownEvent>(_ => Begin());
            UI.I.Screens.Add(splash);
            Kit.Rise(mark, 0);
            Kit.Rise(logo[1], 0.12f);
            Kit.Rise(logo[2], 0.24f);
            Kit.Rise(hint, 0.5f);
            splash.schedule.Execute(() =>
            {
                float k = Time.unscaledTime % 2.4f / 2.4f;
                if (Time.unscaledTime > 1.5f) hint.Fade(0.675f + 0.325f * Mathf.Cos(k * Mathf.PI * 2));
                float e = 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime % 3.2f / 3.2f * Mathf.PI * 2);
                if (!Store.settings.reduceMotion)
                {
                    mark.Size(1 - 0.14f * e);
                    mark.Spin(45 * e);
                }
            }).Every(16);
        }

        public void Begin()
        {
            if (started) return;
            started = true;
            Sound.I.Begin();
            Sound.I.SetVolumes(Store.settings.music, Store.settings.sfx);
            Sound.I.MenuMusic();
            var s = splash;
            Tw.Run(0.6f, x => x, t => s.Fade(1 - t), 0, () =>
            {
                s.RemoveFromHierarchy();
                ShowTitle();
            });
        }

        // ---- screens ----------------------------------------------------------------------------------------------------

        void ShowTitle()
        {
            screen = Screen.Title;
            title = new Title(PlayMode, OpenSheet);
            UI.I.Screens.Add(title.root);
            title.root.Fade(0);
            Tw.Run(0.55f, Ease.Out, t =>
            {
                title.root.Fade(t);
                title.root.Size(0.96f + 0.04f * t);
            });
        }

        public Play CurrentPlay => play;

        public void StartMode(Mode m) => PlayMode(m);

        public void Open(string which) => OpenSheet(which);

        public void ToTitle()
        {
            if (play != null) Quit();
        }

        void PlayMode(Mode m)
        {
            if (leaving) return;
            if (!Store.settings.tutorialDone)
            {
                pendingMode = m;
                OpenSheet("howto");
                return;
            }
            leaving = true;
            title.Leave(() =>
            {
                title.Destroy();
                title = null;
                leaving = false;
                screen = Screen.Play;
                play = new Play(m, Quit, OpenSheet);
                UI.I.Screens.Add(play.root);
                UnityEngine.Screen.sleepTimeout = SleepTimeout.NeverSleep;
            });
        }

        void Quit()
        {
            Sound.I.MenuMusic();
            Stage.I.Sky.SetHeat(0);
            play?.Leave();
            play = null;
            UnityEngine.Screen.sleepTimeout = SleepTimeout.SystemSetting;
            ShowTitle();
        }

        void OpenSheet(string which)
        {
            if (sheet != null)
            {
                // one sheet at a time: swap
                var old = sheet;
                sheet = null;
                old.Dismiss();
            }
            if (play != null) play.Blocked = true;
            switch (which)
            {
                case "settings":
                    sheet = Sheets.Settings(screen == Screen.Play, CloseSheet, () => OpenSheet("howto"), () =>
                    {
                        ApplyLook();
                        play?.Restyle();
                    });
                    break;
                case "stats":
                    sheet = Sheets.Records(CloseSheet);
                    break;
                default:
                    sheet = Sheets.HowTo(() =>
                    {
                        if (!Store.settings.tutorialDone) Store.SetSettings(s => s.tutorialDone = true);
                        CloseSheet();
                        if (pendingMode.HasValue)
                        {
                            var m = pendingMode.Value;
                            pendingMode = null;
                            Tw.After(0.05f, () => PlayMode(m));
                        }
                    });
                    break;
            }
        }

        void CloseSheet()
        {
            sheet = null;
            if (play != null) play.Blocked = false;
        }

        // ---- every frame --------------------------------------------------------------------------------------------

        void Update()
        {
            Tw.Update();
            Store.Tick(Time.unscaledDeltaTime);
            title?.Update();
            play?.Update();
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Back();
            if (screen == Screen.Splash && !started && kb != null && kb.anyKey.wasPressedThisFrame) Begin();
        }

        /// <summary>Escape, or the phone's back button.</summary>
        void Back()
        {
            if (Layer.CloseTop()) return;
            if (play != null && play.Back()) return;
            if (screen == Screen.Title) Native.Minimize();
        }

        void OnApplicationPause(bool paused)
        {
            if (!paused) return;
            play?.Pause();
            Store.Flush();
        }

        void OnApplicationFocus(bool focus)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!focus) play?.Pause();
#endif
            if (!focus) Store.Flush();
        }

        void OnApplicationQuit() => Store.Flush();
    }
}
