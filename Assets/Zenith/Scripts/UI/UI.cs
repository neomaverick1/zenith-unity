// The interface's frame: one panel over the 3D scene, sized in layout points so it's the same physical size on
// every screen, kept clear of notches and rounded corners, with layers for screens, dialogs and toasts. It also
// tells the 3D scene where interface slots sit on screen, so the board lands exactly in its place.
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UIElements;

namespace Zenith
{
    public class UI : MonoBehaviour
    {
        public static UI I { get; private set; }

        /// <summary>Screen pixels per layout point.</summary>
        public static float Scale { get; private set; } = 1;

        UIDocument doc;
        PanelSettings settings;
        public VisualElement Root { get; private set; }
        public VisualElement Safe { get; private set; }
        public VisualElement Screens { get; private set; }
        public VisualElement Overlays { get; private set; }
        public VisualElement Toasts { get; private set; }
        Vector2Int lastScreen;
        Rect lastSafe;
        public event Action Resized;

        public static void Create()
        {
            if (I) return;
            var go = new GameObject("UI");
            DontDestroyOnLoad(go);
            I = go.AddComponent<UI>();
        }

        void Awake()
        {
            if (!FindAnyObjectByType<EventSystem>())
            {
                var es = new GameObject("EventSystem");
                es.transform.SetParent(transform, false);
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
            }
            settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.name = "Zenith Panel";
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/ZenithTheme");
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.clearColor = false;
            settings.sortingOrder = 10;
            Scale = ComputeScale();
            settings.scale = Scale;
            doc = gameObject.AddComponent<UIDocument>();
            doc.panelSettings = settings;
            Root = doc.rootVisualElement;
            Root.styleSheets.Add(Resources.Load<StyleSheet>("UI/zenith"));
            Root.AddToClassList("root");
            Root.Font(Face.Ui);
            Root.pickingMode = PickingMode.Ignore;
            Safe = Kit.El("layer");
            Safe.pickingMode = PickingMode.Ignore;
            Root.Add(Safe);
            Screens = Kit.El("layer");
            Screens.pickingMode = PickingMode.Ignore;
            Overlays = Kit.El("layer");
            Overlays.pickingMode = PickingMode.Ignore;
            Toasts = Kit.El("layer");
            Toasts.pickingMode = PickingMode.Ignore;
            Safe.Add(Screens);
            Root.Add(Overlays);
            Root.Add(Toasts);
            ApplySafeArea();
        }

        static float ComputeScale()
        {
            float s = Native.PixelRatio();
            // a fixed density for test runs: -zenithScale 2.5
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-zenithScale" && float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var forced))
                    return forced;
            // never so big that the layout gets cramped
            s = Mathf.Min(s, Screen.width / 360f, Screen.height / 560f);
            return Mathf.Max(0.5f, s);
        }

        void ApplySafeArea()
        {
            var sa = Screen.safeArea;
            Safe.style.paddingLeft = sa.x / Scale;
            Safe.style.paddingRight = (Screen.width - sa.xMax) / Scale;
            Safe.style.paddingTop = (Screen.height - sa.yMax) / Scale;
            Safe.style.paddingBottom = sa.y / Scale;
            lastSafe = sa;
            lastScreen = new Vector2Int(Screen.width, Screen.height);
        }

        public float SafeTop => (Screen.height - Screen.safeArea.yMax) / Scale;
        public float SafeBottom => Screen.safeArea.y / Scale;

        void Update()
        {
            if (lastScreen.x != Screen.width || lastScreen.y != Screen.height || lastSafe != Screen.safeArea)
            {
                Scale = ComputeScale();
                settings.scale = Scale;
                ApplySafeArea();
                Resized?.Invoke();
            }
        }

        /// <summary>Where an interface rectangle (in layout points) lands on the board's plane: its center and width.</summary>
        public static (Vector3 center, float width) ToWorld(Rect r)
        {
            var stage = Stage.I;
            var c = new Vector2(r.center.x * Scale, Screen.height - r.center.y * Scale);
            return (stage.ScreenToWorld(c), r.width * Scale * stage.WorldPerPixel);
        }

        /// <summary>A point in the scene, in layout points from the top left.</summary>
        public static Vector2 FromWorld(Vector3 world)
        {
            var s = Stage.I.WorldToScreen(world);
            return new Vector2(s.x / Scale, (Screen.height - s.y) / Scale);
        }

        /// <summary>World units per layout point on the board's plane.</summary>
        public static float Px => Stage.I.WorldPerPixel * Scale;

        // ---- toasts ---------------------------------------------------------------------------------------------------

        readonly System.Collections.Generic.Queue<Achievement> pending = new System.Collections.Generic.Queue<Achievement>();
        bool toasting;

        public void Toast(Achievement a)
        {
            pending.Enqueue(a);
            if (!toasting) NextToast();
        }

        void NextToast()
        {
            if (pending.Count == 0)
            {
                toasting = false;
                return;
            }
            toasting = true;
            var a = pending.Dequeue();
            var pill = Kit.El("toast-pill",
                Kit.Icon("award"),
                Kit.El(null, Kit.Txt(a.name, "toast-name", Face.Bold), Kit.Txt(a.how, "toast-how"))).Fill(Kit.ToastFill);
            var t = Kit.El("toast", pill);
            t.pickingMode = PickingMode.Ignore;
            t.style.top = 72 + SafeTop;
            Toasts.Add(t);
            const float dur = 3f;
            Tw.Run(dur, x => x, k =>
            {
                float a1, y, s = 1;
                if (k < 0.1f)
                {
                    float e = Ease.Out(k / 0.1f);
                    a1 = e;
                    y = -30 * (1 - e);
                    s = 0.9f + 0.1f * e;
                }
                else if (k < 0.85f)
                {
                    a1 = 1;
                    y = 0;
                }
                else
                {
                    float e = Ease.Out((k - 0.85f) / 0.15f);
                    a1 = 1 - e;
                    y = -20 * e;
                }
                pill.Fade(a1);
                pill.Move(0, y);
                pill.Size(s);
            }, 0, () =>
            {
                t.RemoveFromHierarchy();
                NextToast();
            });
        }
    }
}
