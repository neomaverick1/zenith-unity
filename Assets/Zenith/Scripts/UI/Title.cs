// The title screen: the logo, a game playing itself on the horizon, and the ways in.
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Zenith
{
    public class Title
    {
        public readonly VisualElement root;
        readonly VisualElement horizon, mark, logo;
        BoardView demo;
        Game demoGame;
        float nextMove;
        readonly bool showDemo;

        public static VisualElement Logo(string tag, out VisualElement mark)
        {
            mark = Kit.El("mark");
            mark.style.backgroundImage = new StyleBackground(Kit.IconTex("star"));
            return Kit.El("logo", mark, Kit.Txt("ZENITH", "logo-title", Face.Display), Kit.Txt(tag, "tag", Face.Bold));
        }

        public Title(Action<Mode> onPlay, Action<string> onSheet)
        {
            var settings = Store.settings;
            var stats = Store.stats;
            var saved = Store.SavedGame(Mode.Classic);
            var daily = Store.SavedGame(Mode.Daily);
            string today = Store.TodayKey();
            long dailyBest = stats.dailyBest[today];

            logo = Logo("Fuse the stars", out mark);
            var top = Kit.El(null, logo);
            top.style.paddingTop = Length.Percent(0);
            horizon = Kit.El("horizon");
            horizon.pickingMode = PickingMode.Ignore;

            void Go(Mode m)
            {
                Sound.I.Click();
                Native.Haptic(Feel.Tap);
                onPlay(m);
            }
            void Tap(string s)
            {
                Sound.I.Click();
                Native.Haptic(Feel.Tap);
                onSheet(s);
            }

            bool cont = saved != null && saved.moves > 0;
            var play = Kit.Btn("btn primary play-big wide", () => Go(Mode.Classic), Kit.Icon("play"), Kit.Txt(cont ? "Continue" : "Play", "btn-label", Face.Bold)).Fill(Kit.GoldFill);
            if (cont) play.Add(Kit.Txt($"· {Stars.Fmt(saved.score)}", "btn-small", Face.Bold));

            string dailySub = dailyBest > 0 ? $"Today’s best {Stars.Fmt(dailyBest)}" : daily != null && daily.day == today ? "In progress" : "Same board for everyone";
            var dailyBtn = Kit.Btn("mode", () => Go(Mode.Daily), Kit.Icon("daily"), Kit.Txt("Daily", "mode-name", Face.Bold), Kit.Txt(dailySub, "mode-sub"));
            var zenBtn = Kit.Btn("mode", () => Go(Mode.Zen), Kit.Icon("zen"), Kit.Txt("Zen", "mode-name", Face.Bold), Kit.Txt("Endless rewinds, calm music", "mode-sub"));
            var row = Kit.El("mode-row", dailyBtn, zenBtn);
            Layer.Next(row);
            var modes = Kit.El("modes", play, row);

            var foot = Kit.El("title-foot",
                Kit.Btn("icon-btn", () => Tap("howto"), Kit.Icon("help")),
                Kit.Btn("icon-btn", () => Tap("stats"), Kit.Icon("records")),
                Kit.Btn("icon-btn", () => Tap("settings"), Kit.Icon("settings")));

            root = Kit.El("title", top, horizon, modes, foot);
#if UNITY_WEBGL && !UNITY_EDITOR
            // in a browser, point at the Android app
            var get = Kit.Btn("get-app", () => Application.OpenURL("https://github.com/neomaverick1/zenith-unity/releases/latest"), Kit.Icon("download"), Kit.Txt("Get the Android app", "get-app-text", Face.Bold));
            root.Add(get);
#endif
            root.RegisterCallback<GeometryChangedEvent>(_ => top.style.paddingTop = root.layout.height * 0.06f);
            horizon.RegisterCallback<GeometryChangedEvent>(_ => PlaceDemo());

            Kit.Rise(mark, 0);
            Kit.Rise(logo[1], 0.12f);
            Kit.Rise(logo[2], 0.24f);
            Kit.Rise(modes, 0.36f);
            Kit.Rise(foot, 0.48f);

            showDemo = !settings.reduceMotion;
            if (showDemo)
            {
                demo = BoardView.Create("Demo");
                demo.alpha = 0;
                demoGame = Engine.NewGame(Mode.Classic, 4, 2048);
                demo.Layout(Vector3.zero, 1, 4);
                demo.Show(demoGame, 0, false, true);
                nextMove = Time.unscaledTime + 1.2f;
                Tw.Run(1.2f, Ease.Out, t => { if (demo) demo.alpha = 0.6f * t; }, 0.3f, null, "demo-in");
            }
        }

        void PlaceDemo()
        {
            if (!demo) return;
            var r = horizon.worldBound;
            if (r.width <= 0 || r.height <= 0) return;
            // as wide as fits on the horizon, tilted back into the distance
            float side = Mathf.Min(r.width * 0.72f, 300, r.height * 0.9f);
            var slot = new Rect(r.center.x - side / 2, r.center.y - side / 2 + side * 0.04f, side, side);
            var (c, w) = UI.ToWorld(slot);
            demo.Layout(c, w, 4, Quaternion.Euler(44, 0, 0));
        }

        public void Update()
        {
            // the star twinkles: it shrinks and turns a little, then opens up again
            float k = Time.unscaledTime % 3.2f / 3.2f;
            float e = 0.5f - 0.5f * Mathf.Cos(k * Mathf.PI * 2);
            if (!Store.settings.reduceMotion)
            {
                mark.Size(1 - 0.14f * e);
                mark.Spin(45 * e);
            }
            if (!demo || Time.unscaledTime < nextMove) return;
            nextMove = Time.unscaledTime + 0.72f;
            if (demoGame.over || demoGame.best >= 1024) demoGame = Engine.NewGame(Mode.Classic, 4);
            else
            {
                var d = Autoplay.BestMove(demoGame);
                demoGame = d.HasValue ? Engine.Move(demoGame, d.Value).game : Engine.NewGame(Mode.Classic, 4);
            }
            demo.Show(demoGame, 0.11f);
        }

        public void Leave(Action done)
        {
            Tw.Run(0.32f, x => x, t =>
            {
                root.Fade(1 - t);
                root.Size(1 + 0.04f * t);
                if (demo) demo.alpha = 0.6f * (1 - t);
            }, 0, done, root);
        }

        public void Destroy()
        {
            Tw.Kill("demo-in");
            root.RemoveFromHierarchy();
            if (demo) UnityEngine.Object.Destroy(demo.gameObject);
            demo = null;
        }
    }
}
