using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Gravity
{
    internal static partial class Theme
    {
        public static readonly Color ViewBack = Color.FromArgb(0xE8, 0xEB, 0xEF);
        public static readonly Color Chrome = Color.FromArgb(0xF7, 0xF8, 0xFA);
        public static readonly Color ChromeBorder = Color.FromArgb(0xCD, 0xD3, 0xDB);
        public static readonly Color TabActive = Color.White;
        public static readonly Color Text = Color.FromArgb(0x22, 0x28, 0x31);
        public static readonly Color SubtleText = Color.FromArgb(0x41, 0x4B, 0x57);
        public static readonly Color PageBorder = Color.FromArgb(0xC4, 0xC6, 0xCA);
        public static readonly Color SearchHit = Color.FromArgb(120, 0xFF, 0xB0, 0x00);
        public static readonly Color Modified = Color.FromArgb(0xD9, 0x6C, 0x00);

        /// <summary>Draws the matching bundled Lucide vector at the requested display size.</summary>
        public static Bitmap Glyph(string glyph, int px, Color color, string fontName = null, bool mirror = false)
        {
            return LucideIcons.Create(LucideIcons.FromGlyph(glyph, mirror), px, color);
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

        public static void PaperShadow(Graphics graphics, Rectangle page, float scale, bool thumbnail = false)
        {
            int blur = Math.Max(3, (int)Math.Round((thumbnail ? 5 : 10) * scale));
            int drop = Math.Max(1, (int)Math.Round((thumbnail ? 2 : 3) * scale));
            int key = blur * 2 + (thumbnail ? 1 : 0);
            Bitmap mask;
            if (!shadowMasks.TryGetValue(key, out mask))
            {
                int size = blur * 4 + 1;
                mask = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                var pixels = new byte[size * size * 4];
                double sigma = blur / 2.0;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        double horizontal = NormalCdf((x - blur) / sigma) - NormalCdf((x - blur * 3) / sigma);
                        double vertical = NormalCdf((y - blur) / sigma) - NormalCdf((y - blur * 3) / sigma);
                        int index = (y * size + x) * 4;
                        pixels[index] = thumbnail ? (byte)0x44 : (byte)0x4f;
                        pixels[index + 1] = thumbnail ? (byte)0x30 : (byte)0x38;
                        pixels[index + 2] = thumbnail ? (byte)0x23 : (byte)0x26;
                        pixels[index + 3] = (byte)Math.Round((thumbnail ? 39 : 36) * horizontal * vertical);
                    }
                var data = mask.LockBits(new Rectangle(0, 0, size, size), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                Marshal.Copy(pixels, 0, data.Scan0, pixels.Length); mask.UnlockBits(data);
                shadowMasks.Add(key, mask);
            }
            var bounds = new Rectangle(page.X - blur, page.Y + drop - blur, page.Width + blur * 2, page.Height + blur * 2);
            int corner = blur * 2;
            if (bounds.Width <= corner * 2 || bounds.Height <= corner * 2) { graphics.DrawImage(mask, bounds); return; }
            int[] source = { 0, corner, corner + 1, mask.Width };
            int[] xs = { bounds.Left, bounds.Left + corner, bounds.Right - corner, bounds.Right };
            int[] ys = { bounds.Top, bounds.Top + corner, bounds.Bottom - corner, bounds.Bottom };
            for (int row = 0; row < 3; row++)
                for (int column = 0; column < 3; column++)
                    graphics.DrawImage(mask, Rectangle.FromLTRB(xs[column], ys[row], xs[column + 1], ys[row + 1]),
                        source[column], source[row], source[column + 1] - source[column], source[row + 1] - source[row], GraphicsUnit.Pixel);
        }

        private static readonly System.Collections.Generic.Dictionary<int, Bitmap> shadowMasks = new System.Collections.Generic.Dictionary<int, Bitmap>();

        private static double NormalCdf(double value)
        {
            double x = Math.Abs(value) / Math.Sqrt(2), t = 1 / (1 + 0.3275911 * x);
            double erf = 1 - (((((1.061405429 * t - 1.453152027) * t) + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t * Math.Exp(-x * x);
            return 0.5 * (1 + (value < 0 ? -erf : erf));
        }

        public static void DrawTintedImage(Graphics graphics, Image image, Rectangle bounds, Color color)
        {
            var matrix = new System.Drawing.Imaging.ColorMatrix(new[] {
                new float[] { 0, 0, 0, 0, 0 }, new float[] { 0, 0, 0, 0, 0 }, new float[] { 0, 0, 0, 0, 0 },
                new float[] { 0, 0, 0, 1, 0 }, new[] { color.R / 255f, color.G / 255f, color.B / 255f, 0f, 1f } });
            using (var attributes = new System.Drawing.Imaging.ImageAttributes())
            {
                attributes.SetColorMatrix(matrix);
                graphics.DrawImage(image, bounds, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
            }
        }
    }

    /// <summary>Brand artwork embedded in the executable (see tools/make_brand.py).</summary>
    internal static class Brand
    {
        private static Image lockup;
        private static bool lockupLoaded;
        private static Image mark;
        public static Image Mark
        {
            get
            {
                if (mark == null)
                    using (var stream = typeof(Brand).Assembly.GetManifestResourceStream("GMark.png"))
                    using (var source = new Bitmap(stream)) mark = new Bitmap(source);
                return mark;
            }
        }

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
