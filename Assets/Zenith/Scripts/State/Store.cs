// What the game remembers on the device: settings, the game in progress for each mode, records and achievements.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zenith
{
    public enum Quality { High, Balanced, Saver }
    public enum Speed { Normal, Fast }

    [Serializable]
    public class Settings
    {
        public float music = 0.7f;
        public float sfx = 0.85f;
        public bool haptics = true;
        public Quality quality = Quality.High;
        public bool reduceMotion;
        public int size = 4;
        public bool undo = true;
        public Speed speed = Speed.Normal;
        public bool colorblind;
        public bool names;
        public bool tutorialDone;
    }

    [Serializable]
    public class Entry
    {
        public string k;
        public long v;
    }

    /// <summary>A small string-to-number table that JsonUtility can save.</summary>
    [Serializable]
    public class Table
    {
        public List<Entry> items = new List<Entry>();

        public long this[string key]
        {
            get
            {
                foreach (var e in items) if (e.k == key) return e.v;
                return 0;
            }
            set
            {
                foreach (var e in items)
                    if (e.k == key)
                    {
                        e.v = value;
                        return;
                    }
                items.Add(new Entry { k = key, v = value });
            }
        }

        public bool Has(string key)
        {
            foreach (var e in items) if (e.k == key) return true;
            return false;
        }

        public int Count => items.Count;
    }

    [Serializable]
    public class Stats
    {
        public int games, wins;
        public Table bestScore = new Table(); // "classic-4" -> score
        public int bestTile;
        public long merges, moves, playMs;
        public int bestCombo;
        public Table dailyBest = new Table(); // date -> best score that day
        public Table achievements = new Table(); // id -> when it was earned
    }

    public static class Store
    {
        const string SettingsKey = "zenith-settings";
        const string StatsKey = "zenith-stats";
        static string GameKey(Mode m) => "zenith-game-" + m.ToString().ToLowerInvariant();

        public static Settings settings = new Settings();
        public static Stats stats = new Stats();
        static readonly Dictionary<Mode, Game> games = new Dictionary<Mode, Game>();
        static bool dirty;
        static float wait;

        public static event Action Changed;

        public static void Load()
        {
            try
            {
                var s = PlayerPrefs.GetString(SettingsKey, "");
                if (s != "") settings = JsonUtility.FromJson<Settings>(s) ?? new Settings();
                var st = PlayerPrefs.GetString(StatsKey, "");
                if (st != "") stats = JsonUtility.FromJson<Stats>(st) ?? new Stats();
                foreach (Mode m in Enum.GetValues(typeof(Mode)))
                {
                    var g = PlayerPrefs.GetString(GameKey(m), "");
                    if (g == "") continue;
                    var game = JsonUtility.FromJson<Game>(g);
                    if (game != null && game.tiles != null && game.tiles.Count > 0)
                    {
                        game.ghosts = new List<Tile>();
                        games[m] = game;
                    }
                }
            }
            catch (Exception e)
            {
                // storage broken: start fresh rather than refuse to play
                Debug.LogWarning($"Couldn't read saved data: {e.Message}");
                settings = new Settings();
                stats = new Stats();
            }
            settings.size = Mathf.Clamp(settings.size, 3, 6);
        }

        static void Touch()
        {
            dirty = true;
            wait = 0.25f;
            Changed?.Invoke();
        }

        public static void SetSettings(Action<Settings> patch)
        {
            patch(settings);
            Touch();
        }

        public static void SetStats(Action<Stats> fn)
        {
            fn(stats);
            Touch();
        }

        public static void ResetStats()
        {
            stats = new Stats();
            Touch();
        }

        /// <summary>Saves the game in progress, or forgets it once it's over.</summary>
        public static void SaveGame(Game g, Mode mode)
        {
            if (g == null || g.over) games.Remove(mode);
            else games[mode] = g;
            Touch();
        }

        public static Game SavedGame(Mode mode) => games.TryGetValue(mode, out var g) ? g : null;

        /// <summary>Called every frame: writes changes shortly after they happen.</summary>
        public static void Tick(float dt)
        {
            if (!dirty) return;
            wait -= dt;
            if (wait <= 0) Flush();
        }

        public static void Flush()
        {
            if (!dirty) return;
            dirty = false;
            try
            {
                PlayerPrefs.SetString(SettingsKey, JsonUtility.ToJson(settings));
                PlayerPrefs.SetString(StatsKey, JsonUtility.ToJson(stats));
                foreach (Mode m in Enum.GetValues(typeof(Mode)))
                {
                    if (games.TryGetValue(m, out var g))
                    {
                        // the screen's animation flags and old move history aren't needed after a restart
                        var keep = g.Copy();
                        foreach (var t in keep.tiles) t.merged = t.spawned = t.restored = false;
                        PlayerPrefs.SetString(GameKey(m), JsonUtility.ToJson(keep));
                    }
                    else PlayerPrefs.DeleteKey(GameKey(m));
                }
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Couldn't save: {e.Message}");
            }
        }

        public static string TodayKey() => DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        public static string ScoreKey(Game g) => g.mode == Mode.Daily ? "daily" : $"{g.mode.ToString().ToLowerInvariant()}-{g.n}";
    }
}
