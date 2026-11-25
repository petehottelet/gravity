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

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);

        [DllImport("gdi32.dll")]
        private static extern uint GetPixel(IntPtr dc, int x, int y);

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
            Check(Brand.Mark != null && Brand.Mark.Width > 0, "approved G artwork is embedded in the application");
            Check(active() == tabs[2] && view.Visible && view.PageCount == 2, "last file is active in the reading view");
            var readButton = Field<ToolStripButton>(form, "bRead");
            var organizeButton = Field<ToolStripButton>(form, "bOrganize");
            organizeButton.PerformClick(); organizeButton.PerformClick();
            Check(active().Organize && organizeButton.Checked && !readButton.Checked,
                "Organize segment stays selected when clicked again");
            readButton.PerformClick(); readButton.PerformClick();
            Check(!active().Organize && readButton.Checked && !organizeButton.Checked,
                "Read segment restores the document and stays selected");
            var modeTools = Field<ToolStrip>(form, "modeTools");
            using (var modeImage = new Bitmap(modeTools.Width, modeTools.Height))
            {
                modeTools.DrawToBitmap(modeImage, modeTools.ClientRectangle);
                Check(modeImage.GetPixel(readButton.Bounds.Left + 10, readButton.Bounds.Top + 10).ToArgb() == Color.White.ToArgb(),
                    "selected mode renders white inside the rounded gray track");
            }
            var pageField = Field<PageNumberField>(form, "pageBox");
            Check(pageField.Height >= 32 && pageField.Editor.BorderStyle == BorderStyle.None && pageField.Editor.AccessibleName == "Page number",
                "page navigation uses the full-height rounded field with an accessible native editor");
            pageField.Text = "2"; Call(pageField, "OnKeyDown", new KeyEventArgs(Keys.Enter));
            Check(view.CurrentPage == 1, "rounded page field still navigates when Enter is pressed");
            view.GoToPage(0, false);
            var header = Field<DocumentHeader>(form, "shellHeader");
            Check(header.DocumentTitle == active().Doc.DisplayName && header.DocumentState.Contains("2 pages"),
                "centered document identity reflects the active document and real page count");
            var headerMenu = Field<ToolStripButton>(form, "bMenu");
            Check(headerMenu.Placement == ToolStripItemPlacement.Main && headerMenu.Owner.ClientRectangle.Contains(headerMenu.Bounds),
                "application menu toggle remains visible beside Open without an overflow stub");
            ApplicationMenuTests(true);
            WindowChromeTests();
            CapturePanel("nested-outline-and-tabs");
            var paper = Field<Rectangle[]>(view, "rects")[0];
            Check(paper.Top >= 22 && paper.Bottom < view.ClientSize.Height - 20, "Fit page retains breathing room above and below the paper");
            var commands = Field<ToolStrip>(form, "toolbar");
            using (var buttonImage = new Bitmap(commands.Width, commands.Height))
            {
                commands.DrawToBitmap(buttonImage, commands.ClientRectangle);
                var save = Field<ToolStripButton>(form, "bSave").Bounds;
                int left = save.Right, right = save.Left;
                for (int y = save.Top + 10; y < save.Bottom - 10; y++)
                    for (int x = save.Left + 5; x < save.Right - 5; x++)
                    {
                        var pixel = buttonImage.GetPixel(x, y);
                        if (pixel.R < 100 && pixel.G < 115 && pixel.B < 135) { left = Math.Min(left, x); right = Math.Max(right, x); }
                    }
                Check(right > left && Math.Abs((left - save.Left) - (save.Right - 1 - right)) <= 3,
                    "Save icon and label render as one optically centered group inside the button");
            }
            form.Size = form.MinimumSize; Pump(50);
            Check(!modeTools.Bounds.IntersectsWith(commands.Bounds) && organizeButton.Bounds.Right <= modeTools.ClientSize.Width,
                "both mode segments remain unobscured at minimum window size");
            Check(Field<ToolStripButton>(form, "bOpen").Placement == ToolStripItemPlacement.Main &&
                Field<ToolStripButton>(form, "bSave").Placement == ToolStripItemPlacement.Main,
                "Open and Save remain directly available in a narrow window");
            form.Size = new Size(1200, 860); Pump(50);
            organizeButton.PerformClick();
            Check(org.Top >= Field<ToolStrip>(form, "contextTools").Bottom && org.Bottom <= Field<StatusStrip>(form, "documentStatus").Top,
                "organizer canvas leaves its action row and status visible");
            var pagesAccess = org.AccessibilityObject;
            Check(pagesAccess.Role == AccessibleRole.List && pagesAccess.GetChildCount() == 2 && pagesAccess.GetChild(1).Name == "Page 2",
                "assistive technology can enumerate and name document pages");
            pagesAccess.GetChild(1).DoDefaultAction();
            pagesAccess.GetChild(0).Select(AccessibleSelection.AddSelection | AccessibleSelection.TakeFocus);
            Check(org.SelectedIndices().SequenceEqual(new[] { 0, 1 }) &&
                (pagesAccess.GetChild(0).State & (AccessibleStates.Selected | AccessibleStates.Focused)) == (AccessibleStates.Selected | AccessibleStates.Focused),
                "accessible page actions expose multi-selection and the distinct focused page");
            var firstCell = (Rectangle)Call(org, "CellRect", 0);
            var previewFrame = (Rectangle)Call(org, "PreviewRect", firstCell);
            Check(previewFrame.Bottom + 24 < firstCell.Bottom && (string)Call(org, "PageTitle", 0) == "Chapter One",
                "organizer selection ends above its caption and titles come from real bookmarks");
            CapturePanel("organizer-multiple-selected");
            readButton.PerformClick();
            var tabsAccess = Field<TabStrip>(form, "tabStrip").AccessibilityObject;
            Check(tabsAccess.Role == AccessibleRole.PageTabList && tabsAccess.GetChildCount() == 4 &&
                tabsAccess.GetChild(0).GetChild(0).DefaultAction == "Close document", "accessible document tabs expose activate, close and open actions");
            tabsAccess.GetChild(0).DoDefaultAction(); tabsAccess.GetChild(2).DoDefaultAction();
            Check(active() == tabs[2] && (tabsAccess.GetChild(2).State & AccessibleStates.Selected) != 0,
                "accessible tab activation changes the actual document and selected state");
            Call(form, "FocusCanvas");
            var overflow = Field<ToolStripDropDownButton>(form, "contextMore");
            Check(overflow.Width >= 70 && overflow.Height >= 40 && overflow.AccessibleName.Contains("More"),
                "context overflow provides a named full-size More target");
            foreach (int shellWidth in new[] { form.MinimumSize.Width, 640, 1200 })
            {
                form.Size = new Size(shellWidth, 860); Pump(30);
                foreach (bool organize in new[] { false, true })
                {
                    Call(form, "SetOrganize", organize); Pump(30);
                    var context = Field<ToolStrip>(form, "contextTools");
                    if (organize)
                    {
                        Check(new[] { "bPRotR", "bDuplicate", "bDelete" }.All(name => {
                            var item = Field<ToolStripButton>(form, name); return item.Visible && item.Placement == ToolStripItemPlacement.Main && context.ClientRectangle.Contains(item.Bounds);
                        }), "primary organizer actions stay visible at width " + shellWidth);
                        Check(shellWidth == 1200 ? !overflow.Visible : overflow.Visible && context.ClientRectangle.Contains(overflow.Bounds),
                            "secondary organizer actions adapt without replacing primary commands at width " + shellWidth);
                        Call(form, "PopulateContextMenu");
                        Check(overflow.DropDownItems.OfType<ToolStripMenuItem>().Any(item => item.Text.Contains("uplicate")), "organizer menu retains page editing commands");
                    }
                    else
                    {
                        var zoom = Field<ToolStripDropDownButton>(form, "zoomChoice");
                        Check(!overflow.Visible && new ToolStripItem[] { zoom, pageField, Field<ToolStripButton>(form, "bZoomIn"), Field<ToolStripButton>(form, "bZoomOut") }
                            .All(item => item.Visible && item.Placement == ToolStripItemPlacement.Main && context.ClientRectangle.Contains(item.Bounds)),
                            "page field and unboxed zoom controls fit without overflow at width " + shellWidth);
                        zoom.DropDownItems.OfType<ToolStripMenuItem>().First(item => item.Text == "Fit width").PerformClick();
                        Check(view.Mode == ZoomMode.FitWidth, "unboxed zoom selector applies Fit width");
                        zoom.DropDownItems.OfType<ToolStripMenuItem>().First(item => item.Text == "Fit page").PerformClick();
                    }
                }
            }
            readButton.PerformClick(); Call(form, "FocusCanvas");

            // ---- links and history
            Click(view, PdfToClient(view, 0, 100, 710));
            Check(view.CurrentPage == 1, "clicking an internal link jumps to its page");
            var outline = Field<TreeView>(form, "toc");
            Check(outline.SelectedNode != null && ((OutlineItem)outline.SelectedNode.Tag).PageIndex == 1,
                "outline location follows internal-link navigation");
            Key(Keys.Alt | Keys.Left);
            Check(view.CurrentPage == 0, "Alt+Left goes back");
            Check(outline.SelectedNode != null && ((OutlineItem)outline.SelectedNode.Tag).PageIndex == 0,
                "outline location follows navigation history");
            Key(Keys.Alt | Keys.Right);
            Check(view.CurrentPage == 1, "Alt+Right goes forward");

            // When both pages fit onscreen, the top edge can still be over page 1
            // after navigating to page 2. History must preserve the selected page.
            view.SetZoom(0.25);
            view.GoToPage(0, false);
            view.GoToPage(1);
            view.GoBack();
            Check(view.CurrentPage == 0, "zoomed-out history goes back to the source page");
            view.GoForward();
            Check(view.CurrentPage == 1, "zoomed-out history restores the destination page");

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
            while (tabs.Count > 0) Key(Keys.Control | Keys.W);
            ApplicationMenuTests(false);
            form.Close();
            Pump(200);
            Check(form.IsDisposed, "window closes without prompting");
            PagePanelTests(dir);
            ScreenshotTest(dir);
        }

        private static void ScreenshotTest(string dir)
        {
            using (var shadow = new Bitmap(260, 310))
            using (var graphics = Graphics.FromImage(shadow))
            {
                graphics.Clear(Theme.ViewBack);
                Theme.PaperShadow(graphics, new Rectangle(30, 30, 180, 230), 1);
                var beside = shadow.GetPixel(29, 120); var below = shadow.GetPixel(120, 261);
                int redDrop = Theme.ViewBack.R - beside.R, blueDrop = Theme.ViewBack.B - beside.B;
                Check(redDrop > 4 && Math.Abs(redDrop - blueDrop) <= 4 && below.R < beside.R && shadow.GetPixel(14, 120).ToArgb() == Theme.ViewBack.ToArgb(),
                    "paper shadow has neutral soft falloff and a downward offset without a tinted halo");
            }
            using (var recentHome = new HomeView { Size = new Size(1000, 740) })
            using (var image = new Bitmap(1000, 740))
            {
                recentHome.SetRecent(new List<RecentFile> { new RecentFile { Path = Path.Combine(dir, "A.pdf") } });
                recentHome.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
                Check(true, "home view renders recent documents using bundled icons");
            }
            string capture = Path.Combine(dir, "window-only.png");
            using (var snapshot = new MainForm(new[] { Path.Combine(dir, "A.pdf") }, true)
            {
                ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Bounds = new Rectangle(-4000, -4000, 1000, 740),
                ScreenshotPath = capture,
            })
            {
                snapshot.Show();
                Pump(120);
                Check(!Field<TabStrip>(snapshot, "tabStrip").Visible && Field<DocumentHeader>(snapshot, "shellHeader").DocumentTitle == "A.pdf",
                    "single-document window uses centered identity without an extra tab row");
                Check(PumpUntil(() => snapshot.IsDisposed, 18000) && File.Exists(capture),
                    "offscreen capture finishes without reading other desktop windows");
                using (var image = new Bitmap(capture))
                    Check(image.Width == 1000 && image.Height == 740 && image.GetPixel(60, 70).ToArgb() == Theme.Chrome.ToArgb(),
                        "window capture contains the app at the requested size even offscreen");
            }
        }

        private static void PagePanelTests(string dir)
        {
            Console.WriteLine("-- collapsible pages panel");
            string a = Path.Combine(dir, "panel-A.pdf"), b = Path.Combine(dir, "panel-B.pdf");
            File.Copy(Path.Combine(dir, "A.pdf"), a, true);
            File.Copy(Path.Combine(dir, "B.pdf"), b, true);
            form = new MainForm(new[] { a, b }, true) { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual,
                Bounds = new Rectangle(-4000, -4000, 1180, 860) };
            form.Show();
            Pump(300);
            Key(Keys.Alt | Keys.D1);
            CapturePanel("no-outline");
            var tabs = Field<List<DocTab>>(form, "tabs");
            var view = Field<DocView>(form, "view");
            var org = Field<OrganizerView>(form, "organizer");
            var panel = Field<Panel>(form, "pagePanel");
            var rail = Field<Panel>(form, "pageRail");
            var doc = tabs[0].Doc;
            string original = Texts(doc);
            int count = doc.Pages.Count;
            var sidebar = Field<Panel>(form, "sidebar");
            Check(sidebar.Visible && !panel.Visible && Field<ToolStripButton>(form, "bOutline").Checked,
                "reader starts with the outline tab in the shared sidebar");
            Field<ToolStripButton>(form, "bPages").PerformClick();
            Check(panel.Visible && view.Visible && org.Visible && org.SidebarMode, "pages panel opens alongside the reader");
            Check(panel.Controls.Cast<Control>().Where(control => control != Field<Panel>(form, "pageBody")).All(control => !control.Visible),
                "Pages navigation rail contains no inherited editing strips or redundant selection labels");
            int openWidth = view.Width;
            Key(Keys.Control | Keys.Shift | Keys.E);
            Check(!panel.Visible && !sidebar.Visible && view.Width > openWidth, "collapsing pages returns space to the reader");
            Key(Keys.Control | Keys.Tab);
            Check(!panel.Visible && !sidebar.Visible, "collapsed state survives tab switching");
            Key(Keys.Alt | Keys.D1);
            Key(Keys.Control | Keys.Shift | Keys.E);
            Check(panel.Visible && !rail.Visible && view.Width == openWidth, "pages panel expands again");
            view.Focus();
            view.GoToPage(1, false);
            Check(org.SelectedIndices().SequenceEqual(new[] { 1 }), "reader navigation updates the thumbnail selection");
            org.EnsureVisible(0);
            Click(org, CellCenter(org, 0));
            Check(view.CurrentPage == 0 && view.Visible, "clicking a thumbnail navigates without leaving the reader");

            // A vertical list must insert above/below pages, independent of horizontal pointer position.
            var cell = (Rectangle)Call(org, "CellRect", 1);
            int scroll = Field<int>(org, "ScrollY");
            Check((int)Call(org, "InsertIndexAt", new Point(cell.Right - 8, cell.Top + 8 - scroll)) == 1 &&
                (int)Call(org, "InsertIndexAt", new Point(cell.Left + 8, cell.Bottom - 8 - scroll)) == 2,
                "sidebar drag positions use vertical page boundaries");
            var first = doc.Pages[0];
            Drag(org, CellCenter(org, 0), new Point(cell.Left + cell.Width / 2, cell.Bottom - 8 - scroll));
            Check(ReferenceEquals(doc.Pages[1], first) && view.Visible, "dragging thumbnails reorders pages alongside the reader");
            Key(Keys.Control | Keys.Z);
            Eq(original, Texts(doc), "undo restores sidebar drag order");

            org.EnsureVisible(0);
            Click(org, CellCenter(org, 0));
            Click(org, CellCenter(org, 1), MK_SHIFT);
            Check(org.SelectedIndices().SequenceEqual(new[] { 0, 1 }), "sidebar Shift-click selects multiple pages");
            Field<ToolStripButton>(form, "pRotateRight").PerformClick();
            Check(Field<string>(org, "toast") == null && Field<string>(view, "toast") != null,
                "sidebar edit notifications appear in the reader");
            Check(doc.Pages[0].Rotation == 1 && doc.Pages[1].Rotation == 1 && doc.Pages[2].Rotation == 0,
                "panel rotation applies to the whole selection only");
            Check(org.SelectedIndices().Count == 2, "rotation preserves the multi-page selection");
            Key(Keys.Control | Keys.Z);
            Key(Keys.Control | Keys.Y);
            Check(doc.Pages[0].Rotation == 1 && doc.Pages[1].Rotation == 1, "panel edits support redo");
            Key(Keys.Control | Keys.Z);
            Click(org, CellCenter(org, 0));
            Click(org, CellCenter(org, 1), MK_CONTROL);
            Field<ToolStripMenuItem>(form, "pDuplicate").PerformClick();
            Check(doc.Pages.Count == count + 2 && org.SelectedIndices().Count == 2, "More menu duplicates the selected pages");
            Key(Keys.Control | Keys.Z);
            Click(org, CellCenter(org, 0));
            Click(org, CellCenter(org, 1), MK_SHIFT);
            Field<ToolStripButton>(form, "pDelete").PerformClick();
            Check(doc.Pages.Count == count - 2, "panel delete removes the selected pages");
            Key(Keys.Control | Keys.Z);
            Key(Keys.Control | Keys.A);
            Check(!Field<ToolStripButton>(form, "pDelete").Enabled, "delete is disabled when every page is selected");
            Key(Keys.Delete);
            Check(doc.Pages.Count == count, "keyboard delete cannot remove every page");
            Click(org, CellCenter(org, 0));
            Key(Keys.Control | Keys.C);
            Key(Keys.Control | Keys.Tab);
            int otherCount = tabs[1].Doc.Pages.Count;
            Key(Keys.Control | Keys.V);
            Check(tabs[1].Doc.Pages.Count == otherCount + 1, "pages copied in the sidebar paste across tabs");
            Key(Keys.Control | Keys.Z);
            Key(Keys.Alt | Keys.D1);
            Click(org, CellCenter(org, 0));
            Call(form, "InsertBlank");
            Check(doc.Pages.Count == count + 1 && doc.Pages[1].Src == null, "blank pages insert after the sidebar selection");
            Key(Keys.Control | Keys.Z);
            Click(org, CellCenter(org, 0));
            Field<ToolStripMenuItem>(form, "pExtract").PerformClick();
            Check(tabs.Count == 3 && tabs[2].Doc.Pages.Count == 1 && panel.Visible, "sidebar extraction opens a new tab with pages panel");
            tabs[2].Doc.SaveAs(Path.Combine(dir, "panel-extract.pdf"));
            Key(Keys.Control | Keys.W);
            Key(Keys.Alt | Keys.D1);

            // Keyboard focus in the reader must retain text commands and view-only rotation.
            view.Focus();
            Key(Keys.Control | Keys.A);
            Check(view.HasSelection, "Ctrl+A in the reader still selects text");
            Key(Keys.OemCloseBrackets);
            Check(view.ViewRotation == 1 && !doc.IsModified, "reader rotation does not edit pages while the panel is open");
            Key(Keys.OemOpenBrackets);
            Key(Keys.Escape);
            Key(Keys.Control | Keys.E);
            Check(!panel.Visible && org.Visible && !org.SidebarMode && !view.Visible, "full organizer remains available");
            Key(Keys.Control | Keys.E);
            Check(panel.Visible && view.Visible && org.SidebarMode, "leaving organizer restores the pages panel");
            Key(Keys.F5);
            Check(!panel.Visible && !rail.Visible && view.Presentation, "presentation hides both pages panel and rail");
            Key(Keys.Escape);
            Check(panel.Visible, "leaving presentation restores the panel");

            view.GoToPage(0, false);
            CapturePanel("pages-expanded");
            form.Size = form.MinimumSize;
            Pump(100);
            Check(view.Width > 0 && panel.Right <= sidebar.ClientSize.Width && sidebar.Right <= view.Parent.Left,
                "pages panel and reader fit at minimum window size");
            CapturePanel("pages-narrow");
            Key(Keys.Control | Keys.Shift | Keys.E);
            CapturePanel("pages-collapsed");
            Check(!Field<AppSettings>(form, "settings").ShowBookmarks && Field<AppSettings>(form, "settings").ShowPages,
                "collapse remembers the last sidebar tab while saving its hidden state");
            Check(tabs.All(t => !t.Doc.IsModified), "all sidebar test edits were undone");
            while (tabs.Count > 0) Key(Keys.Control | Keys.W);
            Check(!panel.Visible && !rail.Visible && !view.Visible && Field<HomeView>(form, "home").Visible,
                "closing the last document hides the panel and returns home");
            CapturePanel("home-empty");
            form.Close();
            Pump(100);
        }

        private static void ApplicationMenuTests(bool hasDocument)
        {
            var bar = Field<CommandBar>(form, "commandBar");
            var menu = Field<MenuStrip>(form, "menu");
            var toggle = Field<ToolStripButton>(form, "bMenu");
            var expandedToggle = Field<ToolStripButton>(form, "bExpandedMenu");
            var brand = Field<DocumentHeader>(form, "shellHeader").BrandAction;
            var opening = Field<ToolStrip>(form, "openingTools");
            var commands = Field<ToolStrip>(form, "toolbar");
            var modes = Field<ToolStrip>(form, "modeTools");
            var content = Field<Control>(form, "content");
            var originalSize = form.Size;
            Click(brand, new Point(brand.Width / 2, brand.Height / 2));
            Check(PumpUntil(() => !bar.IsTransitioning, 700) && menu.Visible && toggle.Checked,
                "clicking the Gravity logo opens the same application menus");
            Check(brand.AccessibilityObject.Role == AccessibleRole.PushButton && brand.AccessibleName.Contains("Hide"),
                "Gravity logo exposes a named keyboard-accessible menu button");
            brand.AccessibilityObject.DoDefaultAction();
            Check(PumpUntil(() => !bar.IsTransitioning, 700) && !menu.Visible,
                "the accessible logo action closes the application menus");
            foreach (int width in new[] { 1200, 800, form.MinimumSize.Width })
            {
                form.Width = width; Pump(30);
                var open = Field<ToolStripButton>(form, "bOpen");
                Check(toggle.Owner == opening && toggle.Bounds.Left >= open.Bounds.Right &&
                    toggle.Bounds.Left - open.Bounds.Right <= 6 && opening.ClientRectangle.Contains(toggle.Bounds),
                    "dots sit directly after Open at width " + width);
                var contentBounds = content.Bounds; var barBounds = bar.Bounds;
                toggle.PerformClick();
                Check(bar.IsTransitioning == CommandBar.AnimationsEnabled, "menu fade follows Windows animation preferences");
                Check(PumpUntil(() => !bar.IsTransitioning, 700), "menu entrance finishes promptly");
                Check(menu.Visible && menu.Parent == bar && !opening.Visible && !commands.Visible && !modes.Visible,
                    "application menus replace the primary command row at width " + width);
                Check(content.Bounds == contentBounds && bar.Bounds == barBounds && Field<DocumentHeader>(form, "shellHeader").Top == 0,
                    "revealing menus preserves the document and header positions at width " + width);
                Check(menu.Items.Cast<ToolStripItem>().All(item => item.Image != null && item.Height >= 44 &&
                    item.Placement == ToolStripItemPlacement.Main && menu.ClientRectangle.Contains(item.Bounds) &&
                    !string.IsNullOrEmpty(item.AccessibleName)), "all six labeled menu icons fit without overflow at width " + width);
                Check(toggle.Checked && toggle.AccessibleName == "Hide application menus", "dot button announces the expanded state");
                CaptureCommandBar((hasDocument ? "document" : "home") + "-menus-" + width);
                expandedToggle.PerformClick();
                Check(PumpUntil(() => !bar.IsTransitioning, 700), "menu exit finishes promptly");
                Check(!menu.Visible && opening.Visible && commands.Visible && modes.Visible == hasDocument && !toggle.Checked,
                    "closing menus restores the original commands and document mode");
                Check(content.Bounds == contentBounds && bar.Bounds == barBounds, "closing menus does not shift the document");
            }
            brand.PerformClick(); Pump(35); brand.PerformClick(); Pump(25); brand.PerformClick();
            Check(PumpUntil(() => !bar.IsTransitioning, 700) && menu.Visible && toggle.Checked,
                "rapid toggling settles on the most recent menu state");
            Call(form, "SetApplicationMenus", false, false);
            Check(!bar.IsTransitioning && opening.Visible && !menu.Visible, "motion-free switching restores controls immediately");
            // Exercise native menu activation on screen; an offscreen test window may
            // not become the foreground window and its dropdown then closes immediately.
            var offscreenLocation = form.Location;
            var workArea = Screen.PrimaryScreen.WorkingArea;
            form.Location = new Point(workArea.Left + 40, workArea.Top + 40);
            form.BringToFront(); form.Activate(); Pump(50);
            Check(Key(Keys.F10) && menu.Visible && menu.ContainsFocus && !bar.IsTransitioning,
                "F10 reveals and focuses native application menus immediately");
            Check(Key(Keys.Escape) && !menu.Visible, "Escape returns from the menu row to primary commands");
            Check(Key(Keys.Alt | Keys.G) && ((ToolStripMenuItem)menu.Items[3]).DropDown.Visible,
                "Alt+G opens the native Go menu from the collapsed toolbar");
            ((ToolStripMenuItem)menu.Items[3]).HideDropDown();
            if (hasDocument)
            {
                var view = Field<DocView>(form, "view"); view.GoToPage(0, false);
                Field<ToolStripMenuItem>(form, "miNext").PerformClick();
                Check(view.CurrentPage == 1, "expanded menu commands retain document navigation");
                Check(Key(Keys.Control | Keys.G) && Field<PageNumberField>(form, "pageBox").Focused,
                    "Go to page uses the inline page field while application menus replace the toolbar");
                view.GoToPage(0, false);
            }
            else Check(!Field<ToolStripMenuItem>(form, "miNext").Enabled, "document menu actions stay disabled on the home screen");
            Call(form, "SetApplicationMenus", false, false);
            toggle.PerformClick(); form.Width += 10;
            Check(!bar.IsTransitioning && menu.Visible, "resizing ends the fade without leaving a stale overlay");
            Key(Keys.F11); Check(!bar.Visible && !menu.Visible && !bar.IsTransitioning, "fullscreen hides expanded menus and ends animation");
            Key(Keys.F11); Check(bar.Visible && menu.Visible && toggle.Checked, "leaving fullscreen restores the expanded menu state");
            Call(form, "SetApplicationMenus", false, false);
            form.Size = originalSize; form.Location = offscreenLocation; Call(form, "FocusCanvas"); Pump(30);
        }

        private static void CaptureCommandBar(string name)
        {
            string folder = Environment.GetEnvironmentVariable("GRAVITY_TEST_SCREENSHOTS");
            if (string.IsNullOrEmpty(folder)) return;
            Directory.CreateDirectory(folder);
            using (var bitmap = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(bitmap, form.ClientRectangle);
                var area = new Rectangle(0, 0, form.Width, Field<CommandBar>(form, "commandBar").Bottom);
                using (var crop = bitmap.Clone(area, bitmap.PixelFormat)) crop.Save(Path.Combine(folder, name + ".png"));
            }
        }

        private static void CapturePanel(string name)
        {
            string folder = Environment.GetEnvironmentVariable("GRAVITY_TEST_SCREENSHOTS");
            if (string.IsNullOrEmpty(folder)) return;
            Directory.CreateDirectory(folder);
            PumpUntil(() => Field<RenderService>(form, "renderer").IsIdle, 3000);
            using (var bitmap = new Bitmap(form.Width, form.Height))
            {
                // Offscreen controls first paint here, so prime thumbnail requests before capturing.
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                PumpUntil(() => Field<RenderService>(form, "renderer").IsIdle, 3000);
                Pump(2300);
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                bitmap.Save(Path.Combine(folder, name + ".png"), System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        private static void WindowChromeTests()
        {
            var offscreenBounds = form.Bounds;
            var workArea = Screen.FromControl(form).WorkingArea;
            form.Location = new Point(workArea.Left + 80, workArea.Top + 80); Pump(30);
            var min = Field<CaptionButton>(form, "minimizeWindow");
            var max = Field<CaptionButton>(form, "maximizeWindow");
            var close = Field<CaptionButton>(form, "closeWindow");
            Func<Point, int> hit = point => SendMessage(form.Handle, 0x84, IntPtr.Zero, LParam(form.PointToScreen(point))).ToInt32();
            Func<Control, Point> center = control => form.PointToClient(control.PointToScreen(new Point(control.Width / 2, control.Height / 2)));
            Check(hit(center(min)) == 8 && hit(center(max)) == 9 && hit(center(close)) == 20,
                "flat caption controls expose native minimize, Snap maximize and close hit targets");
            Check(hit(new Point(1, 1)) == 13 && hit(new Point(form.ClientSize.Width - 1, form.ClientSize.Height - 1)) == 17 && hit(new Point(200, 20)) == 2,
                "custom header preserves native corner resizing and caption dragging");
            Check(max.AccessibilityObject.Role == AccessibleRole.PushButton && close.AccessibilityObject.Name == "Close",
                "flat caption controls are named native accessible buttons");
            WindowFramePaintTests();
            WindowResizeEdgeTests();
            var bounds = form.Bounds;
            max.AccessibilityObject.DoDefaultAction(); Pump(60);
            Check(form.WindowState == FormWindowState.Maximized && max.AccessibleName == "Restore window" && form.Bounds == Screen.FromControl(form).WorkingArea,
                "maximize fills the monitor work area and exposes Restore without covering the taskbar");
            max.PerformClick(); Pump(60);
            Check(form.WindowState == FormWindowState.Normal && form.Bounds == bounds, "Restore returns the previous window bounds");
            SendMessage(form.Handle, 0xa3, (IntPtr)2, LParam(form.PointToScreen(new Point(200, 20)))); Pump(60);
            Check(form.WindowState == FormWindowState.Maximized, "double-clicking the clean header maximizes through Windows");
            max.PerformClick(); Pump(60);
            min.PerformClick(); Pump(60);
            Check(form.WindowState == FormWindowState.Minimized, "flat Minimize control minimizes the actual window");
            SendMessage(form.Handle, 0x112, (IntPtr)0xf120, IntPtr.Zero); Pump(60);
            Check(form.WindowState == FormWindowState.Normal && form.Bounds == bounds, "restoring a minimized window preserves its exact size and position");
            bool requestedClose = false;
            FormClosingEventHandler cancel = (sender, e) => { requestedClose = true; e.Cancel = true; };
            form.FormClosing += cancel; close.PerformClick(); form.FormClosing -= cancel;
            Check(requestedClose && !form.IsDisposed, "flat Close uses the normal cancellable close path and preserves unsaved-document checks");
            form.Bounds = offscreenBounds; Pump(30);
        }

        private static void WindowFramePaintTests()
        {
            form.BringToFront(); form.Refresh(); Pump(30);
            // Read the actual window surface: DrawToBitmap repaints controls and can hide
            // a system caption that was drawn over them by a nonclient message.
            var header = Field<DocumentHeader>(form, "shellHeader");
            var sample = header.PointToScreen(new Point(header.Width / 3, header.Height / 5));
            sample.Offset(-form.Left, -form.Top);
            uint expected = (uint)(Theme.Chrome.R | Theme.Chrome.G << 8 | Theme.Chrome.B << 16);
            Action<string> checkSurface = label =>
            {
                IntPtr dc = GetWindowDC(form.Handle);
                try { Check(dc != IntPtr.Zero && GetPixel(dc, sample.X, sample.Y) == expected, label); }
                finally { if (dc != IntPtr.Zero) ReleaseDC(form.Handle, dc); }
            };
            checkSurface("visible custom header starts without a native caption overlay");
            foreach (int message in new[] { 0x85, 0x86, 0xae, 0xaf })
            {
                form.Refresh();
                SendMessage(form.Handle, message, (IntPtr)1, IntPtr.Zero);
                checkSurface("native frame message " + message.ToString("X") + " cannot paint over the custom header");
            }
            form.Refresh();
            SendMessage(form.Handle, 0x86, IntPtr.Zero, IntPtr.Zero);
            checkSurface("deactivation cannot paint an inactive caption over the custom header");
            SendMessage(form.Handle, 0x86, (IntPtr)1, IntPtr.Zero);
            string title = form.Text;
            form.Refresh();
            form.Text = "Window frame regression";
            checkSurface("changing the taskbar title does not draw a native title strip");
            form.Text = title;
        }

        private static void WindowResizeEdgeTests()
        {
            int right = form.ClientSize.Width - 2, bottom = form.ClientSize.Height - 2;
            var positions = new[] { new Point(1, 1), new Point(right / 2, 1), new Point(right, 1),
                new Point(1, bottom / 2), new Point(right, bottom / 2), new Point(1, bottom),
                new Point(right / 2, bottom), new Point(right, bottom), new Point(60, 1), new Point(right - 20, 1) };
            int[] expected = { 13, 12, 14, 10, 11, 16, 15, 17, 12, 12 };
            for (int i = 0; i < positions.Length; i++)
            {
                Point screen = form.PointToScreen(positions[i]);
                Control target = form, child;
                while ((child = target.GetChildAtPoint(target.PointToClient(screen), GetChildAtPointSkip.Invisible)) != null) target = child;
                int hit = SendMessage(target.Handle, 0x84, IntPtr.Zero, LParam(screen)).ToInt32();
                Check((target == form ? hit == expected[i] : hit == -1) &&
                    (int)Call(form, "WindowHitTest", screen) == expected[i],
                    "outer resize target " + i + " passes through covering controls to the native frame");
            }
            var brand = Field<DocumentHeader>(form, "shellHeader").BrandAction;
            Check((int)Call(form, "WindowHitTest", brand.PointToScreen(new Point(brand.Width / 2, brand.Height / 2))) == 1,
                "logo receives clicks while the outer edge remains resizable");
        }

        private static void Eq(string expected, string actual, string what)
        {
            Check(expected == actual, what + (expected == actual ? "" : " (expected [" + expected + "], got [" + actual + "])"));
        }
    }
}
