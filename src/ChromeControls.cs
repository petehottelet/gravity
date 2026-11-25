using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Gravity
{
    /// <summary>Keeps the command row in place while its two native control sets change.</summary>
    internal sealed class CommandBar : Panel
    {
        private CommandTransition transition;
        public bool IsTransitioning { get { return transition != null; } }
        [DllImport("user32.dll")]
        private static extern bool SystemParametersInfo(uint action, uint parameter, out bool value, uint flags);
        public static bool AnimationsEnabled
        {
            get
            {
                bool enabled;
                return SystemInformation.IsMenuAnimationEnabled && SystemParametersInfo(0x1042, 0, out enabled, 0) && enabled;
            }
        }

        public CommandBar()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }

        private Bitmap Snapshot()
        {
            var bitmap = new Bitmap(Width, Height);
            if (transition != null)
                transition.DrawToBitmap(bitmap, ClientRectangle);
            else DrawToBitmap(bitmap, ClientRectangle);
            return bitmap;
        }

        public void Transition(Action change, bool animate, int duration)
        {
            Bitmap before = animate && Visible && IsHandleCreated ? Snapshot() : null;
            FinishTransition();
            try
            {
                change();
                if (before == null) return;
                var after = Snapshot();
                transition = new CommandTransition(before, after, duration) { Dock = DockStyle.Fill, BackColor = BackColor };
                before = null;
                transition.Completed += delegate { FinishTransition(); };
                Controls.Add(transition); transition.BringToFront();
            }
            finally { if (before != null) before.Dispose(); }
        }

        public void FinishTransition()
        {
            if (transition == null) return;
            var old = transition; transition = null;
            Controls.Remove(old); old.Dispose();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            FinishTransition(); base.OnSizeChanged(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) FinishTransition();
            base.Dispose(disposing);
        }

        private sealed class CommandTransition : Control
        {
            private readonly Bitmap before, after;
            private readonly Timer timer = new Timer { Interval = 15 };
            private readonly Stopwatch elapsed = Stopwatch.StartNew();
            private readonly int duration;
            public event EventHandler Completed;

            public CommandTransition(Bitmap before, Bitmap after, int duration)
            {
                this.before = before; this.after = after; this.duration = duration;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
                SetStyle(ControlStyles.Selectable, false); TabStop = false;
                timer.Tick += delegate
                {
                    if (elapsed.ElapsedMilliseconds >= duration)
                    { timer.Stop(); if (Completed != null) Completed(this, EventArgs.Empty); }
                    else Invalidate();
                };
                timer.Start();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(BackColor);
                double progress = Math.Min(1, (double)elapsed.ElapsedMilliseconds / duration);
                // Fade the old controls clear before the new labels occupy the same space.
                bool entering = progress >= 0.36;
                double amount = entering ? (progress - 0.36) / 0.64 : progress / 0.36;
                float opacity = entering ? (float)(1 - Math.Pow(1 - amount, 3)) : (float)(1 - amount);
                int rise = entering ? (int)Math.Round(4 * e.Graphics.DpiX / 96f * (1 - opacity)) : 0;
                var bitmap = entering ? after : before;
                using (var attributes = new ImageAttributes())
                {
                    attributes.SetColorMatrix(new ColorMatrix { Matrix33 = opacity });
                    e.Graphics.DrawImage(bitmap, new Rectangle(0, rise, Width, Height), 0, 0, bitmap.Width, bitmap.Height, GraphicsUnit.Pixel, attributes);
                }
                // The full-width row divider stays steady throughout the transition.
                using (var pen = new Pen(Theme.ChromeBorder)) e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing) { timer.Dispose(); before.Dispose(); after.Dispose(); }
                base.Dispose(disposing);
            }
        }
    }

    internal sealed class OutlineTreeView : TreeView
    {
        protected override CreateParams CreateParams
        {
            get { var parameters = base.CreateParams; parameters.Style |= 0x8000; return parameters; } // TVS_NOHSCROLL
        }
    }

    internal sealed class SingleLineLabel : Label
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }

    internal sealed class DocumentHeader : Control
    {
        public string DocumentTitle = "", DocumentState = "";
        public readonly BrandMenuButton BrandAction;
        public DocumentHeader()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Chrome; AccessibleRole = AccessibleRole.Grouping;
            BrandAction = new BrandMenuButton { Dock = DockStyle.Left, Width = 144 };
            Controls.Add(BrandAction);
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x84) { m.Result = (IntPtr)(-1); return; }
            base.WndProc(ref m);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; float scale = g.DpiX / 96f;
            Func<float, int> s = value => (int)Math.Round(value * scale);
            g.Clear(Theme.Chrome); g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            int side = s(202), width = Math.Max(1, Width - side * 2);
            using (var title = new Font("Segoe UI Semibold", 9.75f))
            using (var subtitle = new Font("Segoe UI", 8.25f))
            {
                TextRenderer.DrawText(g, DocumentTitle, title, new Rectangle(side, Height / 2 - s(17), width, s(20)), Theme.Text,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                TextRenderer.DrawText(g, DocumentState, subtitle, new Rectangle(side, Height / 2 + s(3), width, s(17)), Theme.SubtleText,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
        }
    }

    internal sealed class BrandMenuButton : Button
    {
        public Color LogoColor { get; set; }
        private bool hovered, pressed;
        public BrandMenuButton()
        {
            LogoColor = IconColor.Parse(IconColor.DefaultHex);
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; TabStop = true;
            BackColor = Theme.Chrome; AccessibleRole = AccessibleRole.PushButton;
            AccessibleName = "Gravity: Show application menus";
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }
        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = e.Button == MouseButtons.Left; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) { pressed = true; Invalidate(); } base.OnKeyDown(e); }
        protected override void OnKeyUp(KeyEventArgs e) { pressed = false; Invalidate(); base.OnKeyUp(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; float scale = g.DpiX / 96f;
            Func<float, int> s = value => (int)Math.Round(value * scale);
            g.Clear(Theme.Chrome); g.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new Rectangle(s(12), s(6), Width - s(24), Height - s(12));
            if (hovered || pressed)
                using (var path = Theme.RoundRect(bounds, s(6)))
                using (var brush = new SolidBrush(pressed ? Color.FromArgb(0xD4, 0xE1, 0xF1) : Color.FromArgb(0xE2, 0xE8, 0xF0))) g.FillPath(brush, path);
            Theme.DrawTintedImage(g, Brand.Mark, new Rectangle(s(20), (Height - s(23)) / 2, s(23), s(23)), LogoColor);
            using (var font = new Font("Segoe UI", 11.25f, FontStyle.Bold))
                TextRenderer.DrawText(g, "Gravity", font, new Rectangle(s(52), 0, Width - s(60), Height), Theme.Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g, bounds, Theme.Accent, Theme.Chrome);
        }
    }

    internal sealed class SegmentStrip : ToolStrip
    {
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Chrome);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Theme.RoundRect(new RectangleF(0, 0, Width - 1, Height - 1), 8 * e.Graphics.DpiX / 96f))
            using (var brush = new SolidBrush(Color.FromArgb(0xE5, 0xE8, 0xED))) e.Graphics.FillPath(brush, path);
        }
    }

    internal sealed class PageNumberField : ToolStripControlHost
    {
        private readonly PageInput input;
        public PageNumberField() : base(new PageInput())
        {
            input = (PageInput)Control;
            input.Editor.KeyDown += (s, e) => OnKeyDown(e);
            input.Editor.LostFocus += (s, e) => OnLostFocus(e);
            input.Editor.TextChanged += (s, e) => OnTextChanged(e);
            AccessibleName = "Page number";
        }
        public new bool Focused { get { return input.Editor.Focused; } }
        public new bool Focus() { return input.Editor.Focus(); }
        public void SelectAll() { input.Editor.SelectAll(); }
        public override string Text { get { return input == null ? "" : input.Editor.Text; } set { if (input != null) input.Editor.Text = value; } }
        public TextBox Editor { get { return input.Editor; } }

        private sealed class PageInput : UserControl
        {
            internal readonly TextBox Editor;
            public PageInput()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                BackColor = Theme.Chrome;
                Editor = new TextBox { BorderStyle = BorderStyle.None, TextAlign = HorizontalAlignment.Center,
                    BackColor = Color.White, ForeColor = Theme.Text, AccessibleName = "Page number" };
                Controls.Add(Editor);
                Editor.GotFocus += delegate { Invalidate(); }; Editor.LostFocus += delegate { Invalidate(); };
                Resize += delegate { LayoutEditor(); }; FontChanged += delegate { LayoutEditor(); };
                Size = new Size(44, 32);
            }
            private void LayoutEditor()
            {
                if (Editor == null) return;
                Editor.Font = Font;
                Editor.Bounds = new Rectangle(7, Math.Max(2, (Height - Editor.PreferredHeight) / 2), Math.Max(1, Width - 14), Editor.PreferredHeight);
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(Theme.Chrome); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                float scale = e.Graphics.DpiX / 96f;
                using (var path = Theme.RoundRect(new RectangleF(1, 1, Width - 3, Height - 3), 5 * scale))
                using (var fill = new SolidBrush(Color.White))
                using (var stroke = new Pen(Editor.Focused ? Theme.Accent : Color.FromArgb(0x7C, 0x87, 0x95), Editor.Focused ? 2 : 1))
                { e.Graphics.FillPath(fill, path); e.Graphics.DrawPath(stroke, path); }
            }
        }
    }
}
