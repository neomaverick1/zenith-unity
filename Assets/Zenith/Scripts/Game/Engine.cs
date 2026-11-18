// The game itself, with no screen attached: a square board of stars, a move in one of four directions, merges,
// a new star after every move, undo, and the end of the game. Every function returns a new game and never changes
// the one it was given, so undo is just "go back to the last one" and the screen can animate from old to new.
// The random numbers match the web version bit for bit, so a daily board is the same on every device.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Zenith
{
    public enum Dir { Up, Down, Left, Right }
    public enum Mode { Classic, Daily, Zen }

    [Serializable]
    public class Tile
    {
        public int id, value, r, c;
        /// <summary>Made by a merge on the last move (the screen pops it).</summary>
        [NonSerialized] public bool merged;
        /// <summary>Appeared after the last move (the screen grows it in).</summary>
        [NonSerialized] public bool spawned;
        /// <summary>Came back with an undo (the screen fades it in).</summary>
        [NonSerialized] public bool restored;

        public Tile Copy() => new Tile { id = id, value = value, r = r, c = c, merged = merged, spawned = spawned, restored = restored };
        public Tile Plain() => new Tile { id = id, value = value, r = r, c = c };
    }

    [Serializable]
    public class Snapshot
    {
        public List<Tile> tiles = new List<Tile>();
        public int score;
        public uint rng;
        public int moves;
    }

    [Serializable]
    public class Game
    {
        public Mode mode;
        public int n;
        /// <summary>The star that counts as a win on this board size (2048 on 4×4).</summary>
        public int target;
        public List<Tile> tiles = new List<Tile>();
        /// <summary>Tiles that merged on the last move: drawn sliding into their partner, then gone.</summary>
        [NonSerialized] public List<Tile> ghosts = new List<Tile>();
        public int score, moves, merges;
        /// <summary>Highest tile on the board this game.</summary>
        public int best;
        public bool won, keepGoing, over;
        /// <summary>Seeded random state, so a daily board is the same for everyone.</summary>
        public uint rng;
        public int nextId;
        public List<Snapshot> history = new List<Snapshot>();
        /// <summary>Rewinds left. Engine.Endless in Zen.</summary>
        public int undoLeft;
        public bool usedUndo;
        public uint seed;
        /// <summary>The daily challenge's date, yyyy-MM-dd.</summary>
        public string day = "";
        public long startedAt;

        public bool EndlessUndo => undoLeft >= Engine.Endless;

        public Game Copy()
        {
            var g = (Game)MemberwiseClone();
            g.tiles = tiles.Select(t => t.Copy()).ToList();
            g.ghosts = (ghosts ?? new List<Tile>()).Select(t => t.Copy()).ToList();
            g.history = new List<Snapshot>(history);
            return g;
        }
    }

    public struct Merge
    {
        public int value, r, c;
    }

    public class MoveResult
    {
        public Game game;
        public bool moved;
        /// <summary>Merges on this move, biggest last.</summary>
        public List<Merge> merged = new List<Merge>();
        public int gained;
        public Tile spawned;
        public bool newBest, justWon, justOver;
    }

    public static class Engine
    {
        public const int Endless = 1_000_000;
        const int HistoryLength = 40;

        public static int TargetFor(int n) => n switch { 3 => 256, 4 => 2048, 5 => 4096, 6 => 8192, _ => 2048 };

        public static int UndosFor(Mode m) => m switch { Mode.Classic => 3, Mode.Daily => 1, _ => Endless };

        /// <summary>mulberry32: small, fast and the same on every device, so a seed gives the same board everywhere.</summary>
        public static (double value, uint next) Rand(uint state)
        {
            uint t = unchecked(state + 0x6D2B79F5u);
            uint next = t;
            t = unchecked((t ^ (t >> 15)) * (t | 1u));
            t ^= unchecked(t + (t ^ (t >> 7)) * (t | 61u));
            return ((t ^ (t >> 14)) / 4294967296.0, next);
        }

        public static uint SeedFrom(string text)
        {
            uint h = 2166136261u;
            foreach (char ch in text)
            {
                h ^= ch;
                h = unchecked(h * 16777619u);
            }
            return h;
        }

        static bool Occupied(List<Tile> tiles, int r, int c)
        {
            foreach (var t in tiles) if (t.r == r && t.c == c) return true;
            return false;
        }

        static Game Spawn(Game g)
        {
            var free = new List<(int r, int c)>();
            for (int r = 0; r < g.n; r++)
                for (int c = 0; c < g.n; c++)
                    if (!Occupied(g.tiles, r, c)) free.Add((r, c));
            if (free.Count == 0) return g;
            var (a, s1) = Rand(g.rng);
            var (b, s2) = Rand(s1);
            var (row, col) = free[(int)Math.Floor(a * free.Count)];
            var next = g.Copy();
            next.tiles.Add(new Tile { id = g.nextId, value = b < 0.9 ? 2 : 4, r = row, c = col, spawned = true });
            next.rng = s2;
            next.nextId = g.nextId + 1;
            return next;
        }

        public static Game NewGame(Mode mode, int n, uint? seed = null, string day = "", bool undo = true)
        {
            uint s = seed ?? (uint)new Random().Next() ^ (uint)Environment.TickCount;
            var g = new Game
            {
                mode = mode,
                n = n,
                target = TargetFor(n),
                rng = s,
                seed = s,
                nextId = 1,
                day = day ?? "",
                undoLeft = !undo && mode != Mode.Zen ? 0 : UndosFor(mode),
                startedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
            g = Spawn(Spawn(g));
            g.best = g.tiles.Max(t => t.value);
            return g;
        }

        /// <summary>The day's challenge: the same opening board and the same new stars for everyone on that date.</summary>
        public static Game DailyGame(string day) => NewGame(Mode.Daily, 4, SeedFrom($"zenith-{day}"), day);

        public static int[,] ToGrid(Game g)
        {
            var grid = new int[g.n, g.n];
            foreach (var t in g.tiles) grid[t.r, t.c] = t.value;
            return grid;
        }

        public static bool CanMove(Game g)
        {
            if (g.tiles.Count < g.n * g.n) return true;
            var grid = ToGrid(g);
            for (int r = 0; r < g.n; r++)
                for (int c = 0; c < g.n; c++)
                {
                    int v = grid[r, c];
                    if ((c + 1 < g.n && grid[r, c + 1] == v) || (r + 1 < g.n && grid[r + 1, c] == v)) return true;
                }
            return false;
        }

        static (int dr, int dc) Vector(Dir d) => d switch
        {
            Dir.Up => (-1, 0),
            Dir.Down => (1, 0),
            Dir.Left => (0, -1),
            _ => (0, 1),
        };

        /// <summary>Plays one move. When nothing can move that way, the same game comes back with moved = false.</summary>
        public static MoveResult Move(Game g, Dir dir)
        {
            var none = new MoveResult { game = g, moved = false };
            if (g.over) return none;
            var (dr, dc) = Vector(dir);
            int n = g.n;
            // walk each line starting from the edge the stars move toward
            var order = Enumerable.Range(0, n).ToArray();
            var rows = dr == 1 ? order.Reverse().ToArray() : order;
            var cols = dc == 1 ? order.Reverse().ToArray() : order;
            var at = new Dictionary<(int, int), Tile>();
            foreach (var t in g.tiles)
            {
                var copy = t.Plain();
                at[(t.r, t.c)] = copy;
            }
            var placed = new Dictionary<(int, int), Tile>();
            var placedOrder = new List<(int, int)>();
            var fresh = new HashSet<int>();
            var ghosts = new List<Tile>();
            var merged = new List<Merge>();
            int nextId = g.nextId;
            int gained = 0;
            bool moved = false;

            foreach (int r0 in rows)
                foreach (int c0 in cols)
                {
                    if (!at.TryGetValue((r0, c0), out var t)) continue;
                    int r = r0, c = c0;
                    // slide to the farthest free cell
                    while (r + dr >= 0 && r + dr < n && c + dc >= 0 && c + dc < n && !placed.ContainsKey((r + dr, c + dc)))
                    {
                        r += dr;
                        c += dc;
                    }
                    if (placed.TryGetValue((r + dr, c + dc), out var ahead) && !fresh.Contains(ahead.id) && ahead.value == t.value)
                    {
                        // merge into the star ahead: both slide there and a new, bigger star takes their place
                        int value = t.value * 2;
                        var made = new Tile { id = nextId++, value = value, r = ahead.r, c = ahead.c, merged = true };
                        ghosts.Add(ahead.Copy());
                        ghosts.Add(new Tile { id = t.id, value = t.value, r = ahead.r, c = ahead.c });
                        placed[(ahead.r, ahead.c)] = made;
                        fresh.Add(made.id);
                        merged.Add(new Merge { value = value, r = ahead.r, c = ahead.c });
                        gained += value;
                        moved = true;
                    }
                    else
                    {
                        if (r != r0 || c != c0) moved = true;
                        t.r = r;
                        t.c = c;
                        placed[(r, c)] = t;
                        placedOrder.Add((r, c));
                    }
                }

            if (!moved) return none;
            var tiles = new List<Tile>();
            var seen = new HashSet<(int, int)>();
            foreach (var key in placedOrder)
                if (seen.Add(key)) tiles.Add(placed[key]);
            foreach (var kv in placed)
                if (seen.Add(kv.Key)) tiles.Add(kv.Value);

            var snapshot = new Snapshot
            {
                tiles = g.tiles.Select(t => t.Plain()).ToList(),
                score = g.score,
                rng = g.rng,
                moves = g.moves,
            };
            var next = g.Copy();
            next.tiles = tiles;
            next.ghosts = ghosts;
            next.nextId = nextId;
            next.score = g.score + gained;
            next.moves = g.moves + 1;
            next.merges = g.merges + merged.Count;
            next.history = new List<Snapshot>(g.history) { snapshot };
            if (next.history.Count > HistoryLength) next.history.RemoveRange(0, next.history.Count - HistoryLength);
            next = Spawn(next);
            next.ghosts = ghosts;
            var spawned = next.tiles.FirstOrDefault(t => t.spawned);
            int top = next.tiles.Max(t => t.value);
            bool justWon = !g.won && top >= g.target;
            bool over = !CanMove(next);
            next.best = Math.Max(g.best, top);
            next.won = g.won || justWon;
            next.over = over;
            merged.Sort((a, b) => a.value.CompareTo(b.value));
            return new MoveResult
            {
                game = next,
                moved = true,
                merged = merged,
                gained = gained,
                spawned = spawned,
                newBest = top > g.best,
                justWon = justWon,
                justOver = over,
            };
        }

        public static bool CanUndo(Game g) => g.history.Count > 0 && g.undoLeft > 0;

        /// <summary>Goes back one move. The stars that come back fade in where they were.</summary>
        public static Game Undo(Game g)
        {
            if (!CanUndo(g)) return g;
            var last = g.history[g.history.Count - 1];
            var now = new HashSet<int>(g.tiles.Select(t => t.id));
            var u = g.Copy();
            u.tiles = last.tiles.Select(t => new Tile { id = t.id, value = t.value, r = t.r, c = t.c, restored = !now.Contains(t.id) }).ToList();
            u.ghosts = new List<Tile>();
            u.score = last.score;
            u.rng = last.rng;
            u.moves = last.moves;
            u.over = false;
            u.history = g.history.Take(g.history.Count - 1).ToList();
            u.undoLeft = g.EndlessUndo ? g.undoLeft : g.undoLeft - 1;
            u.usedUndo = true;
            return u;
        }

        public static Game KeepGoing(Game g)
        {
            var k = g.Copy();
            k.keepGoing = true;
            k.ghosts = new List<Tile>();
            foreach (var t in k.tiles) t.merged = t.spawned = t.restored = false;
            return k;
        }

        public static int MaxTile(Game g) => g.tiles.Count == 0 ? 0 : g.tiles.Max(t => t.value);

        public static int Log2(int v) => (int)Math.Round(Math.Log(Math.Max(1, v), 2));
    }
}
