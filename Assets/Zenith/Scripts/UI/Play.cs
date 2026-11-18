// A game in progress: the heads-up display, the board, swipes and keys, and everything a move sets off: the
// slide, the bells, sparks and shockwaves, the score counting up, records, achievements, the supernova and the
// end of the game.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Zenith
{
    public class Play
    {
        public readonly VisualElement root;
        readonly Mode mode;
        readonly Action onQuit;
        readonly Action<string> onSheet;
        Game game;
        readonly BoardView board;
        readonly VisualElement stage, slot, scoreBox, dots, undoBtn, center, scoresRow, sequenceRow;
        readonly Label undoCount;
        readonly Label whereMain, whereSub, bestN, topLabel, goalLabel;
        readonly Kit.Counter score;
        readonly List<VisualElement> dotEls = new List<VisualElement>();
        Layer pauseLayer, endLayer;
        VisualElement nova;
        bool paused, ended, novaUp;
        float lastMove;
        Vector2? swipeFrom;
        bool swipeDone;
        int swipePointer = -1;
        public bool Blocked; // a sheet is open over the game

        float Slide => Store.settings.reduceMotion ? 0.06f : Store.settings.speed == Speed.Fast ? 0.07f : 0.11f;
        bool Live => !Blocked && !paused && !ended && !novaUp;

        static Game StartGame(Mode mode, int size, bool undo, bool fresh = false)
        {
            string today = Store.TodayKey();
            if (!fresh)
            {
                var saved = Store.SavedGame(mode);
                if (saved != null && !saved.over && (mode != Mode.Daily || saved.day == today)) return saved;
            }
            if (mode == Mode.Daily) return Engine.DailyGame(today);
            return Engine.NewGame(mode, size, null, "", undo);
        }

        public Play(Mode mode, Action onQuit, Action<string> onSheet)
        {
            this.mode = mode;
            this.onQuit = onQuit;
            this.onSheet = onSheet;
            game = StartGame(mode, Store.settings.size, Store.settings.undo);

            // heads-up display
            var pause = Kit.Btn("icon-btn", () =>
            {
                Sound.I.Click();
                Pause();
            }, Kit.Icon("pause"));
            whereMain = Kit.Txt("", "where-main", Face.Bold);
            whereSub = Kit.Txt("", "where-sub");
            undoCount = Kit.Txt("", "count", Face.Bold);
            undoBtn = Kit.Btn("icon-btn", Rewind, Kit.Icon("undo"), undoCount);
            var hud = Kit.El("hud", pause, Kit.El("where", whereMain, whereSub), undoBtn);

            var scoreN = Kit.Txt("0", "score-n", Face.Display);
            score = new Kit.Counter(scoreN, game.score);
            scoreBox = Kit.El("score", Kit.Txt("Score", "score-label", Face.Bold), scoreN);
            bestN = Kit.Txt("0", "score-best", Face.Display);
            var bestBox = Kit.El("score", Kit.Txt("Best", "score-label", Face.Bold), bestN);
            var scores = Kit.El("scores", scoreBox, bestBox);
            Layer.Next(scores);

            slot = Kit.El("board-slot");
            slot.pickingMode = PickingMode.Ignore;
            stage = Kit.El("stage", slot);
            stage.pickingMode = PickingMode.Ignore;

            dots = Kit.El("dots");
            topLabel = Kit.Txt("", "seq-text", Face.Bold);
            goalLabel = Kit.Txt("", "seq-text", Face.Bold);
            var sequence = Kit.El("sequence", dots, Kit.El("seq-label", topLabel, goalLabel));

            center = Kit.El("center", scores, stage, sequence);
            scoresRow = scores;
            sequenceRow = sequence;
            center.pickingMode = PickingMode.Ignore;
            root = Kit.El("play", hud, center);
            root.pickingMode = PickingMode.Position;

            // swipes anywhere on the screen
            root.RegisterCallback<PointerDownEvent>(e =>
            {
                if (!Live) return;
                swipeFrom = e.position;
                swipeDone = false;
                swipePointer = e.pointerId;
                root.CapturePointer(e.pointerId);
            });
            root.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!swipeFrom.HasValue || swipeDone || !Live || e.pointerId != swipePointer) return;
                var d = (Vector2)e.position - swipeFrom.Value;
                if (Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y)) < 26) return;
                swipeDone = true;
                Act(Mathf.Abs(d.x) > Mathf.Abs(d.y) ? (d.x > 0 ? Dir.Right : Dir.Left) : d.y > 0 ? Dir.Down : Dir.Up);
            });
            void End(int id)
            {
                swipeFrom = null;
                if (root.HasPointerCapture(id)) root.ReleasePointer(id);
            }
            root.RegisterCallback<PointerUpEvent>(e => End(e.pointerId));
            root.RegisterCallback<PointerCancelEvent>(e => End(e.pointerId));

            board = BoardView.Create("Board");
            board.showNames = Store.settings.names;
            board.showMarks = Store.settings.colorblind;
            center.RegisterCallback<GeometryChangedEvent>(_ => FitSlot());
            slot.RegisterCallback<GeometryChangedEvent>(_ => PlaceBoard());

            Refresh();
            first = true;
            lastMove = Time.unscaledTime;
            Sound.I.GameMusic(game.best, game.target, mode == Mode.Zen);
            Stage.I.Sky.SetHeat(Stars.Heat(game.best, game.target));

            // the screen comes in: a slight zoom out of a blur
            root.Fade(0);
            Tw.Run(0.55f, Ease.Out, t =>
            {
                root.Fade(t);
                root.Size(0.96f + 0.04f * t);
            }, 0, null, root);
            if (game.over) Tw.After(0.3f, ShowEnd);
        }

        bool first;

        void FitSlot()
        {
            // the board is as big as fits between the scores and the sequence, up to 520 points, and the three
            // sit together in the middle of the screen
            var c = center.layout;
            if (float.IsNaN(c.width) || c.width <= 0) return;
            float used = scoresRow.layout.height + 16 + sequenceRow.layout.height + 16 + 24;
            float side = Mathf.Floor(Mathf.Min(c.width, c.height - used, 520));
            if (side <= 40) side = Mathf.Max(40, Mathf.Floor(Mathf.Min(c.width, 520) * 0.6f));
            if (Mathf.Abs(slot.resolvedStyle.width - side) < 0.5f) return;
            slot.style.width = side;
            slot.style.height = side;
            stage.style.height = side;
        }

        void PlaceBoard()
        {
            var r = slot.worldBound;
            if (r.width <= 1) return;
            var (c, w) = UI.ToWorld(r);
            board.Layout(c, w, game.n);
            if (first)
            {
                first = false;
                board.Show(game, Slide, true);
            }
        }

        /// <summary>Puts a whole game on the board at once (used by the automated screenshot run).</summary>
        public void Load(Game g)
        {
            Set(g);
            ended = false;
            board.Clear();
            first = true;
            PlaceBoard();
            Refresh();
            Sound.I.Follow(g.best, g.target);
            Stage.I.Sky.SetHeat(Stars.Heat(g.best, g.target));
        }

        public Game Current => game;

        public void Restyle()
        {
            board.showNames = Store.settings.names;
            board.showMarks = Store.settings.colorblind;
            board.Restyle();
        }

        void Set(Game g)
        {
            game = g;
            Store.SaveGame(g, g.mode);
        }

        void Refresh()
        {
            var goal = Stars.Of(game.target);
            whereMain.text = mode == Mode.Daily ? $"Daily · {Stars.ShortDay(game.day)}" : $"{Stars.ModeName(mode)} · {game.n}×{game.n}";
            whereSub.text = $"Goal · {goal.name} {Stars.Fmt(game.target)}";
            undoBtn.Enable(Engine.CanUndo(game));
            undoCount.text = game.EndlessUndo ? "" : game.undoLeft.ToString();
            undoCount.Show(!game.EndlessUndo && game.undoLeft > 0);
            score.Set(game.score);
            long best = Math.Max(Store.stats.bestScore[Store.ScoreKey(game)], game.score);
            bestN.text = Stars.Fmt(best);

            // the stellar sequence under the board
            int count = 0;
            for (int v = 2; v <= game.target; v *= 2) count++;
            if (dotEls.Count != count)
            {
                dots.Clear();
                dotEls.Clear();
                for (int i = 0; i < count; i++)
                {
                    var d = Kit.El("dot");
                    dots.Add(d);
                    dotEls.Add(d);
                }
            }
            int k = 0;
            for (int v = 2; v <= game.target; v *= 2, k++)
            {
                bool on = game.best >= v;
                var c = Stars.Of(v).accent;
                dotEls[k].style.backgroundColor = on ? c : new Color(1, 1, 1, 0.08f);
            }
            var top = Stars.Of(game.best);
            int nextUp = game.best >= game.target ? game.best * 2 : game.target;
            topLabel.text = $"{top.name} · {Stars.Fmt(game.best)}";
            goalLabel.text = $"{(game.best >= game.target ? "Next" : "Goal")} · {Stars.Of(nextUp).name} {Stars.Fmt(nextUp)}";
        }

        // ---- moves --------------------------------------------------------------------------------------------------

        public void Act(Dir dir)
        {
            if (game.over) return;
            var r = Engine.Move(game, dir);
            if (!r.moved)
            {
                board.Nudge(dir);
                Sound.I.Blocked();
                Native.Haptic(Feel.Blocked);
                return;
            }
            Set(r.game);
            board.Show(r.game, Slide);
            Refresh();
            Effects(r, dir);
        }

        void Effects(MoveResult r, Dir dir)
        {
            var g = r.game;
            float slide = Slide;
            Sound.I.Move(dir);
            int top = r.merged.Count > 0 ? r.merged[r.merged.Count - 1].value : 0;
            Native.Haptic(top >= 1024 ? Feel.Huge : top >= 256 ? Feel.Big : r.merged.Count > 0 ? Feel.Merge : Feel.Move);
            if (r.merged.Count > 0)
            {
                Sound.I.Merges(r.merged, g.n);
                var merged = r.merged.ToList();
                Tw.After(slide * 0.8f, () =>
                {
                    foreach (var m in merged)
                    {
                        var at = board.CellWorld(m.r, m.c);
                        var s = Stars.Of(m.value);
                        float power = Mathf.Min(1.7f, Mathf.Log(m.value, 2) / 7f);
                        Stage.I.Sparks.Burst(at, s.accent, power, UI.Px);
                        var scr = Stage.I.WorldToScreen(at);
                        Stage.I.Sky.Ripple(new Vector2(scr.x / Screen.width, scr.y / Screen.height), 0.25f + power * 0.55f, s.from);
                    }
                    if (top >= 128) Stage.I.Flare(Mathf.Clamp01((Mathf.Log(top, 2) - 6) / 5f) * 0.6f);
                    if (top >= 512 && !Store.settings.reduceMotion) board.Quake(top >= 1024);
                    if (top >= 1024) Stage.I.Punch(top >= 2048 ? 0.9f : 0.55f);
                });
                Gain(r.gained);
                if (r.merged.Count >= 2) Chain(r.merged.Count);
            }
            if (r.spawned != null) Sound.I.Spawn(r.spawned.c, g.n);
            Sound.I.Follow(g.best, g.target);
            Stage.I.Sky.SetHeat(Stars.Heat(g.best, g.target));

            // records
            float now = Time.unscaledTime;
            long spent = (long)(Mathf.Min(30, now - lastMove) * 1000);
            lastMove = now;
            string key = Store.ScoreKey(g);
            long prevBest = Store.stats.bestScore[key];
            Store.SetStats(s =>
            {
                s.merges += r.merged.Count;
                s.moves += 1;
                s.playMs += spent;
                s.bestTile = Math.Max(s.bestTile, g.best);
                s.bestCombo = Math.Max(s.bestCombo, r.merged.Count);
                s.bestScore[key] = Math.Max(prevBest, g.score);
                if (g.mode == Mode.Daily && !string.IsNullOrEmpty(g.day)) s.dailyBest[g.day] = Math.Max(s.dailyBest[g.day], g.score);
                s.wins += r.justWon ? 1 : 0;
            });
            if (prevBest > 0 && g.score > prevBest && g.score - r.gained <= prevBest) Sound.I.NewBest();
            var got = Achievements.Earned(g, r.merged.Count, Store.stats);
            if (got.Count > 0)
            {
                long when = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                Store.SetStats(s =>
                {
                    foreach (var id in got) s.achievements[id] = when;
                });
                Tw.After(0.45f, () =>
                {
                    Sound.I.Achievement();
                    foreach (var id in got) UI.I.Toast(Achievements.Find(id));
                });
            }

            if (r.justWon && !g.keepGoing)
                Tw.After(slide + 0.26f, () =>
                {
                    Sound.I.Supernova();
                    Stage.I.Sky.Supernova();
                    Stage.I.Flare(1);
                    Stage.I.Punch(1.2f, 1.1f);
                    Native.Haptic(Feel.Win);
                    var c = board.CellWorld((g.n - 1) / 2f, (g.n - 1) / 2f);
                    Stage.I.Sparks.Burst(c, Stars.Hex("#bfe4ff"), 2.4f, UI.Px);
                    Stage.I.Sparks.Burst(c, Stars.Hex("#ffd36b"), 1.8f, UI.Px);
                    ShowNova();
                });
            if (r.justOver)
            {
                Store.SetStats(s => s.games += 1);
                Tw.After(0.65f, () =>
                {
                    Sound.I.GameOver();
                    Native.Haptic(Feel.Lose);
                    ShowEnd();
                });
            }
        }

        void Gain(int v)
        {
            var g = Kit.Txt($"+{Stars.Fmt(v)}", "gain", Face.Display);
            scoreBox.Add(g);
            Tw.Run(0.9f, x => x, k =>
            {
                float a, y, s;
                if (k < 0.2f)
                {
                    float e = Ease.Out(k / 0.2f);
                    a = e;
                    y = 10 * (1 - e);
                    s = 0.8f + 0.28f * e;
                }
                else
                {
                    float e = Ease.Out((k - 0.2f) / 0.8f);
                    a = 1 - e;
                    y = -26 * e;
                    s = 1.08f - 0.08f * e;
                }
                g.Fade(a);
                g.Move(0, y);
                g.Size(s);
            }, 0, () => g.RemoveFromHierarchy());
        }

        VisualElement chainEl;

        void Chain(int n)
        {
            chainEl?.RemoveFromHierarchy();
            var pill = Kit.El("chain-pill", Kit.Txt($"×{n} chain", "chain-text", Face.Display)).Fill(Kit.ChainFill);
            var c = Kit.El("chain", pill);
            c.pickingMode = PickingMode.Ignore;
            chainEl = c;
            scoreBox.Add(c);
            Tw.Run(1.3f, x => x, k =>
            {
                float a, s, y = 0;
                if (k < 0.15f)
                {
                    float e = Ease.Spring(k / 0.15f);
                    a = Mathf.Clamp01(e);
                    s = Mathf.LerpUnclamped(0.4f, 1.15f, e);
                }
                else if (k < 0.3f)
                {
                    a = 1;
                    s = Mathf.Lerp(1.15f, 1, Ease.Spring((k - 0.15f) / 0.15f));
                }
                else if (k < 0.8f)
                {
                    a = 1;
                    s = 1;
                }
                else
                {
                    float e = (k - 0.8f) / 0.2f;
                    a = 1 - e;
                    s = 1 - 0.05f * e;
                    y = -12 * e;
                }
                pill.Fade(a);
                pill.Size(s);
                pill.Move(0, y);
            }, 0, () => c.RemoveFromHierarchy(), "chain");
        }

        void Rewind()
        {
            if (!Engine.CanUndo(game)) return;
            var u = Engine.Undo(game);
            Set(u);
            board.Show(u, Slide);
            Refresh();
            Sound.I.Undo();
            Native.Haptic(Feel.Tap);
            if (ended)
            {
                ended = false;
                endLayer?.Dismiss();
                endLayer = null;
                Sound.I.Muffle(false);
                Sound.I.Follow(u.best, u.target);
                Store.SetStats(s => s.games = Math.Max(0, s.games - 1));
            }
        }

        void Restart()
        {
            if (game.moves > 0 && !game.over) Store.SetStats(s => s.games += 1);
            var fresh = StartGame(mode, Store.settings.size, Store.settings.undo, true);
            Set(fresh);
            ended = false;
            paused = false;
            novaUp = false;
            pauseLayer?.Dismiss();
            pauseLayer = null;
            endLayer?.Dismiss();
            endLayer = null;
            HideNova();
            board.Clear();
            first = true;
            PlaceBoard();
            Refresh();
            Sound.I.GameMusic(fresh.best, fresh.target, mode == Mode.Zen);
            Stage.I.Sky.SetHeat(Stars.Heat(fresh.best, fresh.target));
        }

        // ---- pause, the end and the supernova -----------------------------------------------------------------------

        public void Pause()
        {
            if (!Live) return;
            paused = true;
            Sound.I.Muffle(true);
            pauseLayer = new Layer(false, Resume);
            var p = pauseLayer.panel;
            string where = mode == Mode.Daily ? $"Daily · {Stars.ShortDay(game.day)}" : $"{Stars.ModeName(mode)} · {game.n}×{game.n}";
            p.Add(Kit.Txt("Paused", "card-title", Face.Display));
            p.Add(Kit.Txt($"{where} · score {Stars.Fmt(game.score)}", "card-sub"));
            p.Add(Layer.Button("Resume", "play", "primary wide", () => pauseLayer.Close()));
            p.Add(Layer.Row2(
                Layer.Button("New game", "restart", "glass", () =>
                {
                    Sound.I.Click();
                    Restart();
                }),
                Layer.Button("Settings", null, "glass", () =>
                {
                    Sound.I.Click();
                    onSheet("settings");
                })));
            p.Add(Layer.Row2(
                Layer.Button("How to play", null, "glass", () =>
                {
                    Sound.I.Click();
                    onSheet("howto");
                }),
                Layer.Button("Title", "home", "glass", () =>
                {
                    Sound.I.Click();
                    var l = pauseLayer;
                    pauseLayer = null;
                    paused = false;
                    l?.Dismiss();
                    onQuit();
                })));
        }

        void Resume()
        {
            paused = false;
            pauseLayer = null;
            Sound.I.Muffle(false);
        }

        void ShowEnd()
        {
            if (ended) return;
            ended = true;
            var top = Stars.Of(game.best);
            long best = Math.Max(Store.stats.bestScore[Store.ScoreKey(game)], game.score);
            endLayer = new Layer(false, null);
            var p = endLayer.panel;
            var trophy = Kit.UiTile(game.best, 84);
            trophy.AddToClassList("trophy");
            p.Add(trophy);
            p.Add(Kit.Txt("No moves left", "card-title", Face.Display));
            string extra = mode == Mode.Daily ? "A new daily board arrives tomorrow." : game.score >= best && game.score > 0 ? "A new best score." : "";
            p.Add(Kit.Txt($"You made {Stars.A(top.name)}. {extra}".Trim(), "card-sub"));
            var final = Kit.Txt("0", "final", Face.Display);
            var count = new Kit.Counter(final, 0);
            p.Add(final);
            count.Set(game.score, 1.1f);
            var stats = Kit.El("end-stats",
                Kit.El("end-stat", Kit.Txt(Stars.Fmt(game.moves), "end-n", Face.Display), Kit.Txt("Moves", "end-label", Face.Bold)),
                Kit.El("end-stat", Kit.Txt(Stars.Fmt(game.merges), "end-n", Face.Display), Kit.Txt("Fused", "end-label", Face.Bold)),
                Kit.El("end-stat", Kit.Txt(Stars.Fmt(best), "end-n", Face.Display), Kit.Txt("Best", "end-label", Face.Bold)));
            Layer.Next(stats);
            p.Add(stats);
            p.Add(Layer.Button(mode == Mode.Daily ? "Try today’s board again" : "Play again", "restart", "primary wide", () =>
            {
                Sound.I.Click();
                Restart();
            }));
            var second = Engine.CanUndo(game)
                ? Layer.Button("Rewind", "undo", "glass", Rewind)
                : Layer.Button("Share", "share", "glass", () =>
                {
                    Sound.I.Click();
                    Native.Share($"I made {Stars.A(top.name)} ({game.best}) and scored {Stars.Fmt(game.score)} in Zenith.");
                });
            p.Add(Layer.Row2(second, Layer.Button("Title", "home", "glass", () =>
            {
                Sound.I.Click();
                endLayer?.Dismiss();
                endLayer = null;
                onQuit();
            })));
        }

        void ShowNova()
        {
            novaUp = true;
            var goal = Stars.Of(game.target);
            nova = Kit.El("nova");
            nova.pickingMode = PickingMode.Position;
            nova.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
            var glow = Kit.El("nova-glow");
            glow.style.backgroundImage = new StyleBackground(Kit.GlowTex);
            glow.style.unityBackgroundImageTintColor = new Color(170 / 255f, 220 / 255f, 1, 0.3f);
            glow.pickingMode = PickingMode.Ignore;
            nova.Add(glow);
            float cell = Mathf.Min(UI.I.Root.layout.width * 0.46f, 210);
            var big = Kit.UiTile(game.target, cell);
            var flash = Kit.El("ui-tile-sheen");
            flash.style.bottom = 0;
            flash.style.backgroundColor = Color.white;
            big.Add(flash);
            nova.Add(big);
            var title = Kit.Txt("SUPERNOVA", "nova-title", Face.Display);
            nova.Add(title);
            var text = Kit.Txt($"You fused {Stars.A(goal.name)} in {Stars.Fmt(game.moves)} moves. The universe is yours to keep growing.", "nova-text", Face.Bold);
            nova.Add(text);
            var actions = Kit.El("nova-actions",
                Layer.Button("Keep going", null, "primary wide", () =>
                {
                    Sound.I.Click();
                    Set(Engine.KeepGoing(game));
                    HideNova();
                }),
                Layer.Row2(
                    Layer.Button("Share", "share", "glass", () =>
                    {
                        Sound.I.Click();
                        Native.Share($"I went Supernova ({game.target}) in {Stars.Fmt(game.moves)} moves in Zenith. Score {Stars.Fmt(game.score)}.");
                    }),
                    Layer.Button("New game", "restart", "glass", () =>
                    {
                        Sound.I.Click();
                        Restart();
                    })));
            nova.Add(actions);
            UI.I.Overlays.Add(nova);

            nova.Fade(0);
            Tw.Run(0.6f, x => x, t => nova.Fade(t));
            // the tile arrives: tiny, turned and blazing white, overshoots, settles
            Tw.Run(1.6f, x => x, k =>
            {
                float s, rot, a, white;
                if (k < 0.6f)
                {
                    float e = Ease.Spring(k / 0.6f);
                    s = Mathf.LerpUnclamped(0.2f, 1.08f, e);
                    rot = Mathf.LerpUnclamped(-25, 3, e);
                    a = Mathf.Clamp01(e);
                    white = Mathf.Lerp(0.85f, 0.25f, Mathf.Clamp01(e));
                }
                else
                {
                    float e = Ease.Spring((k - 0.6f) / 0.4f);
                    s = Mathf.LerpUnclamped(1.08f, 1, e);
                    rot = Mathf.LerpUnclamped(3, 0, e);
                    a = 1;
                    white = Mathf.Lerp(0.25f, 0, e);
                }
                big.Size(s);
                big.Spin(rot);
                big.Fade(a);
                flash.Fade(white);
            });
            Kit.Rise(title, 0.5f, 1);
            Kit.Rise(text, 0.7f, 1);
            Kit.Rise(actions, 0.9f, 1);
        }

        void HideNova()
        {
            novaUp = false;
            if (nova == null) return;
            var n = nova;
            nova = null;
            Tw.Run(0.3f, x => x, t => n.Fade(1 - t), 0, () => n.RemoveFromHierarchy());
        }

        // ---- input --------------------------------------------------------------------------------------------------

        public void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (Live)
            {
                if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) Act(Dir.Up);
                else if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) Act(Dir.Down);
                else if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) Act(Dir.Left);
                else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) Act(Dir.Right);
                else if (kb.zKey.wasPressedThisFrame || kb.uKey.wasPressedThisFrame) Rewind();
                else if (kb.pKey.wasPressedThisFrame) Pause();
            }
        }

        /// <summary>The back button or Escape with nothing open: pause. True when it was handled.</summary>
        public bool Back()
        {
            if (Live)
            {
                Pause();
                return true;
            }
            return novaUp;
        }

        public void Leave()
        {
            HideNova();
            pauseLayer?.Dismiss();
            endLayer?.Dismiss();
            root.RemoveFromHierarchy();
            if (board) UnityEngine.Object.Destroy(board.gameObject);
        }

        public void OnResize() => FitSlot();
    }
}
