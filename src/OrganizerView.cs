using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Gravity
{
    /// <summary>
    /// Thumbnail grid for rearranging pages: multi-select, drag to reorder, drop PDF files to insert.
    /// It edits nothing itself; it raises requests that MainForm turns into undoable page operations.
    /// </summary>
    internal sealed partial class OrganizerView : CanvasControl
    {
        public RenderService Renderer;

        /// <summary>Move the given pages so they start at an insertion index (0..Count).</summary>
        public event Action<List<PageRef>, int> MoveRequested;
        /// <summary>Insert PDF files at an insertion index.</summary>
        public event Action<string[], int> FilesDropped;
        /// <summary>Open a page in the reading view.</summary>
        public event Action<int> OpenPageRequested;
        public event EventHandler SelectionChanged;

        private EditDoc doc;
        private List<PageRef> pages = new List<PageRef>();
        private readonly HashSet<PageRef> selected = new HashSet<PageRef>(RefComparer<PageRef>.Instance);
        private int focus = -1, anchor = -1;
        private int thumb = 160;   // box size in 96-dpi pixels
        private bool sidebarMode;

        public bool SidebarMode
        {
            get { return sidebarMode; }
            set { if (sidebarMode != value) { sidebarMode = value; DoLayout(); } }
        }

        private int cols = 1, cellW, cellH, gridLeft, gridTop;

        private enum DragKind { None, Pending, Move, Band }
        private DragKind drag;
        private Point downPt, curPt;
        private int downIndex = -1;
        private bool deferredSelectOnly;
        private int insertAt = -1;
        private HashSet<PageRef> bandBase;
        private readonly Timer autoScroll = new Timer { Interval = 30 };
        private readonly ToolTip tip = new ToolTip { InitialDelay = 700 };
        private int tipIndex = -1;

        public OrganizerView()
        {
            BackColor = Theme.ViewBack;
            AllowDrop = true;
            autoScroll.Tick += OnAutoScroll;
        }

        public int ThumbSize
        {
            get { return thumb; }
            set
            {
                int v = Math.Max(72, Math.Min(480, value));
                if (v == thumb) return;
                int keep = focus >= 0 ? focus : FirstVisibleIndex();
                thumb = v;
                DoLayout();
                if (keep >= 0) EnsureVisible(keep);
            }
        }

        public EditDoc Document { get { return doc; } }

        public void SetDocument(EditDoc d, int focusPage)
        {
            doc = d;
            pages = d != null ? d.Pages : new List<PageRef>();
            selected.Clear();
            focus = anchor = pages.Count > 0 ? Math.Max(0, Math.Min(pages.Count - 1, focusPage)) : -1;
            if (focus >= 0) selected.Add(pages[focus]);
            ScrollY = 0;
            DoLayout();
            if (focus >= 0) EnsureVisible(focus, true);
            RaiseSelection();
            AccessibilityNotifyClients(AccessibleEvents.Reorder, -1);
        }

        /// <summary>The page list changed; keep selection by identity where possible.</summary>
        public void OnDocumentChanged(IEnumerable<PageRef> newSelection)
        {
            if (doc == null) return;
            pages = doc.Pages;
            var set = new HashSet<PageRef>(pages, RefComparer<PageRef>.Instance);
            var keep = (newSelection ?? selected.ToList()).Where(set.Contains).ToList();
            selected.Clear();
            foreach (var p in keep) selected.Add(p);
            if (selected.Count > 0)
            {
                focus = pages.FindIndex(p => selected.Contains(p));
                int lastSel = pages.FindLastIndex(p => selected.Contains(p));
                if (newSelection != null) focus = lastSel;
            }
            else focus = Math.Min(focus, pages.Count - 1);
            anchor = focus;
            DoLayout();
            if (focus >= 0 && newSelection != null) EnsureVisible(focus);
            RaiseSelection();
        }

        /// <summary>Selected page indices in document order (focus page if nothing is selected).</summary>
        public List<int> SelectedIndices()
        {
            var list = new List<int>();
            for (int i = 0; i < pages.Count; i++) if (selected.Contains(pages[i])) list.Add(i);
            if (list.Count == 0 && focus >= 0 && focus < pages.Count) list.Add(focus);
            return list;
        }

        public int FocusIndex { get { return focus; } }

        public void SelectPage(int index)
        {
            if (index < 0 || index >= pages.Count) return;
            if (focus != index || selected.Count != 1 || !selected.Contains(pages[index])) SelectOnly(index);
            EnsureVisible(index);
        }

        /// <summary>Where pasted pages go: after the last selected page, or at the end.</summary>
        public int InsertionPoint(bool before)
        {
            var sel = SelectedIndices();
            if (sel.Count == 0) return before ? 0 : pages.Count;
            return before ? sel[0] : sel[sel.Count - 1] + 1;
        }

        public void SelectAll()
        {
            selected.Clear();
            foreach (var p in pages) selected.Add(p);
            Invalidate();
            RaiseSelection();
        }

        private void RaiseSelection()
        {
            AccessibilityNotifyClients(AccessibleEvents.SelectionWithin, -1);
            if (Focused && focus >= 0) AccessibilityNotifyClients(AccessibleEvents.Focus, focus);
            var h = SelectionChanged;
            if (h != null) h(this, EventArgs.Empty);
        }

        // ---- layout -----------------------------------------------------------

        private int Pad { get { return S(10); } }
        private int LabelH { get { return S(24); } }
        private int Box { get { return sidebarMode ? Math.Max(S(48), Math.Min(S(thumb), ClientSize.Width - S(52))) : S(thumb); } }

        private void DoLayout()
        {
            int cw = Math.Max(1, ClientSize.Width);
            int margin = S(16);
            cellW = Box + 2 * Pad;
            cellH = Box + LabelH + 2 * Pad;
            cols = sidebarMode ? 1 : Math.Max(1, (cw - 2 * margin) / cellW);
            gridLeft = Math.Max(margin, (cw - cols * cellW) / 2);
            gridTop = margin;
            int rowsN = (pages.Count + cols - 1) / cols;
            ContentW = cw;
            ContentH = gridTop * 2 + rowsN * cellH + S(40);
            ScrollX = 0;
            ScrollY = Math.Max(0, Math.Min(MaxScrollY, ScrollY));
            UpdateScrollbars();
            Invalidate();
        }

        protected override void OnViewportResized()
        {
            int keep = FirstVisibleIndex();
            int oldCols = cols;
            DoLayout();
            if (oldCols != cols && keep >= 0) ScrollTo(0, CellRect(keep).Y - gridTop);
        }

        private Rectangle CellRect(int i)
        {
            return new Rectangle(gridLeft + (i % cols) * cellW, gridTop + (i / cols) * cellH, cellW, cellH);
        }

        private Rectangle ThumbRect(int i, Rectangle cell)
        {
            var p = pages[i];
            double w = p.BaseWidth, h = p.BaseHeight;
            if ((p.Rotation & 1) == 1) { var t = w; w = h; h = t; }
            double s = Math.Min(Box / w, Box / h);
            int tw = Math.Max(4, (int)Math.Floor(w * s)), th = Math.Max(4, (int)Math.Floor(h * s));
            return new Rectangle(cell.X + (cellW - tw) / 2, cell.Y + Pad + (Box - th), tw, th);
        }

        private int FirstVisibleIndex()
        {
            if (pages.Count == 0 || cellH == 0) return -1;
            int row = Math.Max(0, (ScrollY - gridTop) / cellH);
            return Math.Min(pages.Count - 1, row * cols);
        }

        private int IndexAt(Point client)
        {
            var c = new Point(client.X, client.Y + ScrollY);
            if (c.X < gridLeft || c.Y < gridTop) return -1;
            int col = (c.X - gridLeft) / cellW, row = (c.Y - gridTop) / cellH;
            if (col >= cols) return -1;
            int i = row * cols + col;
            if (i >= pages.Count) return -1;
            var cell = CellRect(i);
            cell.Inflate(-Pad / 2, -Pad / 2);
            return cell.Contains(c) ? i : -1;
        }

        /// <summary>Insertion index (0..Count) nearest to a client point.</summary>
        private int InsertIndexAt(Point client)
        {
            if (pages.Count == 0) return 0;
            int x = client.X, y = client.Y + ScrollY;
            if (sidebarMode)
                return Math.Max(0, Math.Min(pages.Count, (int)Math.Floor((y - gridTop + cellH / 2.0) / Math.Max(1, cellH))));
            int rowsN = (pages.Count + cols - 1) / cols;
            int row = Math.Max(0, Math.Min(rowsN - 1, (y - gridTop) / Math.Max(1, cellH)));
            int col = (int)Math.Round((x - gridLeft) / (double)cellW);
            col = Math.Max(0, Math.Min(cols, col));
            return Math.Max(0, Math.Min(pages.Count, row * cols + col));
        }

        public void EnsureVisible(int i, bool center = false)
        {
            if (i < 0 || i >= pages.Count) return;
            var r = CellRect(i);
            if (center) { ScrollTo(0, r.Y - (ClientSize.Height - r.Height) / 2); return; }
            if (r.Y < ScrollY) ScrollTo(0, r.Y - S(8));
            else if (r.Bottom > ScrollY + ClientSize.Height) ScrollTo(0, r.Bottom - ClientSize.Height + S(8));
        }

        // ---- painting ---------------------------------------------------------

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.ViewBack);
            if (doc == null) return;
            var want = new List<TileKey>();
            if (pages.Count == 0)
            {
                TextRenderer.DrawText(g, "No pages. Paste pages here (Ctrl+V) or drop a PDF file.", Font, ClientRectangle,
                    Theme.SubtleText, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            int first = Math.Max(0, (ScrollY - gridTop) / Math.Max(1, cellH) * cols);
            int last = Math.Min(pages.Count - 1, ((ScrollY + ClientSize.Height - gridTop) / Math.Max(1, cellH) + 1) * cols - 1);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            bool multiSource = pages.Any(p => p.Src != (doc.Base ?? pages[0].Src));
            using (var labelFont = new Font("Segoe UI", 9f))
            using (var selBrush = new SolidBrush(Theme.AccentSoft))
            using (var selPen = new Pen(Theme.Accent, Math.Max(1, S(2))))
            using (var borderPen = new Pen(Theme.PageBorder))
            using (var focusPen = new Pen(Theme.Accent) { DashStyle = DashStyle.Dot })
            {
                for (int i = first; i <= last; i++)
                {
                    var cell = CellRect(i);
                    cell.Offset(0, -ScrollY);
                    var p = pages[i];
                    bool sel = selected.Contains(p);
                    var inner = Rectangle.Inflate(cell, -S(3), -S(3));
                    if (sel)
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        using (var path = Theme.RoundRect(inner, S(6)))
                        {
                            g.FillPath(selBrush, path);
                            g.DrawPath(selPen, path);
                        }
                        g.SmoothingMode = SmoothingMode.None;
                    }
                    if (i == focus && Focused)
                    {
                        g.DrawRectangle(focusPen, Rectangle.Inflate(inner, -S(4), -S(4)));
                    }
                    var tr = ThumbRect(i, CellRect(i));
                    tr.Offset(0, -ScrollY);
                    using (var border = new SolidBrush(Theme.PageBorder))
                        g.FillRectangle(border, tr.X - 1, tr.Y - 1, tr.Width + 2, tr.Height + 2);
                    g.FillRectangle(Brushes.White, tr);
                    if (p.Src != null && Renderer != null)
                    {
                        var k = new TileKey(p.Src, p.Index, p.Rotation, tr.Width, tr.Height, 0, 0);
                        var bmp = Renderer.Get(k);
                        if (bmp != null) g.DrawImage(bmp, tr, 0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel);
                        else
                        {
                            want.Add(k);
                            DrawThumbFallback(g, p, tr);
                        }
                    }
                    if (sel && !SidebarMode)
                    {
                        var badge = new Rectangle(inner.Right - S(24), inner.Top + S(7), S(18), S(18));
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        using (var brush = new SolidBrush(Theme.Accent)) g.FillEllipse(brush, badge);
                        LucideIcons.Draw(g, "check", Rectangle.Inflate(badge, -S(3), -S(3)), Color.White);
                        g.SmoothingMode = SmoothingMode.None;
                    }
                    string label = "Page " + (i + 1);
                    var lr = new Rectangle(cell.X, tr.Bottom + S(4), cell.Width, LabelH - S(4));
                    TextRenderer.DrawText(g, label, labelFont, lr, sel ? Theme.Accent : Theme.Text,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.EndEllipsis);
                    if (multiSource && p.Src != null && p.Src != doc.Base)
                    {
                        // A small dot marks pages that came from another file.
                        int d = S(6);
                        using (var b = new SolidBrush(SourceColor(p.Src)))
                        {
                            g.SmoothingMode = SmoothingMode.AntiAlias;
                            g.FillEllipse(b, tr.Right - d - S(4), tr.Y + S(4), d, d);
                            g.SmoothingMode = SmoothingMode.None;
                        }
                    }
                    if (p.Src == null)
                        TextRenderer.DrawText(g, "Blank", labelFont, tr, Theme.SubtleText, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }

                if (drag == DragKind.Move && insertAt >= 0) DrawInsertBar(g, insertAt);
                if (drag == DragKind.Band)
                {
                    var band = BandRect();
                    band.Offset(0, -ScrollY);
                    using (var b = new SolidBrush(Color.FromArgb(40, Theme.Accent)))
                    using (var pen = new Pen(Theme.Accent))
                    {
                        g.FillRectangle(b, band);
                        g.DrawRectangle(pen, band);
                    }
                }
                if (drag == DragKind.Move)
                {
                    int n = selected.Count;
                    string s = n == 1 ? "Move 1 page" : "Move " + n + " pages";
                    var sz = TextRenderer.MeasureText(s, labelFont);
                    var r = new Rectangle(curPt.X + S(14), curPt.Y + S(14), sz.Width + S(12), sz.Height + S(6));
                    using (var b = new SolidBrush(Theme.Accent)) g.FillRectangle(b, r);
                    TextRenderer.DrawText(g, s, labelFont, r, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }
            if (Renderer != null) Renderer.Request(RenderService.ClientThumbs, want);
            PaintToast(g);
        }

        private static Color SourceColor(PdfSource src)
        {
            Color[] palette = { Color.FromArgb(0xE0, 0x6C, 0x00), Color.FromArgb(0x2E, 0x9E, 0x5B), Color.FromArgb(0x8E, 0x44, 0xAD), Color.FromArgb(0xC0, 0x39, 0x2B), Color.FromArgb(0x16, 0x8A, 0xAD) };
            int h = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(src);
            return palette[(h & 0x7FFFFFFF) % palette.Length];
        }

        private void DrawThumbFallback(Graphics g, PageRef p, Rectangle tr)
        {
            var alts = new List<KeyValuePair<TileKey, Bitmap>>();
            Renderer.CollectAlternates(p.Src, p.Index, p.Rotation, alts);
            foreach (var kv in alts)
            {
                if (kv.Key.TX != 0 || kv.Key.TY != 0 || kv.Key.W > RenderService.TileSize || kv.Key.H > RenderService.TileSize) continue;
                var old = g.InterpolationMode;
                g.InterpolationMode = InterpolationMode.Bilinear;
                g.DrawImage(kv.Value, tr, 0, 0, kv.Value.Width, kv.Value.Height, GraphicsUnit.Pixel);
                g.InterpolationMode = old;
                return;
            }
        }

        private void DrawInsertBar(Graphics g, int idx)
        {
            if (sidebarMode)
            {
                int y = gridTop + idx * cellH - ScrollY;
                using (var b = new SolidBrush(Theme.Accent))
                    g.FillRectangle(b, gridLeft, y - S(1), cellW, Math.Max(2, S(3)));
                return;
            }
            Rectangle cell;
            int x;
            if (pages.Count == 0) { x = gridLeft; cell = new Rectangle(gridLeft, gridTop, cellW, cellH); }
            else if (idx < pages.Count && (idx % cols != 0 || idx == 0))
            {
                cell = CellRect(idx);
                x = cell.X;
            }
            else if (idx < pages.Count && insertPrefersRowStart)
            {
                cell = CellRect(idx);
                x = cell.X;
            }
            else
            {
                cell = CellRect(idx - 1);
                x = cell.Right;
            }
            int w = Math.Max(2, S(3));
            var r = new Rectangle(x - w / 2, cell.Y + S(4) - ScrollY, w, cell.Height - S(8));
            using (var b = new SolidBrush(Theme.Accent)) g.FillRectangle(b, r);
        }

        private bool insertPrefersRowStart;

        // ---- mouse ----------------------------------------------------------------

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int i = IndexAt(e.Location);
            downPt = e.Location;
            curPt = e.Location;
            downIndex = i;
            deferredSelectOnly = false;
            bool ctrl = (MouseModifiers & Keys.Control) != 0, shift = (MouseModifiers & Keys.Shift) != 0;
            if (e.Button == MouseButtons.Right)
            {
                if (i >= 0 && !selected.Contains(pages[i])) SelectOnly(i);
                return;
            }
            if (e.Button != MouseButtons.Left) return;
            if (i < 0)
            {
                bandBase = ctrl ? new HashSet<PageRef>(selected, RefComparer<PageRef>.Instance) : new HashSet<PageRef>(RefComparer<PageRef>.Instance);
                if (!ctrl) { selected.Clear(); RaiseSelection(); }
                bandScroll0 = ScrollY;
                drag = DragKind.Band;
                Capture = true;
                Invalidate();
                return;
            }
            if (shift && anchor >= 0)
            {
                if (!ctrl) selected.Clear();
                int a = Math.Min(anchor, i), b = Math.Max(anchor, i);
                for (int k = a; k <= b; k++) selected.Add(pages[k]);
                focus = i;
            }
            else if (ctrl)
            {
                if (!selected.Remove(pages[i])) selected.Add(pages[i]);
                focus = anchor = i;
            }
            else if (!selected.Contains(pages[i]))
            {
                SelectOnly(i);
            }
            else
            {
                focus = anchor = i;
                deferredSelectOnly = selected.Count > 1;
            }
            drag = selected.Contains(pages[i]) ? DragKind.Pending : DragKind.None;
            Capture = true;
            Invalidate();
            RaiseSelection();
        }

        private void SelectOnly(int i)
        {
            selected.Clear();
            selected.Add(pages[i]);
            focus = anchor = i;
            Invalidate();
            RaiseSelection();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            curPt = e.Location;
            switch (drag)
            {
                case DragKind.Pending:
                    if (Math.Abs(e.X - downPt.X) > SystemInformation.DragSize.Width ||
                        Math.Abs(e.Y - downPt.Y) > SystemInformation.DragSize.Height)
                    {
                        drag = DragKind.Move;
                        deferredSelectOnly = false;
                        autoScroll.Start();
                        UpdateInsert(e.Location);
                    }
                    break;
                case DragKind.Move:
                    UpdateInsert(e.Location);
                    break;
                case DragKind.Band:
                    UpdateBand();
                    autoScroll.Start();
                    break;
                default:
                    UpdateTip(e.Location);
                    break;
            }
        }

        private void UpdateTip(Point pt)
        {
            int i = IndexAt(pt);
            if (i == tipIndex) return;
            tipIndex = i;
            if (i < 0) { tip.Hide(this); return; }
            var p = pages[i];
            string text = "Page " + (i + 1);
            if (p.Src == null) text += " (blank)";
            else
            {
                text += " — page " + (p.Index + 1) + " of " + p.Src.Name;
                double w = p.BaseWidth, h = p.BaseHeight;
                if ((p.Rotation & 1) == 1) { var t = w; w = h; h = t; }
                text += "\n" + Math.Round(w / 72 * 25.4) + " × " + Math.Round(h / 72 * 25.4) + " mm";
            }
            if (p.Rotation != 0) text += ", rotated " + (p.Rotation * 90) + "°";
            tip.SetToolTip(this, text);
        }

        private void UpdateInsert(Point pt)
        {
            int idx = InsertIndexAt(pt);
            // At a row boundary the same index is both "end of row" and "start of next row";
            // draw the bar where the pointer is.
            insertPrefersRowStart = idx < pages.Count && idx % cols == 0 && (pt.X - gridLeft) < cellW / 2;
            insertAt = idx;
            Invalidate();
        }

        private Rectangle BandRect()
        {
            var a = new Point(downPt.X, downPt.Y + bandScroll0);
            var b = new Point(curPt.X, curPt.Y + ScrollY);
            return Rectangle.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
        }

        private int bandScroll0;

        private void UpdateBand()
        {
            var band = BandRect();
            selected.Clear();
            foreach (var p in bandBase) selected.Add(p);
            for (int i = 0; i < pages.Count; i++)
            {
                var c = CellRect(i);
                c.Inflate(-Pad, -Pad);
                if (band.IntersectsWith(c)) { selected.Add(pages[i]); focus = i; }
            }
            Invalidate();
            RaiseSelection();
        }

        private void OnAutoScroll(object sender, EventArgs e)
        {
            if (drag != DragKind.Move && drag != DragKind.Band) { autoScroll.Stop(); return; }
            var pt = PointToClient(Cursor.Position);
            int edge = S(40), dy = 0;
            if (pt.Y < edge) dy = -Math.Max(S(4), (edge - pt.Y) / 2);
            else if (pt.Y > ClientSize.Height - edge) dy = Math.Max(S(4), (pt.Y - (ClientSize.Height - edge)) / 2);
            if (dy != 0)
            {
                ScrollBy(0, dy);
                curPt = pt;
                if (drag == DragKind.Move) UpdateInsert(pt); else UpdateBand();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            autoScroll.Stop();
            Capture = false;
            var kind = drag;
            drag = DragKind.None;
            if (kind == DragKind.Move)
            {
                int at = insertAt;
                insertAt = -1;
                Invalidate();
                var moving = pages.Where(p => selected.Contains(p)).ToList();
                var h = MoveRequested;
                if (h != null && moving.Count > 0 && at >= 0) h(moving, at);
                return;
            }
            if (kind == DragKind.Pending && deferredSelectOnly && downIndex >= 0 && downIndex < pages.Count)
                SelectOnly(downIndex);
            if (kind == DragKind.Band) Invalidate();
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            int i = IndexAt(e.Location);
            if (i >= 0 && e.Button == MouseButtons.Left)
            {
                var h = OpenPageRequested;
                if (h != null) h(i);
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if ((MouseModifiers & Keys.Control) != 0)
            {
                ThumbSize = (int)(thumb * (e.Delta > 0 ? 1.15 : 1 / 1.15));
                return;
            }
            int lines = SystemInformation.MouseWheelScrollLines;
            if (lines <= 0) lines = 3;
            ScrollBy(0, -(int)((long)e.Delta * lines * S(30) / 120));
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            tipIndex = -1;
            tip.Hide(this);
        }

        protected override void OnScrolled()
        {
            if (drag == DragKind.Band) UpdateBand();
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); if (focus >= 0) AccessibilityNotifyClients(AccessibleEvents.Focus, focus); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        // ---- keyboard ---------------------------------------------------------------

        public bool HandleKey(Keys key)
        {
            if (pages.Count == 0) return false;
            Keys code = key & Keys.KeyCode;
            bool shift = (key & Keys.Shift) != 0, ctrl = (key & Keys.Control) != 0;
            if ((key & Keys.Alt) != 0) return false;
            int rowsVisible = Math.Max(1, ClientSize.Height / Math.Max(1, cellH));
            int next = focus < 0 ? 0 : focus;
            switch (code)
            {
                case Keys.Left: next--; break;
                case Keys.Right: next++; break;
                case Keys.Up: next -= cols; break;
                case Keys.Down: next += cols; break;
                case Keys.PageUp: next -= cols * rowsVisible; break;
                case Keys.PageDown: next += cols * rowsVisible; break;
                case Keys.Home: next = 0; break;
                case Keys.End: next = pages.Count - 1; break;
                case Keys.Space:
                    if (ctrl && focus >= 0)
                    {
                        if (!selected.Remove(pages[focus])) selected.Add(pages[focus]);
                        Invalidate();
                        RaiseSelection();
                        return true;
                    }
                    return false;
                case Keys.Enter:
                    if (focus >= 0)
                    {
                        var h = OpenPageRequested;
                        if (h != null) h(focus);
                    }
                    return true;
                default:
                    return false;
            }
            next = Math.Max(0, Math.Min(pages.Count - 1, next));
            if (shift)
            {
                if (anchor < 0) anchor = focus < 0 ? next : focus;
                selected.Clear();
                int a = Math.Min(anchor, next), b = Math.Max(anchor, next);
                for (int k = a; k <= b; k++) selected.Add(pages[k]);
                focus = next;
            }
            else if (ctrl)
            {
                focus = next;
            }
            else
            {
                selected.Clear();
                selected.Add(pages[next]);
                focus = anchor = next;
            }
            EnsureVisible(focus);
            Invalidate();
            RaiseSelection();
            return true;
        }

        // ---- file drops -------------------------------------------------------------

        private static string[] PdfFiles(DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return null;
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null) return null;
            var pdfs = files.Where(f => string.Equals(Path.GetExtension(f), ".pdf", StringComparison.OrdinalIgnoreCase) || ImageImport.Supports(f)).ToArray();
            return pdfs.Length > 0 ? pdfs : null;
        }

        protected override void OnDragEnter(DragEventArgs e)
        {
            base.OnDragEnter(e);
            e.Effect = PdfFiles(e) != null && doc != null ? DragDropEffects.Copy : DragDropEffects.None;
        }

        protected override void OnDragOver(DragEventArgs e)
        {
            base.OnDragOver(e);
            if (PdfFiles(e) == null || doc == null) { e.Effect = DragDropEffects.None; return; }
            e.Effect = DragDropEffects.Copy;
            var pt = PointToClient(new Point(e.X, e.Y));
            drag = DragKind.Move;
            curPt = new Point(-1000, -1000);
            UpdateInsert(pt);
            int edge = S(40);
            if (pt.Y < edge) ScrollBy(0, -S(12));
            else if (pt.Y > ClientSize.Height - edge) ScrollBy(0, S(12));
        }

        protected override void OnDragLeave(EventArgs e)
        {
            base.OnDragLeave(e);
            drag = DragKind.None;
            insertAt = -1;
            Invalidate();
        }

        protected override void OnDragDrop(DragEventArgs e)
        {
            base.OnDragDrop(e);
            var files = PdfFiles(e);
            int at = insertAt >= 0 ? insertAt : pages.Count;
            drag = DragKind.None;
            insertAt = -1;
            Invalidate();
            if (files == null) return;
            var h = FilesDropped;
            if (h != null) BeginInvoke((Action)delegate { h(files, at); });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                autoScroll.Dispose();
                tip.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
