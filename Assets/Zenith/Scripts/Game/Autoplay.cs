// A small player for the title screen's demo board: it looks one move ahead and keeps its biggest star in a
// corner, which is how good human players play too. Not unbeatable, just good enough to look like someone who
// knows the game.
using System;

namespace Zenith
{
    public static class Autoplay
    {
        static readonly Dir[] Dirs = { Dir.Down, Dir.Left, Dir.Right, Dir.Up };

        static double Score(Game g)
        {
            var grid = Engine.ToGrid(g);
            int n = g.n;
            int empty = 0;
            double smooth = 0, mono = 0;
            int top = 0;
            for (int r = 0; r < n; r++)
                for (int c = 0; c < n; c++)
                {
                    int v = grid[r, c];
                    top = Math.Max(top, v);
                    if (v == 0)
                    {
                        empty++;
                        continue;
                    }
                    double l = Math.Log(v, 2);
                    if (c + 1 < n && grid[r, c + 1] != 0) smooth -= Math.Abs(l - Math.Log(grid[r, c + 1], 2));
                    if (r + 1 < n && grid[r + 1, c] != 0) smooth -= Math.Abs(l - Math.Log(grid[r + 1, c], 2));
                    // reward rows that rise toward the bottom-left corner
                    if (c + 1 < n && grid[r, c + 1] <= v) mono += l;
                    if (r > 0 && grid[r - 1, c] <= v) mono += l;
                }
            double corner = grid[n - 1, 0] == top ? 40 : 0;
            return empty * 12 + smooth * 1.5 + mono * 0.8 + corner;
        }

        public static Dir? BestMove(Game g)
        {
            Dir? best = null;
            double top = double.NegativeInfinity;
            foreach (var d in Dirs)
            {
                var r = Engine.Move(g, d);
                if (!r.moved) continue;
                double s = Score(r.game) + r.gained * 0.05 - (d == Dir.Up ? 30 : 0);
                if (s > top)
                {
                    top = s;
                    best = d;
                }
            }
            return best;
        }
    }
}
