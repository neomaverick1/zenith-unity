// The music, written down: tempo, the chord progression and how loud the soundtrack gets. The soundtrack is in
// D minor at 100 beats a minute over an eight-bar progression that ends on an A major chord. Its loops were
// rendered from the same synthesizer as the web version; this file decides which notes the merges play live.
using System;

namespace Zenith
{
    public struct Chord
    {
        public string name;
        public int root; // MIDI note of the bass
        public bool minor;
        public int[] voicing; // the pad's voicing

        public Chord(string name, int root, bool minor, params int[] voicing)
        {
            this.name = name;
            this.root = root;
            this.minor = minor;
            this.voicing = voicing;
        }
    }

    public static class Theory
    {
        public const double Bpm = 100;
        public const double Beat = 60.0 / Bpm;
        public const double Step = Beat / 4; // a sixteenth note
        public const double Bar = Beat * 4;
        public const int LoopBars = 8;

        static readonly int[] Minor = { 0, 3, 7 };
        static readonly int[] Major = { 0, 4, 7 };

        // i – VI – III – VII, then i – VI – iv – V: the classic heroic progression, with A major pulling back to D minor
        public static readonly Chord[] Progression =
        {
            new Chord("Dm", 38, true, 62, 65, 69),
            new Chord("Bb", 34, false, 62, 65, 70),
            new Chord("F", 41, false, 60, 65, 69),
            new Chord("C", 36, false, 60, 64, 67),
            new Chord("Dm", 38, true, 62, 65, 69),
            new Chord("Bb", 34, false, 62, 65, 70),
            new Chord("Gm", 43, true, 62, 67, 70),
            new Chord("A", 33, false, 61, 64, 69),
        };

        public static Chord ChordAt(int bar) => Progression[((bar % Progression.Length) + Progression.Length) % Progression.Length];

        /// <summary>
        /// The note a merge plays: a tone of the chord that's sounding right now, one step up the chord for every
        /// doubling. So 2 + 2 plays the low root, a 2048 rings three octaves higher, and every merge is in tune.
        /// </summary>
        public static int MergeNote(int value, Chord chord)
        {
            int k = Math.Max(1, (int)Math.Round(Math.Log(value, 2)) - 1);
            var tones = chord.minor ? Minor : Major;
            int pc = chord.root % 12;
            int octave = 3 + (k - 1) / 3;
            return 12 * (octave + 1) + pc + tones[(k - 1) % 3];
        }

        /// <summary>
        /// How intense the music is, from 0 (a soft pad) to 5 (everything), set by the biggest star compared with the
        /// board's goal. On 4×4: 16 brings the arpeggio, 64 the bass and drums, 256 the war drums, 1024 the choir and
        /// 2048 the full theme.
        /// </summary>
        public static int LevelFor(int top, int target)
        {
            double f = Math.Log(Math.Max(2, top), 2) / Math.Log(target, 2);
            double[] steps = { 4 / 11.0, 6 / 11.0, 8 / 11.0, 10 / 11.0, 1 };
            int level = 0;
            foreach (var s in steps) if (f >= s - 1e-9) level++;
            return level;
        }
    }
}
