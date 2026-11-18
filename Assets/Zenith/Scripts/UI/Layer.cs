// A dimmed backdrop with a card or a sheet on it. Closes on the backdrop, the Escape key or the phone's back
// button, and only the one on top closes.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Zenith
{
    public class Layer
    {
        public static readonly List<Layer> Open = new List<Layer>();

        public readonly VisualElement scrim, panel;
        readonly Action onClose;
        readonly bool sheet;
        bool closing;

        public Layer(bool sheet, Action onClose)
        {
            this.sheet = sheet;
            this.onClose = onClose;
            scrim = Kit.El("scrim" + (sheet ? " sheet-scrim" : ""));
            scrim.pickingMode = PickingMode.Position;
            panel = Kit.El(sheet ? "sheet" : "card").Fill(sheet ? Kit.SheetFill : Kit.CardFill);
            panel.pickingMode = PickingMode.Position;
            if (sheet) panel.style.paddingBottom = UI.I.SafeBottom;
            else
            {
                scrim.style.paddingTop = 20 + UI.I.SafeTop;
                scrim.style.paddingBottom = 20 + UI.I.SafeBottom;
            }
            scrim.Add(panel);
            scrim.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.target == scrim) Close();
                e.StopPropagation();
            });
            // swallow taps so nothing underneath reacts
            panel.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
            UI.I.Overlays.Add(scrim);
            Open.Add(this);
            scrim.Fade(0);
            Tw.Run(0.25f, x => x, t => scrim.Fade(t), 0, null, scrim);
            if (sheet)
            {
                panel.Move(0, 900);
                panel.RegisterCallback<GeometryChangedEvent>(First);
            }
            else
            {
                panel.Fade(0);
                Tw.Run(0.42f, Ease.Spring, t =>
                {
                    panel.Fade(Mathf.Clamp01(t * 1.6f));
                    panel.Move(0, 24 * (1 - t));
                    panel.Size(0.94f + 0.06f * t);
                }, 0, null, panel);
            }
        }

        void First(GeometryChangedEvent e)
        {
            panel.UnregisterCallback<GeometryChangedEvent>(First);
            float h = panel.layout.height;
            Tw.Run(0.45f, Ease.Out, t => panel.Move(0, h * (1 - t)), 0, null, panel);
        }

        public bool Closable => onClose != null;

        public void Close()
        {
            if (closing || onClose == null) return;
            Sound.I?.Click();
            Remove(onClose);
        }

        /// <summary>Takes the layer away without asking: the game moved on.</summary>
        public void Dismiss(Action after = null) => Remove(after);

        void Remove(Action after)
        {
            if (closing) return;
            closing = true;
            Open.Remove(this);
            float h = panel.layout.height;
            Tw.Run(0.22f, x => x, t =>
            {
                scrim.Fade(1 - t);
                if (sheet) panel.Move(0, (float.IsNaN(h) ? 600 : h) * Ease.Out(t));
            }, 0, () => scrim.RemoveFromHierarchy(), scrim);
            Tw.After(0.2f, () => after?.Invoke());
        }

        /// <summary>The Escape key or back button: closes the top layer if it can be closed. True when a layer was open.</summary>
        public static bool CloseTop()
        {
            if (Open.Count == 0) return false;
            var top = Open[Open.Count - 1];
            if (top.Closable) top.Close();
            return true;
        }

        public static bool Any => Open.Count > 0;

        // ---- shared pieces ----------------------------------------------------------------------------------------------

        public static VisualElement Head(string title, Action onClose)
        {
            var close = Kit.Btn("icon-btn", onClose, Kit.Icon("close"));
            return Kit.El("sheet-head", Kit.Txt(title, "sheet-title", Face.Display), close);
        }

        public static VisualElement Button(string text, string icon, string classes, Action onTap)
        {
            var b = Kit.Btn("btn " + classes, onTap);
            if (icon != null) b.Add(Kit.Icon(icon));
            b.Add(Kit.Txt(text, "btn-label", Face.Bold));
            if (classes.Contains("primary")) b.Fill(Kit.GoldFill);
            return b;
        }

        public static VisualElement Row2(params VisualElement[] kids)
        {
            var r = Kit.El("row2", kids);
            Next(r);
            return r;
        }

        /// <summary>Marks every child after the first, for spacing between siblings.</summary>
        public static void Next(VisualElement parent)
        {
            for (int i = 0; i < parent.childCount; i++) parent[i].EnableInClassList("next", i > 0);
        }
    }
}
