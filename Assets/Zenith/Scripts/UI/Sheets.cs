// The sheets that slide up from the bottom: settings, records and how to play.
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Zenith
{
    public static class Sheets
    {
        static VisualElement Row(string icon, string title, string hint, VisualElement control, bool stack = false)
        {
            var what = Kit.El("what", Kit.Txt(title, "what-title", Face.Bold));
            if (!string.IsNullOrEmpty(hint)) what.Add(Kit.Txt(hint, "what-sub"));
            var row = Kit.El("setting" + (stack ? " stack" : ""), Kit.Icon(icon), what);
            if (control != null) row.Add(Kit.El("control", control));
            return row;
        }

        static VisualElement Group(string title, params VisualElement[] rows)
        {
            var r = Kit.El("rows", rows);
            Layer.Next(r);
            return Kit.El("group", Kit.Txt(title, "group-title", Face.Bold), r);
        }

        static (Layer layer, ScrollView body) Sheet(string title, Action onClose)
        {
            Layer layer = null;
            layer = new Layer(true, onClose);
            layer.panel.Add(Layer.Head(title, () => layer.Close()));
            var body = new ScrollView(ScrollViewMode.Vertical);
            body.AddToClassList("sheet-body");
            body.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            body.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            body.touchScrollBehavior = ScrollView.TouchScrollBehavior.Elastic;
            layer.panel.Add(body);
            return (layer, body);
        }

        public static Layer Settings(bool inGame, Action onClose, Action onTutorial, Action changed)
        {
            var s = Store.settings;
            var (layer, body) = Sheet("Settings", onClose);
            var music = new Kit.Slider(s.music, v =>
            {
                Store.SetSettings(x => x.music = v);
                Sound.I.SetVolumes(v, Store.settings.sfx);
            });
            var sfx = new Kit.Slider(s.sfx, v =>
            {
                Store.SetSettings(x => x.sfx = v);
                Sound.I.SetVolumes(Store.settings.music, v);
            }, () => Sound.I.Preview());
            var haptics = new Kit.Switch(s.haptics, v => Store.SetSettings(x => x.haptics = v));
            body.Add(Group("Sound",
                Row("music", "Music", "Builds as your stars grow", music.root),
                Row("volume", "Effects", "Merges, swipes and buttons", sfx.root),
                Row("vibrate", "Haptics", "Feel every merge", haptics.root)));

            var size = new Kit.Seg<int>(s.size, new[] { (3, "3×3"), (4, "4×4"), (5, "5×5"), (6, "6×6") }, v => Store.SetSettings(x => x.size = v));
            var undo = new Kit.Switch(s.undo, v => Store.SetSettings(x => x.undo = v));
            var speed = new Kit.Seg<Speed>(s.speed, new[] { (Speed.Normal, "Smooth"), (Speed.Fast, "Fast") }, v => Store.SetSettings(x => x.speed = v));
            body.Add(Group("Game",
                Row("grid", "Board size", inGame ? "Starts with your next game" : "The goal grows with the board", size.root, true),
                Row("undo", "Undo in Classic", "Three rewinds a game", undo.root),
                Row("zap", "Animation speed", null, speed.root, true)));

            var quality = new Kit.Seg<Quality>(s.quality, new[] { (Quality.High, "Ultra"), (Quality.Balanced, "Balanced"), (Quality.Saver, "Battery saver") }, v =>
            {
                Store.SetSettings(x => x.quality = v);
                changed();
            });
            var motion = new Kit.Switch(s.reduceMotion, v =>
            {
                Store.SetSettings(x => x.reduceMotion = v);
                changed();
            });
            var names = new Kit.Switch(s.names, v =>
            {
                Store.SetSettings(x => x.names = v);
                changed();
            });
            var marks = new Kit.Switch(s.colorblind, v =>
            {
                Store.SetSettings(x => x.colorblind = v);
                changed();
            });
            body.Add(Group("Look",
                Row("gauge", "Graphics", "Lower it to save battery", quality.root, true),
                Row("sparkles", "Reduce motion", "Calmer animations, no screen shake", motion.root),
                Row("hash", "Star names", "Show each star’s name on its tile", names.root),
                Row("palette", "Color-blind marks", "A shape on every tile, so color isn’t needed", marks.root)));

            bool confirm = false;
            VisualElement reset = null;
            Label resetLabel = null;
            reset = Kit.Btn("btn glass small-btn", () =>
            {
                Sound.I.Click();
                if (!confirm)
                {
                    confirm = true;
                    resetLabel.text = "Tap again to reset";
                    reset.AddToClassList("danger");
                }
                else
                {
                    Store.ResetStats();
                    confirm = false;
                    resetLabel.text = "Reset";
                    reset.RemoveFromClassList("danger");
                }
            }, resetLabel = Kit.Txt("Reset", "btn-label", Face.Bold));
            var show = Kit.Btn("btn glass small-btn", () =>
            {
                Sound.I.Click();
                onTutorial();
            }, Kit.Txt("Show", "btn-label", Face.Bold));
            body.Add(Group("Help and data",
                Row("sparkles", "How to play", null, show),
                Row("restart", "Reset records", "Scores, stats and achievements", reset)));
            body.Add(Kit.Txt($"Zenith {Application.version} · design, code, music and sound by Randy Kundinger\nEvery sound was synthesized from scratch for this game and is mixed live as you play.", "footnote"));
            return layer;
        }

        static string Hm(long ms)
        {
            long m = (long)Math.Round(ms / 60000.0);
            return m < 60 ? $"{m}m" : $"{m / 60}h {m % 60}m";
        }

        static VisualElement Stat(string n, string label, Color? color = null)
        {
            var nl = Kit.Txt(n, "stat-n", Face.Display);
            if (color.HasValue) nl.style.color = color.Value;
            return Kit.El("stat", Kit.El("stat-box", nl, Kit.Txt(label, "stat-label", Face.Bold)));
        }

        public static Layer Records(Action onClose)
        {
            var st = Store.stats;
            var (layer, body) = Sheet("Records", onClose);
            long bestAny = 0;
            foreach (var e in st.bestScore.items) bestAny = Math.Max(bestAny, e.v);
            var top = st.bestTile > 0 ? Stars.Of(st.bestTile) : null;
            var grid = Kit.El("stat-grid",
                Stat(Stars.Fmt(bestAny), "Best score"),
                Stat(st.bestTile > 0 ? st.bestTile.ToString() : "–", top != null ? top.name : "Biggest star", top?.accent),
                Stat(Stars.Fmt(st.wins), "Supernovas"),
                Stat(Stars.Fmt(st.games), "Games"),
                Stat(Stars.Fmt(st.merges), "Stars fused"),
                Stat($"×{st.bestCombo}", "Best chain"),
                Stat(Stars.Fmt(st.moves), "Moves"),
                Stat(Hm(st.playMs), "Time played"),
                Stat(st.dailyBest.Count.ToString(), "Daily boards"));
            body.Add(Kit.El("group", Kit.Txt("Your universe", "group-title", Face.Bold), grid));

            var ach = Kit.El("ach-grid");
            foreach (var a in Achievements.All)
            {
                bool got = st.achievements.Has(a.id);
                var box = Kit.El("ach-box" + (got ? " got" : ""),
                    Kit.Icon(got ? "award" : "lock"),
                    Kit.El("ach-text", Kit.Txt(a.name, "ach-name", Face.Bold), Kit.Txt(a.how, "ach-how")));
                if (got) box.Fill(Kit.AchFill);
                ach.Add(Kit.El("ach", box));
            }
            body.Add(Kit.El("group", Kit.Txt($"Achievements · {st.achievements.Count} of {Achievements.All.Length}", "group-title", Face.Bold), ach));

            var rows = Kit.El("rows");
            foreach (var kv in Stars.All)
            {
                var sw = Kit.El("swatch").Fill(Kit.StarFill(kv.Key));
                var row = Kit.El("seq-row", sw, Kit.Txt(kv.Value.name, "seq-name", Face.Bold), Kit.Txt(kv.Key.ToString(), "seq-value", Face.Display));
                row.Fade(st.bestTile >= kv.Key ? 1 : 0.45f);
                rows.Add(row);
            }
            Layer.Next(rows);
            body.Add(Kit.El("group", Kit.Txt("The stellar sequence", "group-title", Face.Bold), rows));
            return layer;
        }

        // ---- how to play --------------------------------------------------------------------------------------------

        static VisualElement Mini(int v, int x, int y)
        {
            var t = Kit.UiTile(v, 26);
            t.Move(4 + x * 30, 4 + y * 30);
            return t;
        }

        static float P(int i) => 4 + i * 30;

        static VisualElement Lesson(VisualElement art, string title, string text) =>
            Kit.El("lesson", art, Kit.El("lesson-text", Kit.Txt(title, "lesson-title", Face.Bold), Kit.Txt(text, "lesson-sub")));

        public static Layer HowTo(Action onClose)
        {
            var (layer, body) = Sheet("How to play", onClose);

            // swipe: two stars slide to the left edge
            var m1 = Kit.El("mini");
            var a = Mini(2, 2, 1);
            var b = Mini(8, 1, 2);
            m1.Add(a);
            m1.Add(b);
            body.Add(Lesson(m1, "Swipe to move every star", "Swipe anywhere, or use the arrow keys. All stars slide as far as they can, and a new one appears after every move."));

            // fuse: two 4s meet and become an 8
            var m2 = Kit.El("mini");
            var f1 = Mini(4, 0, 1);
            var f2 = Mini(4, 2, 1);
            var f3 = Mini(8, 0, 1);
            m2.Add(f1);
            m2.Add(f2);
            m2.Add(f3);
            body.Add(Lesson(m2, "Two of the same fuse", "Push two equal stars together and they fuse into the next, hotter star. Fuse several in one move for a chain."));

            var ladder = Kit.El("ladder");
            for (int v = 2; v <= 2048; v *= 2) ladder.Add(Kit.El("ladder-tile").Fill(Kit.StarFill(v)));
            body.Add(Lesson(ladder, "Reach the Supernova", "Dust becomes a red dwarf, a sun, a white star, a blue giant, then a 2048 Supernova. The music grows with your biggest star."));

            var rows = Kit.El("rows",
                Row("sparkles", "Classic", "Three rewinds a game. Chase your best score.", null),
                Row("clock", "Daily", "The same board for everyone today, with one rewind.", null),
                Row("music", "Zen", "Rewind as often as you like, with calmer music.", null));
            Layer.Next(rows);
            body.Add(Kit.El("group", rows));
            body.Add(Layer.Button("Got it", null, "primary wide", () => layer.Close()));

            // the lessons play on a loop, 2.4 seconds each
            body.schedule.Execute(() =>
            {
                float k = Time.unscaledTime % 2.4f / 2.4f;
                float slide = k < 0.25f ? 0 : k < 0.4f ? Ease.Out((k - 0.25f) / 0.15f) : 1;
                a.Move(Mathf.Lerp(P(2), P(0), slide), P(1));
                b.Move(Mathf.Lerp(P(1), P(0), slide), P(2));
                f2.Move(Mathf.Lerp(P(2), P(0), slide), P(1));
                float gone = k < 0.405f ? 1 : 0;
                f1.Fade(gone);
                f2.Fade(gone);
                float fs, fa;
                if (k < 0.38f)
                {
                    fs = 0;
                    fa = 0;
                }
                else if (k < 0.48f)
                {
                    float e = (k - 0.38f) / 0.1f;
                    fs = 1.2f * e;
                    fa = e;
                }
                else if (k < 0.56f)
                {
                    fs = Mathf.Lerp(1.2f, 1, (k - 0.48f) / 0.08f);
                    fa = 1;
                }
                else
                {
                    fs = 1;
                    fa = 1;
                }
                f3.Size(fs);
                f3.Fade(fa);
            }).Every(16);
            return layer;
        }
    }
}
