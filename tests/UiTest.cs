using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Gravity.Tests
{
    /// <summary>
    /// Drives the real main window, placed off-screen, with synthetic Windows mouse and keyboard
    /// messages: link clicks, text selection, drag-reordering, cut/paste between tabs, saving,
    /// reload-on-change, presentation mode. Settings are ephemeral (nothing is written to %APPDATA%).
    /// </summary>
    internal static class UiTest
    {
        private const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private const int WM_MOUSEMOVE = 0x200, WM_LBUTTONDOWN = 0x201, WM_LBUTTONUP = 0x202, MK_LBUTTON = 1;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

        private static MainForm form;

        private static void Check(bool ok, string what) { SelfTest.Check(ok, what); }

        private static T Field<T>(object o, string name)
        {
            for (var t = o.GetType(); t != null; t = t.BaseType)
            {
                var f = t.GetField(name, F);
                if (f != null) return (T)f.GetValue(o);
            }
            throw new MissingFieldException(o.GetType().Name, name);
        }

        private static object Call(object o, string name, params object[] args)
        {
            return o.GetType().GetMethod(name, F).Invoke(o, args);
        }

        private static bool Key(Keys k)
        {
            // Press keys from the document, as a user would; the off-screen window's focus
            // can otherwise rest in a toolbar box, where letter keys are (correctly) typed.
            if ((bool)form.GetType().GetProperty("TextFocused", F).GetValue(form) || Field<TreeView>(form, "toc").Focused)
                Call(form, "FocusCanvas");
            bool handled = (bool)Call(form, "HandleKey", k);
            Pump(30);
            return handled;
        }

        private static void Pump(int ms)
        {
            var sw = Stopwatch.StartNew();
            do
            {
                Application.DoEvents();
                Thread.Sleep(5);
            } while (sw.ElapsedMilliseconds < ms);
        }

        private static bool PumpUntil(Func<bool> cond, int ms)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < ms)
            {
                Application.DoEvents();
                if (cond()) return true;
                Thread.Sleep(10);
            }
            return cond();
        }

        private static IntPtr LParam(Point p) { return (IntPtr)(((p.Y & 0xFFFF) << 16) | (p.X & 0xFFFF)); }

        private static void Click(Control c, Point p, int keyFlags = 0)
        {
            SendMessage(c.Handle, WM_MOUSEMOVE, (IntPtr)keyFlags, LParam(p));
            SendMessage(c.Handle, WM_LBUTTONDOWN, (IntPtr)(MK_LBUTTON | keyFlags), LParam(p));
            SendMessage(c.Handle, WM_LBUTTONUP, (IntPtr)keyFlags, LParam(p));
            Pump(30);
        }

        private const int MK_SHIFT = 0x4, MK_CONTROL = 0x8;

        private static void Drag(Control c, Point from, Point to)
        {
            SendMessage(c.Handle, WM_MOUSEMOVE, IntPtr.Zero, LParam(from));
            SendMessage(c.Handle, WM_LBUTTONDOWN, (IntPtr)MK_LBUTTON, LParam(from));
            for (int i = 1; i <= 10; i++)
            {
                var p = new Point(from.X + (to.X - from.X) * i / 10, from.Y + (to.Y - from.Y) * i / 10);
                SendMessage(c.Handle, WM_MOUSEMOVE, (IntPtr)MK_LBUTTON, LParam(p));
            }
            SendMessage(c.Handle, WM_LBUTTONUP, IntPtr.Zero, LParam(to));
            Pump(30);
        }

        /// <summary>A PDF point on a 612x792 page of the reading view, in view client coordinates.</summary>
        private static Point PdfToClient(DocView v, int page, double px, double py)
        {
            var r = Field<Rectangle[]>(v, "rects")[page];
            int sx = Field<int>(v, "ScrollX"), sy = Field<int>(v, "ScrollY");
            return new Point((int)(r.X - sx + px / 612 * r.Width), (int)(r.Y - sy + (792 - py) / 792 * r.Height));
        }

        private static Point CellCenter(OrganizerView o, int i)
        {
            var c = (Rectangle)Call(o, "CellRect", i);
            return new Point(c.X + c.Width / 2, c.Y + c.Height / 2 - Field<int>(o, "ScrollY"));
        }

        private static string Texts(EditDoc d)
        {
            return string.Join(" ", d.Pages.Select(p => p.Src == null ? "_" : SelfTest.PageText(p.Src, p.Index)));
        }

        public static void Run(string dir)
        {
            // A watchdog so a stray modal dialog can never hang the run.
            var watchdog = new Thread(delegate ()
            {
                Thread.Sleep(90000);
                Console.WriteLine("   FAIL  UI test timed out (a dialog may have opened)");
                Environment.Exit(2);
            }) { IsBackground = true };
            watchdog.Start();

            string a = Path.Combine(dir, "ui-A.pdf"), b = Path.Combine(dir, "ui-B.pdf"), o = Path.Combine(dir, "outline.pdf");
            File.Copy(Path.Combine(dir, "A.pdf"), a, true);
            File.Copy(Path.Combine(dir, "B.pdf"), b, true);

            form = new MainForm(new[] { a, b, o }, true);
            form.StartPosition = FormStartPosition.Manual;
            form.WindowState = FormWindowState.Normal;
            form.ShowInTaskbar = false;
            form.Bounds = new Rectangle(-4000, -4000, 1200, 860);
            form.Show();
            Pump(600);

            var tabs = Field<List<DocTab>>(form, "tabs");
            var view = Field<DocView>(form, "view");
            var org = Field<OrganizerView>(form, "organizer");
            Func<DocTab> active = () => Field<DocTab>(form, "active");

            Check(tabs.Count == 3, "three files from the command line open as tabs");
            using (var appIcon = Brand.AppIcon())
                Check(appIcon != null && appIcon.Width > 0, "embedded app icon loads");
            Check(Brand.Lockup != null && Brand.Lockup.Width > Brand.Lockup.Height * 3, "embedded logo lockup loads");
            Check(active() == tabs[2] && view.Visible && view.PageCount == 2, "last file is active in the reading view");

            // ---- links and history
            Click(view, PdfToClient(view, 0, 100, 710));
            Check(view.CurrentPage == 1, "clicking an internal link jumps to its page");
            Key(Keys.Alt | Keys.Left);
            Check(view.CurrentPage == 0, "Alt+Left goes back");
            Key(Keys.Alt | Keys.Right);
            Check(view.CurrentPage == 1, "Alt+Right goes forward");

            // ---- text selection by dragging (page 2 text "Page two target" at y=400)
            Key(Keys.Control | Keys.D0);
            Key(Keys.End);
            Drag(view, PdfToClient(view, 1, 74, 407), PdfToClient(view, 1, 260, 407));
            string sel = view.GetSelectedText();
            Check(view.HasSelection && sel.Contains("Page two"), "dragging over text selects it (\"" + sel + "\")");
            Key(Keys.Control | Keys.A);
            sel = view.GetSelectedText();
            Check(sel.Contains("Go to page two") && sel.Contains("Page two target"), "Ctrl+A selects all text");
            Key(Keys.Escape);
            Check(!view.HasSelection, "Esc clears the selection");

            // ---- zoom, layout, rotation keys
            Check(view.Mode == ZoomMode.FitPage, "Ctrl+0 fits the page");
            double z0 = view.Zoom;
            Key(Keys.Oemplus);
            Check(view.Mode == ZoomMode.Custom && view.Zoom > z0, "+ zooms in");
            Key(Keys.OemMinus);
            Key(Keys.OemMinus);
            Check(view.Zoom < z0 * 1.01, "- zooms out");
            Key(Keys.Control | Keys.D2);
            Check(view.Mode == ZoomMode.FitWidth, "Ctrl+2 fits the width");
            Key(Keys.C);
            Check(!view.Continuous, "C toggles continuous off");
            Key(Keys.C);
            Check(view.Continuous, "C toggles continuous on");
            Key(Keys.Control | Keys.D7);
            Check(view.LayoutMode == PageLayout.Facing, "Ctrl+7 facing pages");
            Key(Keys.Control | Keys.D6);
            Key(Keys.OemCloseBrackets);
            Check(view.ViewRotation == 1, "] rotates the view");
            Key(Keys.OemOpenBrackets);
            Check(view.ViewRotation == 0 && !tabs[2].Doc.IsModified, "view rotation does not modify the file");
            Key(Keys.Control | Keys.D0);

            // ---- search
            Field<ToolStripTextBox>(form, "findBox").Text = "target";
            Key(Keys.Home);
            // Regression: results must be applied on the UI thread even when the ambient
            // SynchronizationContext is not the WinForms one (WinForms can uninstall it).
            var uiContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
            Call(form, "FindNext", true);
            Thread.Sleep(600);
            Check(Field<int>(view, "hitPage") == -1, "search result waits for the UI thread (not applied from a worker)");
            SynchronizationContext.SetSynchronizationContext(uiContext);
            Check(PumpUntil(() => Field<int>(view, "hitPage") == 1, 3000), "find locates text on page 2");

            // ---- tab A: keyboard navigation
            Key(Keys.Alt | Keys.D1);
            Check(active() == tabs[0] && view.PageCount == 5, "Alt+1 switches to the first tab");
            Pump(50);
            Key(Keys.End);
            Check(view.CurrentPage == 4, "End goes to the last page");
            Key(Keys.Home);
            Check(view.CurrentPage == 0, "Home goes to the first page");
            Key(Keys.PageDown);
            Check(view.CurrentPage == 1, "Page Down goes to the next page");

            // ---- organize: drag-reorder
            Key(Keys.Control | Keys.E);
            Check(org.Visible && !view.Visible, "Ctrl+E opens the page organizer");
            var c2 = (Rectangle)Call(org, "CellRect", 2);
            Drag(org, CellCenter(org, 0), new Point(c2.Right - 4, c2.Y + c2.Height / 2));
            Eq("A2 A3 A1 A4 A5", Texts(tabs[0].Doc), "dragging page 1 after page 3 reorders");
            Check(tabs[0].Doc.IsModified, "reordering marks the document modified");

            // ---- modifier keys come from the mouse message, not the live keyboard state
            Click(org, CellCenter(org, 0));
            Click(org, CellCenter(org, 2), MK_SHIFT);
            Check(org.SelectedIndices().SequenceEqual(new[] { 0, 1, 2 }), "Shift+click selects a range");
            Click(org, CellCenter(org, 4), MK_CONTROL);
            Check(org.SelectedIndices().SequenceEqual(new[] { 0, 1, 2, 4 }), "Ctrl+click adds a page to the selection");
            Click(org, CellCenter(org, 1));
            Check(org.SelectedIndices().SequenceEqual(new[] { 1 }), "a plain click selects one page");

            // ---- cut two pages, paste into another tab
            Click(org, CellCenter(org, 3));
            Key(Keys.Shift | Keys.Right);
            Check(org.SelectedIndices().SequenceEqual(new[] { 3, 4 }), "click + Shift+Right selects two pages");
            Key(Keys.Control | Keys.X);
            Eq("A2 A3 A1", Texts(tabs[0].Doc), "Ctrl+X cuts the selected pages");
            Key(Keys.Control | Keys.Tab);
            Check(active() == tabs[1] && view.Visible, "Ctrl+Tab switches to the next tab (reading view)");
            Key(Keys.Control | Keys.V);
            Eq("B1 A4 A5 B2 B3", Texts(tabs[1].Doc), "Ctrl+V pastes after the current page in another document");
            Key(Keys.Control | Keys.OemCloseBrackets);
            Check(tabs[1].Doc.Pages[0].Rotation == 1, "Ctrl+] rotates the current page");

            // ---- save in place
            Key(Keys.Control | Keys.S);
            Check(!tabs[1].Doc.IsModified, "Ctrl+S saves");
            Eq("B1 A4 A5 B2 B3", SelfTest.FileTexts(b), "saved file has the pasted pages");
            var saved = PdfSource.Open(b, null);
            int rot;
            lock (Pdfium.Lock) rot = Pdfium.FPDFPage_GetRotation(saved.GetPage(0));
            saved.Dispose();
            Check(rot == 1, "saved file keeps the rotation");
            Pump(1200);
            Check(!tabs[1].Doc.IsModified && tabs[1].Doc.Pages.Count == 5, "our own save does not trigger a reload");

            // ---- undo back to the original
            Key(Keys.Alt | Keys.D1);
            Check(org.Visible, "organizer mode is remembered per tab");
            Key(Keys.Control | Keys.Z);
            Eq("A2 A3 A1 A4 A5", Texts(tabs[0].Doc), "Ctrl+Z undoes the cut");
            Key(Keys.Control | Keys.Z);
            Eq("A1 A2 A3 A4 A5", Texts(tabs[0].Doc), "Ctrl+Z undoes the move");
            Check(!tabs[0].Doc.IsModified, "fully undone document is clean");
            Key(Keys.Control | Keys.Y);
            Key(Keys.Control | Keys.Z);
            Check(!tabs[0].Doc.IsModified, "redo then undo");

            // ---- reload when the file changes on disk
            File.Copy(Path.Combine(dir, "reordered.pdf"), a, true);
            Check(PumpUntil(() => Texts(tabs[0].Doc) == "A2 A4 A5 A3 A1", 4000), "an unmodified tab reloads when its file changes");

            // ---- more organizer commands
            Key(Keys.Control | Keys.A);
            Key(Keys.Delete);
            Check(tabs[0].Doc.Pages.Count == 5, "deleting every page is refused");
            Click(org, CellCenter(org, 0));
            Key(Keys.Alt | Keys.Right);
            Eq("A4 A2 A5 A3 A1", Texts(tabs[0].Doc), "Alt+Right moves the selected page later");
            Key(Keys.Control | Keys.Z);
            Key(Keys.Control | Keys.D);
            Check(tabs[0].Doc.Pages.Count == 6 && ReferenceEquals(tabs[0].Doc.Pages[0].Src, tabs[0].Doc.Pages[1].Src) && tabs[0].Doc.Pages[1].Index == tabs[0].Doc.Pages[0].Index, "Ctrl+D duplicates");
            Key(Keys.Control | Keys.Z);
            Click(org, CellCenter(org, 0));
            Key(Keys.Delete);
            Eq("A4 A5 A3 A1", Texts(tabs[0].Doc), "Delete removes the selected page");
            Key(Keys.Control | Keys.Z);
            Call(form, "InsertBlank");
            Check(tabs[0].Doc.Pages.Count == 6 && tabs[0].Doc.Pages.Count(p => p.Src == null) == 1, "insert blank page");
            Key(Keys.Control | Keys.Z);
            Call(form, "InsertFiles", new[] { b }, 0);
            Eq("B1 A4 A5 B2 B3 A2 A4 A5 A3 A1", Texts(tabs[0].Doc), "inserting a file puts its pages at the drop point");
            Key(Keys.Control | Keys.Z);
            Key(Keys.OemCloseBrackets);
            Check(org.SelectedIndices().All(i => tabs[0].Doc.Pages[i].Rotation == 1), "] rotates the selected pages in the organizer");
            Key(Keys.Control | Keys.Z);
            Check(!tabs[0].Doc.IsModified, "back to clean after undoing everything");

            // ---- extract to a new tab
            Click(org, CellCenter(org, 2));
            Call(form, "ExtractPages");
            Check(tabs.Count == 4 && active() == tabs[3] && tabs[3].Doc.Pages.Count == 1 && tabs[3].Doc.FilePath == null, "extract opens the page in a new untitled tab");
            tabs[3].Doc.SaveAs(Path.Combine(dir, "ui-extract.pdf"));
            Eq("A5", SelfTest.FileTexts(Path.Combine(dir, "ui-extract.pdf")), "extracted page saved");
            Key(Keys.Control | Keys.W);
            Check(tabs.Count == 3, "Ctrl+W closes a saved tab without asking");

            // ---- presentation and full screen
            Key(Keys.Alt | Keys.D3);
            var border = form.FormBorderStyle;
            Key(Keys.F5);
            Check(view.Presentation && form.FormBorderStyle == FormBorderStyle.None, "F5 starts presentation mode");
            Key(Keys.Home);
            Key(Keys.Right);
            Check(view.CurrentPage == 1, "arrow keys page through the presentation");
            Key(Keys.Escape);
            Check(!view.Presentation && form.FormBorderStyle == border, "Esc leaves presentation mode");
            Key(Keys.F11);
            Check(Field<bool>(form, "fullscreen"), "F11 enters full screen");
            Key(Keys.Escape);
            Check(!Field<bool>(form, "fullscreen") && form.FormBorderStyle == border, "Esc leaves full screen");

            // ---- close
            Check(tabs.All(t => !t.Doc.IsModified), "all tabs clean before closing");
            form.Close();
            Pump(200);
            Check(form.IsDisposed, "window closes without prompting");
        }

        private static void Eq(string expected, string actual, string what)
        {
            Check(expected == actual, what + (expected == actual ? "" : " (expected [" + expected + "], got [" + actual + "])"));
        }
    }
}
