// The stars: what each tile value is called and how it looks. The colors follow real stars: cooling dust and
// nebula at the start, then red, orange, yellow and white as they heat up, blue at the top, and beyond the goal
// the strange objects stars become.
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Zenith
{
    public enum Aura { None, Corona, Pulse, Hole }

    public class Star
    {
        public string name;
        public Color from, to, ink, glow;
        public bool hasGlow;
        /// <summary>For color-blind mode: a shape that tells values apart without color.</summary>
        public string mark;
        public bool shine;
        public Aura aura;

        /// <summary>The color sparks, ripples and progress dots use (pure white stars read as ice blue).</summary>
        public Color accent => from.r > 0.99f && from.g > 0.99f && from.b > 0.99f ? Stars.Hex("#bfe4ff") : from;
    }

    public static class Stars
    {
        public static Color Hex(string hex, float a = 1)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            c.a = a;
            return c;
        }

        static Color Rgba(int r, int g, int b, float a) => new Color(r / 255f, g / 255f, b / 255f, a);

        static Star S(string name, string from, string to, string ink, Color glow, string mark, bool hasGlow = true) =>
            new Star { name = name, from = Hex(from), to = Hex(to), ink = Hex(ink), glow = glow, hasGlow = hasGlow, mark = mark };

        public static readonly SortedDictionary<int, Star> All = Build();

        static SortedDictionary<int, Star> Build()
        {
            var d = new SortedDictionary<int, Star>
            {
                [2] = S("Stardust", "#2c2f6e", "#232559", "#c9ccff", Color.clear, "·", false),
                [4] = S("Nebula", "#41318c", "#33266f", "#ded6ff", Color.clear, "··", false),
                [8] = S("Protostar", "#7d2b80", "#612066", "#ffd9f8", Color.clear, "△", false),
                [16] = S("Red Dwarf", "#b8294d", "#931f3e", "#ffe2e8", Rgba(220, 50, 90, 0.35f), "▲"),
                [32] = S("Ember", "#e04a38", "#bd352a", "#fff1ec", Rgba(240, 90, 60, 0.4f), "◇"),
                [64] = S("Orange Star", "#f27a2c", "#d65b1c", "#fff5ea", Rgba(250, 130, 50, 0.45f), "◆"),
                [128] = S("Sun", "#fbb03a", "#ec8f17", "#3a1a00", Rgba(255, 180, 60, 0.55f), "○"),
                [256] = S("Yellow Giant", "#ffd862", "#f7bb33", "#3b2300", Rgba(255, 210, 90, 0.6f), "●"),
                [512] = S("White Star", "#fff7df", "#ffe7a8", "#3d2a00", Rgba(255, 240, 200, 0.65f), "□"),
                [1024] = S("Blue Giant", "#d7ecff", "#93c9ff", "#0b2850", Rgba(150, 205, 255, 0.7f), "■"),
                [2048] = S("Supernova", "#ffffff", "#a9d8ff", "#071a3d", Rgba(170, 220, 255, 0.9f), "✦"),
                [4096] = S("Neutron Star", "#7ff3ff", "#2fb6ff", "#03203a", Rgba(90, 230, 255, 0.85f), "✧"),
                [8192] = S("Black Hole", "#1b0d2b", "#07040d", "#ffcf8a", Rgba(255, 150, 60, 0.75f), "◉"),
                [16384] = S("Quasar", "#ff5fd6", "#7a2bff", "#ffffff", Rgba(220, 90, 255, 0.85f), "✺"),
                [32768] = S("Singularity", "#fff8e6", "#ffd36b", "#2a1600", Rgba(255, 230, 160, 0.95f), "❂"),
            };
            foreach (var kv in d)
            {
                kv.Value.shine = kv.Key >= 256 && kv.Key < 8192;
                kv.Value.aura = kv.Key == 2048 || kv.Key >= 16384 ? Aura.Corona : kv.Key == 4096 ? Aura.Pulse : kv.Key == 8192 ? Aura.Hole : Aura.None;
            }
            return d;
        }

        public static Star Of(int v) => All.TryGetValue(v, out var s) ? s : All[32768];

        /// <summary>The heat of the board, 0 to 1, from the biggest star: tints the nebula behind it.</summary>
        public static float Heat(int top, int target) =>
            Mathf.Clamp01((float)((Math.Log(Math.Max(2, top), 2) - 1) / (Math.Log(target, 2) - 1)));

        public static string Fmt(long n) => n.ToString("#,0", CultureInfo.InvariantCulture);

        /// <summary>"a Sun", "an Ember".</summary>
        public static string A(string name) => ("AEIOU".IndexOf(char.ToUpperInvariant(name[0])) >= 0 ? "an " : "a ") + name;

        public static string ModeName(Mode m) => m switch { Mode.Classic => "Classic", Mode.Daily => "Daily", _ => "Zen" };

        /// <summary>"Nov 17" from a yyyy-MM-dd date.</summary>
        public static string ShortDay(string day)
        {
            if (string.IsNullOrEmpty(day)) return "";
            return DateTime.TryParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                ? d.ToString("MMM d", CultureInfo.InvariantCulture)
                : day;
        }
    }
}
