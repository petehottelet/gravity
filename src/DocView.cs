using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Gravity
{
    internal enum ZoomMode { FitPage, FitWidth, Custom }
    internal enum PageLayout { Single, Facing, Book }

    /// <summary>Per-tab view settings, saved when switching tabs.</summary>
    internal sealed class ViewState
    {
        public ZoomMode Mode = ZoomMode.FitPage;
        public double Zoom = 1.0;
        public int Rotation;
        public PageLayout Layout = PageLayout.Single;
        public bool Continuous = true;
        public int Page;
        public double PageV;
        public int ScrollX;
    }

    /// <summary>The reading view: renders pages continuously (or one row at a time) with tiles.</summary>
    internal sealed class DocView : CanvasControl
    {
        public static readonly double[] ZoomSteps = { 0.0833, 0.125, 0.18, 0.25, 0.3333, 0.5, 0.6667, 0.75, 1, 1.25, 1.5, 2, 3, 4, 6, 8, 10, 12, 16, 20, 24, 32, 48, 64 };
        private const int RefScale = 16;

        public RenderService Renderer;
        public event EventHandler ViewChanged;
        public event Action<string> Notify;

        private EditDoc doc;
        private List<PageRef> pages = new List<PageRef>();
        private ZoomMode zoomMode = ZoomMode.FitPage;
        private double zoom = 1, scale = 1;
        private int viewRot;
        private PageLayout layout = PageLayout.Single;
        private bool continuous = true, presentation;
        private ZoomMode prePresZoom;
        private bool prePresContinuous;

        private Rectangle[] rects = new Rectangle[0];
        private List<int[]> rows = new List<int[]>();
        private int[] rowTop = new int[0], rowH = new int[0], pageRow = new int[0];
        private int curRow;
        private int currentPage;

        // text selection: (page, char) .. (page, char), inclusive
        private int selP0 = -1, selC0, selP1, selC1;
        private readonly Dictionary<int, List<RectangleF>> selRects = new Dictionary<int, List<RectangleF>>();

        // search
        private int hitPage = -1, hitChar, hitCount;
        private PageRef hitRef;
        private List<RectangleF> hitRects;
        private string lastQuery;
        private bool lastMatchCase;
        private CancellationTokenSource searchCts;

        // navigation history
        private readonly List<KeyValuePair<int, double>> back = new List<KeyValuePair<int, double>>();
        private readonly List<KeyValuePair<int, double>> fwd = new List<KeyValuePair<int, double>>();

        private enum DragKind { None, Pan, Select, Link, Present }
        private DragKind drag;
        private Point dragStart;
        private bool dragMoved;
        private int panX0, panY0;
        private LinkTarget pressedLink;
        private Cursor hoverCursor = Cursors.Default;

        private readonly System.Windows.Forms.Timer cursorTimer = new System.Windows.Forms.Timer { Interval = 2500 };
        private bool cursorHidden;

        public DocView()
        {
            BackColor = Theme.ViewBack;
            cursorTimer.Tick += delegate
            {
                cursorTimer.Stop();
                if (presentation && !cursorHidden && ClientRectangle.Contains(PointToClient(Cursor.Position)))
                {
                    Cursor.Hide();
                    cursorHidden = true;
                }
            };
        }

        // ---- public state ---------------------------------------------------

        public EditDoc Document { get { return doc; } }
        public int PageCount { get { return pages.Count; } }
        public int CurrentPage { get { return currentPage; } }
        public ZoomMode Mode { get { return zoomMode; } }
        public double Zoom { get { return zoom; } }
        public PageLayout LayoutMode { get { return layout; } }
        public bool Continuous { get { return continuous; } }
        public int ViewRotation { get { return viewRot; } }
        public bool Presentation { get { return presentation; } }
        public bool HasSelection { get { return selP0 >= 0; } }
        public bool CanGoBack { get { return back.Count > 0; } }
        public bool CanGoForward { get { return fwd.Count > 0; } }

        public void SetDocument(EditDoc d, ViewState st)
        {
            CancelSearch();
            doc = d;
            pages = d != null ? d.Pages : new List<PageRef>();
            ClearSelection();
            ClearHit();
            back.Clear();
            fwd.Clear();
            if (st == null) st = new ViewState();
            zoomMode = st.Mode;
            zoom = st.Zoom;
            viewRot = st.Rotation & 3;
            layout = st.Layout;
            continuous = st.Continuous || presentation;
            currentPage = Clamp(st.Page, 0, Math.Max(0, pages.Count - 1));
            curRow = 0;
            DoLayout();
            if (pages.Count > 0)
            {
                if (!continuous) { curRow = pageRow[currentPage]; DoLayout(); }
                var rc = rects[currentPage];
                ScrollX = Clamp(st.ScrollX, 0, MaxScrollX);
                ScrollY = Clamp(rc.Y + (int)Math.Round(st.PageV * rc.Height) - (st.PageV == 0 ? EdgePad : 0), 0, MaxScrollY);
                UpdateScrollbars();
            }
            Invalidate();
            RaiseViewChanged();
        }

        public ViewState GetState()
        {
            var st = new ViewState { Mode = zoomMode, Zoom = zoom, Rotation = viewRot, Layout = layout, Continuous = continuous, ScrollX = ScrollX };
            if (presentation) { st.Mode = prePresZoom; st.Continuous = prePresContinuous; }
            if (pages.Count > 0)
            {
                st.Page = currentPage;
                var rc = rects[currentPage];
                if (!rc.IsEmpty && rc.Height > 0)
                {
                    double v = (ScrollY + EdgePad - rc.Y) / (double)rc.Height;
                    st.PageV = Math.Max(0, Math.Min(1, v));
                    if (Math.Abs(st.PageV) < 0.002) st.PageV = 0;
                }
            }
            return st;
        }

        /// <summary>The page list changed (edit, undo, save): re-layout and keep the reading position.</summary>
        public void OnDocumentChanged()
        {
            if (doc == null) return;
            CancelSearch();
            var anchor = CaptureAnchor(new Point(ClientSize.Width / 2, 0));
            PageRef anchorRef = anchor.Valid && anchor.Page < pages.Count ? pages[anchor.Page] : null;
            pages = doc.Pages;
            ClearSelection();
            ClearHit();
            back.Clear();
            fwd.Clear();
            int idx = -1;
            if (anchorRef != null)
                for (int i = 0; i < pages.Count; i++) if (ReferenceEquals(pages[i], anchorRef)) { idx = i; break; }
            if (idx < 0) idx = Math.Min(anchor.Valid ? anchor.Page : currentPage, pages.Count - 1);
            currentPage = Math.Max(0, idx);
            if (pages.Count > 0) curRow = pageRow.Length == pages.Count ? curRow : 0;
            DoLayout();
            if (pages.Count > 0)
            {
                if (!continuous) { curRow = pageRow[currentPage]; DoLayout(); }
                anchor.Page = currentPage;
                RestoreAnchor(anchor);
            }
            Invalidate();
            RaiseViewChanged();
        }

        // ---- layout -----------------------------------------------------------

        private int EdgePad { get { return presentation ? 0 : S(10); } }
        private int Gap { get { return presentation ? 0 : S(10); } }

        private void PageSizePts(int i, out double w, out double h)
        {
            var p = pages[i];
            double w0 = p.BaseWidth, h0 = p.BaseHeight;
            if (((p.Rotation + viewRot) & 1) == 1) { w = h0; h = w0; }
            else { w = w0; h = h0; }
        }

        private Size PagePixelSize(int i)
        {
            double w, h;
            PageSizePts(i, out w, out h);
            return new Size(Math.Max(1, (int)Math.Floor(w * scale)), Math.Max(1, (int)Math.Floor(h * scale)));
        }

        private void BuildRows()
        {
            rows = new List<int[]>();
            int n = pages.Count;
            if (layout == PageLayout.Single)
                for (int i = 0; i < n; i++) rows.Add(new[] { i });
            else if (layout == PageLayout.Facing)
                for (int i = 0; i < n; i += 2) rows.Add(new[] { i, i + 1 < n ? i + 1 : -1 });
            else
            {
                if (n > 0) rows.Add(new[] { -1, 0 });
                for (int i = 1; i < n; i += 2) rows.Add(new[] { i, i + 1 < n ? i + 1 : -1 });
            }
        }

        private void DoLayout()
        {
            int n = pages.Count;
            BuildRows();
            int cols = layout == PageLayout.Single ? 1 : 2;
            int margin = EdgePad, gap = Gap;
            var colW = new double[cols];
            double maxH = 0;
            foreach (var row in rows)
                for (int c = 0; c < row.Length; c++)
                {
                    if (row[c] < 0) continue;
                    double w, h;
                    PageSizePts(row[c], out w, out h);
                    colW[c] = Math.Max(colW[c], w);
                    maxH = Math.Max(maxH, h);
                }
            for (int c = 0; c < cols; c++) if (colW[c] <= 0) colW[c] = colW[0] > 0 ? colW[0] : 612;
            if (maxH <= 0) maxH = 792;
            double sumW = colW.Sum();
            int cw = Math.Max(1, ClientSize.Width), ch = Math.Max(1, ClientSize.Height);
            double availW = Math.Max(40, cw - 2 * margin - (cols - 1) * gap);
            double availH = Math.Max(40, ch - 2 * margin);
            double baseScale = Dpi / 72.0;
            switch (zoomMode)
            {
                case ZoomMode.FitWidth: scale = availW / sumW; break;
                case ZoomMode.FitPage: scale = Math.Min(availW / sumW, availH / maxH); break;
                default: scale = zoom * baseScale; break;
            }
            scale = Math.Max(0.02 * baseScale, Math.Min(64 * baseScale, scale));
            zoom = scale / baseScale;

            var colPx = new int[cols];
            for (int c = 0; c < cols; c++) colPx[c] = (int)Math.Floor(colW[c] * scale);
            int rowWpx = colPx.Sum() + (cols - 1) * gap;
            ContentW = rowWpx + 2 * margin;
            int viewW = Math.Max(ContentW, cw);
            int x0 = (viewW - rowWpx) / 2;

            rects = new Rectangle[n];
            rowTop = new int[rows.Count];
            rowH = new int[rows.Count];
            pageRow = new int[n];
            if (rows.Count > 0) curRow = Clamp(curRow, 0, rows.Count - 1);
            int y = margin;
            bool any = false;
            for (int r = 0; r < rows.Count; r++)
            {
                var row = rows[r];
                int rh = 0;
                foreach (int idx in row)
                {
                    if (idx < 0) continue;
                    pageRow[idx] = r;
                    rh = Math.Max(rh, PagePixelSize(idx).Height);
                }
                rowH[r] = rh;
                if (!continuous && r != curRow) { rowTop[r] = -1; continue; }
                rowTop[r] = y;
                for (int c = 0; c < row.Length; c++)
                {
                    int idx = row[c];
                    if (idx < 0) continue;
                    var sz = PagePixelSize(idx);
                    int x;
                    if (cols == 1) x = x0 + (colPx[0] - sz.Width) / 2;
                    else if (c == 0) x = x0 + colPx[0] - sz.Width;
                    else x = x0 + colPx[0] + gap;
                    rects[idx] = new Rectangle(x, y + (rh - sz.Height) / 2, sz.Width, sz.Height);
                }
                y += rh + gap;
                any = true;
            }
            ContentH = any ? y - gap + margin : 2 * margin;
            if (ContentH < ch)
            {
                int off = (ch - ContentH) / 2;
                for (int i = 0; i < n; i++) if (!rects[i].IsEmpty) rects[i].Offset(0, off);
                for (int r = 0; r < rows.Count; r++) if (rowTop[r] >= 0) rowTop[r] += off;
                ContentH = ch;
            }
            ScrollX = Clamp(ScrollX, 0, MaxScrollX);
            ScrollY = Clamp(ScrollY, 0, MaxScrollY);
            selRects.Clear();
            UpdateScrollbars();
            Invalidate();
        }

        protected override void OnViewportResized()
        {
            if (pages.Count == 0) { DoLayout(); return; }
            var a = CaptureAnchor(new Point(ClientSize.Width / 2, 0));
            DoLayout();
            RestoreAnchor(a);
        }

        private void VisibleRows(out int first, out int last)
        {
            first = 0;
            last = -1;
            if (rows.Count == 0) return;
            if (!continuous) { first = last = curRow; return; }
            int lo = 0, hi = rows.Count - 1, ans = rows.Count;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                if (rowTop[mid] + rowH[mid] >= ScrollY) { ans = mid; hi = mid - 1; }
                else lo = mid + 1;
            }
            first = Math.Min(ans, rows.Count - 1);
            last = first;
            int bottom = ScrollY + ClientSize.Height;
            while (last + 1 < rows.Count && rowTop[last + 1] <= bottom) last++;
        }

        private struct ViewAnchor
        {
            public bool Valid;
            public int Page;
            public double U, V;
            public Point Client;
        }

        private ViewAnchor CaptureAnchor(Point client)
        {
            var a = new ViewAnchor { Client = client };
            if (pages.Count == 0 || rects.Length != pages.Count) return a;
            var c = new Point(client.X + ScrollX, client.Y + ScrollY);
            int first, last, best = -1;
            long bestD = long.MaxValue;
            VisibleRows(out first, out last);
            for (int r = first; r <= last; r++)
                foreach (int idx in rows[r])
                {
                    if (idx < 0 || rects[idx].IsEmpty) continue;
                    var rc = rects[idx];
                    long dx = c.X < rc.Left ? rc.Left - c.X : c.X > rc.Right ? c.X - rc.Right : 0;
                    long dy = c.Y < rc.Top ? rc.Top - c.Y : c.Y > rc.Bottom ? c.Y - rc.Bottom : 0;
                    long d = dx * dx + dy * dy * 4;
                    if (d < bestD) { bestD = d; best = idx; }
                }
            if (best < 0) best = Clamp(currentPage, 0, pages.Count - 1);
            if (rects[best].IsEmpty) return a;
            var pr = rects[best];
            a.Valid = true;
            a.Page = best;
            a.U = (c.X - pr.X) / (double)Math.Max(1, pr.Width);
            a.V = (c.Y - pr.Y) / (double)Math.Max(1, pr.Height);
            return a;
        }

        private void RestoreAnchor(ViewAnchor a)
        {
            if (!a.Valid || a.Page >= pages.Count) return;
            if (!continuous && pageRow[a.Page] != curRow) { curRow = pageRow[a.Page]; DoLayout(); }
            var rc = rects[a.Page];
            if (rc.IsEmpty) return;
            int cx = rc.X + (int)Math.Round(a.U * rc.Width);
            int cy = rc.Y + (int)Math.Round(a.V * rc.Height);
            ScrollX = Clamp(cx - a.Client.X, 0, MaxScrollX);
            ScrollY = Clamp(cy - a.Client.Y, 0, MaxScrollY);
            UpdateScrollbars();
            Invalidate();
            UpdateCurrentPage();
        }

        private static int Clamp(int v, int lo, int hi) { return v < lo ? lo : v > hi ? hi : v; }

        private void UpdateCurrentPage()
        {
            if (pages.Count == 0) return;
            int first, last, best = -1, bestVis = -1;
            VisibleRows(out first, out last);
            int top = ScrollY, bottom = ScrollY + ClientSize.Height;
            for (int r = first; r <= last; r++)
                foreach (int idx in rows[r])
                {
                    if (idx < 0 || rects[idx].IsEmpty) continue;
                    int vis = Math.Min(bottom, rects[idx].Bottom) - Math.Max(top, rects[idx].Top);
                    if (vis > bestVis) { bestVis = vis; best = idx; }
                }
            if (best >= 0 && best != currentPage)
            {
                currentPage = best;
                RaiseViewChanged();
            }
        }

        protected override void OnScrolled()
        {
            UpdateCurrentPage();
        }

        private void RaiseViewChanged()
        {
            var h = ViewChanged;
            if (h != null) h(this, EventArgs.Empty);
        }

        // ---- navigation -------------------------------------------------------

        private KeyValuePair<int, double> CurrentLocation()
        {
            var a = CaptureAnchor(new Point(ClientSize.Width / 2, EdgePad));
            return new KeyValuePair<int, double>(a.Valid ? a.Page : currentPage, a.Valid ? Math.Max(0, a.V) : 0);
        }

        private void PushHistory()
        {
            if (pages.Count == 0) return;
            back.Add(CurrentLocation());
            if (back.Count > 200) back.RemoveAt(0);
            fwd.Clear();
        }

        public void GoBack()
        {
            if (back.Count == 0) return;
            fwd.Add(CurrentLocation());
            var loc = back[back.Count - 1];
            back.RemoveAt(back.Count - 1);
            GoTo(loc.Key, loc.Value, false);
        }

        public void GoForward()
        {
            if (fwd.Count == 0) return;
            back.Add(CurrentLocation());
            var loc = fwd[fwd.Count - 1];
            fwd.RemoveAt(fwd.Count - 1);
            GoTo(loc.Key, loc.Value, false);
        }

        public void GoToPage(int page, bool record = true)
        {
            GoTo(page, 0, record);
        }

        /// <summary>Scrolls so that fraction <paramref name="v"/> of the page is at the top of the view.</summary>
        public void GoTo(int page, double v, bool record)
        {
            if (pages.Count == 0) return;
            page = Clamp(page, 0, pages.Count - 1);
            if (record) PushHistory();
            if (!continuous && pageRow[page] != curRow) { curRow = pageRow[page]; DoLayout(); }
            var rc = rects[page];
            if (rc.IsEmpty) return;
            int y = v <= 0 ? rowTop[pageRow[page]] - EdgePad : rc.Y + (int)Math.Round(v * rc.Height) - EdgePad;
            if (!continuous && v <= 0) y = 0;
            ScrollX = Clamp(ScrollX, 0, MaxScrollX);
            ScrollY = Clamp(y, 0, MaxScrollY);
            UpdateScrollbars();
            Invalidate();
            if (currentPage != page)
            {
                currentPage = page;
                RaiseViewChanged();
            }
        }

        private int RowFirstPage(int r)
        {
            foreach (int idx in rows[r]) if (idx >= 0) return idx;
            return 0;
        }

        public void NextPage()
        {
            if (pages.Count == 0) return;
            int r = pageRow[currentPage];
            if (r + 1 < rows.Count) GoToPage(RowFirstPage(r + 1), false);
            else if (continuous) ScrollTo(ScrollX, MaxScrollY);
        }

        public void PrevPage()
        {
            if (pages.Count == 0) return;
            int r = pageRow[currentPage];
            var rc = rects[currentPage];
            // In continuous mode, first snap to the top of the current page if we're inside it.
            if (continuous && !rc.IsEmpty && ScrollY > rowTop[r] - EdgePad + S(4)) { GoToPage(RowFirstPage(r), false); return; }
            if (r > 0) GoToPage(RowFirstPage(r - 1), false);
        }

        public void FirstPage() { GoToPage(0); }
        public void LastPage() { GoToPage(pages.Count - 1); }

        /// <summary>Scrolls by about one screen; in single-row mode, flips rows at the ends.</summary>
        public void ScrollScreen(int dir)
        {
            if (pages.Count == 0) return;
            if (!continuous)
            {
                if (dir > 0 && ScrollY >= MaxScrollY) { if (pageRow[currentPage] + 1 < rows.Count) NextPage(); return; }
                if (dir < 0 && ScrollY <= 0)
                {
                    int r = pageRow[currentPage];
                    if (r > 0) { GoToPage(RowFirstPage(r - 1), false); ScrollTo(ScrollX, MaxScrollY); }
                    return;
                }
            }
            ScrollBy(0, dir * Math.Max(S(40), ClientSize.Height - S(40)));
        }

        public bool HandleKey(Keys key)
        {
            if (pages.Count == 0) return false;
            if (presentation)
            {
                switch (key)
                {
                    case Keys.Right: case Keys.Down: case Keys.PageDown: case Keys.Space: case Keys.Enter: case Keys.N:
                        NextPage(); return true;
                    case Keys.Left: case Keys.Up: case Keys.PageUp: case Keys.Back: case Keys.P: case Keys.Shift | Keys.Space:
                        PrevPage(); return true;
                    case Keys.Home: FirstPage(); return true;
                    case Keys.End: LastPage(); return true;
                }
                return false;
            }
            switch (key)
            {
                case Keys.Up: case Keys.K: ScrollLines(-1); return true;
                case Keys.Down: case Keys.J: ScrollLines(1); return true;
                case Keys.Left:
                    if (MaxScrollX > 0) ScrollBy(-LineStep, 0); else PrevPage();
                    return true;
                case Keys.Right:
                    if (MaxScrollX > 0) ScrollBy(LineStep, 0); else NextPage();
                    return true;
                case Keys.H: ScrollBy(-LineStep, 0); return true;
                case Keys.L: ScrollBy(LineStep, 0); return true;
                case Keys.PageDown: case Keys.N: NextPage(); return true;
                case Keys.PageUp: case Keys.P: PrevPage(); return true;
                case Keys.Space: ScrollScreen(1); return true;
                case Keys.Shift | Keys.Space: ScrollScreen(-1); return true;
                case Keys.Home: case Keys.Control | Keys.Home: FirstPage(); return true;
                case Keys.End: case Keys.Control | Keys.End: LastPage(); return true;
            }
            return false;
        }

        private void ScrollLines(int dir)
        {
            if (!continuous)
            {
                if (dir > 0 && ScrollY >= MaxScrollY) { ScrollScreen(1); return; }
                if (dir < 0 && ScrollY <= 0) { ScrollScreen(-1); return; }
            }
            ScrollBy(0, dir * LineStep);
        }

        // ---- zoom, rotation, layout ------------------------------------------

        public void SetZoomMode(ZoomMode m)
        {
            if (presentation) return;
            var a = CaptureAnchor(new Point(ClientSize.Width / 2, 0));
            zoomMode = m;
            DoLayout();
            RestoreAnchor(a);
            RaiseViewChanged();
        }

        public void SetZoom(double z, Point? anchorPoint = null)
        {
            if (presentation) return;
            z = Math.Max(0.08, Math.Min(64, z));
            Point ap = anchorPoint ?? new Point(ClientSize.Width / 2, ClientSize.Height / 2);
            var a = CaptureAnchor(ap);
            zoomMode = ZoomMode.Custom;
            zoom = z;
            DoLayout();
            RestoreAnchor(a);
            RaiseViewChanged();
        }

        public void ZoomStep(int dir, Point? anchorPoint = null)
        {
            double z = zoom;
            double next = z;
            if (dir > 0) { foreach (var s in ZoomSteps) if (s > z * 1.01) { next = s; break; } }
            else { for (int i = ZoomSteps.Length - 1; i >= 0; i--) if (ZoomSteps[i] < z / 1.01) { next = ZoomSteps[i]; break; } }
            SetZoom(next, anchorPoint);
        }

        public void CycleFitMode()
        {
            SetZoomMode(zoomMode == ZoomMode.FitPage ? ZoomMode.FitWidth : ZoomMode.FitPage);
        }

        public void RotateView(int quarterTurns)
        {
            int page = currentPage;
            viewRot = (viewRot + quarterTurns) & 3;
            DoLayout();
            GoTo(page, 0, false);
            RaiseViewChanged();
        }

        public void SetLayout(PageLayout l)
        {
            int page = currentPage;
            layout = l;
            curRow = 0;
            DoLayout();
            if (pages.Count > 0) { curRow = pageRow[page]; DoLayout(); GoTo(page, 0, false); }
            RaiseViewChanged();
        }

        public void SetContinuous(bool value)
        {
            if (presentation || value == continuous) return;
            int page = currentPage;
            continuous = value;
            if (pages.Count > 0) curRow = pageRow[page];
            DoLayout();
            GoTo(page, 0, false);
            RaiseViewChanged();
        }

        public void SetPresentation(bool on)
        {
            if (on == presentation) return;
            int page = currentPage;
            if (on)
            {
                prePresZoom = zoomMode;
                prePresContinuous = continuous;
                zoomMode = ZoomMode.FitPage;
                continuous = false;
                BackColor = Color.Black;
            }
            else
            {
                zoomMode = prePresZoom;
                continuous = prePresContinuous;
                BackColor = Theme.ViewBack;
                ShowCursor();
            }
            presentation = on;
            HideScrollbars = on;
            if (pages.Count > 0) curRow = pageRow[page];
            DoLayout();
            GoTo(page, 0, false);
            RaiseViewChanged();
        }

        private void ShowCursor()
        {
            cursorTimer.Stop();
            if (cursorHidden) { Cursor.Show(); cursorHidden = false; }
        }

        // ---- painting -------------------------------------------------------

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(presentation ? Color.Black : Theme.ViewBack);
            if (doc == null) return;
            if (pages.Count == 0)
            {
                TextRenderer.DrawText(g, "This document has no pages.", Font, ClientRectangle, Theme.SubtleText,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            var want = new List<TileKey>();
            int first, last, firstPage = -1, lastPage = -1;
            VisibleRows(out first, out last);
            for (int r = first; r <= last; r++)
                foreach (int idx in rows[r])
                {
                    if (idx < 0 || rects[idx].IsEmpty) continue;
                    var rc = rects[idx];
                    rc.Offset(-ScrollX, -ScrollY);
                    if (!rc.IntersectsWith(ClientRectangle)) continue;
                    if (firstPage < 0) firstPage = idx;
                    lastPage = idx;
                    DrawPage(g, idx, rc, e.ClipRectangle, want);
                }
            // Prefetch the neighbouring pages so paging feels instant.
            if (lastPage >= 0)
            {
                Prefetch(lastPage + 1, want);
                if (layout != PageLayout.Single) Prefetch(lastPage + 2, want);
                Prefetch(firstPage - 1, want);
            }
            if (Renderer != null) Renderer.Request(RenderService.ClientView, want);
            PaintToast(g);
        }

        private void Prefetch(int idx, List<TileKey> want)
        {
            if (idx < 0 || idx >= pages.Count || pages[idx].Src == null) return;
            var sz = PagePixelSize(idx);
            int t = RenderService.TileSize;
            int nx = (sz.Width + t - 1) / t, ny = (sz.Height + t - 1) / t;
            if (nx * ny > 4) return;
            var p = pages[idx];
            int rot = (p.Rotation + viewRot) & 3;
            for (int ty = 0; ty < ny; ty++)
                for (int tx = 0; tx < nx; tx++)
                    want.Add(new TileKey(p.Src, p.Index, rot, sz.Width, sz.Height, tx, ty));
        }

        private void DrawPage(Graphics g, int idx, Rectangle rc, Rectangle clip, List<TileKey> want)
        {
            var p = pages[idx];
            bool inClip = rc.IntersectsWith(clip) || Rectangle.Inflate(rc, 2, 2).IntersectsWith(clip);
            if (inClip)
            {
                if (!presentation)
                {
                    using (var shadow = new SolidBrush(Color.FromArgb(28, 0, 0, 0)))
                    {
                        g.FillRectangle(shadow, rc.X + 1, rc.Bottom + 1, rc.Width + 1, 2);
                        g.FillRectangle(shadow, rc.Right + 1, rc.Y + 1, 2, rc.Height + 2);
                    }
                    using (var border = new SolidBrush(Theme.PageBorder))
                        g.FillRectangle(border, rc.X - 1, rc.Y - 1, rc.Width + 2, rc.Height + 2);
                }
                g.FillRectangle(Brushes.White, rc);
            }
            if (p.Src != null)
            {
                int rot = (p.Rotation + viewRot) & 3;
                int t = RenderService.TileSize;
                var vis = Rectangle.Intersect(rc, ClientRectangle);
                vis.Offset(-rc.X, -rc.Y);
                if (vis.Width > 0 && vis.Height > 0)
                {
                    int tx0 = vis.Left / t, tx1 = (vis.Right - 1) / t, ty0 = vis.Top / t, ty1 = (vis.Bottom - 1) / t;
                    bool missing = false;
                    var have = new List<KeyValuePair<TileKey, Bitmap>>();
                    for (int ty = ty0; ty <= ty1; ty++)
                        for (int tx = tx0; tx <= tx1; tx++)
                        {
                            var k = new TileKey(p.Src, p.Index, rot, rc.Width, rc.Height, tx, ty);
                            var bmp = Renderer != null ? Renderer.Get(k) : null;
                            if (bmp == null) { missing = true; want.Add(k); }
                            else have.Add(new KeyValuePair<TileKey, Bitmap>(k, bmp));
                        }
                    if (inClip)
                    {
                        if (missing) DrawFallback(g, p, rot, rc);
                        foreach (var kv in have)
                        {
                            var tr = kv.Key.TileRect;
                            var dest = new Rectangle(rc.X + tr.X, rc.Y + tr.Y, tr.Width, tr.Height);
                            if (dest.IntersectsWith(clip))
                                g.DrawImage(kv.Value, dest, 0, 0, tr.Width, tr.Height, GraphicsUnit.Pixel);
                        }
                    }
                }
            }
            if (!inClip) return;
            int prot = (p.Rotation + viewRot) & 3;
            if (hitRects != null && hitPage == idx && ReferenceEquals(hitRef, p))
            {
                using (var b = new SolidBrush(Theme.SearchHit))
                using (var pen = new Pen(Color.FromArgb(220, 0xE0, 0x80, 0x00)))
                    foreach (var nr in hitRects)
                    {
                        var r = ToClient(RotRect(nr, prot), rc);
                        r.Inflate(1, 1);
                        g.FillRectangle(b, r);
                        g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
                    }
            }
            if (selP0 >= 0)
            {
                var list = GetSelRects(idx);
                if (list != null && list.Count > 0)
                    using (var b = new SolidBrush(Theme.Selection))
                        foreach (var nr in list) g.FillRectangle(b, ToClient(RotRect(nr, prot), rc));
            }
        }

        private void DrawFallback(Graphics g, PageRef p, int rot, Rectangle rc)
        {
            var alts = new List<KeyValuePair<TileKey, Bitmap>>();
            Renderer.CollectAlternates(p.Src, p.Index, rot, alts);
            if (alts.Count == 0) return;
            var best = alts[0].Key;
            foreach (var kv in alts)
                if (Math.Abs(kv.Key.W - rc.Width) < Math.Abs(best.W - rc.Width)) best = kv.Key;
            float sx = rc.Width / (float)best.W, sy = rc.Height / (float)best.H;
            var oldMode = g.InterpolationMode;
            g.InterpolationMode = InterpolationMode.Bilinear;
            foreach (var kv in alts)
            {
                if (kv.Key.W != best.W || kv.Key.H != best.H) continue;
                var tr = kv.Key.TileRect;
                var dest = new RectangleF(rc.X + tr.X * sx, rc.Y + tr.Y * sy, tr.Width * sx, tr.Height * sy);
                g.DrawImage(kv.Value, dest, new RectangleF(0, 0, tr.Width, tr.Height), GraphicsUnit.Pixel);
            }
            g.InterpolationMode = oldMode;
        }

        private static RectangleF ToClient(RectangleF n, Rectangle rc)
        {
            return new RectangleF(rc.X + n.X * rc.Width, rc.Y + n.Y * rc.Height, n.Width * rc.Width, n.Height * rc.Height);
        }

        // ---- coordinate helpers ----------------------------------------------
        // "Base-normalized" coordinates are 0..1 over the page as defined in the PDF (its own
        // /Rotate applied); "display-normalized" coordinates also include extra + view rotation.

        private static PointF RotN(double u, double v, int r)
        {
            switch (r & 3)
            {
                case 1: return new PointF((float)(1 - v), (float)u);
                case 2: return new PointF((float)(1 - u), (float)(1 - v));
                case 3: return new PointF((float)v, (float)(1 - u));
                default: return new PointF((float)u, (float)v);
            }
        }

        private static RectangleF RotRect(RectangleF n, int r)
        {
            if ((r & 3) == 0) return n;
            var a = RotN(n.Left, n.Top, r);
            var b = RotN(n.Right, n.Bottom, r);
            return RectangleF.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
        }

        private static void RefSize(PageRef p, out int rw, out int rh)
        {
            rw = Math.Max(1, (int)(p.BaseWidth * RefScale));
            rh = Math.Max(1, (int)(p.BaseHeight * RefScale));
        }

        /// <summary>PDF user-space point → base-normalized. Caller holds the PDFium lock.</summary>
        private static PointF PdfToBase(PageRef p, IntPtr page, double px, double py)
        {
            int rw, rh, dx, dy;
            RefSize(p, out rw, out rh);
            Pdfium.FPDF_PageToDevice(page, 0, 0, rw, rh, 0, px, py, out dx, out dy);
            return new PointF(dx / (float)rw, dy / (float)rh);
        }

        /// <summary>Client point → PDF user-space for page idx. Caller holds the PDFium lock.</summary>
        private bool ClientToPdf(int idx, Point client, out double px, out double py)
        {
            px = py = 0;
            var p = pages[idx];
            if (p.Src == null || rects[idx].IsEmpty) return false;
            IntPtr page = p.Src.GetPage(p.Index);
            if (page == IntPtr.Zero) return false;
            var rc = rects[idx];
            double u = (client.X + ScrollX - rc.X) / (double)rc.Width;
            double v = (client.Y + ScrollY - rc.Y) / (double)rc.Height;
            int rot = (p.Rotation + viewRot) & 3;
            var b = RotN(u, v, 4 - rot);
            int rw, rh;
            RefSize(p, out rw, out rh);
            return Pdfium.FPDF_DeviceToPage(page, 0, 0, rw, rh, 0, (int)Math.Round(b.X * rw), (int)Math.Round(b.Y * rh), out px, out py) != 0;
        }

        private int PageAtClient(Point client, bool nearest)
        {
            if (pages.Count == 0) return -1;
            var c = new Point(client.X + ScrollX, client.Y + ScrollY);
            int first, last, best = -1;
            long bestD = long.MaxValue;
            VisibleRows(out first, out last);
            for (int r = first; r <= last; r++)
                foreach (int idx in rows[r])
                {
                    if (idx < 0 || rects[idx].IsEmpty) continue;
                    var rc = rects[idx];
                    if (rc.Contains(c)) return idx;
                    if (!nearest) continue;
                    long dx = c.X < rc.Left ? rc.Left - c.X : c.X > rc.Right ? c.X - rc.Right : 0;
                    long dy = c.Y < rc.Top ? rc.Top - c.Y : c.Y > rc.Bottom ? c.Y - rc.Bottom : 0;
                    long d = dx * dx + dy * dy;
                    if (d < bestD) { bestD = d; best = idx; }
                }
            return best;
        }

        private int CharAt(int idx, double px, double py, double tol)
        {
            var p = pages[idx];
            IntPtr tp = p.Src.GetTextPage(p.Index);
            if (tp == IntPtr.Zero) return -1;
            return Pdfium.FPDFText_GetCharIndexAtPos(tp, px, py, tol, tol);
        }

        // ---- mouse ----------------------------------------------------------------

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            dragStart = e.Location;
            dragMoved = false;
            if (presentation)
            {
                drag = DragKind.Present;
                return;
            }
            if (e.Button == MouseButtons.Middle)
            {
                StartPan();
                return;
            }
            if (e.Button != MouseButtons.Left) return;
            int idx = PageAtClient(e.Location, false);
            if (idx >= 0 && pages[idx].Src != null)
            {
                lock (Pdfium.Lock)
                {
                    double px, py;
                    if (!pages[idx].Src.IsDisposed && ClientToPdf(idx, e.Location, out px, out py))
                    {
                        var link = pages[idx].Src.LinkAt(pages[idx].Index, px, py);
                        if (link != null)
                        {
                            drag = DragKind.Link;
                            pressedLink = link;
                            return;
                        }
                        int ci = CharAt(idx, px, py, 3);
                        if (ci >= 0)
                        {
                            if ((MouseModifiers & Keys.Shift) != 0 && selP0 >= 0) { selP1 = idx; selC1 = ci; }
                            else { selP0 = selP1 = idx; selC0 = selC1 = ci; }
                            selRects.Clear();
                            drag = DragKind.Select;
                            Capture = true;
                            Invalidate();
                            return;
                        }
                    }
                }
            }
            if (selP0 >= 0) ClearSelection();
            StartPan();
        }

        private void StartPan()
        {
            drag = DragKind.Pan;
            panX0 = ScrollX;
            panY0 = ScrollY;
            Capture = true;
            Cursor = Cursors.SizeAll;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (presentation)
            {
                ShowCursor();
                cursorTimer.Start();
            }
            if (drag != DragKind.None && !dragMoved)
            {
                if (Math.Abs(e.X - dragStart.X) > SystemInformation.DragSize.Width / 2 ||
                    Math.Abs(e.Y - dragStart.Y) > SystemInformation.DragSize.Height / 2) dragMoved = true;
            }
            switch (drag)
            {
                case DragKind.Pan:
                    ScrollTo(panX0 - (e.X - dragStart.X), panY0 - (e.Y - dragStart.Y));
                    return;
                case DragKind.Select:
                    if (e.Y < 0) ScrollBy(0, e.Y / 2 - S(4));
                    else if (e.Y > ClientSize.Height) ScrollBy(0, (e.Y - ClientSize.Height) / 2 + S(4));
                    ExtendSelection(e.Location);
                    return;
                case DragKind.Link:
                    if (dragMoved) { pressedLink = null; StartPanFromLink(e); }
                    return;
                case DragKind.None:
                    UpdateHoverCursor(e.Location);
                    return;
            }
        }

        private void StartPanFromLink(MouseEventArgs e)
        {
            drag = DragKind.Pan;
            panX0 = ScrollX;
            panY0 = ScrollY;
            Capture = true;
            Cursor = Cursors.SizeAll;
            ScrollTo(panX0 - (e.X - dragStart.X), panY0 - (e.Y - dragStart.Y));
        }

        private void ExtendSelection(Point client)
        {
            int idx = PageAtClient(client, true);
            if (idx < 0 || pages[idx].Src == null) return;
            lock (Pdfium.Lock)
            {
                double px, py;
                if (pages[idx].Src.IsDisposed || !ClientToPdf(idx, client, out px, out py)) return;
                int ci = CharAt(idx, px, py, 40);
                if (ci < 0) return;
                if (idx == selP1 && ci == selC1) return;
                selP1 = idx;
                selC1 = ci;
            }
            selRects.Clear();
            Invalidate();
        }

        private void UpdateHoverCursor(Point client)
        {
            Cursor c = Cursors.Default;
            int idx = PageAtClient(client, false);
            if (idx >= 0 && pages[idx].Src != null)
            {
                if (!Monitor.TryEnter(Pdfium.Lock)) return;
                try
                {
                    double px, py;
                    if (!pages[idx].Src.IsDisposed && ClientToPdf(idx, client, out px, out py))
                    {
                        if (pages[idx].Src.LinkAt(pages[idx].Index, px, py) != null) c = Cursors.Hand;
                        else if (CharAt(idx, px, py, 3) >= 0) c = Cursors.IBeam;
                    }
                }
                finally { Monitor.Exit(Pdfium.Lock); }
            }
            if (c != hoverCursor) { hoverCursor = c; Cursor = c; }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            var kind = drag;
            drag = DragKind.None;
            Capture = false;
            switch (kind)
            {
                case DragKind.Present:
                    if (!dragMoved)
                    {
                        if (e.Button == MouseButtons.Left) NextPage();
                        else if (e.Button == MouseButtons.Right) PrevPage();
                    }
                    break;
                case DragKind.Link:
                    if (!dragMoved && pressedLink != null) Navigate(pressedLink);
                    pressedLink = null;
                    break;
                case DragKind.Select:
                    if (!dragMoved && selP0 == selP1 && selC0 == selC1) ClearSelection();
                    break;
            }
            hoverCursor = null;
            UpdateHoverCursor(e.Location);
            if (hoverCursor == null) { hoverCursor = Cursors.Default; Cursor = Cursors.Default; }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if ((MouseModifiers & Keys.Control) != 0)
            {
                SetZoom(zoom * Math.Pow(1.12, e.Delta / 120.0), e.Location);
                return;
            }
            int lines = SystemInformation.MouseWheelScrollLines;
            if (lines <= 0) lines = 3;
            int delta = -(int)((long)e.Delta * lines * S(30) / 120);
            if ((MouseModifiers & Keys.Shift) != 0) { ScrollBy(delta, 0); return; }
            if (!continuous && pages.Count > 0)
            {
                if (delta > 0 && ScrollY >= MaxScrollY) { ScrollScreen(1); return; }
                if (delta < 0 && ScrollY <= 0) { ScrollScreen(-1); return; }
            }
            ScrollBy(0, delta);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (presentation) ShowCursor();
        }

        // ---- links & bookmarks --------------------------------------------------

        public void Navigate(LinkTarget t)
        {
            if (t.Uri != null)
            {
                OpenUri(t.Uri);
                return;
            }
            int pos = doc.Find(t.Src, t.PageIndex);
            if (pos < 0)
            {
                Toast("The link target page is not in this document.");
                return;
            }
            GoTo(pos, t.HasY ? DestV(pos, t.Y) : 0, true);
        }

        public void Navigate(OutlineItem item)
        {
            if (doc == null || doc.Base == null || item.PageIndex < 0) return;
            int pos = doc.Find(doc.Base, item.PageIndex);
            if (pos < 0)
            {
                Toast("That page was removed from this document.");
                return;
            }
            GoTo(pos, item.HasY ? DestV(pos, item.Y) : 0, true);
        }

        /// <summary>Fraction of the displayed page height for a destination's y coordinate.</summary>
        private double DestV(int pos, float y)
        {
            var p = pages[pos];
            if (p.Src == null) return 0;
            lock (Pdfium.Lock)
            {
                IntPtr page = p.Src.GetPage(p.Index);
                if (page == IntPtr.Zero) return 0;
                var b = PdfToBase(p, page, 0, y);
                var d = RotN(b.X, b.Y, (p.Rotation + viewRot) & 3);
                double v = Math.Max(0, Math.Min(1, d.Y));
                return v < 0.02 ? 0 : v;
            }
        }

        private void OpenUri(string uri)
        {
            Uri u;
            if (!Uri.TryCreate(uri.Trim(), UriKind.Absolute, out u) ||
                (u.Scheme != Uri.UriSchemeHttp && u.Scheme != Uri.UriSchemeHttps && u.Scheme != Uri.UriSchemeMailto))
            {
                Toast("Blocked a link that isn't a web or e-mail address.");
                return;
            }
            try { System.Diagnostics.Process.Start(u.AbsoluteUri); }
            catch (Exception ex) { Toast("Could not open the link: " + ex.Message); }
        }

        private void Toast(string s)
        {
            var h = Notify;
            if (h != null) h(s);
            else ShowToast(s);
        }

        // ---- text selection ----------------------------------------------------------

        public void ClearSelection()
        {
            if (selP0 < 0) return;
            selP0 = -1;
            selRects.Clear();
            Invalidate();
        }

        public void SelectAll()
        {
            if (pages.Count == 0) return;
            selP0 = 0;
            selC0 = 0;
            selP1 = pages.Count - 1;
            selC1 = int.MaxValue / 2;
            selRects.Clear();
            Invalidate();
        }

        private void OrderedSelection(out int p0, out int c0, out int p1, out int c1)
        {
            if (selP0 < selP1 || (selP0 == selP1 && selC0 <= selC1)) { p0 = selP0; c0 = selC0; p1 = selP1; c1 = selC1; }
            else { p0 = selP1; c0 = selC1; p1 = selP0; c1 = selC0; }
        }

        private List<RectangleF> GetSelRects(int idx)
        {
            List<RectangleF> list;
            if (selRects.TryGetValue(idx, out list)) return list;
            int p0, c0, p1, c1;
            OrderedSelection(out p0, out c0, out p1, out c1);
            if (idx < p0 || idx > p1) return null;
            var p = pages[idx];
            list = new List<RectangleF>();
            if (p.Src != null)
            {
                if (!Monitor.TryEnter(Pdfium.Lock, 40))
                {
                    BeginInvoke((Action)Invalidate);
                    return null;
                }
                try
                {
                    if (p.Src.IsDisposed) return null;
                    IntPtr tp = p.Src.GetTextPage(p.Index);
                    IntPtr page = p.Src.GetPage(p.Index);
                    if (tp != IntPtr.Zero && page != IntPtr.Zero)
                    {
                        int count = Pdfium.FPDFText_CountChars(tp);
                        int s = idx == p0 ? c0 : 0;
                        int e = idx == p1 ? Math.Min(c1, count - 1) : count - 1;
                        if (e >= s && count > 0)
                        {
                            int nr = Pdfium.FPDFText_CountRects(tp, s, e - s + 1);
                            for (int i = 0; i < nr; i++)
                            {
                                double l, t, r, b;
                                if (Pdfium.FPDFText_GetRect(tp, i, out l, out t, out r, out b) == 0) continue;
                                var a1 = PdfToBase(p, page, l, t);
                                var a2 = PdfToBase(p, page, r, b);
                                list.Add(RectangleF.FromLTRB(Math.Min(a1.X, a2.X), Math.Min(a1.Y, a2.Y), Math.Max(a1.X, a2.X), Math.Max(a1.Y, a2.Y)));
                            }
                        }
                    }
                }
                finally { Monitor.Exit(Pdfium.Lock); }
            }
            selRects[idx] = list;
            return list;
        }

        public string GetSelectedText()
        {
            if (selP0 < 0) return "";
            int p0, c0, p1, c1;
            OrderedSelection(out p0, out c0, out p1, out c1);
            var sb = new StringBuilder();
            lock (Pdfium.Lock)
            {
                for (int k = p0; k <= p1 && k < pages.Count; k++)
                {
                    var p = pages[k];
                    if (p.Src == null || p.Src.IsDisposed) continue;
                    IntPtr tp = p.Src.GetTextPage(p.Index);
                    if (tp == IntPtr.Zero) continue;
                    int count = Pdfium.FPDFText_CountChars(tp);
                    int s = k == p0 ? c0 : 0;
                    int e = k == p1 ? Math.Min(c1, count - 1) : count - 1;
                    if (e < s) continue;
                    if (sb.Length > 0) sb.Append("\r\n");
                    sb.Append(Pdfium.GetText(tp, s, e - s + 1));
                }
            }
            return sb.ToString();
        }

        // ---- search ----------------------------------------------------------------

        public void ClearHit()
        {
            hitPage = -1;
            hitRef = null;
            hitRects = null;
            Invalidate();
        }

        private void CancelSearch()
        {
            if (searchCts != null) { searchCts.Cancel(); searchCts = null; }
        }

        private sealed class Hit
        {
            public int Page, Char, Count;
        }

        public void Find(string query, bool forward, bool matchCase)
        {
            if (doc == null || pages.Count == 0 || string.IsNullOrEmpty(query)) return;
            CancelSearch();
            bool continuing = hitRef != null && hitPage >= 0 && hitPage < pages.Count && ReferenceEquals(pages[hitPage], hitRef)
                && query == lastQuery && matchCase == lastMatchCase;
            int startPage = continuing ? hitPage : currentPage;
            int startChar = continuing ? (forward ? hitChar + 1 : hitChar) : (forward ? 0 : -1);
            lastQuery = query;
            lastMatchCase = matchCase;
            var snapshot = pages;
            int version = doc.Version;
            var cts = new CancellationTokenSource();
            searchCts = cts;
            uint flags = matchCase ? Pdfium.FPDF_MATCHCASE : 0;
            int n = snapshot.Count;
            Task.Factory.StartNew(() =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                long lastReport = 0;
                for (int step = 0; step <= n; step++)
                {
                    if (cts.IsCancellationRequested) return (Hit)null;
                    int pi = forward ? (startPage + step) % n : (((startPage - step) % n) + n) % n;
                    var p = snapshot[pi];
                    if (p.Src == null) continue;
                    int sIdx = step == 0 ? startChar : (forward ? 0 : -1);
                    if (sw.ElapsedMilliseconds - lastReport > 400)
                    {
                        lastReport = sw.ElapsedMilliseconds;
                        int shown = step;
                        PostToUi(delegate { if (!cts.IsCancellationRequested) ShowToast("Searching… " + shown + " of " + n + " pages", 1500); });
                    }
                    lock (Pdfium.Lock)
                    {
                        if (p.Src.IsDisposed) return (Hit)null;
                        IntPtr tp = p.Src.GetTextPage(p.Index);
                        if (tp == IntPtr.Zero) continue;
                        if (sIdx >= Pdfium.FPDFText_CountChars(tp) && forward) continue;
                        IntPtr h = Pdfium.FPDFText_FindStart(tp, query, flags, sIdx);
                        if (h == IntPtr.Zero) continue;
                        try
                        {
                            bool ok = forward ? Pdfium.FPDFText_FindNext(h) != 0 : Pdfium.FPDFText_FindPrev(h) != 0;
                            if (ok)
                            {
                                int ci = Pdfium.FPDFText_GetSchResultIndex(h);
                                int cnt = Pdfium.FPDFText_GetSchCount(h);
                                if (!forward && step == 0 && sIdx >= 0 && ci >= sIdx) continue;
                                return new Hit { Page = pi, Char = ci, Count = cnt };
                            }
                        }
                        finally { Pdfium.FPDFText_FindClose(h); }
                    }
                }
                return (Hit)null;
            }, cts.Token).ContinueWith(delegate (Task<Hit> task)
            {
                var hit = task.IsFaulted || task.IsCanceled ? null : task.Result;
                // Always finish on the UI thread, explicitly: the ambient SynchronizationContext
                // is not guaranteed to be the WinForms one.
                PostToUi(delegate
                {
                    if (cts.IsCancellationRequested || doc == null || doc.Version != version || !ReferenceEquals(pages, snapshot)) return;
                    if (ReferenceEquals(searchCts, cts)) searchCts = null;
                    if (hit == null)
                    {
                        ShowToast("No matches for “" + query + "”");
                        return;
                    }
                    ShowHit(hit);
                });
            }, TaskScheduler.Default);
        }

        private void PostToUi(Action a)
        {
            try
            {
                if (IsHandleCreated && !IsDisposed) BeginInvoke(a);
            }
            catch (InvalidOperationException)
            {
                // The view is being torn down; the result is no longer needed.
            }
        }

        private void ShowHit(Hit hit)
        {
            var p = pages[hit.Page];
            var list = new List<RectangleF>();
            lock (Pdfium.Lock)
            {
                if (p.Src.IsDisposed) return;
                IntPtr tp = p.Src.GetTextPage(p.Index);
                IntPtr page = p.Src.GetPage(p.Index);
                if (tp == IntPtr.Zero || page == IntPtr.Zero) return;
                int nr = Pdfium.FPDFText_CountRects(tp, hit.Char, hit.Count);
                for (int i = 0; i < nr; i++)
                {
                    double l, t, r, b;
                    if (Pdfium.FPDFText_GetRect(tp, i, out l, out t, out r, out b) == 0) continue;
                    var a1 = PdfToBase(p, page, l, t);
                    var a2 = PdfToBase(p, page, r, b);
                    list.Add(RectangleF.FromLTRB(Math.Min(a1.X, a2.X), Math.Min(a1.Y, a2.Y), Math.Max(a1.X, a2.X), Math.Max(a1.Y, a2.Y)));
                }
            }
            hitPage = hit.Page;
            hitRef = p;
            hitChar = hit.Char;
            hitCount = hit.Count;
            hitRects = list;
            ScrollHitIntoView();
            Invalidate();
        }

        private void ScrollHitIntoView()
        {
            int page = hitPage;
            var hr = hitRects;
            if (page < 0 || page >= pages.Count) return;
            if (hr == null || hr.Count == 0) { GoToPage(page, false); return; }
            var p = pages[page];
            if (!continuous && pageRow[page] != curRow) { curRow = pageRow[page]; DoLayout(); }
            var rc = rects[page];
            var nr = RotRect(hr[0], (p.Rotation + viewRot) & 3);
            int hx = rc.X + (int)(nr.X * rc.Width), hy = rc.Y + (int)(nr.Y * rc.Height);
            int hw = (int)(nr.Width * rc.Width), hh = (int)(nr.Height * rc.Height);
            int x = ScrollX, y = ScrollY;
            if (hy < ScrollY + S(20) || hy + hh > ScrollY + ClientSize.Height - S(20)) y = hy - ClientSize.Height / 3;
            if (hx < ScrollX || hx + hw > ScrollX + ClientSize.Width) x = hx - ClientSize.Width / 3;
            ScrollTo(x, y);
            if (currentPage != page)
            {
                currentPage = page;
                RaiseViewChanged();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CancelSearch();
                ShowCursor();
                cursorTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
