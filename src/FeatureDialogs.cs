using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Gravity
{
    internal class FeatureDialog : Form
    {
        protected FeatureDialog(string title, int width, int height)
        {
            Text = title;
            Font = new Font("Segoe UI", 9f);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            ClientSize = new Size(width, height);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = MaximizeBox = ShowInTaskbar = false;
        }

        protected Label LabelAt(string text, int x, int y, int width, int height = 24)
        {
            var label = new Label { Text = text, Left = x, Top = y, Width = width, Height = height };
            Controls.Add(label);
            return label;
        }

        protected Button ButtonAt(string text, int x, int y, int width, Action action)
        {
            var button = new Button { Text = text, Left = x, Top = y, Width = width, Height = 28 };
            button.Click += delegate { action(); };
            Controls.Add(button);
            return button;
        }
    }

    internal sealed class PaletteCommand
    {
        public string Name, Shortcut;
        public ToolStripMenuItem Item;
        public bool Enabled;

        public static List<PaletteCommand> FromMenu(ToolStripItemCollection items, string prefix = "", bool enabled = true)
        {
            var result = new List<PaletteCommand>();
            foreach (ToolStripItem entry in items)
            {
                var item = entry as ToolStripMenuItem;
                if (item == null || Equals(item.Tag, "palette")) continue;
                string name = (prefix.Length == 0 ? "" : prefix + " › ") + item.Text.Replace("&", "");
                if (item.HasDropDownItems) result.AddRange(FromMenu(item.DropDownItems, name, enabled && item.Enabled));
                else result.Add(new PaletteCommand { Name = name, Shortcut = item.ShortcutKeyDisplayString,
                    Item = item, Enabled = enabled && item.Enabled });
            }
            return result;
        }

        public bool Matches(string query)
        {
            string text = Name + " " + Shortcut;
            return query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .All(word => text.IndexOf(word, StringComparison.CurrentCultureIgnoreCase) >= 0);
        }
    }

    internal sealed class CommandPalette : FeatureDialog
    {
        private readonly TextBox query;
        private readonly ListView list;
        private readonly Label status;
        private readonly List<PaletteCommand> commands;
        public PaletteCommand SelectedCommand { get; private set; }

        public CommandPalette(List<PaletteCommand> commands) : base("Command palette", 620, 416)
        {
            this.commands = commands;
            LabelAt("Search commands", 16, 12, 480);
            query = new TextBox { Left = 16, Top = 36, Width = 588, AccessibleName = "Search commands" };
            list = new ListView { Left = 16, Top = 72, Width = 588, Height = 278, View = View.Details,
                FullRowSelect = true, HideSelection = false, MultiSelect = false, HeaderStyle = ColumnHeaderStyle.Nonclickable,
                AccessibleName = "Commands" };
            list.Columns.Add("Command", 425); list.Columns.Add("Shortcut", 135);
            status = LabelAt("Enter to run · Escape to close", 16, 365, 410, 36);
            var close = ButtonAt("Close", 516, 366, 88, () => Close());
            close.DialogResult = DialogResult.Cancel; CancelButton = close;
            Controls.AddRange(new Control[] { query, list });
            query.TextChanged += delegate { RefreshCommands(); };
            query.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { RunSelected(); e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Up)
                {
                    int at = list.SelectedIndices.Count == 0 ? 0 : list.SelectedIndices[0];
                    at = Math.Max(0, Math.Min(list.Items.Count - 1, at + (e.KeyCode == Keys.Down ? 1 : -1)));
                    if (at >= 0 && list.Items.Count > 0) { list.Items[at].Selected = true; list.EnsureVisible(at); }
                    e.SuppressKeyPress = true;
                }
            };
            list.ItemActivate += delegate { RunSelected(); };
            Shown += delegate { query.Focus(); };
            RefreshCommands();
        }

        private void RefreshCommands()
        {
            list.BeginUpdate(); list.Items.Clear();
            foreach (var command in commands.Where(c => c.Matches(query.Text)))
            {
                var row = new ListViewItem(command.Name) { Tag = command, ForeColor = command.Enabled ? SystemColors.WindowText : SystemColors.GrayText };
                row.SubItems.Add(command.Shortcut ?? ""); list.Items.Add(row);
            }
            if (list.Items.Count > 0) list.Items[0].Selected = true;
            list.EndUpdate();
            status.Text = list.Items.Count == 0 ? "No matching commands. Try a different word." : "Enter to run · Escape to close";
        }

        private void RunSelected()
        {
            if (list.SelectedItems.Count == 0) return;
            var command = (PaletteCommand)list.SelectedItems[0].Tag;
            if (!command.Enabled) { status.Text = "This command is unavailable in the current document or view."; return; }
            SelectedCommand = command;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    internal sealed class WorkDialog : FeatureDialog
    {
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly Label status;
        private readonly ProgressBar progress;
        private readonly Button cancel;
        private bool finished;
        private Exception error;
        private bool cancelled;

        private WorkDialog(string title, Action<CancellationToken, Action<int, string>> work) : base(title, 440, 148)
        {
            status = LabelAt("Starting…", 16, 16, 408, 38);
            progress = new ProgressBar { Left = 16, Top = 60, Width = 408, Height = 16 };
            Controls.Add(progress);
            cancel = ButtonAt("Cancel", 336, 100, 88, CancelWork);
            CancelButton = cancel;
            FormClosing += (s, e) => { if (!finished) { e.Cancel = true; CancelWork(); } };
            Shown += delegate
            {
                var clock = Stopwatch.StartNew();
                long reported = -100;
                Action<int, string> report = (percent, text) =>
                {
                    if (percent < 100 && clock.ElapsedMilliseconds - reported < 100) return;
                    reported = clock.ElapsedMilliseconds;
                    BeginInvoke((Action)delegate
                    {
                        if (cancellation.IsCancellationRequested) return;
                        progress.Value = Math.Max(0, Math.Min(100, percent)); status.Text = text;
                    });
                };
                Task.Factory.StartNew(() =>
                {
                    try { work(cancellation.Token, report); }
                    catch (OperationCanceledException) { cancelled = true; }
                    catch (Exception ex) { error = ex; }
                }).ContinueWith(task => BeginInvoke((Action)delegate { finished = true; Close(); }), TaskScheduler.Default);
            };
        }

        private void CancelWork()
        {
            cancellation.Cancel(); cancel.Enabled = false; status.Text = "Cancelling after the current page…";
        }

        public static bool Run(IWin32Window owner, string title, Action<CancellationToken, Action<int, string>> work)
        {
            using (var dialog = new WorkDialog(title, work))
            {
                dialog.ShowDialog(owner);
                dialog.cancellation.Dispose();
                if (dialog.error != null) throw dialog.error;
                return !dialog.cancelled;
            }
        }
    }

    internal sealed class ExportDialog : FeatureDialog
    {
        private readonly ComboBox scope, format;
        private readonly TextBox range;
        private readonly NumericUpDown resolution;
        private readonly Label error, resolutionLabel;
        private readonly IList<int> selection;
        private readonly int count, current;
        public List<int> Indices { get; private set; }
        public int Format { get { return format.SelectedIndex; } } // PNG, JPEG, TXT
        public int Resolution { get { return (int)resolution.Value; } }

        public ExportDialog(int count, int current, IList<int> selection) : base("Export pages", 456, 320)
        {
            this.count = count; this.current = current; this.selection = selection;
            LabelAt("Pages", 16, 20, 100);
            scope = new ComboBox { Left = 128, Top = 16, Width = 310, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Pages to export" };
            scope.Items.AddRange(new object[] { "All pages (" + count + ")", "Current page (" + (current + 1) + ")", "Selected pages (" + selection.Count + ")", "Page range" });
            scope.SelectedIndex = 0;
            LabelAt("Range", 16, 58, 100);
            range = new TextBox { Left = 128, Top = 54, Width = 310, Text = "1-" + count, Enabled = false, AccessibleName = "Page range" };
            LabelAt("Example: 1, 3-5, 8", 128, 82, 310);
            LabelAt("Format", 16, 120, 100);
            format = new ComboBox { Left = 128, Top = 116, Width = 310, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Export format" };
            format.Items.AddRange(new object[] { "PNG image", "JPEG image", "Plain text (UTF-8)" }); format.SelectedIndex = 0;
            resolutionLabel = LabelAt("Resolution", 16, 158, 100);
            resolution = new NumericUpDown { Left = 128, Top = 154, Width = 100, Minimum = 72, Maximum = 600, Value = 150, Increment = 25, AccessibleName = "Resolution in dpi" };
            var units = LabelAt("dpi (72–600)", 240, 158, 198);
            var hint = LabelAt("Images use the saved page rotation. Existing image files are kept; new exports receive unique names.", 16, 196, 422, 44);
            error = LabelAt("", 16, 240, 422, 28); error.ForeColor = Color.Firebrick;
            var export = ButtonAt("Export…", 254, 276, 88, Accept);
            var cancel = ButtonAt("Cancel", 350, 276, 88, () => Close()); cancel.DialogResult = DialogResult.Cancel;
            AcceptButton = export; CancelButton = cancel;
            Controls.AddRange(new Control[] { scope, range, format, resolution });
            scope.SelectedIndexChanged += delegate { range.Enabled = scope.SelectedIndex == 3; error.Text = ""; };
            format.SelectedIndexChanged += delegate
            {
                resolution.Enabled = resolutionLabel.Enabled = units.Enabled = Format != 2;
                hint.Text = Format == 2 ? "Exports existing PDF text in page order. Scanned images need OCR first; columns may not retain their reading order."
                    : "Images use the saved page rotation. Existing image files are kept; new exports receive unique names.";
            };
        }

        private void Accept()
        {
            try
            {
                Indices = scope.SelectedIndex == 0 ? Enumerable.Range(0, count).ToList() : scope.SelectedIndex == 1 ? new List<int> { current }
                    : scope.SelectedIndex == 2 ? selection.ToList() : PageRanges.Parse(range.Text, count);
                if (Indices.Count == 0) throw new ArgumentException("Select pages in the organizer, or choose a different page range.");
                DialogResult = DialogResult.OK; Close();
            }
            catch (ArgumentException ex) { error.Text = ex.Message; }
        }
    }

    internal sealed class ImagesDialog : FeatureDialog
    {
        private readonly ListBox list;
        private readonly List<string> files = new List<string>();
        private readonly ComboBox size;
        private readonly NumericUpDown margin;
        private readonly CheckBox landscape;
        private readonly Button accept;
        public List<string> Files { get { return files.ToList(); } }
        public ImageImportOptions Options { get { return new ImageImportOptions { Size = (ImagePageSize)size.SelectedIndex, Margin = (double)margin.Value * 72, Landscape = landscape.Checked }; } }

        public ImagesDialog(IEnumerable<string> initial, bool insert) : base(insert ? "Insert images as pages" : "Create PDF from images", 600, 468)
        {
            LabelAt("One image per page, in the order below. Add or drop PNG, JPEG, or BMP files.", 16, 14, 568, 34);
            list = new ListBox { Left = 16, Top = 50, Width = 448, Height = 224, IntegralHeight = false,
                SelectionMode = SelectionMode.MultiExtended, HorizontalScrollbar = true, AllowDrop = true, AccessibleName = "Images in page order" };
            Controls.Add(list);
            ButtonAt("Add…", 476, 50, 108, () =>
            {
                using (var dialog = new OpenFileDialog { Filter = ImageImport.Filter, Multiselect = true, Title = "Add images" })
                    if (dialog.ShowDialog(this) == DialogResult.OK) AddFiles(dialog.FileNames);
            });
            ButtonAt("Remove", 476, 88, 108, RemoveSelected);
            ButtonAt("Move up", 476, 136, 108, () => MoveImages(-1));
            ButtonAt("Move down", 476, 174, 108, () => MoveImages(1));
            list.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Delete) { RemoveSelected(); e.SuppressKeyPress = true; }
                if (e.Alt && (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)) { MoveImages(e.KeyCode == Keys.Up ? -1 : 1); e.SuppressKeyPress = true; }
            };
            list.DragEnter += (s, e) =>
            {
                var dropped = e.Data.GetData(DataFormats.FileDrop) as string[];
                e.Effect = dropped != null && dropped.Any(ImageImport.Supports) ? DragDropEffects.Copy : DragDropEffects.None;
            };
            list.DragDrop += (s, e) => { var dropped = e.Data.GetData(DataFormats.FileDrop) as string[]; if (dropped != null) AddFiles(dropped); };
            LabelAt("Page size", 16, 294, 96);
            size = new ComboBox { Left = 116, Top = 290, Width = 230, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Page size" };
            size.Items.AddRange(new object[] { "Image size (96 dpi)", "Letter (8.5 × 11 in)", "A4 (210 × 297 mm)" }); size.SelectedIndex = 1;
            landscape = new CheckBox { Left = 366, Top = 292, Width = 150, Text = "Landscape" };
            LabelAt("Margins", 16, 334, 96);
            margin = new NumericUpDown { Left = 116, Top = 330, Width = 100, Minimum = 0, Maximum = 2, DecimalPlaces = 2, Increment = 0.05m, Value = 0.25m, AccessibleName = "Margins in inches" };
            LabelAt("inches", 226, 334, 100);
            LabelAt("Images fit inside the margins without cropping. Transparent areas appear white. You can reorder and rotate pages in the organizer before saving.", 16, 370, 568, 42);
            accept = ButtonAt(insert ? "Insert pages" : "Create PDF", 376, 424, 100, () => { DialogResult = DialogResult.OK; Close(); });
            var cancel = ButtonAt("Cancel", 484, 424, 100, () => Close()); cancel.DialogResult = DialogResult.Cancel;
            AcceptButton = accept; CancelButton = cancel;
            Controls.AddRange(new Control[] { size, margin, landscape });
            size.SelectedIndexChanged += delegate { landscape.Enabled = size.SelectedIndex != 0; };
            AddFiles(initial ?? Enumerable.Empty<string>());
        }

        private void AddFiles(IEnumerable<string> paths) { files.AddRange(paths.Where(ImageImport.Supports)); RefreshFiles(); }
        private void RefreshFiles()
        {
            list.BeginUpdate(); list.Items.Clear();
            foreach (var path in files) list.Items.Add(Path.GetFileName(path));
            list.EndUpdate(); accept.Enabled = files.Count > 0;
        }
        private void RemoveSelected()
        {
            foreach (int i in list.SelectedIndices.Cast<int>().OrderByDescending(i => i)) files.RemoveAt(i);
            RefreshFiles();
        }
        private void MoveImages(int direction)
        {
            var selected = list.SelectedIndices.Cast<int>().OrderBy(i => i).ToList();
            if (selected.Count == 0 || (direction < 0 && selected[0] == 0) || (direction > 0 && selected.Last() == files.Count - 1)) return;
            if (direction > 0) selected.Reverse();
            foreach (int i in selected) { string item = files[i]; files[i] = files[i + direction]; files[i + direction] = item; }
            RefreshFiles(); foreach (int i in selected) list.SetSelected(i + direction, true);
        }
    }
}
