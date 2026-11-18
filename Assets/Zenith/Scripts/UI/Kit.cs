// The building blocks of the interface: elements with classes, icons, gradient fills, buttons that press in,
// switches, sliders and segmented pickers, and a small animation runner that every screen uses.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Zenith
{
    public enum Face { Ui, Bold, Display }

    public static class Tw
    {
        class Job
        {
            public float start, dur;
            public Func<float, float> ease;
            public Action<float> set;
            public Action done;
            public object key;
            public bool begun;
        }

        static readonly List<Job> jobs = new List<Job>();
        static readonly List<Job> adding = new List<Job>();
        public static float Now => Time.unscaledTime;

        /// <summary>Runs set(0..1) over `dur` seconds after `delay`, shaped by `ease`. A key replaces any running job with the same key.</summary>
        public static void Run(float dur, Func<float, float> ease, Action<float> set, float delay = 0, Action done = null, object key = null)
        {
            if (key != null) Kill(key);
            var j = new Job { start = Now + delay, dur = Mathf.Max(0.0001f, dur), ease = ease ?? (t => t), set = set, done = done, key = key };
            adding.Add(j);
            if (delay <= 0)
            {
                j.begun = true;
                set(j.ease(0));
            }
        }

        public static void After(float delay, Action a, object key = null) => Run(0.0001f, null, _ => { }, delay, a, key);

        public static void Kill(object key)
        {
            if (key == null) return;
            jobs.RemoveAll(j => Equals(j.key, key));
            adding.RemoveAll(j => Equals(j.key, key));
        }

        public static void Update()
        {
            if (adding.Count > 0)
            {
                jobs.AddRange(adding);
                adding.Clear();
            }
            float now = Now;
            for (int i = 0; i < jobs.Count; i++)
            {
                var j = jobs[i];
                if (now < j.start) continue;
                float t = Mathf.Clamp01((now - j.start) / j.dur);
                j.begun = true;
                try
                {
                    j.set(j.ease(t));
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    t = 1;
                }
                if (t >= 1)
                {
                    jobs.RemoveAt(i--);
                    try
                    {
                        j.done?.Invoke();
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            }
        }
    }

    public static class Kit
    {
        static readonly Dictionary<string, Texture2D> icons = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Texture2D> gradients = new Dictionary<string, Texture2D>();
        static Font ui, bold, display;

        public static readonly Color Ink = Stars.Hex("#eef0ff");
        public static readonly Color Ink2 = new Color(226 / 255f, 230 / 255f, 1f, 0.74f);
        public static readonly Color Ink3 = new Color(226 / 255f, 230 / 255f, 1f, 0.5f);
        public static readonly Color Gold = Stars.Hex("#ffcf6e");
        public static readonly Color Gold2 = Stars.Hex("#ff9d4a");
        public static readonly Color Ice = Stars.Hex("#9fd3ff");
        public static readonly Color Danger = Stars.Hex("#ff8293");

        public static void LoadFonts()
        {
            ui = Resources.Load<Font>("Fonts/Manrope-SemiBold");
            bold = Resources.Load<Font>("Fonts/Manrope-ExtraBold");
            display = Resources.Load<Font>("Fonts/Unbounded-Bold");
        }

        public static T Font<T>(this T e, Face f) where T : VisualElement
        {
            var font = f == Face.Display ? display : f == Face.Bold ? bold : ui;
            if (font) e.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(font));
            return e;
        }

        public static VisualElement El(string classes = null, params VisualElement[] kids)
        {
            var e = new VisualElement();
            Classes(e, classes);
            foreach (var k in kids)
                if (k != null)
                    e.Add(k);
            return e;
        }

        public static Label Txt(string text, string classes = null, Face face = Face.Ui)
        {
            var l = new Label(text);
            Classes(l, classes);
            l.pickingMode = PickingMode.Ignore;
            return l.Font(face);
        }

        public static void Classes(VisualElement e, string classes)
        {
            if (string.IsNullOrEmpty(classes)) return;
            foreach (var c in classes.Split(' '))
                if (c.Length > 0)
                    e.AddToClassList(c);
        }

        public static Texture2D IconTex(string name)
        {
            if (!icons.TryGetValue(name, out var t))
            {
                t = Resources.Load<Texture2D>($"Icons/{name}");
                icons[name] = t;
            }
            return t;
        }

        public static VisualElement Icon(string name, string classes = "icon", Color? tint = null)
        {
            var e = El(classes);
            e.pickingMode = PickingMode.Ignore;
            var t = IconTex(name);
            if (t) e.style.backgroundImage = new StyleBackground(t);
            if (tint.HasValue) e.style.unityBackgroundImageTintColor = tint.Value;
            return e;
        }

        /// <summary>A gradient texture from color stops (0 to 1), top to bottom, or left to right.</summary>
        public static Texture2D Gradient(string key, bool horizontal, params (float at, Color c)[] stops)
        {
            if (gradients.TryGetValue(key, out var tex)) return tex;
            const int N = 64;
            tex = new Texture2D(horizontal ? N : 2, horizontal ? 2 : N, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = key,
            };
            for (int i = 0; i < N; i++)
            {
                float t = i / (N - 1f);
                if (!horizontal) t = 1 - t; // textures run bottom to top
                Color c = stops[0].c;
                for (int s = 1; s < stops.Length; s++)
                    if (t <= stops[s].at)
                    {
                        float k = Mathf.InverseLerp(stops[s - 1].at, stops[s].at, t);
                        c = Color.Lerp(stops[s - 1].c, stops[s].c, k);
                        break;
                    }
                    else c = stops[s].c;
                for (int j = 0; j < 2; j++)
                {
                    if (horizontal) tex.SetPixel(i, j, c);
                    else tex.SetPixel(j, i, c);
                }
            }
            tex.Apply(false, true);
            gradients[key] = tex;
            return tex;
        }

        public static T Fill<T>(this T e, Texture2D tex) where T : VisualElement
        {
            e.style.backgroundImage = new StyleBackground(tex);
            return e;
        }

        public static Texture2D GoldFill => Gradient("gold", false, (0, Stars.Hex("#ffe39a")), (0.45f, Gold), (1, Gold2));
        public static Texture2D CardFill => Gradient("card", false, (0, new Color(30 / 255f, 33 / 255f, 86 / 255f, 0.96f)), (1, new Color(16 / 255f, 18 / 255f, 52 / 255f, 0.97f)));
        public static Texture2D SheetFill => Gradient("sheet", false, (0, Stars.Hex("#1a1d4c")), (1, Stars.Hex("#10122f")));
        public static Texture2D ChainFill => Gradient("chain", false, (0, Stars.Hex("#ffe7a8")), (1, Gold2));
        public static Texture2D ToastFill => Gradient("toast", false, (0, new Color(48 / 255f, 40 / 255f, 30 / 255f, 0.96f)), (1, new Color(28 / 255f, 22 / 255f, 18 / 255f, 0.96f)));
        public static Texture2D SwitchFill => Gradient("switch", true, (0, Gold2), (1, Gold));
        public static Texture2D AchFill => Gradient("ach", false, (0, new Color(1, 207 / 255f, 110 / 255f, 0.14f)), (1, new Color(1, 157 / 255f, 74 / 255f, 0.05f)));

        public static Texture2D StarFill(int v)
        {
            var s = Stars.Of(v);
            return Gradient($"star{v}", false, (0, s.from), (1, s.to));
        }

        // ---- transforms ---------------------------------------------------------------------------------------------

        public static void Move(this VisualElement e, float x, float y) => e.style.translate = new StyleTranslate(new Translate(x, y));

        public static void Size(this VisualElement e, float s) => e.style.scale = new StyleScale(new Scale(new Vector3(s, s, 1)));

        public static void Spin(this VisualElement e, float deg) => e.style.rotate = new StyleRotate(new Rotate(Angle.Degrees(deg)));

        public static void Fade(this VisualElement e, float a) => e.style.opacity = a;

        /// <summary>Rises into place: up from below, out of a slight shrink, fading in.</summary>
        public static void Rise(VisualElement e, float delay = 0, float dur = 0.9f, float dist = 18)
        {
            e.Fade(0);
            e.Move(0, dist);
            Tw.Run(dur, Ease.Out, t =>
            {
                e.Fade(t);
                e.Move(0, dist * (1 - t));
                e.Size(0.98f + 0.02f * t);
            }, delay, null, e);
        }

        public static void Show(this VisualElement e, bool on) => e.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;

        // ---- controls -----------------------------------------------------------------------------------------------

        /// <summary>A button: anything you can tap. It presses in under your finger and plays a click.</summary>
        public static VisualElement Btn(string classes, Action onTap, params VisualElement[] kids)
        {
            var b = El("btn-base " + classes, kids);
            b.pickingMode = PickingMode.Position;
            bool down = false;
            float pressed = classes != null && classes.Contains("icon-btn") ? 0.92f : classes != null && classes.Contains("mode") ? 0.97f : 0.96f;
            b.RegisterCallback<PointerDownEvent>(e =>
            {
                if (!b.enabledSelf) return;
                down = true;
                b.CapturePointer(e.pointerId);
                Tw.Run(0.16f, Ease.Out, t => b.Size(Mathf.Lerp(1, pressed, t)), 0, null, b);
                e.StopPropagation();
            });
            void Release(IPointerEvent e, bool fire)
            {
                if (!down) return;
                down = false;
                if (b.HasPointerCapture(e.pointerId)) b.ReleasePointer(e.pointerId);
                float from = b.resolvedStyle.scale.value.x;
                Tw.Run(0.2f, Ease.Out, t => b.Size(Mathf.Lerp(from, 1, t)), 0, null, b);
                if (fire && b.enabledSelf && b.worldBound.Contains(e.position)) onTap?.Invoke();
            }
            b.RegisterCallback<PointerUpEvent>(e =>
            {
                Release(e, true);
                e.StopPropagation();
            });
            b.RegisterCallback<PointerCancelEvent>(e => Release(e, false));
            b.RegisterCallback<PointerCaptureOutEvent>(_ =>
            {
                if (!down) return;
                down = false;
                Tw.Run(0.2f, Ease.Out, t => b.Size(Mathf.Lerp(pressed, 1, t)), 0, null, b);
            });
            return b;
        }

        public static void Enable(this VisualElement e, bool on)
        {
            e.SetEnabled(on);
            e.EnableInClassList("disabled", !on);
        }

        public class Switch
        {
            public readonly VisualElement root;
            readonly VisualElement knob;
            bool on;

            public Switch(bool value, Action<bool> changed)
            {
                knob = El("switch-knob");
                root = Btn("switch", () =>
                {
                    Set(!on, true);
                    Sound.I?.Toggle(on);
                    Native.Haptic(Feel.Tap);
                    changed(on);
                }, knob);
                Set(value, false);
            }

            public void Set(bool value, bool animate)
            {
                bool was = on;
                on = value;
                root.EnableInClassList("on", on);
                root.style.backgroundImage = on ? new StyleBackground(SwitchFill) : new StyleBackground(StyleKeyword.None);
                float from = was ? 20 : 0, to = on ? 20 : 0;
                if (!animate) knob.Move(to, 0);
                else Tw.Run(0.3f, Ease.Spring, t => knob.Move(Mathf.LerpUnclamped(from, to, t), 0), 0, null, knob);
            }
        }

        public class Slider
        {
            public readonly VisualElement root;
            readonly VisualElement track, fill, thumb;
            readonly Label output;
            float value;
            readonly Action<float> changed;
            readonly Action released;

            public Slider(float v, Action<float> changed, Action released = null)
            {
                this.changed = changed;
                this.released = released;
                fill = El("slider-fill");
                thumb = El("slider-thumb");
                track = El("slider-track", fill, thumb);
                var hit = El("slider-hit", track);
                output = Txt("", "slider-out", Face.Bold);
                root = El("slider", hit, output);
                hit.pickingMode = PickingMode.Position;
                bool drag = false;
                hit.RegisterCallback<PointerDownEvent>(e =>
                {
                    drag = true;
                    hit.CapturePointer(e.pointerId);
                    From(e.localPosition.x, hit);
                    e.StopPropagation();
                });
                hit.RegisterCallback<PointerMoveEvent>(e =>
                {
                    if (drag) From(e.localPosition.x, hit);
                });
                hit.RegisterCallback<PointerUpEvent>(e =>
                {
                    if (!drag) return;
                    drag = false;
                    hit.ReleasePointer(e.pointerId);
                    this.released?.Invoke();
                    e.StopPropagation();
                });
                hit.RegisterCallback<GeometryChangedEvent>(_ => Draw());
                Set(v);
            }

            void From(float x, VisualElement hit)
            {
                float w = Mathf.Max(1, hit.layout.width - 22);
                Set(Mathf.Clamp01((x - 11) / w));
                changed(value);
            }

            public void Set(float v)
            {
                value = Mathf.Round(Mathf.Clamp01(v) * 100) / 100f;
                output.text = Mathf.RoundToInt(value * 100).ToString();
                Draw();
            }

            void Draw()
            {
                float w = track.layout.width;
                if (float.IsNaN(w) || w <= 0) w = 120;
                fill.style.width = Length.Percent(value * 100);
                thumb.style.left = (w - 22) * value;
            }
        }

        public class Seg<T>
        {
            public readonly VisualElement root;
            readonly List<(T v, VisualElement b)> options = new List<(T, VisualElement)>();

            public Seg(T value, (T v, string text)[] opts, Action<T> changed)
            {
                root = El("seg");
                foreach (var (v, text) in opts)
                {
                    var val = v;
                    VisualElement b = null;
                    b = Btn("seg-btn", () =>
                    {
                        Sound.I?.Click();
                        Native.Haptic(Feel.Tap);
                        Set(val);
                        changed(val);
                    }, Txt(text, "seg-text", Face.Bold));
                    options.Add((val, b));
                    root.Add(b);
                }
                Set(value);
            }

            public void Set(T value)
            {
                foreach (var (v, b) in options) b.EnableInClassList("on", EqualityComparer<T>.Default.Equals(v, value));
            }
        }

        /// <summary>A number that counts up to its new value instead of jumping.</summary>
        public class Counter
        {
            public readonly Label label;
            long shown, target;

            public Counter(Label l, long v)
            {
                label = l;
                shown = target = v;
                l.text = Stars.Fmt(v);
            }

            public void Set(long v, float dur = 0.38f, bool instant = false)
            {
                if (v == target && !instant) return;
                long from = shown;
                target = v;
                if (instant || dur <= 0)
                {
                    Tw.Kill(this);
                    shown = v;
                    label.text = Stars.Fmt(v);
                    return;
                }
                Tw.Run(dur, t => 1 - Mathf.Pow(1 - t, 3), t =>
                {
                    shown = (long)Math.Round(from + (v - from) * (double)t);
                    label.text = Stars.Fmt(shown);
                }, 0, null, this);
            }
        }

        /// <summary>A star tile drawn by the interface, for cards and lessons.</summary>
        public static VisualElement UiTile(int v, float cell, string classes = "ui-tile")
        {
            var s = Stars.Of(v);
            var t = El(classes);
            t.style.width = cell;
            t.style.height = cell;
            t.style.borderTopLeftRadius = t.style.borderTopRightRadius = t.style.borderBottomLeftRadius = t.style.borderBottomRightRadius = cell * 0.16f;
            t.Fill(StarFill(v));
            var sheen = El("ui-tile-sheen");
            sheen.style.borderTopLeftRadius = sheen.style.borderTopRightRadius = sheen.style.borderBottomLeftRadius = sheen.style.borderBottomRightRadius = cell * 0.16f;
            t.Add(sheen);
            int digits = v.ToString().Length;
            float fs = cell * (digits <= 2 ? 0.42f : digits == 3 ? 0.35f : digits == 4 ? 0.28f : 0.225f);
            var n = Txt(v.ToString(), "ui-tile-num", Face.Display);
            n.style.fontSize = fs;
            n.style.color = s.ink;
            t.Add(n);
            if (s.hasGlow)
            {
                var g = El("ui-tile-glow");
                g.style.unityBackgroundImageTintColor = new Color(s.glow.r, s.glow.g, s.glow.b, Mathf.Min(1, s.glow.a * 1.4f));
                g.style.backgroundImage = new StyleBackground(GlowTex);
                g.style.left = g.style.top = -cell * 0.45f;
                g.style.right = g.style.bottom = -cell * 0.45f;
                t.Insert(0, g);
            }
            return t;
        }

        static Texture2D glow;

        /// <summary>A soft round glow, white, for tinting.</summary>
        public static Texture2D GlowTex
        {
            get
            {
                if (glow) return glow;
                const int N = 64;
                glow = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "glow" };
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float dx = (x + 0.5f) / N * 2 - 1, dy = (y + 0.5f) / N * 2 - 1;
                        // a rounded square falloff, so it hugs a tile
                        float d = Mathf.Pow(Mathf.Pow(Mathf.Abs(dx), 4) + Mathf.Pow(Mathf.Abs(dy), 4), 0.25f);
                        float a = Mathf.Clamp01(1 - d);
                        a = a * a * (3 - 2 * a);
                        glow.SetPixel(x, y, new Color(1, 1, 1, a));
                    }
                glow.Apply(false, true);
                return glow;
            }
        }
    }
}
