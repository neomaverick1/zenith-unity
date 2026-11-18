// The board you play on, in the 3D scene: the glass panel, and one view per star that slides, pops, grows in and
// fades back on undo. Each move is drawn by comparing the new game with what's on screen, star by star, so a
// star keeps its view as it slides and two stars that merge visibly slide into each other.
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Zenith
{
    public class BoardView : MonoBehaviour
    {
        enum Anim { None, Pop, Appear, Restore }

        class View
        {
            public int id, value, r, c;
            public GameObject go;
            public Transform root, body;
            public MeshRenderer face;
            public TextMeshPro num, name, mark;
            public MaterialPropertyBlock mpb;
            public Vector3 from, to;
            public float slideStart, slideDur;
            public Anim anim;
            public float animStart, animDur;
            public bool ghost;
            public float hideAt = -1;
            public float sheenDelay;
            public int layer;
        }

        static TMP_FontAsset numFont, nameFont, markFont;
        static Material tileMat, boardMat;

        public int N { get; private set; } = 4;
        public float Size { get; private set; } = 4;
        public float alpha = 1;
        public bool showNames, showMarks;
        float GapFrac => N <= 3 ? 0.04f : N == 4 ? 0.032f : N == 5 ? 0.026f : 0.022f;
        public float Gap => Size * GapFrac;
        public float Cell => (Size - Gap * (N + 1)) / N;

        Transform content; // moves for nudges and quakes
        MeshRenderer panel;
        MaterialPropertyBlock panelMpb;
        readonly Dictionary<int, View> views = new Dictionary<int, View>();
        readonly Stack<View> pool = new Stack<View>();
        const float TilePad = 0.42f;
        const float BoardPad = 0.26f;
        Vector3 nudge;
        float nudgeStart = -10;
        float quakeStart = -10, quakeDur;
        float lastAlpha = -1;

        public static BoardView Create(string name)
        {
            var go = new GameObject(name);
            var b = go.AddComponent<BoardView>();
            return b;
        }

        void Awake()
        {
            if (!numFont)
            {
                numFont = Resources.Load<TMP_FontAsset>("Fonts/Unbounded-Bold SDF");
                nameFont = Resources.Load<TMP_FontAsset>("Fonts/Manrope-ExtraBold SDF");
                markFont = Resources.Load<TMP_FontAsset>("Fonts/ZenithMarks SDF");
                tileMat = Resources.Load<Material>("Materials/Tile");
                boardMat = Resources.Load<Material>("Materials/Board");
            }
            content = new GameObject("Content").transform;
            content.SetParent(transform, false);
            var p = new GameObject("Panel");
            p.transform.SetParent(content, false);
            p.AddComponent<MeshFilter>().sharedMesh = Stage.Quad;
            panel = p.AddComponent<MeshRenderer>();
            panel.sharedMaterial = boardMat;
            panel.sortingOrder = 0;
            panelMpb = new MaterialPropertyBlock();
        }

        /// <summary>Puts the board at a point in the scene, `size` units wide, with n×n cells.</summary>
        public void Layout(Vector3 center, float size, int n, Quaternion? tilt = null)
        {
            bool resized = n != N || Mathf.Abs(size - Size) > 1e-4f;
            transform.position = center;
            transform.rotation = tilt ?? Quaternion.identity;
            N = n;
            Size = size;
            panel.transform.localScale = new Vector3(size * (1 + 2 * BoardPad), size * (1 + 2 * BoardPad), 1);
            panelMpb.SetVector("_Grid", new Vector4(n, GapFrac, BoardPad, alpha));
            panel.SetPropertyBlock(panelMpb);
            if (resized)
                foreach (var v in views.Values)
                {
                    Fit(v);
                    v.from = v.to = CellLocal(v.r, v.c);
                    v.slideDur = 0;
                }
        }

        public Vector3 CellLocal(float r, float c)
        {
            float x = -Size / 2 + Gap + c * (Cell + Gap) + Cell / 2;
            float y = Size / 2 - (Gap + r * (Cell + Gap) + Cell / 2);
            return new Vector3(x, y, 0);
        }

        /// <summary>The center of a cell in the scene.</summary>
        public Vector3 CellWorld(float r, float c) => transform.TransformPoint(CellLocal(r, c));

        View Take(int id, int value)
        {
            var v = pool.Count > 0 ? pool.Pop() : Build();
            v.id = id;
            v.value = value;
            v.go.SetActive(true);
            v.ghost = false;
            v.hideAt = -1;
            v.anim = Anim.None;
            v.sheenDelay = id % 7 * 0.6f;
            Style(v);
            Fit(v);
            views[id] = v;
            return v;
        }

        View Build()
        {
            var v = new View { mpb = new MaterialPropertyBlock() };
            v.go = new GameObject("Star");
            v.root = v.go.transform;
            v.root.SetParent(content, false);
            v.body = new GameObject("Body").transform;
            v.body.SetParent(v.root, false);
            var f = new GameObject("Face");
            f.transform.SetParent(v.body, false);
            f.AddComponent<MeshFilter>().sharedMesh = Stage.Quad;
            v.face = f.AddComponent<MeshRenderer>();
            v.face.sharedMaterial = tileMat;
            v.num = Text(v.body, "Number", numFont);
            v.name = Text(v.body, "Name", nameFont);
            v.mark = Text(v.body, "Mark", markFont ? markFont : numFont);
            return v;
        }

        static TextMeshPro Text(Transform parent, string name, TMP_FontAsset font)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshPro>();
            if (font) t.font = font;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.rectTransform.sizeDelta = new Vector2(10, 2);
            t.isOrthographic = false;
            t.richText = false;
            t.raycastTarget = false;
            return t;
        }

        void Style(View v)
        {
            var s = Stars.Of(v.value);
            v.mpb.SetColor("_From", s.from);
            v.mpb.SetColor("_To", s.to);
            v.mpb.SetColor("_Glow", s.hasGlow ? s.glow : Color.clear);
            v.mpb.SetVector("_Shape", new Vector4(0.16f, TilePad, v.sheenDelay, s.aura == Aura.Corona ? 1 : s.aura == Aura.Pulse ? 2 : s.aura == Aura.Hole ? 3 : 0));
            v.num.text = v.value.ToString();
            v.num.color = s.ink;
            v.name.text = s.name;
            v.name.color = new Color(s.ink.r, s.ink.g, s.ink.b, 0.8f);
            v.mark.text = s.mark;
            v.mark.color = new Color(s.ink.r, s.ink.g, s.ink.b, 0.85f);
        }

        void Fit(View v)
        {
            float cell = Cell;
            float k = TilePad;
            v.face.transform.localScale = new Vector3(cell * (1 + 2 * k), cell * (1 + 2 * k), 1);
            int digits = v.value.ToString().Length;
            float fs = cell * (digits <= 2 ? 0.42f : digits == 3 ? 0.35f : digits == 4 ? 0.28f : 0.225f);
            v.num.fontSize = fs * 10; // TextMeshPro draws one em as a tenth of its font size
            v.num.characterSpacing = -3;
            float pxCell = cell / Mathf.Max(1e-5f, Stage.I ? Stage.I.WorldPerPixel * UI.Scale : cell / 80);
            bool names = showNames && pxCell >= 64;
            v.name.gameObject.SetActive(names);
            if (names)
            {
                float nameSize = cell * 0.105f;
                float total = fs + cell * 0.05f + nameSize * 1.45f;
                v.num.transform.localPosition = new Vector3(0, total / 2 - fs / 2, 0);
                v.name.fontSize = nameSize * 10;
                v.name.characterSpacing = 2;
                v.name.transform.localPosition = new Vector3(0, -(total / 2 - nameSize * 1.45f / 2), 0);
            }
            else v.num.transform.localPosition = Vector3.zero;
            v.mark.gameObject.SetActive(showMarks);
            if (showMarks)
            {
                float ms = cell * 0.16f;
                v.mark.fontSize = ms * 10;
                v.mark.alignment = TextAlignmentOptions.Center;
                v.mark.transform.localPosition = new Vector3(-cell * 0.5f + cell * 0.09f + ms * 0.5f, cell * 0.5f - cell * 0.06f - ms * 0.5f, 0);
            }
            v.num.alignment = TextAlignmentOptions.CenterGeoAligned;
        }

        void Order(View v, int layer)
        {
            v.layer = layer;
            int baseOrder = 10 + layer * 10;
            v.face.sortingOrder = baseOrder;
            v.num.sortingOrder = baseOrder + 1;
            v.name.sortingOrder = baseOrder + 1;
            v.mark.sortingOrder = baseOrder + 1;
            v.root.localPosition = new Vector3(v.root.localPosition.x, v.root.localPosition.y, -0.002f * layer);
        }

        /// <summary>
        /// Shows a game. Stars already on screen slide to their new cells over `slide` seconds; new stars pop (merges),
        /// grow in (new arrivals) or fade back (undo). With `enter`, every star grows in one after another.
        /// </summary>
        public void Show(Game g, float slide, bool enter = false, bool instant = false)
        {
            float now = Time.unscaledTime;
            var keep = new HashSet<int>();
            // the stars that just merged slide into their partner, under the star they made
            if (!instant && g.ghosts != null)
                foreach (var t in g.ghosts)
                {
                    if (!views.TryGetValue(t.id, out var v)) continue;
                    keep.Add(t.id);
                    v.ghost = true;
                    v.r = t.r;
                    v.c = t.c;
                    Order(v, 0);
                    SlideTo(v, CellLocal(t.r, t.c), now, slide);
                    v.anim = Anim.None;
                    v.hideAt = now + slide + 0.3f;
                }
            int i = 0;
            foreach (var t in g.tiles)
            {
                keep.Add(t.id);
                bool existed = views.TryGetValue(t.id, out var v);
                if (!existed) v = Take(t.id, t.value);
                v.ghost = false;
                v.hideAt = -1;
                v.r = t.r;
                v.c = t.c;
                if (!v.go.activeSelf) v.go.SetActive(true);
                Order(v, t.merged ? 2 : 1);
                var target = CellLocal(t.r, t.c);
                if (existed && !instant) SlideTo(v, target, now, slide);
                else
                {
                    v.from = v.to = target;
                    v.slideDur = 0;
                    v.root.localPosition = new Vector3(target.x, target.y, v.root.localPosition.z);
                }
                if (instant) Animate(v, Anim.None, 0, 0);
                else if (enter) Animate(v, Anim.Appear, now + 0.15f + i * 0.05f, 0.5f);
                else if (t.merged && !existed) Animate(v, Anim.Pop, now + slide * 0.8f, 0.28f);
                else if (t.spawned && !existed) Animate(v, Anim.Appear, now + slide, 0.24f);
                else if (t.restored && !existed) Animate(v, Anim.Restore, now, 0.3f);
                else if (!existed) Animate(v, Anim.Appear, now, 0.24f);
                i++;
            }
            var gone = new List<int>();
            foreach (var kv in views)
                if (!keep.Contains(kv.Key))
                    gone.Add(kv.Key);
            foreach (var id in gone) Release(id);
            Tick();
        }

        void SlideTo(View v, Vector3 target, float now, float dur)
        {
            v.from = v.root.localPosition;
            v.from.z = 0;
            v.to = target;
            v.slideStart = now;
            v.slideDur = dur;
        }

        void Animate(View v, Anim a, float start, float dur)
        {
            v.anim = a;
            v.animStart = start;
            v.animDur = dur;
        }

        void Release(int id)
        {
            if (!views.TryGetValue(id, out var v)) return;
            views.Remove(id);
            v.go.SetActive(false);
            pool.Push(v);
        }

        public void Clear()
        {
            foreach (var id in new List<int>(views.Keys)) Release(id);
        }

        public void Restyle()
        {
            foreach (var v in views.Values)
            {
                Style(v);
                Fit(v);
            }
        }

        /// <summary>The board leans the way you pushed when nothing could move.</summary>
        public void Nudge(Dir d)
        {
            float k = 7 * Stage.I.WorldPerPixel * UI.Scale;
            nudge = d switch { Dir.Left => new Vector3(-k, 0), Dir.Right => new Vector3(k, 0), Dir.Up => new Vector3(0, k), _ => new Vector3(0, -k) };
            nudgeStart = Time.unscaledTime;
        }

        /// <summary>The board shakes after a huge merge.</summary>
        public void Quake(bool big)
        {
            quakeStart = Time.unscaledTime;
            quakeDur = big ? 0.9f : 0.42f;
        }

        void Update() => Tick();

        void Tick()
        {
            float now = Time.unscaledTime;
            float px = Stage.I ? Stage.I.WorldPerPixel * UI.Scale : 0.01f;
            // nudge: out and back over 0.2s; quake: a few sharp jolts settling down
            Vector3 off = Vector3.zero;
            float rot = 0;
            float nk = (now - nudgeStart) / 0.2f;
            if (nk >= 0 && nk < 1) off += nudge * (nk < 0.4f ? Ease.Out(nk / 0.4f) : 1 - Ease.Out((nk - 0.4f) / 0.6f));
            float qk = (now - quakeStart) / Mathf.Max(0.01f, quakeDur);
            if (qk >= 0 && qk < 1)
            {
                off += new Vector3(
                    Ease.Keys(qk, new Vector2(0, 0), new Vector2(0.1f, -4), new Vector2(0.25f, 5), new Vector2(0.4f, -3), new Vector2(0.6f, 2), new Vector2(0.8f, -1), new Vector2(1, 0)),
                    -Ease.Keys(qk, new Vector2(0, 0), new Vector2(0.1f, 3), new Vector2(0.25f, -3), new Vector2(0.4f, -2), new Vector2(0.6f, 2), new Vector2(0.8f, 1), new Vector2(1, 0)),
                    0) * px;
                rot = Ease.Keys(qk, new Vector2(0, 0), new Vector2(0.1f, -0.4f), new Vector2(0.25f, 0.4f), new Vector2(0.4f, 0), new Vector2(1, 0));
            }
            content.localPosition = off;
            content.localRotation = Quaternion.Euler(0, 0, -rot);

            bool alphaChanged = Mathf.Abs(alpha - lastAlpha) > 1e-4f;
            if (alphaChanged)
            {
                lastAlpha = alpha;
                panelMpb.SetVector("_Grid", new Vector4(N, GapFrac, BoardPad, alpha));
                panel.SetPropertyBlock(panelMpb);
            }

            foreach (var v in views.Values)
            {
                if (v.hideAt > 0 && now >= v.hideAt)
                {
                    v.go.SetActive(false);
                    continue;
                }
                // slide
                Vector3 p = v.to;
                if (v.slideDur > 0)
                {
                    float k = Mathf.Clamp01((now - v.slideStart) / v.slideDur);
                    p = Vector3.LerpUnclamped(v.from, v.to, Ease.Out(k));
                }
                v.root.localPosition = new Vector3(p.x, p.y, -0.002f * v.layer);
                // pop, grow in, fade back
                float scale = 1, a = 1, bright = 1;
                if (v.anim != Anim.None)
                {
                    float k = (now - v.animStart) / Mathf.Max(0.001f, v.animDur);
                    if (k < 0)
                    {
                        scale = v.anim == Anim.Pop ? 0.5f : v.anim == Anim.Restore ? 0.7f : 0;
                        a = 0;
                    }
                    else if (k >= 1) v.anim = Anim.None;
                    else
                        switch (v.anim)
                        {
                            case Anim.Pop:
                                // spring: 0.5 -> 1.2 at 45% -> 1
                                float sp = Ease.Spring(k);
                                scale = k < 0.45f ? Mathf.Lerp(0.5f, 1.2f, Ease.Spring(k / 0.45f)) : Mathf.Lerp(1.2f, 1f, Ease.Spring((k - 0.45f) / 0.55f));
                                a = Mathf.Clamp01(k / 0.45f);
                                bright = 1 + 0.35f * (1 - sp);
                                break;
                            case Anim.Appear:
                                scale = Ease.Spring(k);
                                a = Mathf.Clamp01(Ease.Spring(k));
                                break;
                            case Anim.Restore:
                                float e = Ease.Out(k);
                                scale = Mathf.Lerp(0.7f, 1, e);
                                a = e;
                                break;
                        }
                }
                v.body.localScale = new Vector3(scale, scale, 1);
                float fa = a * alpha;
                v.mpb.SetVector("_Look", new Vector4(fa, bright, Stars.Of(v.value).shine ? 1 : 0, 0));
                v.face.SetPropertyBlock(v.mpb);
                SetAlpha(v.num, fa);
                if (v.name.gameObject.activeSelf) SetAlpha(v.name, fa * 0.8f);
                if (v.mark.gameObject.activeSelf) SetAlpha(v.mark, fa * 0.85f);
            }
        }

        static void SetAlpha(TextMeshPro t, float a)
        {
            if (Mathf.Abs(t.alpha - a) > 0.002f) t.alpha = a;
        }

        public bool Busy
        {
            get
            {
                float now = Time.unscaledTime;
                foreach (var v in views.Values)
                    if ((v.slideDur > 0 && now < v.slideStart + v.slideDur) || v.anim != Anim.None)
                        return true;
                return false;
            }
        }
    }
}
