using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Gravity
{
    internal sealed partial class MainForm
    {
        private CaptionButton minimizeWindow, maximizeWindow, closeWindow;
        private int pressedCaption;
        private Rectangle restoreWindowBounds;

        protected override CreateParams CreateParams
        {
            get { var parameters = base.CreateParams; parameters.Style &= ~0x00c00000; return parameters; }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { int corners = 2; DwmSetWindowAttribute(Handle, 33, ref corners, sizeof(int)); }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }

        private void BuildWindowChrome()
        {
            minimizeWindow = new CaptionButton("Minimize", 8);
            maximizeWindow = new CaptionButton("Maximize", 9);
            closeWindow = new CaptionButton("Close", 20);
            minimizeWindow.Click += delegate { WindowCommand(0xf020); };
            maximizeWindow.Click += delegate { WindowCommand(WindowState == FormWindowState.Maximized ? 0xf120 : 0xf030); };
            closeWindow.Click += delegate { Close(); };
            shellHeader.Controls.AddRange(new Control[] { minimizeWindow, maximizeWindow, closeWindow });
            shellHeader.Resize += delegate { LayoutWindowChrome(); };
            Resize += delegate { LayoutWindowChrome(); };
            LayoutWindowChrome();
        }

        private void LayoutWindowChrome()
        {
            if (closeWindow == null) return;
            int width = S(46), height = S(34);
            closeWindow.Bounds = new Rectangle(shellHeader.Width - width, 0, width, height);
            maximizeWindow.Bounds = new Rectangle(shellHeader.Width - width * 2, 0, width, height);
            minimizeWindow.Bounds = new Rectangle(shellHeader.Width - width * 3, 0, width, height);
            headerTools.Dock = DockStyle.None;
            headerTools.Bounds = new Rectangle(shellHeader.Width - width * 3 - S(64), 0, S(64), shellHeader.Height);
            maximizeWindow.Restore = WindowState == FormWindowState.Maximized;
            maximizeWindow.AccessibleName = maximizeWindow.Restore ? "Restore window" : "Maximize";
            maximizeWindow.Invalidate();
        }

        private CaptionButton CaptionAt(int hit)
        {
            return hit == 8 ? minimizeWindow : hit == 9 ? maximizeWindow : hit == 20 ? closeWindow : null;
        }

        private int WindowHitTest(Point screen)
        {
            Point p = PointToClient(screen);
            if (fullscreen) return 1;
            int edge = S(6);
            if (WindowState == FormWindowState.Normal)
            {
                bool left = p.X < edge, right = p.X >= ClientSize.Width - edge;
                bool top = p.Y < edge, bottom = p.Y >= ClientSize.Height - edge;
                if (top) return left ? 13 : right ? 14 : 12;
                if (bottom) return left ? 16 : right ? 17 : 15;
                if (left) return 10;
                if (right) return 11;
            }
            foreach (var button in new[] { minimizeWindow, maximizeWindow, closeWindow })
                if (button != null && button.Visible && button.RectangleToScreen(button.ClientRectangle).Contains(screen)) return button.Hit;
            if (shellHeader != null && shellHeader.Visible && shellHeader.RectangleToScreen(shellHeader.ClientRectangle).Contains(screen))
            {
                if (headerTools.RectangleToScreen(headerTools.ClientRectangle).Contains(screen)) return 1;
                return 2; // HTCAPTION: native drag, double-click, system menu and snapping.
            }
            return 1;
        }

        private void HoverCaption(int hit)
        {
            foreach (var button in new[] { minimizeWindow, maximizeWindow, closeWindow })
                if (button != null) button.SetHover(button.Hit == hit);
        }

        private void WindowCommand(int command) { SendWindowMessage(Handle, 0x112, (IntPtr)command, IntPtr.Zero); }

        protected override void WndProc(ref Message m)
        {
            if (!fullscreen && m.Msg == 0x112) // WM_SYSCOMMAND
            {
                int command = m.WParam.ToInt32() & 0xfff0;
                if ((command == 0xf020 || command == 0xf030) && WindowState == FormWindowState.Normal) restoreWindowBounds = Bounds;
                base.WndProc(ref m);
                // WinForms otherwise adds its assumed nonclient border to the restored client size.
                if (command == 0xf120 && WindowState == FormWindowState.Normal && !restoreWindowBounds.IsEmpty) Bounds = restoreWindowBounds;
                return;
            }
            if (!fullscreen && m.Msg == 0x83 && m.WParam != IntPtr.Zero) // WM_NCCALCSIZE
            {
                // Keep the native resizable window styles; extend the client into the caption.
                m.Result = IntPtr.Zero; return;
            }
            if (!fullscreen && m.Msg == 0x24) // WM_GETMINMAXINFO
            {
                base.WndProc(ref m);
                var info = (WindowMinMax)Marshal.PtrToStructure(m.LParam, typeof(WindowMinMax));
                var screen = Screen.FromHandle(Handle);
                info.MaxPosition = new Point(screen.WorkingArea.Left - screen.Bounds.Left, screen.WorkingArea.Top - screen.Bounds.Top);
                info.MaxSize = screen.WorkingArea.Size;
                Marshal.StructureToPtr(info, m.LParam, false); return;
            }
            if (!fullscreen && m.Msg == 0x84) // WM_NCHITTEST
            {
                long coordinates = m.LParam.ToInt64();
                m.Result = (IntPtr)WindowHitTest(new Point((short)(coordinates & 0xffff), (short)((coordinates >> 16) & 0xffff)));
                return;
            }
            if (!fullscreen && m.Msg == 0xa0) // WM_NCMOUSEMOVE
            {
                HoverCaption(m.WParam.ToInt32());
                var tracking = new WindowMouseTracking { Size = Marshal.SizeOf(typeof(WindowMouseTracking)), Flags = 0x12, Window = Handle };
                TrackMouseEvent(ref tracking);
            }
            if (m.Msg == 0x2a2) HoverCaption(0); // WM_NCMOUSELEAVE
            if (m.Msg == 0xa1 && CaptionAt(m.WParam.ToInt32()) != null) // WM_NCLBUTTONDOWN
            {
                pressedCaption = m.WParam.ToInt32(); Capture = true; m.Result = IntPtr.Zero; return;
            }
            if ((m.Msg == 0x202 || m.Msg == 0xa2) && pressedCaption != 0) // client / nonclient mouse up
            {
                int pressed = pressedCaption; pressedCaption = 0; Capture = false;
                var button = CaptionAt(pressed);
                if (button != null && button.RectangleToScreen(button.ClientRectangle).Contains(Cursor.Position)) button.PerformClick();
                m.Result = IntPtr.Zero; return;
            }
            if (m.Msg == 0x215) pressedCaption = 0; // WM_CAPTURECHANGED
            base.WndProc(ref m);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowMinMax
        {
            public Point Reserved;
            public Size MaxSize;
            public Point MaxPosition;
            public Size MinTrackSize, MaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowMouseTracking
        {
            public int Size, Flags;
            public IntPtr Window;
            public int HoverTime;
        }

        [DllImport("user32.dll")]
        private static extern bool TrackMouseEvent(ref WindowMouseTracking tracking);

        [DllImport("user32.dll", EntryPoint = "SendMessage")]
        private static extern IntPtr SendWindowMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
    }

    internal sealed class CaptionButton : Button
    {
        public readonly int Hit;
        public bool Restore;
        private bool hovered;
        private readonly bool clientHitTest;
        public CaptionButton(string name, int hit, bool clientHitTest = false)
        {
            this.clientHitTest = clientHitTest;
            Hit = hit; AccessibleName = name; AccessibleRole = AccessibleRole.PushButton;
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Text = ""; TabStop = true;
            BackColor = Theme.Chrome; Cursor = Cursors.Default;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }
        public void SetHover(bool value) { if (hovered != value) { hovered = value; Invalidate(); } }
        protected override void OnMouseEnter(EventArgs e) { if (clientHitTest) SetHover(true); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { if (clientHitTest) SetHover(false); base.OnMouseLeave(e); }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x84 && !clientHitTest) { m.Result = (IntPtr)(-1); return; } // HTTRANSPARENT: the frame owns native hit testing.
            base.WndProc(ref m);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(hovered ? Hit == 20 ? Color.FromArgb(196, 43, 28) : Color.FromArgb(0xE2, 0xE8, 0xF0) : Theme.Chrome);
            float scale = g.DpiX / 96f;
            int side = (int)Math.Round(10 * scale), x = (Width - side) / 2, y = (Height - side) / 2;
            using (var pen = new Pen(hovered && Hit == 20 ? Color.White : Theme.SubtleText, Math.Max(1, scale)))
            {
                if (Hit == 8) g.DrawLine(pen, x, y + side / 2, x + side, y + side / 2);
                else if (Hit == 20) { g.DrawLine(pen, x, y, x + side, y + side); g.DrawLine(pen, x + side, y, x, y + side); }
                else if (Restore)
                {
                    int offset = Math.Max(2, (int)Math.Round(2 * scale));
                    g.DrawLines(pen, new[] { new Point(x + offset, y + offset), new Point(x + offset, y), new Point(x + side, y), new Point(x + side, y + side - offset), new Point(x + side - offset, y + side - offset) });
                    g.DrawRectangle(pen, x, y + offset, side - offset, side - offset);
                }
                else g.DrawRectangle(pen, x, y, side, side);
            }
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(ClientRectangle, -4, -4), Theme.Accent, BackColor);
        }
    }
}
