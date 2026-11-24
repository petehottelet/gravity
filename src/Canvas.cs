using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Gravity
{
    internal static class Theme
    {
        public static readonly Color ViewBack = Color.FromArgb(0xE3, 0xE4, 0xE6);
        public static readonly Color Chrome = Color.FromArgb(0xF7, 0xF7, 0xF8);
        public static readonly Color ChromeBorder = Color.FromArgb(0xDA, 0xDB, 0xDE);
        public static readonly Color TabActive = Color.White;
        public static readonly Color Text = Color.FromArgb(0x1F, 0x1F, 0x22);
        public static readonly Color SubtleText = Color.FromArgb(0x6B, 0x6E, 0x76);
        public static readonly Color Accent = Color.FromArgb(0x0B, 0x57, 0xD0);       // brand blue
        public static readonly Color AccentHot = Color.FromArgb(0x08, 0x47, 0xAE);
        public static readonly Color AccentSoft = Color.FromArgb(0xD8, 0xE5, 0xFB);
        public static readonly Color PageBorder = Color.FromArgb(0xC4, 0xC6, 0xCA);
        public static readonly Color Selection = Color.FromArgb(80, 0x1A, 0x73, 0xE8);
        public static readonly Color SearchHit = Color.FromArgb(120, 0xFF, 0xB0, 0x00);
        public static readonly Color Modified = Color.FromArgb(0xD9, 0x6C, 0x00);

        private static string iconFont;
        public static string IconFontName
        {
            get
            {
                if (iconFont != null) return iconFont;
                // Probe directly: enumerating every installed font is slow on machines with many fonts.
                using (var probe = new Font("Segoe Fluent Icons", 10f))
                    iconFont = probe.Name == "Segoe Fluent Icons" ? probe.Name : "Segoe MDL2 Assets";
                return iconFont;
            }
        }

        /// <summary>Draws an icon-font glyph into a bitmap so ToolStrip items stay crisp at any DPI.</summary>
        public static Bitmap Glyph(string glyph, int px, Color color, string fontName = null, bool mirror = false)
        {
            var bmp = new Bitmap(px, px);
            using (var g = Graphics.FromImage(bmp))
            using (var font = new Font(fontName ?? IconFontName, px * 0.75f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(color))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.DrawString(glyph, font, brush, new RectangleF(0, 0, px, px + 1), sf);
            }
            if (mirror) bmp.RotateFlip(RotateFlipType.RotateNoneFlipX);
            return bmp;
        }

        public static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = radius * 2;
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    /// <summary>Brand artwork embedded in the executable (see tools/make_brand.py).</summary>
    internal static class Brand
    {
        private static Image lockup;
        private static bool lockupLoaded;

        /// <summary>The icon + GRAVITY wordmark lockup, or null if the resource is missing.</summary>
        public static Image Lockup
        {
            get
            {
                if (!lockupLoaded)
                {
                    lockupLoaded = true;
                    using (var s = typeof(Brand).Assembly.GetManifestResourceStream("Lockup.png"))
                        if (s != null)
                            using (var tmp = new Bitmap(s))
                                lockup = new Bitmap(tmp);  // copy so the stream can close
                }
                return lockup;
            }
        }

        /// <summary>The application icon with all of its sizes, or null if the resource is missing.</summary>
        public static Icon AppIcon()
        {
            using (var s = typeof(Brand).Assembly.GetManifestResourceStream("Gravity.ico"))
                return s != null ? new Icon(s) : null;
        }
    }

    /// <summary>
    /// Base for the custom-drawn views: native window scrollbars, double buffering, and a toast overlay.
    /// </summary>
    internal abstract class CanvasControl : Control
    {
        private const int WS_VSCROLL = 0x00200000, WS_HSCROLL = 0x00100000;
        private const int WM_HSCROLL = 0x114, WM_VSCROLL = 0x115;
        private const int SB_HORZ = 0, SB_VERT = 1;
        private const int SIF_RANGE = 1, SIF_PAGE = 2, SIF_POS = 4, SIF_DISABLENOSCROLL = 8, SIF_TRACKPOS = 0x10;

        [StructLayout(LayoutKind.Sequential)]
        private struct SCROLLINFO
        {
            public int cbSize, fMask, nMin, nMax, nPage, nPos, nTrackPos;
        }

        [DllImport("user32.dll")] private static extern int SetScrollInfo(IntPtr hwnd, int bar, ref SCROLLINFO si, bool redraw);
        [DllImport("user32.dll")] private static extern bool GetScrollInfo(IntPtr hwnd, int bar, ref SCROLLINFO si);

        protected int ScrollX, ScrollY;
        protected int ContentW, ContentH;
        protected bool HideScrollbars;
        private bool inScrollUpdate, resizePending;
        private int resizeDepth;

        private string toast;
        private readonly Timer toastTimer = new Timer();

        protected readonly float Dpi;

        protected CanvasControl()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.Selectable | ControlStyles.ResizeRedraw, true);
            TabStop = true;
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) Dpi = g.DpiX;
            toastTimer.Tick += delegate { toastTimer.Stop(); toast = null; Invalidate(); };
        }

        protected int S(float v) { return (int)Math.Round(v * Dpi / 96f); }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.Style |= WS_VSCROLL | WS_HSCROLL;
                return cp;
            }
        }

        protected virtual int LineStep { get { return S(48); } }

        protected int MaxScrollX { get { return Math.Max(0, ContentW - ClientSize.Width); } }
        protected int MaxScrollY { get { return Math.Max(0, ContentH - ClientSize.Height); } }

        protected void UpdateScrollbars()
        {
            if (!IsHandleCreated) return;
            inScrollUpdate = true;
            try
            {
                var si = new SCROLLINFO();
                si.cbSize = Marshal.SizeOf(typeof(SCROLLINFO));
                si.fMask = SIF_RANGE | SIF_PAGE | SIF_POS | (HideScrollbars ? 0 : SIF_DISABLENOSCROLL);
                si.nMin = 0;
                si.nMax = HideScrollbars ? 0 : Math.Max(0, ContentH - 1);
                si.nPage = HideScrollbars ? 0 : Math.Max(0, ClientSize.Height);
                si.nPos = ScrollY;
                SetScrollInfo(Handle, SB_VERT, ref si, true);

                si.fMask = SIF_RANGE | SIF_PAGE | SIF_POS;
                bool needH = !HideScrollbars && ContentW > ClientSize.Width;
                si.nMax = needH ? ContentW - 1 : 0;
                si.nPage = needH ? ClientSize.Width : 0;
                si.nPos = needH ? ScrollX : 0;
                SetScrollInfo(Handle, SB_HORZ, ref si, true);
            }
            finally
            {
                inScrollUpdate = false;
            }
            if (resizePending && resizeDepth < 3)
            {
                resizePending = false;
                resizeDepth++;
                try { OnViewportResized(); }
                finally { resizeDepth--; }
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateScrollbars();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (inScrollUpdate) { resizePending = true; return; }
            OnViewportResized();
        }

        /// <summary>Called when the client area changes size; derived views re-layout here.</summary>
        protected abstract void OnViewportResized();

        /// <summary>Called after the scroll position changed.</summary>
        protected virtual void OnScrolled() { }

        public void ScrollTo(int x, int y)
        {
            x = Math.Max(0, Math.Min(MaxScrollX, x));
            y = Math.Max(0, Math.Min(MaxScrollY, y));
            if (x == ScrollX && y == ScrollY) return;
            ScrollX = x;
            ScrollY = y;
            UpdateScrollbars();
            Invalidate();
            OnScrolled();
        }

        public void ScrollBy(int dx, int dy) { ScrollTo(ScrollX + dx, ScrollY + dy); }

        /// <summary>
        /// Shift/Ctrl as recorded in the mouse message being handled. Unlike Control.ModifierKeys
        /// (the keyboard's state right now), this is the state when the click or wheel happened.
        /// </summary>
        protected Keys MouseModifiers { get; private set; }

        protected override void WndProc(ref Message m)
        {
            if ((m.Msg >= 0x200 && m.Msg <= 0x20A) || m.Msg == 0x20E)  // WM_MOUSEMOVE..WM_MOUSEWHEEL, WM_MOUSEHWHEEL
            {
                int flags = (int)((long)m.WParam & 0xFFFF);
                MouseModifiers = ((flags & 0x4) != 0 ? Keys.Shift : Keys.None) | ((flags & 0x8) != 0 ? Keys.Control : Keys.None);
            }
            if (m.Msg == WM_VSCROLL || m.Msg == WM_HSCROLL)
            {
                bool vert = m.Msg == WM_VSCROLL;
                int code = (int)((long)m.WParam & 0xFFFF);
                int page = vert ? ClientSize.Height : ClientSize.Width;
                int cur = vert ? ScrollY : ScrollX;
                int next = cur;
                switch (code)
                {
                    case 0: next = cur - LineStep; break;              // SB_LINEUP
                    case 1: next = cur + LineStep; break;              // SB_LINEDOWN
                    case 2: next = cur - page * 9 / 10; break;         // SB_PAGEUP
                    case 3: next = cur + page * 9 / 10; break;         // SB_PAGEDOWN
                    case 4:
                    case 5:                                            // SB_THUMBPOSITION / SB_THUMBTRACK
                        var si = new SCROLLINFO();
                        si.cbSize = Marshal.SizeOf(typeof(SCROLLINFO));
                        si.fMask = SIF_TRACKPOS;
                        if (GetScrollInfo(Handle, vert ? SB_VERT : SB_HORZ, ref si)) next = si.nTrackPos;
                        break;
                    case 6: next = 0; break;                           // SB_TOP
                    case 7: next = int.MaxValue / 2; break;            // SB_BOTTOM
                    default: return;
                }
                if (vert) ScrollTo(ScrollX, next); else ScrollTo(next, ScrollY);
                return;
            }
            base.WndProc(ref m);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Up:
                case Keys.Down:
                case Keys.Left:
                case Keys.Right:
                case Keys.PageUp:
                case Keys.PageDown:
                case Keys.Home:
                case Keys.End:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (!Focused) Focus();
            base.OnMouseDown(e);
        }

        public void ShowToast(string text, int ms = 2200)
        {
            toast = text;
            toastTimer.Stop();
            toastTimer.Interval = ms;
            toastTimer.Start();
            Invalidate();
        }

        protected void PaintToast(Graphics g)
        {
            if (string.IsNullOrEmpty(toast)) return;
            using (var font = new Font("Segoe UI", 10f))
            {
                float pad = S(12);
                float textWidth = Math.Max(1, ClientSize.Width - S(16) - pad * 2);
                var size = g.MeasureString(toast, font, new SizeF(textWidth, 10000));
                float h = size.Height + S(12);
                var r = new RectangleF((ClientSize.Width - size.Width) / 2 - pad,
                    Math.Max(S(8), ClientSize.Height - h - S(28)), size.Width + pad * 2, h);
                var oldMode = g.SmoothingMode;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = Theme.RoundRect(r, h / 2))
                using (var bg = new SolidBrush(Color.FromArgb(230, 32, 33, 36)))
                    g.FillPath(bg, path);
                g.SmoothingMode = oldMode;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                using (var fg = new SolidBrush(Color.White))
                    g.DrawString(toast, font, fg, new RectangleF(r.X + pad, r.Y + (h - size.Height) / 2, size.Width, size.Height));
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) toastTimer.Dispose();
            base.Dispose(disposing);
        }
    }
}
