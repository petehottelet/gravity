using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Gravity
{
    internal sealed class TextEditor : Form
    {
        private readonly List<PdfTextRun> runs;
        private readonly string[] values;
        private readonly ListBox sections;
        private readonly TextBox replacement;
        private readonly Label detail, status;
        private readonly Button apply;
        private readonly TextPagePreview preview;
        private bool selecting;

        public TextEditor(PageRef page, int pageIndex, int selected, Action<IList<PdfTextEdit>> commit)
        {
            Text = "Edit text — page " + (pageIndex + 1);
            Font = new Font("Segoe UI", 9f);
            ForeColor = Theme.Text; BackColor = Theme.Chrome;
            AutoScaleMode = AutoScaleMode.Dpi; AutoScaleDimensions = new SizeF(96, 96);
            ClientSize = new Size(820, 540); MinimumSize = new Size(660, 480);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = MaximizeBox = ShowInTaskbar = false;
            lock (Pdfium.Lock) runs = PdfTextEditing.Read(page).OrderByDescending(run => run.Bounds.Bottom).ThenBy(run => run.Bounds.Left).ToList();
            values = runs.Select(run => run.Text).ToArray();

            var hint = new Label { Dock = DockStyle.Top, Height = 64, Padding = new Padding(18, 14, 18, 0),
                Text = runs.Count == 0 ? "No editable text sections on this page. Scanned images and text inside grouped artwork are not supported. Use Fill Form for form fields."
                    : "Select a text section, then change its text. Font and position stay the same. Longer text may overlap nearby content; paragraphs do not reflow.", UseMnemonic = false };
            var body = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18, 0, 18, 0), ColumnCount = 2, RowCount = 1 };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38)); body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var original = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 18, 0) };
            preview = new TextPagePreview(page, runs) { Dock = DockStyle.Fill, AccessibleName = "Original page with selected text highlighted" };
            original.Controls.Add(preview);
            original.Controls.Add(new Label { Dock = DockStyle.Top, Height = 26, Text = "Original page" });
            body.Controls.Add(original, 0, 0);
            var fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Margin = Padding.Empty };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            fields.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            fields.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            fields.Controls.Add(new Label { Text = "Text sections", Dock = DockStyle.Fill }, 0, 0);
            sections = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, HorizontalScrollbar = true,
                AccessibleName = "Text sections", Margin = Padding.Empty };
            sections.Items.AddRange(runs.Cast<object>().ToArray()); fields.Controls.Add(sections, 0, 1);
            fields.Controls.Add(new Label { Text = "Replacement text", Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft, Padding = new Padding(0, 0, 0, 5) }, 0, 2);
            replacement = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical,
                AccessibleName = "Replacement text", MaxLength = 0, Enabled = runs.Count > 0, Margin = Padding.Empty };
            fields.Controls.Add(replacement, 0, 3);
            detail = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Theme.SubtleText, UseMnemonic = false };
            fields.Controls.Add(detail, 0, 4);
            fields.Controls.Add(new Label { Text = "One line per section. Clear its text to remove it.\nApply changes, then save the PDF to keep them.", Dock = DockStyle.Fill }, 0, 5);
            body.Controls.Add(fields, 1, 0);
            status = new Label { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(18, 8, 18, 0), AccessibleName = "Text editing status", UseMnemonic = false };
            var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 58, Padding = new Padding(12, 8, 12, 12), FlowDirection = FlowDirection.RightToLeft };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Size = new Size(90, 34) };
            apply = new Button { Text = "Apply", Enabled = false, Size = new Size(90, 34) };
            footer.Controls.AddRange(new Control[] { cancel, apply });
            Controls.Add(body); Controls.Add(status); Controls.Add(footer); Controls.Add(hint);
            AcceptButton = apply; CancelButton = cancel;
            sections.SelectedIndexChanged += delegate
            {
                selecting = true;
                int index = sections.SelectedIndex;
                replacement.Text = index >= 0 ? values[index] : "";
                detail.Text = index >= 0 ? "Section " + (index + 1) + " of " + runs.Count + " · " + runs[index].FontSize.ToString("0.#") + " pt" : "";
                preview.SelectedIndex = index; preview.Invalidate();
                selecting = false;
            };
            replacement.TextChanged += delegate
            {
                if (selecting || sections.SelectedIndex < 0) return;
                values[sections.SelectedIndex] = replacement.Text;
                int count = Edits().Count;
                apply.Enabled = count > 0;
                status.Text = count == 0 ? "" : count + (count == 1 ? " section changed" : " sections changed") + " · Ctrl+Z undoes changes after applying";
            };
            apply.Click += delegate
            {
                try { commit(Edits()); DialogResult = DialogResult.OK; }
                catch (InvalidOperationException ex) { status.Text = ex.Message; }
                catch (IOException ex) { status.Text = ex.Message; }
            };
            if (runs.Count > 0) sections.SelectedIndex = Math.Max(0, runs.FindIndex(run => run.Index == selected));
            Shown += delegate { if (runs.Count > 0) { replacement.Focus(); replacement.SelectAll(); } };
        }

        private List<PdfTextEdit> Edits()
        {
            return runs.Select((run, index) => new PdfTextEdit { Index = run.Index, Original = run.Text, Text = values[index] })
                .Where(edit => edit.Text != edit.Original).ToList();
        }
    }

    internal sealed class TextPagePreview : Control
    {
        private readonly Bitmap pageImage;
        private readonly List<RectangleF> bounds = new List<RectangleF>();
        public int SelectedIndex = -1;

        public TextPagePreview(PageRef page, IList<PdfTextRun> runs)
        {
            DoubleBuffered = true; BackColor = Theme.ViewBack;
            double width = page.BaseWidth, height = page.BaseHeight;
            if ((page.Rotation & 1) != 0) { double swap = width; width = height; height = swap; }
            double scale = 1000 / Math.Max(width, height);
            int w = Math.Max(1, (int)(width * scale)), h = Math.Max(1, (int)(height * scale));
            pageImage = RenderService.RenderPage(page, w, h, Pdfium.FPDF_ANNOT);
            lock (Pdfium.Lock)
            {
                IntPtr native = page.Src == null ? IntPtr.Zero : page.Src.GetPage(page.Index);
                foreach (var run in runs)
                {
                    var points = new List<Point>();
                    foreach (float x in new[] { run.Bounds.Left, run.Bounds.Right })
                        foreach (float y in new[] { run.Bounds.Top, run.Bounds.Bottom })
                        {
                            int dx, dy;
                            Pdfium.FPDF_PageToDevice(native, 0, 0, w, h, page.Rotation, x, y, out dx, out dy);
                            points.Add(new Point(dx, dy));
                        }
                    bounds.Add(RectangleF.FromLTRB(points.Min(p => p.X), points.Min(p => p.Y), points.Max(p => p.X), points.Max(p => p.Y)));
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            float scale = Math.Min((Width - 16f) / pageImage.Width, (Height - 16f) / pageImage.Height);
            if (scale <= 0) return;
            var paper = new RectangleF((Width - pageImage.Width * scale) / 2, (Height - pageImage.Height * scale) / 2, pageImage.Width * scale, pageImage.Height * scale);
            e.Graphics.DrawImage(pageImage, paper);
            if (SelectedIndex < 0 || SelectedIndex >= bounds.Count) return;
            var box = bounds[SelectedIndex];
            box = new RectangleF(paper.X + box.X * scale, paper.Y + box.Y * scale, Math.Max(2, box.Width * scale), Math.Max(2, box.Height * scale));
            box.Inflate(2, 2);
            using (var brush = new SolidBrush(Color.FromArgb(48, Theme.Accent))) e.Graphics.FillRectangle(brush, box);
            using (var pen = new Pen(Theme.Accent, 1.5f)) e.Graphics.DrawRectangle(pen, box.X, box.Y, box.Width, box.Height);
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); Invalidate(); }
        protected override void Dispose(bool disposing) { if (disposing) pageImage.Dispose(); base.Dispose(disposing); }
    }
}
