using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Gravity
{
    internal static class Dialogs
    {
        private static GravityDialog MakeForm(string title, int w, int h)
        {
            return new GravityDialog(title, w, h);
        }

        private static DialogResult ShowScaled(Form f, IWin32Window owner)
        {
            f.PerformAutoScale();
            return f.ShowDialog(owner);
        }

        /// <summary>Asks for a short text value. Returns null if cancelled.</summary>
        public static string Input(IWin32Window owner, string title, string prompt, string initial, bool password = false)
        {
            using (var f = MakeForm(title, 420, 176))
            {
                var label = new Label { Text = prompt, Left = 24, Top = 18, Width = 372, Height = 40 };
                var box = new TextBox { Left = 24, Top = 62, Width = 372, Text = initial ?? "", UseSystemPasswordChar = password, AccessibleName = prompt, TabIndex = 0 };
                var ok = new DialogButton { Text = "OK", DialogResult = DialogResult.OK, TabIndex = 0 };
                var cancel = new DialogButton { Text = "Cancel", DialogResult = DialogResult.Cancel, TabIndex = 1 };
                var footer = new DialogFooter { TabIndex = 1 };
                footer.Controls.AddRange(new Control[] { cancel, ok });
                f.Content.Controls.AddRange(new Control[] { label, box, footer });
                f.AcceptButton = ok;
                f.CancelButton = cancel;
                f.Shown += delegate { box.SelectAll(); box.Focus(); };
                return ShowScaled(f, owner) == DialogResult.OK ? box.Text : null;
            }
        }

        public static string Password(IWin32Window owner, string fileName, bool wasWrong)
        {
            string prompt = (wasWrong ? "That password didn't work. " : "") + "“" + fileName + "” is password protected. Enter its password:";
            return Input(owner, "Password required", prompt, "", true);
        }

        /// <summary>Lets the user order a list of PDF files before combining them.</summary>
        public static List<string> Combine(IWin32Window owner, IEnumerable<string> initial)
        {
            using (var f = MakeForm("Combine PDFs", 580, 430))
            {
                var hint = new Label { Text = "Files are combined from top to bottom. Drag files in, or use the buttons to reorder.", Left = 24, Top = 18, Width = 532, Height = 40, ForeColor = Theme.SubtleText };
                var list = new ListBox { Left = 24, Top = 66, Width = 412, Height = 272, IntegralHeight = false, SelectionMode = SelectionMode.MultiExtended, AllowDrop = true, HorizontalScrollbar = true, AccessibleName = "PDFs in page order", TabIndex = 0 };
                var add = new DialogButton { Text = "Add…", Left = 448, Top = 66, Width = 108, TabIndex = 1 };
                var remove = new DialogButton { Text = "Remove", Left = 448, Top = 110, Width = 108, TabIndex = 2 };
                var up = new DialogButton { Text = "Move up", Left = 448, Top = 166, Width = 108, TabIndex = 3 };
                var down = new DialogButton { Text = "Move down", Left = 448, Top = 210, Width = 108, TabIndex = 4 };
                var sort = new DialogButton { Text = "Sort by name", Left = 448, Top = 266, Width = 108, TabIndex = 5 };
                var ok = new DialogButton { Text = "Combine", DialogResult = DialogResult.OK, TabIndex = 0 };
                var cancel = new DialogButton { Text = "Cancel", DialogResult = DialogResult.Cancel, TabIndex = 1 };
                var footer = new DialogFooter { TabIndex = 6 };
                footer.Controls.AddRange(new Control[] { cancel, ok });
                f.Content.Controls.AddRange(new Control[] { hint, list, add, remove, up, down, sort, footer });
                f.AcceptButton = ok;
                f.CancelButton = cancel;

                var paths = new List<string>(initial ?? Enumerable.Empty<string>());
                Action refresh = null;
                refresh = delegate
                {
                    list.BeginUpdate();
                    list.Items.Clear();
                    foreach (var p in paths) list.Items.Add(Path.GetFileName(p) + "    —  " + Path.GetDirectoryName(p));
                    list.EndUpdate();
                    ok.Enabled = paths.Count > 0;
                };
                Action<IEnumerable<string>> addFiles = delegate (IEnumerable<string> files)
                {
                    foreach (var p in files)
                        if (string.Equals(Path.GetExtension(p), ".pdf", StringComparison.OrdinalIgnoreCase)) paths.Add(p);
                    refresh();
                };
                Action<int> move = delegate (int dir)
                {
                    var sel = list.SelectedIndices.Cast<int>().OrderBy(i => i).ToList();
                    if (sel.Count == 0) return;
                    if (dir < 0 && sel[0] == 0) return;
                    if (dir > 0 && sel[sel.Count - 1] == paths.Count - 1) return;
                    if (dir > 0) sel.Reverse();
                    foreach (int i in sel)
                    {
                        var t = paths[i];
                        paths[i] = paths[i + dir];
                        paths[i + dir] = t;
                    }
                    refresh();
                    foreach (int i in sel) list.SetSelected(i + dir, true);
                };
                add.Click += delegate
                {
                    using (var dlg = new OpenFileDialog { Filter = "PDF files (*.pdf)|*.pdf", Multiselect = true, Title = "Add PDFs" })
                        if (dlg.ShowDialog(f) == DialogResult.OK) addFiles(dlg.FileNames);
                };
                remove.Click += delegate
                {
                    foreach (int i in list.SelectedIndices.Cast<int>().OrderByDescending(i => i)) paths.RemoveAt(i);
                    refresh();
                };
                up.Click += delegate { move(-1); };
                down.Click += delegate { move(1); };
                sort.Click += delegate
                {
                    paths = paths.OrderBy(p => Path.GetFileName(p), StringComparer.CurrentCultureIgnoreCase).ToList();
                    refresh();
                };
                list.KeyDown += delegate (object s, KeyEventArgs e)
                {
                    if (e.KeyCode == Keys.Delete) { remove.PerformClick(); e.Handled = true; }
                    else if (e.Alt && e.KeyCode == Keys.Up) { move(-1); e.Handled = true; }
                    else if (e.Alt && e.KeyCode == Keys.Down) { move(1); e.Handled = true; }
                };
                list.DragEnter += delegate (object s, DragEventArgs e)
                {
                    e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
                };
                list.DragDrop += delegate (object s, DragEventArgs e)
                {
                    var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                    if (files != null) addFiles(files);
                };
                refresh();
                f.Shown += delegate { if (paths.Count == 0) add.PerformClick(); };
                return ShowScaled(f, owner) == DialogResult.OK && paths.Count > 0 ? paths : null;
            }
        }

        public const string ShortcutText =
@"READING
  Command palette             Ctrl+Shift+P
  Next / previous page        N, P, Page Down, Page Up
  Scroll                      Arrow keys, J, K, H, L
  Scroll one screen           Space, Shift+Space
  First / last page           Home, End
  Go to page                  Ctrl+G, G
  Back / forward              Alt+Left, Alt+Right
  Find                        Ctrl+F, /      (F3, Shift+F3 for next/previous)
  Copy selected text          Ctrl+C         (drag across text to select)
  Select all text             Ctrl+A
  Fill form fields            Ctrl+Shift+F   (or click a field)
  Edit page text (Windows)     Ctrl+Shift+T

ZOOM AND VIEW
  Zoom in / out               +, -, Ctrl+mouse wheel
  Fit page / actual size      Ctrl+0, Ctrl+1
  Fit width                   Ctrl+2
  Toggle fit page / width     Z
  Single / facing / book      Ctrl+6, Ctrl+7, Ctrl+8
  Toggle continuous           C
  Rotate view                 [ and ]
  Bookmarks sidebar           F12
  Full screen                 F11
  Presentation                F5, Ctrl+L     (click = next, right-click = previous)

PAGES  (selection in the Pages panel / Organize, or the current page)
  Show / hide Pages panel     Ctrl+Shift+E
  Organize pages              Ctrl+E
  Cut / copy / paste pages    Ctrl+X, Ctrl+C, Ctrl+V   (Ctrl+Shift+V pastes before)
  Delete pages                Delete
  Duplicate pages             Ctrl+D
  Rotate pages                Ctrl+[, Ctrl+]   ([ and ] with thumbnails focused)
  Move pages earlier / later  Alt+Up, Alt+Down (thumbnails focused)
  Select all pages            Ctrl+A (thumbnails focused)
  Thumbnail size              Ctrl+mouse wheel, +, - (thumbnails focused)
  Undo / redo                 Ctrl+Z, Ctrl+Y

FILES AND TABS
  Open                        Ctrl+O
  Save / save as              Ctrl+S, Ctrl+Shift+S
  Print                       Ctrl+P
  Close tab                   Ctrl+W
  Next / previous tab         Ctrl+Tab, Ctrl+Shift+Tab
  Go to tab 1-9               Alt+1 ... Alt+9
  Reload from disk            R";

        public static void Shortcuts(IWin32Window owner)
        {
            using (var f = MakeForm("Keyboard shortcuts", 620, 560))
            {
                f.FormBorderStyle = FormBorderStyle.Sizable;
                f.MinimumSize = new Size(400, 300);
                var box = new TextBox
                {
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Vertical,
                    Dock = DockStyle.Fill,
                    Font = new Font("Consolas", 10f),
                    Text = ShortcutText,
                    BackColor = Color.White,
                    BorderStyle = BorderStyle.None,
                };
                var pad = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), BackColor = Color.White };
                pad.Controls.Add(box);
                f.Content.Controls.Add(pad);
                f.Shown += delegate { box.SelectionLength = 0; f.ActiveControl = null; };
                f.KeyPreview = true;
                f.KeyDown += delegate (object s, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) f.Close(); };
                ShowScaled(f, owner);
            }
        }
    }
}
