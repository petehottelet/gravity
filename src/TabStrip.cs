using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Gravity
{
    /// <summary>A lightweight owner-drawn tab bar.</summary>
    internal sealed partial class TabStrip : Control
    {
        internal sealed class Tab
        {
            public string Title = "";
            public string ToolTip = "";
            public bool Modified;
        }

        public readonly List<Tab> Tabs = new List<Tab>();
        public event Action<int> TabSelected;
        public event Action<int> TabCloseRequested;
        public event EventHandler AddRequested;

        private int selected = -1, hover = -1;
        private bool hoverClose, hoverAdd;
        private readonly ToolTip tip = new ToolTip();
        private int tipFor = -2;
        private readonly float dpi;

        public TabStrip()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) dpi = g.DpiX;
            Height = S(34);
            BackColor = Theme.Chrome;
            Font = new Font("Segoe UI", 9f);
            TabStop = true;
            AccessibleName = "Documents";
        }

        private int S(float v) { return (int)Math.Round(v * dpi / 96f); }

        public int SelectedIndex
        {
            get { return selected; }
            set
            {
                if (selected == value) { Invalidate(); return; }
                selected = value; Invalidate();
                AccessibilityNotifyClients(AccessibleEvents.SelectionWithin, -1);
                if (Focused && selected >= 0) AccessibilityNotifyClients(AccessibleEvents.Focus, selected);
            }
        }

        private int TabWidth
        {
            get
            {
                if (Tabs.Count == 0) return 0;
                int avail = Width - S(8) - S(36);
                return Math.Max(S(80), Math.Min(S(230), avail / Tabs.Count));
            }
        }

        private Rectangle TabRect(int i)
        {
            int w = TabWidth;
            return new Rectangle(S(6) + i * w, S(5), w, Height - S(5));
        }

        private Rectangle CloseRect(Rectangle tab)
        {
            int s = S(18);
            return new Rectangle(tab.Right - s - S(8), tab.Y + (tab.Height - s) / 2, s, s);
        }

        private Rectangle AddRect
        {
            get
            {
                int x = S(6) + Tabs.Count * TabWidth + S(4);
                int s = S(24);
                return new Rectangle(x, S(5) + (Height - S(5) - s) / 2, s, s);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Chrome);
            using (var line = new Pen(Theme.ChromeBorder))
                g.DrawLine(line, 0, Height - 1, Width, Height - 1);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            for (int i = 0; i < Tabs.Count; i++)
            {
                var r = TabRect(i);
                bool sel = i == selected;
                if (sel || i == hover)
                {
                    var rr = new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height + S(6));
                    using (var path = Theme.RoundRect(rr, S(7)))
                    {
                        using (var b = new SolidBrush(sel ? Theme.TabActive : Color.FromArgb(0xEC, 0xED, 0xEF)))
                            g.FillPath(b, path);
                        if (sel)
                            using (var pen = new Pen(Theme.ChromeBorder))
                                g.DrawPath(pen, path);
                    }
                    if (sel)
                        using (var b = new SolidBrush(Theme.TabActive))
                            g.FillRectangle(b, r.X + 1, Height - 1, r.Width - 1, 1);
                }
                else if (i + 1 != selected && i < Tabs.Count - 1)
                {
                    using (var pen = new Pen(Theme.ChromeBorder))
                        g.DrawLine(pen, r.Right - 1, r.Y + S(8), r.Right - 1, r.Bottom - S(8));
                }
                var t = Tabs[i];
                if (sel && Focused) ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(r, -S(4), -S(4)), Theme.Accent, Theme.Chrome);
                int textLeft = r.X + S(12);
                if (t.Modified)
                {
                    int d = S(7);
                    using (var b = new SolidBrush(Theme.Modified))
                        g.FillEllipse(b, textLeft, r.Y + (r.Height - d) / 2, d, d);
                    textLeft += d + S(6);
                }
                var cr = CloseRect(r);
                var textRect = new Rectangle(textLeft, r.Y, cr.X - textLeft - S(2), r.Height);
                TextRenderer.DrawText(g, t.Title, Font, textRect, sel ? Theme.Text : Theme.SubtleText,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                if (sel || i == hover)
                {
                    if (i == hover && hoverClose)
                        using (var b = new SolidBrush(Color.FromArgb(0xDD, 0xDE, 0xE1)))
                            g.FillEllipse(b, cr);
                    LucideIcons.Draw(g, "x", Rectangle.Inflate(cr, -S(2), -S(2)), Theme.SubtleText);
                }
            }
            var ar = AddRect;
            if (hoverAdd)
                using (var b = new SolidBrush(Color.FromArgb(0xE3, 0xE4, 0xE7)))
                    g.FillEllipse(b, ar);
            LucideIcons.Draw(g, "plus", Rectangle.Inflate(ar, -S(3), -S(3)), Theme.SubtleText);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = -1;
            bool hc = false;
            for (int i = 0; i < Tabs.Count; i++)
            {
                var r = TabRect(i);
                if (r.Contains(e.Location)) { h = i; hc = CloseRect(r).Contains(e.Location); break; }
            }
            bool ha = AddRect.Contains(e.Location);
            if (h != hover || hc != hoverClose || ha != hoverAdd)
            {
                hover = h;
                hoverClose = hc;
                hoverAdd = ha;
                Invalidate();
            }
            int want = ha ? -1 : h;
            if (want != tipFor)
            {
                tipFor = want;
                tip.SetToolTip(this, ha ? "Open a PDF (Ctrl+O)" : h >= 0 ? Tabs[h].ToolTip : "");
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hover = -1;
            hoverClose = hoverAdd = false;
            tipFor = -2;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (AddRect.Contains(e.Location) && e.Button == MouseButtons.Left)
            {
                var a = AddRequested;
                if (a != null) a(this, EventArgs.Empty);
                return;
            }
            for (int i = 0; i < Tabs.Count; i++)
            {
                var r = TabRect(i);
                if (!r.Contains(e.Location)) continue;
                if (e.Button == MouseButtons.Middle || (e.Button == MouseButtons.Left && CloseRect(r).Contains(e.Location)))
                {
                    var c = TabCloseRequested;
                    if (c != null) c(i);
                }
                else if (e.Button == MouseButtons.Left)
                {
                    var s = TabSelected;
                    if (s != null) s(i);
                }
                return;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) tip.Dispose();
            base.Dispose(disposing);
        }
    }
}
