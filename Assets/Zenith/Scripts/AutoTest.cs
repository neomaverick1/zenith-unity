// An automated run for checking the game on a build machine: started with -zenithShots <folder>, it plays through
// every screen and saves a screenshot of each. It does nothing in a normal launch.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Zenith
{
    public class AutoTest : MonoBehaviour
    {
        string dir;
        readonly List<string> log = new List<string>();

        void Start()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-zenithShots")
                    dir = args[i + 1];
            if (dir == null) return;
            Directory.CreateDirectory(dir);
            Application.logMessageReceived += (msg, trace, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Warning) log.Add($"{type}: {msg}\n{trace}");
            };
            StartCoroutine(Run());
        }

        IEnumerator Shot(string name, float wait = 0)
        {
            if (wait > 0) yield return new WaitForSecondsRealtime(wait);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
            yield return null;
            yield return null;
        }

        static Game Board(int n, int[,] values, int score = 0, bool won = false)
        {
            var g = Engine.NewGame(Mode.Classic, n, 7);
            g.tiles.Clear();
            int id = 100;
            for (int r = 0; r < n; r++)
                for (int c = 0; c < n; c++)
                    if (values[r, c] > 0)
                        g.tiles.Add(new Tile { id = id++, value = values[r, c], r = r, c = c });
            g.nextId = id;
            g.best = Engine.MaxTile(g);
            g.score = score;
            g.won = won;
            g.keepGoing = won;
            g.over = !Engine.CanMove(g);
            return g;
        }

        IEnumerator Run()
        {
            var app = App.I;
            Store.SetSettings(s => s.tutorialDone = false);
            yield return Shot("01-splash", 2.2f);
            app.Begin();
            yield return Shot("02-title", 3f);
            app.Open("howto");
            yield return Shot("03-howto", 1.4f);
            Layer.CloseTop();
            yield return new WaitForSecondsRealtime(0.6f);
            Store.SetSettings(s => s.tutorialDone = true);
            app.StartMode(Mode.Classic);
            yield return Shot("04-play-start", 2.2f);

            // play a while with the demo player, catching a merge mid-burst
            var p = app.CurrentPlay;
            for (int i = 0; i < 40 && !p.Current.over; i++)
            {
                var d = Autoplay.BestMove(p.Current);
                if (!d.HasValue) break;
                p.Act(d.Value);
                yield return new WaitForSecondsRealtime(0.16f);
            }
            for (int i = 0; i < 12; i++)
            {
                var d = Autoplay.BestMove(p.Current);
                if (!d.HasValue) break;
                var r = Engine.Move(p.Current, d.Value);
                p.Act(d.Value);
                if (r.merged.Count > 0)
                {
                    yield return Shot("05-play-merge", 0.12f);
                    break;
                }
                yield return new WaitForSecondsRealtime(0.16f);
            }
            yield return new WaitForSecondsRealtime(0.8f);

            // every kind of star at once
            p.Load(Board(4, new[,] { { 2, 4, 8, 16 }, { 256, 128, 64, 32 }, { 512, 1024, 2048, 4096 }, { 0, 0, 8192, 0 } }, 45210, true));
            yield return Shot("06-play-stars", 1.6f);

            // a supernova
            p.Load(Board(4, new[,] { { 1024, 1024, 0, 0 }, { 512, 256, 128, 64 }, { 4, 8, 16, 32 }, { 2, 0, 0, 2 } }, 20480));
            yield return new WaitForSecondsRealtime(1.2f);
            p.Act(Dir.Left);
            yield return Shot("07-nova-burst", 0.35f);
            yield return Shot("08-nova", 1.8f);
            app.ToTitle();
            yield return new WaitForSecondsRealtime(1f);

            // the end of a game
            app.StartMode(Mode.Zen);
            yield return new WaitForSecondsRealtime(1.2f);
            p = app.CurrentPlay;
            p.Load(Board(4, new[,] { { 2, 4, 2, 4 }, { 4, 2, 4, 2 }, { 8, 4, 2, 4 }, { 16, 32, 16, 0 } }, 1234));
            yield return new WaitForSecondsRealtime(0.8f);
            p.Act(Dir.Right);
            yield return Shot("09-over", 2.4f);
            app.ToTitle();
            yield return new WaitForSecondsRealtime(1f);

            // 6×6 with names and marks
            Store.SetSettings(s =>
            {
                s.size = 6;
                s.names = true;
                s.colorblind = true;
            });
            app.StartMode(Mode.Classic);
            yield return new WaitForSecondsRealtime(1.2f);
            p = app.CurrentPlay;
            p.Restyle();
            p.Load(Board(6, new[,]
            {
                { 2, 4, 8, 16, 32, 64 }, { 128, 256, 512, 1024, 2048, 4096 }, { 8192, 0, 0, 0, 0, 2 },
                { 0, 0, 4, 0, 0, 0 }, { 0, 2, 0, 0, 8, 0 }, { 0, 0, 0, 0, 0, 16 },
            }, 98000, true));
            yield return Shot("10-six", 1.6f);
            p.Pause();
            yield return Shot("11-pause", 1f);
            app.Open("settings");
            yield return Shot("12-settings", 1.4f);
            Layer.CloseTop();
            yield return new WaitForSecondsRealtime(0.5f);
            app.Open("stats");
            yield return Shot("13-records", 1.4f);

            File.WriteAllText(Path.Combine(dir, "log.txt"), string.Join("\n\n", log));
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit();
        }
    }
}
