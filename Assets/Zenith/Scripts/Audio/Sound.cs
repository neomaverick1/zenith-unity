// Everything you hear. The soundtrack is six intensities of the same eight-bar piece, all playing in lockstep,
// so the music can build on any downbeat by fading one up and another down. Merges ring bells tuned to the chord
// that's sounding, big merges add trailer-style hits, pausing muffles the music as if you'd stepped into another
// room, and reaching the goal sets off a supernova of sound. Every sound was rendered from the game's own synth.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zenith
{
    public class Sound : MonoBehaviour
    {
        public static Sound I { get; private set; }

        const int Menu = 0; // music source 0 is the title screen's music; sources 1..6 are intensities 0..5
        const int Pool = 24;

        readonly AudioSource[] music = new AudioSource[7];
        readonly AudioLowPassFilter[] filters = new AudioLowPassFilter[7];
        readonly float[] vol = new float[7]; // each loop's current mix level
        readonly float[] want = new float[7];
        readonly float[] glide = new float[7];
        readonly AudioSource[] pool = new AudioSource[Pool];
        int next;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly List<int> bellNotes = new List<int>();

        double dspStart;
        bool started;
        bool menu = true;
        int level; // the intensity the game asks for
        int cap = 5; // zen keeps the music calm
        int playing; // the intensity the loops are set to (-1 while the supernova holds the music)
        int applied = -2; // the intensity and screen the loops were last set for
        double switchAt = -1; // the downbeat a pending change lands on
        double holdUntil; // the music is silent until this time (after a supernova)
        float duck = 1, duckTarget = 1, duckGlide = 0.15f;
        double duckRelease;
        float cutoff = 22000, cutoffTarget = 22000, cutoffGlide = 0.25f;
        float musicVolume = 0.7f, sfxVolume = 0.85f;

        public bool Ready => started;

        public static void Create()
        {
            if (I) return;
            var go = new GameObject("Sound");
            DontDestroyOnLoad(go);
            I = go.AddComponent<Sound>();
        }

        void Awake()
        {
            foreach (var c in Resources.LoadAll<AudioClip>("Audio"))
            {
                clips[c.name] = c;
                if (c.name.StartsWith("bell_") && int.TryParse(c.name.Substring(5), out var m)) bellNotes.Add(m);
            }
            bellNotes.Sort();
            for (int i = 0; i < music.Length; i++)
            {
                var go = new GameObject(i == Menu ? "Music menu" : $"Music {i - 1}");
                go.transform.SetParent(transform, false);
                var s = go.AddComponent<AudioSource>();
                s.clip = Clip(i == Menu ? "music_menu" : $"music_{i - 1}");
                s.loop = true;
                s.playOnAwake = false;
                s.volume = 0;
                s.priority = 0;
                s.spatialBlend = 0;
                music[i] = s;
#if !UNITY_WEBGL || UNITY_EDITOR
                var f = go.AddComponent<AudioLowPassFilter>();
                f.cutoffFrequency = 22000;
                f.lowpassResonanceQ = 1;
                filters[i] = f;
#endif
            }
            for (int i = 0; i < Pool; i++)
            {
                var go = new GameObject($"Effect {i}");
                go.transform.SetParent(transform, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.priority = 64;
                s.spatialBlend = 0;
                pool[i] = s;
            }
        }

        AudioClip Clip(string name) => clips.TryGetValue(name, out var c) ? c : null;

        /// <summary>Starts the soundtrack. Called from the first tap, which browsers need before they play sound.</summary>
        public void Begin()
        {
            if (started) return;
            started = true;
            dspStart = AudioSettings.dspTime + 0.15;
            foreach (var s in music)
                if (s.clip)
                    s.PlayScheduled(dspStart);
            menu = true;
            Apply(Menu, 0.01f);
            playing = 0;
            applied = Menu;
        }

        public void SetVolumes(float m, float s)
        {
            musicVolume = m;
            sfxVolume = s;
        }

        // ---- music --------------------------------------------------------------------------------------------------

        /// <summary>The title screen's music: just the pad and a slow arpeggio.</summary>
        public void MenuMusic()
        {
            menu = true;
            level = 0;
            cap = 5;
            Muffle(false);
        }

        /// <summary>Game music: intensity follows the biggest star.</summary>
        public void GameMusic(int top, int target, bool zen)
        {
            menu = false;
            cap = zen ? 2 : 5;
            level = Theory.LevelFor(top, target);
            holdUntil = 0;
            Muffle(false);
        }

        public void Follow(int top, int target) => level = Theory.LevelFor(top, target);

        double Now => AudioSettings.dspTime;
        double Pos => Math.Max(0, Now - dspStart);
        int BarNow => (int)Math.Floor(Pos / Theory.Bar);
        Chord ChordNow => Theory.ChordAt(BarNow);
        double NextBar => dspStart + Math.Ceiling(Pos / Theory.Bar + 1e-6) * Theory.Bar;

        void Apply(int source, float g)
        {
            for (int i = 0; i < music.Length; i++)
            {
                want[i] = i == source ? 1 : 0;
                glide[i] = g;
            }
        }

        void Update()
        {
            if (!started) return;
            float dt = Mathf.Min(0.1f, Time.unscaledDeltaTime);
            double now = Now;
            if (holdUntil > 0 && now >= holdUntil - 0.01)
            {
                // the supernova's silence ends exactly on its downbeat
                holdUntil = 0;
                switchAt = now;
            }
            if (holdUntil <= 0)
            {
                int target = menu ? Menu : 1 + Mathf.Min(level, cap);
                if (target != applied && switchAt < 0)
                {
                    // new intensities come in on the downbeat, with a hit to mark the moment
                    switchAt = NextBar;
                    int up = target - 1;
                    if (!menu && up >= 2 && up > playing && playing >= 0) Play("impact_05", switchAt - now, 0.85f);
                }
                if (switchAt >= 0 && now >= switchAt - 0.01)
                {
                    int lv = target - 1;
                    Apply(target, menu || lv > playing ? (playing < 0 ? 0.12f : 0.5f) : 0.9f);
                    playing = menu ? 0 : lv;
                    applied = target;
                    switchAt = -1;
                }
            }
            if (duckRelease > 0 && now >= duckRelease)
            {
                duckRelease = 0;
                duckTarget = 1;
                duckGlide = 0.3f;
            }
            duck += (duckTarget - duck) * (1 - Mathf.Exp(-dt / duckGlide));
            cutoff += (cutoffTarget - cutoff) * (1 - Mathf.Exp(-dt / cutoffGlide));
            float master = musicVolume * musicVolume * duck;
            for (int i = 0; i < music.Length; i++)
            {
                vol[i] += (want[i] - vol[i]) * (1 - Mathf.Exp(-dt / Mathf.Max(0.005f, glide[i])));
                music[i].volume = vol[i] * master;
                if (filters[i]) filters[i].cutoffFrequency = cutoff;
            }
        }

        /// <summary>Muffles the music, as if heard through a wall: for the pause menu and the end of a game.</summary>
        public void Muffle(bool on, bool deep = false)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // browsers get no filter here, so the music just steps back
            duckTarget = on ? (deep ? 0.35f : 0.45f) : 1;
#else
            cutoffTarget = on ? (deep ? 420 : 900) : 22000;
            cutoffGlide = on ? 0.12f : 0.25f;
            duckTarget = on ? 0.55f : 1;
#endif
            duckGlide = 0.15f;
            duckRelease = 0;
        }

        void DuckFor(double seconds, float depth)
        {
            duckTarget = depth;
            duckGlide = 0.015f;
            duckRelease = Now + seconds;
        }

        // ---- one-shots ----------------------------------------------------------------------------------------------

        AudioSource Free()
        {
            for (int k = 0; k < Pool; k++)
            {
                var s = pool[(next + k) % Pool];
                if (!s.isPlaying)
                {
                    next = (next + k + 1) % Pool;
                    return s;
                }
            }
            var oldest = pool[next];
            next = (next + 1) % Pool;
            oldest.Stop();
            return oldest;
        }

        /// <summary>Plays a rendered sound after a delay in seconds, at a volume and pan.</summary>
        void Play(string name, double delay, float volume, float pan = 0, float pitch = 1)
        {
            if (!started) return;
            var c = Clip(name);
            if (!c) return;
            var s = Free();
            s.clip = c;
            s.volume = Mathf.Clamp01(volume) * sfxVolume * sfxVolume;
            s.panStereo = Mathf.Clamp(pan, -1, 1);
            s.pitch = pitch;
            s.PlayScheduled(Now + Math.Max(0.0, delay));
        }

        void Bell(int note, double delay, float vel, float pan)
        {
            if (clips.ContainsKey($"bell_{note}"))
            {
                Play($"bell_{note}", delay, vel, pan);
                return;
            }
            // a note we didn't render: bend the nearest one
            int best = bellNotes.Count > 0 ? bellNotes[0] : 74;
            foreach (int m in bellNotes) if (Math.Abs(m - note) < Math.Abs(best - note)) best = m;
            Play($"bell_{best}", delay, vel, pan, Mathf.Pow(2, (note - best) / 12f));
        }

        void Blip(int note, double delay, float vol = 0.04f) => Play($"blip_{note}", delay, vol / 0.04f);

        public void Move(Dir d) => Play(d == Dir.Left ? "whoosh_l" : d == Dir.Right ? "whoosh_r" : "whoosh_c", 0, 0.9f);

        /// <summary>Merges in one move, smallest first, strummed like a harp and placed left to right where they happened.</summary>
        public void Merges(IList<Merge> list, int n)
        {
            if (!started || list.Count == 0) return;
            var chord = ChordNow;
            const double t0 = 0.07; // land with the slide
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                double t = t0 + i * 0.035;
                float pan = n > 1 ? m.c / (float)(n - 1) * 1.2f - 0.6f : 0;
                int note = Theory.MergeNote(m.value, chord);
                Bell(note, t, Mathf.Min(1, 0.55f + Mathf.Log(m.value, 2) * 0.05f), pan);
                if (m.value >= 256) Play($"shimmer_{note}", t, 1, pan * 0.5f);
            }
            int top = list[list.Count - 1].value;
            if (top >= 512)
            {
                Play(top >= 1024 ? "impact_11" : "impact_07", t0, 1);
                DuckFor(0.4, 0.5f);
            }
            if (top >= 1024) Play($"braam_{chord.root}", t0, 1);
            if (list.Count >= 3)
            {
                // a rising flourish for a chain reaction
                var v = chord.voicing;
                for (int k = 0; k < 4; k++) Blip(v[k % 3] + 24 + (k == 3 ? 12 : 0), t0 + 0.12 + k * 0.05, 0.035f);
            }
        }

        public void Spawn(int c, int n) => Play($"tick_{ChordNow.voicing[2] + 24}", 0.12, 1, n > 1 ? c / (float)(n - 1) * 0.8f - 0.4f : 0);

        public void Blocked() => Play("thud", 0, 1);

        public void Undo() => Play("rewind", 0, 1);

        public void Click() => Blip(84, 0);

        public void Toggle(bool on)
        {
            Blip(on ? 79 : 84, 0);
            Blip(on ? 86 : 74, 0.06);
        }

        /// <summary>The effects slider plays a bell at the new loudness, so you hear what you're setting.</summary>
        public void Preview() => Bell(Theory.MergeNote(64, ChordNow), 0, 0.8f, 0);

        public void NewBest()
        {
            var v = ChordNow.voicing;
            int[] notes = { v[0] + 24, v[1] + 24, v[2] + 24, v[0] + 36 };
            for (int i = 0; i < notes.Length; i++) Blip(notes[i], 0.25 + i * 0.07, 0.035f);
        }

        public void Achievement()
        {
            int[] notes = { 74, 78, 81, 86 };
            for (int i = 0; i < notes.Length; i++) Bell(notes[i], i * 0.09, 0.6f, -0.3f + i * 0.2f);
        }

        /// <summary>The end of a game: a falling arpeggio while the music sinks underwater.</summary>
        public void GameOver()
        {
            Muffle(true, true);
            level = 0;
            int[] notes = { 69, 65, 62, 57 };
            for (int i = 0; i < notes.Length; i++) Bell(notes[i], 0.15 + i * 0.22, 0.6f, 0);
        }

        /// <summary>
        /// Reaching the goal: the music stops dead, a cinematic hit and a trailer horn land, a choir swells on a major
        /// chord, and the full soundtrack comes back in two bars later at its peak.
        /// </summary>
        public void Supernova()
        {
            if (!started) return;
            for (int i = 0; i < music.Length; i++)
            {
                want[i] = 0;
                glide[i] = 0.03f;
            }
            double pos = Pos;
            double barStart = dspStart + Math.Floor(pos / Theory.Bar) * Theory.Bar;
            holdUntil = barStart + Theory.Bar * 2; // back on a downbeat, two bars on
            playing = -1;
            applied = -2;
            switchAt = -1;
            level = 5;
            Play("impact_16", 0, 1);
            Play("braam_38", 0, 1);
            Play("choir_supernova", 0, 1);
            Play("shimmer_74", 0.3, 1);
            Play("riser", holdUntil - Theory.Bar - Now, 1);
        }
    }
}
