using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Gravity.Tests
{
    internal static class DialogTests
    {
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

        private static IEnumerable<Control> Children(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                yield return child;
                foreach (var nested in Children(child)) yield return nested;
            }
        }

        private static void Show(GravityDialog dialog)
        {
            dialog.StartPosition = FormStartPosition.Manual;
            dialog.Location = new Point(-4000, -4000);
            dialog.Show(); Application.DoEvents();
        }

        private static void Capture(Form dialog, string name)
        {
            string output = Environment.GetEnvironmentVariable("GRAVITY_DIALOG_CAPTURE_DIR");
            if (string.IsNullOrEmpty(output)) return;
            Directory.CreateDirectory(output);
            using (var bitmap = new Bitmap(dialog.Width, dialog.Height))
            {
                dialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                bitmap.Save(Path.Combine(output, name + ".png"));
            }
        }

        private static void CheckLayout(GravityDialog dialog, string name)
        {
            var children = Children(dialog).ToList();
            SelfTest.Check(children.OfType<DialogField>().All(field => field.ClientRectangle.Contains(field.Editor.Bounds)), name + " keeps native editors inside their rounded frames");
            SelfTest.Check(children.OfType<Button>().Where(button => button.Visible).All(button =>
            {
                var scroll = button.Parent as ScrollableControl;
                return button.Parent.ClientRectangle.Contains(button.Bounds) || scroll != null && scroll.AutoScroll && scroll.DisplayRectangle.Contains(button.Bounds);
            }), name + " keeps actions visible or reachable by scrolling");
            SelfTest.Check(children.OfType<DialogFooter>().All(footer => footer.Parent.ClientRectangle.Contains(footer.Bounds)), name + " keeps the action footer visible");
            SelfTest.Check(dialog.Content.Top > 30 && dialog.Content.BackColor == Color.White, name + " separates the integrated heading from the white content surface");
        }

        private static object Modal(Func<object> open, Action<GravityDialog> inspect)
        {
            Exception failure = null;
            using (var timer = new Timer { Interval = 30 })
            {
                timer.Tick += delegate
                {
                    var dialog = Application.OpenForms.OfType<GravityDialog>().FirstOrDefault(form => form.Modal);
                    if (dialog == null) return;
                    timer.Stop();
                    try { inspect(dialog); }
                    catch (Exception ex) { failure = ex; dialog.Close(); }
                };
                timer.Start();
                object result = open();
                if (failure != null) throw failure;
                return result;
            }
        }

        internal static void Run()
        {
            var fields = new List<PdfFormField> {
                new PdfFormField { Index = 0, Type = 6, Name = "Guest", Label = "Guest name", Value = "Sample Guest" },
                new PdfFormField { Index = 1, Type = 6, Name = "Performance", Label = "Performance", Value = "Evening show" },
                new PdfFormField { Index = 2, Type = 6, Name = "Seats", Label = "Number of seats", Value = "2" },
                new PdfFormField { Index = 3, Type = 2, Name = "Programme", Label = "Include a souvenir programme", Flags = 2, Checked = true }
            };
            using (var editor = new FormEditor(fields, 0, 0))
            {
                Show(editor); CheckLayout(editor, "form dialog"); Capture(editor, "form");
                var input = Children(editor).OfType<TextBox>().First();
                SelfTest.Check(input.Focused, "rounded form field receives initial keyboard focus");
                input.SelectAll(); input.SelectedText = "Updated guest";
                SelfTest.Check(editor.Edits.Count == 1 && editor.Edits[0].Text == "Updated guest", "rounded field keeps selection, typing and form edits");
                Point caption = editor.PointToScreen(new Point(100, 20));
                var packed = (IntPtr)((caption.Y & 0xffff) << 16 | (caption.X & 0xffff));
                SelfTest.Check(SendMessage(editor.Handle, 0x84, IntPtr.Zero, packed).ToInt32() == 2, "dialog header retains native caption dragging");
                Point edge = editor.PointToScreen(new Point(editor.Width - 3, editor.Height / 2));
                packed = (IntPtr)((edge.Y & 0xffff) << 16 | (edge.X & 0xffff));
                SelfTest.Check(SendMessage(editor.Handle, 0x84, IntPtr.Zero, packed).ToInt32() == 11 &&
                    SendMessage(editor.Content.Handle, 0x84, IntPtr.Zero, packed).ToInt32() == -1,
                    "dialog content passes edge hit tests through for native resizing");
                SendMessage(editor.Handle, 0xae, IntPtr.Zero, IntPtr.Zero);
                SendMessage(editor.Handle, 0x86, (IntPtr)1, IntPtr.Zero);
                Capture(editor, "form-activated");
                editor.Size = editor.MinimumSize; Application.DoEvents();
                CheckLayout(editor, "minimum form dialog"); Capture(editor, "form-minimum");
                typeof(Form).GetMethod("ProcessDialogKey", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(editor, new object[] { Keys.Escape });
                SelfTest.Check(editor.DialogResult == DialogResult.Cancel, "Escape still cancels the styled form dialog");
            }
            fields[0].Label = "A long field label that must wrap naturally when the form editor is resized to its minimum width";
            fields[0].ReadOnly = true;
            fields[1].Flags = 1 << 12;
            fields[1].Value = "First line\r\nSecond line";
            fields[2].Flags = 1 << 13;
            for (int i = 4; i < 18; i++) fields.Add(new PdfFormField { Index = i, Type = 6, Label = "Additional field " + i, Value = "" });
            using (var editor = new FormEditor(fields, 0, 17))
            {
                Show(editor); editor.Size = editor.MinimumSize; Application.DoEvents();
                var inputs = Children(editor).OfType<TextBox>().ToList();
                SelfTest.Check(!inputs[0].Enabled && inputs[1].Multiline && inputs[1].AcceptsReturn && inputs[2].UseSystemPasswordChar,
                    "styled form fields preserve read-only, multiline and password behavior");
                var label = Children(editor).OfType<Label>().First(c => c.Text.StartsWith("A long field"));
                SelfTest.Check(label.Width <= label.Parent.ClientSize.Width && label.Height > label.Font.Height, "long field labels wrap within narrow forms");
                inputs.Last().Focus(); Application.DoEvents();
                SelfTest.Check(inputs.Last().Focused && inputs.Last().RectangleToScreen(inputs.Last().ClientRectangle).IntersectsWith(editor.RectangleToScreen(editor.ClientRectangle)),
                    "keyboard navigation scrolls the last wrapped field into view");
                CheckLayout(editor, "long form dialog"); Capture(editor, "form-long");
                editor.Close();
            }
            foreach (float scale in new[] { 1f, 1.5f, 2f })
            {
                using (var export = new ExportDialog(12, 0, new[] { 0, 1 }))
                using (var images = new ImagesDialog(new[] { "first-page.png", "second-page.png" }, false))
                using (var palette = new CommandPalette(new List<PaletteCommand> { new PaletteCommand { Name = "File > Open", Shortcut = "Ctrl+O", Enabled = true } }))
                {
                    foreach (var dialog in new GravityDialog[] { export, images, palette })
                    {
                        Show(dialog);
                        SelfTest.Check(dialog.CancelButton != null && (dialog is CommandPalette || dialog.AcceptButton != null),
                            dialog.GetType().Name + " retains default and cancel actions after footer layout");
                        if (scale != 1) { dialog.Scale(new SizeF(scale, scale)); dialog.Font = new Font(dialog.Font.FontFamily, dialog.Font.Size * scale); }
                        Application.DoEvents();
                        string name = dialog.GetType().Name + "-" + (int)(scale * 100);
                        CheckLayout(dialog, name); Capture(dialog, name); dialog.Close();
                    }
                }
            }
            using (var images = new ImagesDialog(new[] { "first-page.png" }, false))
            {
                Show(images); images.ClientSize = new Size(420, 360); Application.DoEvents();
                var move = Children(images).OfType<Button>().Single(button => button.Text == "Move down");
                var body = (ScrollableControl)move.Parent;
                body.ScrollControlIntoView(move); move.Focus(); Application.DoEvents();
                SelfTest.Check(body.AutoScrollPosition.X < 0 && body.ClientRectangle.Contains(move.Bounds), "small displays can scroll image actions into view");
                var cancel = (Control)images.CancelButton;
                SelfTest.Check(images.RectangleToScreen(images.ClientRectangle).Contains(cancel.RectangleToScreen(cancel.ClientRectangle)), "small displays keep Cancel visible while options scroll");
                CheckLayout(images, "small image dialog"); Capture(images, "images-small"); images.Close();
            }
            using (var owner = new GravityDialog("Dialog tests", 200, 100))
            {
                Show(owner);
                object value = Modal(() => Dialogs.Input(owner, "Go to page", "Page number (1–12):", "1"), dialog =>
                {
                    CheckLayout(dialog, "input dialog"); Capture(dialog, "input");
                    var input = Children(dialog).OfType<TextBox>().Single();
                    SelfTest.Check(input.Focused && input.SelectedText == "1", "text prompt selects its initial value");
                    input.SelectedText = "12";
                    typeof(Form).GetMethod("ProcessDialogKey", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(dialog, new object[] { Keys.Enter });
                });
                SelfTest.Check((string)value == "12", "Enter accepts the styled text prompt");
                value = Modal(() => Dialogs.Password(owner, "Example.pdf", false), dialog =>
                {
                    SelfTest.Check(Children(dialog).OfType<TextBox>().Single().UseSystemPasswordChar, "styled password prompt masks its value");
                    dialog.CancelButton.PerformClick();
                });
                SelfTest.Check(value == null, "Cancel leaves the password prompt without a value");
                value = Modal(() => Dialogs.Combine(owner, new[] { "first.pdf", "second.pdf" }), dialog =>
                {
                    CheckLayout(dialog, "combine dialog"); Capture(dialog, "combine");
                    var list = Children(dialog).OfType<ListBox>().Single();
                    list.SelectedIndex = 0;
                    Children(dialog).OfType<Button>().Single(button => button.Text == "Move down").PerformClick();
                    dialog.AcceptButton.PerformClick();
                });
                SelfTest.Check(((List<string>)value).SequenceEqual(new[] { "second.pdf", "first.pdf" }), "styled Combine dialog preserves reordering and acceptance");
                Modal(() => { Dialogs.Shortcuts(owner); return null; }, dialog =>
                {
                    CheckLayout(dialog, "shortcuts dialog"); Capture(dialog, "shortcuts");
                    Children(dialog).OfType<CaptionButton>().Single().PerformClick();
                });
                SelfTest.Check(true, "integrated Close button closes the shortcuts dialog");
                owner.Close();
            }
        }
    }
}
