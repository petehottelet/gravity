using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace Gravity
{
    /// <summary>Start page shown when no document is open: open/combine actions and recent files.</summary>
    internal sealed class HomeView : Control
    {
        public event EventHandler OpenRequested, CombineRequested;
        public event Action<string> RecentRequested;
        public event Action<string> RecentRemoveRequested;

        private List<RecentFile> recent = new List<RecentFile>();
        private readonly List<KeyValuePair<Rectangle, object>> hits = new List<KeyValuePair<Rectangle, object>>();
        private object hoverItem;
        private readonly float dpi;
        private readonly ContextMenuStrip recentMenu = new ContextMenuStrip();
        private string menuPath;

        private static readonly object OpenAction = new object(), CombineAction = new object();

        public HomeView()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) dpi = g.DpiX;
            BackColor = Theme.ViewBack;
            recentMenu.Items.Add("Open", null, delegate { if (menuPath != null && RecentRequested != null) RecentRequested(menuPath); });
            recentMenu.Items.Add("Remove from list", null, delegate { if (menuPath != null && RecentRemoveRequested != null) RecentRemoveRequested(menuPath); });
        }

        private int S(float v) { return (int)Math.Round(v * dpi / 96f); }

        public void SetRecent(List<RecentFile> list)
        {
            recent = list;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.ViewBack);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            hits.Clear();

            int colW = Math.Min(S(620), Width - S(48));
            int x = (Width - colW) / 2;
            int y = Math.Max(S(32), Height / 9);

            using (var sub = new Font("Segoe UI", 10f))
            using (var btnFont = new Font("Segoe UI Semibold", 10f))
            using (var itemFont = new Font("Segoe UI", 10f))
            using (var small = new Font("Segoe UI", 8.5f))
            using (var head = new Font("Segoe UI Semibold", 9f))
            {
                DrawLockup(g, x, y, S(46));
                TextRenderer.DrawText(g, "Fast PDF viewer and page organizer", sub, new Point(x + S(1), y + S(56)), Theme.SubtleText, TextFormatFlags.NoPadding);
                y += S(96);

                var b1 = new Rectangle(x, y, S(190), S(40));
                var b2 = new Rectangle(b1.Right + S(12), y, S(190), S(40));
                DrawButton(g, b1, "Open PDF…", "Ctrl+O", true, btnFont, small, hoverItem == OpenAction);
                DrawButton(g, b2, "Combine PDFs…", null, false, btnFont, small, hoverItem == CombineAction);
                hits.Add(new KeyValuePair<Rectangle, object>(b1, OpenAction));
                hits.Add(new KeyValuePair<Rectangle, object>(b2, CombineAction));
                y = b1.Bottom + S(36);

                if (recent.Count > 0)
                {
                    TextRenderer.DrawText(g, "RECENT", head, new Point(x, y), Theme.SubtleText, TextFormatFlags.NoPadding);
                    y += S(24);
                    int rowH = S(46);
                    var cardRect = new Rectangle(x, y, colW, 0);
                    int maxRows = Math.Max(1, (Height - y - S(60)) / rowH);
                    int shown = Math.Min(maxRows, recent.Count);
                    cardRect.Height = shown * rowH;
                    using (var path = Theme.RoundRect(cardRect, S(8)))
                    using (var white = new SolidBrush(Color.White))
                    using (var border = new Pen(Theme.ChromeBorder))
                    {
                        g.FillPath(white, path);
                        g.DrawPath(border, path);
                    }
                    for (int i = 0; i < shown; i++)
                    {
                        var r = new Rectangle(x + 1, y + i * rowH, colW - 2, rowH);
                        var item = recent[i];
                        if (ReferenceEquals(hoverItem, item))
                            using (var hb = new SolidBrush(Color.FromArgb(0xEE, 0xF4, 0xFC)))
                                g.FillRectangle(hb, Rectangle.Inflate(r, 0, -1));
                        if (i > 0)
                            using (var sep = new Pen(Color.FromArgb(0xEC, 0xED, 0xEF)))
                                g.DrawLine(sep, r.X + S(14), r.Y, r.Right - S(14), r.Y);
                        bool exists = File.Exists(item.Path);
                        string name = Path.GetFileName(item.Path), dir = Path.GetDirectoryName(item.Path);
                        LucideIcons.Draw(g, "book-open", new Rectangle(r.X + S(14), r.Y + (r.Height - S(20)) / 2, S(20), S(20)),
                            exists ? Theme.Accent : Theme.SubtleText);
                        var tr = new Rectangle(r.X + S(44), r.Y + S(5), r.Width - S(56), S(20));
                        TextRenderer.DrawText(g, name, itemFont, tr, exists ? Theme.Text : Theme.SubtleText, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.Left);
                        var dr = new Rectangle(r.X + S(44), r.Y + S(25), r.Width - S(56), S(16));
                        string pageInfo = item.State != null && item.State.Page > 0 ? "  ·  page " + (item.State.Page + 1) : "";
                        TextRenderer.DrawText(g, (exists ? dir : "Missing: " + dir) + pageInfo, small, dr, Theme.SubtleText, TextFormatFlags.PathEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.Left);
                        hits.Add(new KeyValuePair<Rectangle, object>(r, item));
                    }
                }
                else
                {
                    TextRenderer.DrawText(g, "Drop PDF files here, or open one to get started.", sub, new Point(x, y), Theme.SubtleText, TextFormatFlags.NoPadding);
                }
            }
        }

        private void DrawButton(Graphics g, Rectangle r, string text, string hint, bool primary, Font font, Font small, bool hot)
        {
            Color bg = primary ? (hot ? Theme.AccentHot : Theme.Accent) : (hot ? Color.FromArgb(0xF3, 0xF6, 0xFA) : Color.White);
            using (var path = Theme.RoundRect(r, S(6)))
            using (var b = new SolidBrush(bg))
            {
                g.FillPath(b, path);
                if (!primary)
                    using (var pen = new Pen(Theme.ChromeBorder))
                        g.DrawPath(pen, path);
            }
            Color fg = primary ? Color.White : Theme.Text;
            if (hint == null)
                TextRenderer.DrawText(g, text, font, r, fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            else
            {
                var left = new Rectangle(r.X + S(16), r.Y, r.Width - S(32), r.Height);
                TextRenderer.DrawText(g, text, font, left, fg, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                TextRenderer.DrawText(g, hint, small, left, primary ? Color.FromArgb(0xC8, 0xDD, 0xF3) : Theme.SubtleText, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            }
        }

        /// <summary>The logo lockup (icon + GRAVITY wordmark) scaled to a height; returns its width.</summary>
        private int DrawLockup(Graphics g, int x, int y, int height)
        {
            var img = Brand.Lockup;
            if (img == null) return 0;
            int width = (int)Math.Round(img.Width * (double)height / img.Height);
            var oldMode = g.InterpolationMode;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(img, new Rectangle(x, y, width, height));
            g.InterpolationMode = oldMode;
            return width;
        }

        private object HitTest(Point p)
        {
            foreach (var kv in hits) if (kv.Key.Contains(p)) return kv.Value;
            return null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var h = HitTest(e.Location);
            if (!ReferenceEquals(h, hoverItem))
            {
                hoverItem = h;
                Cursor = h != null ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hoverItem = null;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            var h = HitTest(e.Location);
            var rf = h as RecentFile;
            if (e.Button == MouseButtons.Right)
            {
                if (rf != null) { menuPath = rf.Path; recentMenu.Show(this, e.Location); }
                return;
            }
            if (e.Button != MouseButtons.Left) return;
            if (ReferenceEquals(h, OpenAction)) { if (OpenRequested != null) OpenRequested(this, EventArgs.Empty); }
            else if (ReferenceEquals(h, CombineAction)) { if (CombineRequested != null) CombineRequested(this, EventArgs.Empty); }
            else if (rf != null && RecentRequested != null) RecentRequested(rf.Path);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) recentMenu.Dispose();
            base.Dispose(disposing);
        }
    }
}
