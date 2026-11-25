using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace Gravity
{
    /// <summary>Shared app-owned dialog chrome; native editors keep their input and accessibility behavior.</summary>
    internal class GravityDialog : Form
    {
        internal readonly Panel Content;
        private readonly DialogHeading heading;

        internal GravityDialog(string title, int width, int height)
        {
            Text = title;
            Font = new Font("Segoe UI", 9.5f);
            ForeColor = Theme.Text;
            BackColor = Theme.Chrome;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = MaximizeBox = ShowInTaskbar = false;
            Padding = new Padding(1);
            ClientSize = new Size(width + 2, height + 58);
            Content = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, TabIndex = 0 };
            heading = new DialogHeading { Dock = DockStyle.Top, Height = 56, Text = title, TabIndex = 1 };
            var close = new CaptionButton("Close", 20, true) { Size = new Size(46, 34), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            close.Click += delegate { Close(); };
            heading.Controls.Add(close);
            heading.Resize += delegate { close.Location = new Point(heading.Width - close.Width, 0); };
            Controls.Add(Content); Controls.Add(heading);
            TextChanged += delegate { heading.Text = Text; };
        }

        protected override CreateParams CreateParams
        {
            get { var parameters = base.CreateParams; parameters.Style &= ~0x00c00000; return parameters; }
        }

        protected override void OnLoad(EventArgs e)
        {
            DialogStyle.Apply(Content);
            if (FormBorderStyle == FormBorderStyle.Sizable) ForwardResizeEdges(this);
            base.OnLoad(e);
        }

        private void ForwardResizeEdges(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                new DialogResizeWindow(this, child);
                ForwardResizeEdges(child);
            }
        }

        private int ResizeHit(Point screen)
        {
            if (FormBorderStyle != FormBorderStyle.Sizable) return 0;
            Point p = PointToClient(screen);
            int edge = Math.Max(4, Padding.Left * 6);
            bool left = p.X < edge, right = p.X >= Width - edge, top = p.Y < edge, bottom = p.Y >= Height - edge;
            return top ? left ? 13 : right ? 14 : 12 : bottom ? left ? 16 : right ? 17 : 15 : left ? 10 : right ? 11 : 0;
        }

        // Child HWNDs must pass the outer resize band back to the top-level window.
        private sealed class DialogResizeWindow : NativeWindow
        {
            private readonly GravityDialog owner;
            private readonly Control control;
            internal DialogResizeWindow(GravityDialog owner, Control control)
            {
                this.owner = owner; this.control = control;
                control.HandleCreated += HandleCreated;
                control.HandleDestroyed += HandleDestroyed;
                control.Disposed += Disposed;
                if (control.IsHandleCreated) AssignHandle(control.Handle);
            }
            private void HandleCreated(object sender, EventArgs e) { AssignHandle(control.Handle); }
            private void HandleDestroyed(object sender, EventArgs e) { ReleaseHandle(); }
            private void Disposed(object sender, EventArgs e)
            {
                control.HandleCreated -= HandleCreated; control.HandleDestroyed -= HandleDestroyed; control.Disposed -= Disposed;
                ReleaseHandle();
            }
            protected override void WndProc(ref Message m)
            {
                if (m.Msg == 0x84 && !owner.IsDisposed)
                {
                    long coordinates = m.LParam.ToInt64();
                    var point = new Point((short)(coordinates & 0xffff), (short)((coordinates >> 16) & 0xffff));
                    if (owner.ResizeHit(point) != 0) { m.Result = (IntPtr)(-1); return; }
                }
                base.WndProc(ref m);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Theme.ChromeBorder)) e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        protected override void WndProc(ref Message m)
        {
            // Keep native dragging, system-menu and resize behavior without a second classic caption.
            if (m.Msg == 0x85 || m.Msg == 0xae || m.Msg == 0xaf) { m.Result = IntPtr.Zero; return; }
            if (m.Msg == 0x86) { m.LParam = (IntPtr)(-1); base.WndProc(ref m); return; }
            if (m.Msg == 0x83 && m.WParam != IntPtr.Zero) { m.Result = IntPtr.Zero; return; }
            if (m.Msg == 0x84 && heading != null)
            {
                long coordinates = m.LParam.ToInt64();
                var screen = new Point((short)(coordinates & 0xffff), (short)((coordinates >> 16) & 0xffff));
                var p = PointToClient(screen);
                int resize = ResizeHit(screen);
                if (resize != 0) { m.Result = (IntPtr)resize; return; }
                if (heading.Bounds.Contains(p) && !heading.Controls[0].RectangleToScreen(heading.Controls[0].ClientRectangle).Contains(screen))
                { m.Result = (IntPtr)2; return; }
                m.Result = (IntPtr)1; return;
            }
            base.WndProc(ref m);
        }

        private sealed class DialogHeading : Control
        {
            public DialogHeading()
            {
                BackColor = Theme.Chrome;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            }
            protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
            protected override void WndProc(ref Message m)
            {
                if (m.Msg == 0x84) { m.Result = (IntPtr)(-1); return; }
                base.WndProc(ref m);
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                float scale = e.Graphics.DpiX / 96f;
                using (var font = new Font("Segoe UI Semibold", Font.SizeInPoints * 12f / 9.5f))
                    TextRenderer.DrawText(e.Graphics, Text, font, new Rectangle((int)(24 * scale), 0, Math.Max(1, Width - (int)(84 * scale)), Height),
                        Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                using (var pen = new Pen(Theme.ChromeBorder)) e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
            }
        }
    }

    internal sealed class DialogButton : Button
    {
        private bool hovered, pressed;
        public DialogButton()
        {
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            Size = new Size(96, 36);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }
        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = e.Button == MouseButtons.Left; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) { pressed = true; Invalidate(); } base.OnKeyDown(e); }
        protected override void OnKeyUp(KeyEventArgs e) { pressed = false; Invalidate(); base.OnKeyUp(e); }
        protected override void OnLostFocus(EventArgs e) { pressed = false; Invalidate(); base.OnLostFocus(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            bool primary = FindForm() != null && FindForm().AcceptButton == this;
            Color fill = !Enabled ? Theme.Chrome : primary ? pressed ? Theme.OutlineSelectedText : hovered ? Theme.AccentHot : Theme.Accent
                : pressed ? Theme.SidebarSelection : hovered ? Theme.ViewBack : Color.White;
            Color ink = !Enabled ? SystemColors.GrayText : primary ? Color.White : Theme.Text;
            float scale = e.Graphics.DpiX / 96f;
            e.Graphics.Clear(Parent == null ? BackColor : Parent.BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Theme.RoundRect(new RectangleF(1, 1, Width - 3, Height - 3), 6 * scale))
            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(primary && Enabled ? fill : Theme.ChromeBorder))
            { e.Graphics.FillPath(brush, path); e.Graphics.DrawPath(pen, path); }
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ink,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -(int)(5 * scale), -(int)(5 * scale)), ink, fill);
        }
    }

    internal sealed class DialogFooter : FlowLayoutPanel
    {
        public DialogFooter()
        {
            Dock = DockStyle.Bottom; Height = 72; Padding = new Padding(20, 16, 20, 16);
            FlowDirection = FlowDirection.RightToLeft; WrapContents = false; BackColor = Theme.Chrome;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Theme.ChromeBorder)) e.Graphics.DrawLine(pen, 0, 0, Width, 0);
        }
    }

    internal static class DialogStyle
    {
        internal static void Apply(Control parent)
        {
            foreach (Control child in parent.Controls.Cast<Control>().ToArray())
            {
                if (child is DialogField) continue;
                var text = child as TextBox;
                var combo = child as ComboBox;
                var number = child as NumericUpDown;
                var list = child as ListBox;
                var view = child as ListView;
                if ((text != null && !text.ReadOnly) || combo != null || number != null || list != null || view != null)
                {
                    int index = parent.Controls.GetChildIndex(child);
                    var field = new DialogField(child);
                    parent.Controls.Add(field); parent.Controls.SetChildIndex(field, index);
                }
                else Apply(child);
            }
        }
    }

    /// <summary>A rounded, focus-aware frame around a real Windows edit/list control.</summary>
    internal sealed class DialogField : Panel
    {
        internal readonly Control Editor;
        private readonly bool singleLine;
        internal DialogField(Control editor)
        {
            Editor = editor;
            var text = editor as TextBox;
            singleLine = text != null && !text.Multiline || editor is ComboBox || editor is NumericUpDown;
            float scale;
            using (var graphics = editor.CreateGraphics()) scale = graphics.DpiX / 96f;
            Bounds = new Rectangle(editor.Location, new Size(editor.Width, Math.Max((int)Math.Round((singleLine ? 36 : 40) * scale), editor.Height)));
            Dock = editor.Dock; Anchor = editor.Anchor; Margin = editor.Margin; TabIndex = editor.TabIndex; TabStop = false;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            if (text != null) text.BorderStyle = BorderStyle.None;
            var combo = editor as ComboBox; if (combo != null) combo.FlatStyle = FlatStyle.Flat;
            var number = editor as NumericUpDown; if (number != null) number.BorderStyle = BorderStyle.None;
            var list = editor as ListBox; if (list != null) list.BorderStyle = BorderStyle.None;
            var view = editor as ListView; if (view != null) view.BorderStyle = BorderStyle.None;
            editor.Dock = DockStyle.None; editor.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            editor.Margin = Padding.Empty; editor.TabIndex = 0;
            editor.ForeColor = Theme.Text;
            editor.BackColor = editor.Enabled ? Color.White : Theme.Chrome;
            editor.EnabledChanged += delegate { editor.BackColor = editor.Enabled ? Color.White : Theme.Chrome; Invalidate(); };
            editor.Enter += delegate { Invalidate(); }; editor.Leave += delegate { Invalidate(); };
            Controls.Add(editor);
        }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (Editor == null) return;
            int inset = Math.Max(6, (int)Math.Round(Font.Height * 0.5));
            int height = singleLine ? Editor.PreferredSize.Height : Math.Max(1, Height - inset * 2);
            Editor.Bounds = new Rectangle(inset, singleLine ? Math.Max(2, (Height - height) / 2) : inset, Math.Max(1, Width - inset * 2), height);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? Color.White : Parent.BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float scale = e.Graphics.DpiX / 96f;
            using (var path = Theme.RoundRect(new RectangleF(1, 1, Width - 3, Height - 3), 5 * scale))
            using (var fill = new SolidBrush(Editor.Enabled ? Color.White : Theme.Chrome))
            using (var pen = new Pen(ContainsFocus ? Theme.Accent : Color.FromArgb(0x7c, 0x87, 0x95), ContainsFocus ? Math.Max(2, 2 * scale) : 1))
            { e.Graphics.FillPath(fill, path); e.Graphics.DrawPath(pen, path); }
        }
    }
}
