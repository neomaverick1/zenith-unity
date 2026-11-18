// Checks the rules against the web version: the same daily board and the same moves must give the same stars.
//   Unity -batchmode -quit -projectPath . -executeMethod Zenith.EditorTools.Checks.Daily
using System.IO;
using System.Linq;
using UnityEngine;

namespace Zenith.EditorTools
{
    public static class Checks
    {
        public static void Daily()
        {
            var g = Engine.DailyGame("2026-11-17");
            var lines = new System.Collections.Generic.List<string>();
            void Dump() => lines.Add(string.Join(" ", g.tiles.Select(t => $"{t.r}{t.c}:{t.value}").OrderBy(x => x, System.StringComparer.Ordinal)) + $" s{g.score}");
            Dump();
            foreach (var d in new[] { Dir.Left, Dir.Down, Dir.Right, Dir.Down, Dir.Left, Dir.Up, Dir.Left, Dir.Down })
            {
                var r = Engine.Move(g, d);
                if (r.moved) g = r.game;
                Dump();
            }
            File.WriteAllText("Builds/daily-check.txt", string.Join("\n", lines) + "\n");
            Debug.Log("[check] wrote Builds/daily-check.txt");
        }
    }
}
