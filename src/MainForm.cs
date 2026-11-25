using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Gravity
{
    internal sealed class DocTab
    {
        public EditDoc Doc;
        public ViewState View = new ViewState();
        public bool Organize;
        public int OrganizerFocus;
        public FileSystemWatcher Watcher;
        public bool ReloadPending;
        public int ReloadTries;
    }

    internal sealed partial class MainForm : Form
    {
        private readonly AppSettings settings;
        private readonly RenderService renderer;
        private readonly List<DocTab> tabs = new List<DocTab>();
        private DocTab active;
        private List<PageRef> clipboard = new List<PageRef>();
        private List<PageRef> pendingSelection;
        private PdfSource tocBase;
        private readonly string[] startupArgs;
        private readonly float dpi;
        private bool updating;

        private MenuStrip menu;
        private ToolStrip toolbar;
        private TabStrip tabStrip;
        private Panel sidebar, host;
        private Panel pagePanel, pageBody, pageRail, content;
        private Splitter pageSplitter;
        private Label pageSelection;
        private ToolStripButton pRotateLeft, pRotateRight, pDelete, pEarlier, pLater;
        private ToolStripMenuItem pCut, pCopy, pPaste, pDuplicate, pExtract, pSave;
        private ToolStripMenuItem miPagePanel;
        private bool pageSelectionActive, syncingPages, layingOutPanels;
        private TreeView toc;
        private Label tocEmpty;
        private Splitter splitter;
        private DocView view;
        private OrganizerView organizer;
        private HomeView home;
        private SearchPanel searchPanel;
        private readonly Timer findTimer = new Timer { Interval = 300 };
        private ToolStripMenuItem miExport, miInsertImages;
        private ContextMenuStrip viewMenu, organizerMenu;

        private ToolStripButton bOpen, bSave, bPrint, bUndo, bRedo, bSidebar, bOrganize, bPrev, bNext, bZoomOut, bZoomIn, bRotL, bRotR,
            bCut, bCopy, bPaste, bDelete, bPRotL, bPRotR, bExtract, bFindPrev, bFindNext, bMatchCase;
        private ToolStripDropDownButton bInsert;
        private PageNumberField pageBox;
        private ToolStripTextBox findBox;
        private ToolStripLabel pageOf;
        private ToolStripComboBox zoomBox;
        private readonly List<ToolStripItem> docGroup = new List<ToolStripItem>();
        private readonly List<ToolStripItem> readGroup = new List<ToolStripItem>();
        private readonly List<ToolStripItem> editGroup = new List<ToolStripItem>();
        private readonly List<ToolStripItem> findGroup = new List<ToolStripItem>();

        private ToolStripMenuItem miSave, miSaveAs, miSavePages, miPrint, miClose, miRecent, miUndo, miRedo, miCopyText, miSelectAll,
            miFind, miFindNext, miFindPrev, miOrganize, miSingle, miFacing, miBook, miContinuous, miFitPage, miFitWidth, miActual,
            miZoomIn, miZoomOut, miRotL, miRotR, miBookmarks, miFullscreen, miPresentation, miNext, miPrev, miFirst, miLast, miGoto,
            miBack, miForward, miCut, miCopy, miPaste, miPasteBefore, miDelete, miDuplicate, miPRotL, miPRotR, miMoveEarlier,
            miMoveLater, miInsertBlank, miInsertFile, miExtract, miSelectAllPages;

        private bool fullscreen, presentationMode;
        private FormBorderStyle savedBorder;
        private FormWindowState savedState;
        private Rectangle savedBounds;
        private readonly Timer reloadTimer = new Timer { Interval = 500 };

        /// <summary>Documents opened by Program before the window existed, keyed by full path.</summary>
        public static readonly Dictionary<string, PdfSource> Preloaded = new Dictionary<string, PdfSource>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Developer option: save a screenshot of the window to this path, then exit.</summary>
        public string ScreenshotPath;
        public bool ScreenshotOrganize;

        /// <summary>When set, settings are neither read from nor written to disk (used by tests).</summary>
        public readonly bool Ephemeral;

        public MainForm(string[] args) : this(args, false) { }

        public MainForm(string[] args, bool ephemeral)
        {
            startupArgs = args ?? new string[0];
            Ephemeral = ephemeral;
            settings = ephemeral ? new AppSettings() : AppSettings.Load();
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) dpi = g.DpiX;
            Text = "Gravity";
            Font = new Font("Segoe UI", 9f);
            AutoScaleMode = AutoScaleMode.None;
            try { Icon = Brand.AppIcon() ?? Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }
            MinimumSize = new Size(S(520), S(380));
            BackColor = Theme.Chrome;
            renderer = new RenderService(this);
            renderer.TileReady += delegate
            {
                if (view.Visible) view.Invalidate();
                if (organizer.Visible) organizer.Invalidate();
            };
            BuildUi();
            RestoreWindow();
            reloadTimer.Tick += OnReloadTimer;
            AllowDrop = true;
            DragEnter += OnFileDragEnter;
            DragDrop += OnFileDragDrop;
        }

        private int S(float v) { return (int)Math.Round(v * dpi / 96f); }

        // =====================================================================
        // UI construction
        // =====================================================================

        private void BuildUi()
        {
            SuspendLayout();

            host = new Panel { Dock = DockStyle.Fill, BackColor = Theme.ViewBack };
            view = new DocView { Dock = DockStyle.Fill, Renderer = renderer, Visible = false, AllowDrop = true };
            organizer = new OrganizerView { Dock = DockStyle.Fill, Renderer = renderer, Visible = false };
            organizer.ThumbSize = settings.ThumbSize;
            home = new HomeView { Dock = DockStyle.Fill, AllowDrop = true };
            host.Controls.Add(view);
            host.Controls.Add(organizer);
            host.Controls.Add(home);
            searchPanel = new SearchPanel { Dock = DockStyle.Bottom, Height = S(210), Visible = false, Font = Font, MatchCase = settings.MatchCase };
            searchPanel.MatchSelected += match => view.ShowSearchMatch(match);
            searchPanel.CloseRequested += CloseFind;
            searchPanel.QueryChanged += delegate { findBox.Text = searchPanel.Query; };
            searchPanel.CaseChanged += delegate { bMatchCase.Checked = searchPanel.MatchCase; };
            searchPanel.NextRequested += FindNext;
            findTimer.Tick += delegate { findTimer.Stop(); StartFind(true); };

            sidebar = new Panel { Dock = DockStyle.Left, Width = S(Math.Max(140, settings.SidebarWidth)), BackColor = Color.White, Visible = false };
            var sideHeader = new Label
            {
                Text = "Bookmarks",
                Dock = DockStyle.Top,
                Height = S(30),
                Padding = new Padding(S(10), 0, 0, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI Semibold", 9f),
                ForeColor = Theme.SubtleText,
                BackColor = Color.White,
            };
            toc = new OutlineTreeView
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                HideSelection = false,
                ShowLines = false,
                FullRowSelect = true,
                HotTracking = true,
                ItemHeight = S(24),
                Indent = S(14),
                Font = new Font("Segoe UI", 9f),
            };
            tocEmpty = new Label { Text = "This document has no bookmarks.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.TopCenter, ForeColor = Theme.SubtleText, Padding = new Padding(S(10)), Visible = false };
            sidebar.Controls.Add(toc);
            sidebar.Controls.Add(tocEmpty);
            sidebar.Controls.Add(sideHeader);
            sidebar.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 1, BackColor = Theme.ChromeBorder });
            toc.HandleCreated += delegate { SetWindowTheme(toc.Handle, "Explorer", null); };
            splitter = new Splitter { Dock = DockStyle.Left, Width = S(1), BackColor = Theme.ChromeBorder, MinSize = S(160), MinExtra = S(240), Visible = false };
            splitter.SplitterMoved += delegate { settings.SidebarWidth = (int)Math.Round(sidebar.Width * 96f / dpi); };

            BuildPagePanel();
            content = new Panel { Dock = DockStyle.Fill };
            content.Controls.Add(host);
            content.Controls.Add(pageSplitter);
            content.Controls.Add(pagePanel);
            content.Controls.Add(pageRail);
            content.Controls.Add(splitter);
            content.Controls.Add(sidebar);
            content.Controls.Add(searchPanel);
            content.Resize += delegate { searchPanel.Height = Math.Min(S(210), Math.Max(S(112), content.ClientSize.Height / 3)); };
            content.Resize += delegate { ApplySidebar(); };

            BuildToolbar();
            BuildMenu();
            BuildContextMenus();
            tabStrip = new TabStrip { Dock = DockStyle.Top, Visible = false, AllowDrop = true };

            Controls.Add(content);
            Controls.Add(toolbar);
            Controls.Add(tabStrip);
            Controls.Add(menu);
            MainMenuStrip = menu;
            BuildLucideShell();
            ResumeLayout(true);

            // events
            view.ViewChanged += delegate { SyncPageSelection(); UpdateUI(); };
            view.Enter += delegate { pageSelectionActive = false; SyncPageSelection(); UpdateUI(); };
            view.Notify += Toast;
            view.FormFieldClicked += FillForm;
            view.DragEnter += OnFileDragEnter;
            view.DragDrop += OnFileDragDrop;
            home.DragEnter += OnFileDragEnter;
            home.DragDrop += OnFileDragDrop;
            tabStrip.DragEnter += OnFileDragEnter;
            tabStrip.DragDrop += OnFileDragDrop;
            home.OpenRequested += delegate { OpenDialog(); };
            home.CombineRequested += delegate { CombineFiles(); };
            home.RecentRequested += path => OpenFiles(new[] { path }, -1);
            home.RecentRemoveRequested += path => { settings.Forget(path); home.SetRecent(settings.Recent); };
            organizer.Enter += delegate { pageSelectionActive = true; UpdateUI(); };
            organizer.SelectionChanged += delegate
            {
                if (!syncingPages && !Organizing && pagePanel.Visible && organizer.ContainsFocus)
                {
                    pageSelectionActive = true;
                    if (organizer.FocusIndex >= 0) view.GoToPage(organizer.FocusIndex, false);
                }
                UpdateUI();
            };
            organizer.MoveRequested += (moving, at) => Guard(() => MovePages(moving, at));
            organizer.FilesDropped += (files, at) => Guard(() => InsertFiles(files, at));
            organizer.OpenPageRequested += i => { SetOrganize(false); view.GoToPage(i, false); view.Focus(); };
            tabStrip.TabSelected += i => ActivateTab(tabs[i]);
            tabStrip.TabCloseRequested += i => CloseTab(tabs[i]);
            tabStrip.AddRequested += delegate { OpenDialog(); };
            toc.NodeMouseClick += (s, e) => { if (e.Button == MouseButtons.Left) NavigateToc(e.Node); };
            toc.AfterSelect += (s, e) => { if (e.Action == TreeViewAction.ByKeyboard) NavigateToc(e.Node); };
            toc.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { NavigateToc(toc.SelectedNode); e.SuppressKeyPress = true; } };
        }

        private ToolStrip PageTools()
        {
            return new ToolStrip
            {
                Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden,
                Renderer = new FlatRenderer(), BackColor = Color.White,
                ImageScalingSize = new Size(S(16), S(16)),
                Padding = new Padding(S(5), S(2), S(5), S(2)), Font = Font,
            };
        }

        private void PageAction(Action action)
        {
            pageSelectionActive = true;
            organizer.Focus();
            action();
        }

        private void BuildPagePanel()
        {
            pagePanel = new Panel { Dock = DockStyle.Right, Width = S(240), BackColor = Color.White, Visible = false };
            pageBody = new Panel { Dock = DockStyle.Fill };
            var header = PageTools();
            header.Items.Add(new ToolStripLabel("Pages") { Font = new Font("Segoe UI Semibold", 9f) });
            var collapse = Btn("\uE76C", "Collapse pages (Ctrl+Shift+E)", TogglePagePanel);
            collapse.Alignment = ToolStripItemAlignment.Right;
            collapse.AccessibleName = "Collapse pages";
            header.Items.Add(collapse);

            var edits = PageTools();
            pRotateLeft = Btn("\uE7AD", "Rotate selected pages left", () => PageAction(() => RotatePages(-1)), null, null, true);
            pRotateRight = Btn("\uE7AD", "Rotate selected pages right", () => PageAction(() => RotatePages(1)));
            pEarlier = Btn("\uE70E", "Move selected pages earlier (Alt+Up)", () => PageAction(() => MoveBy(-1)));
            pLater = Btn("\uE70D", "Move selected pages later (Alt+Down)", () => PageAction(() => MoveBy(1)));
            pDelete = Btn("\uE74D", "Delete selected pages (Delete)", () => PageAction(DeletePages));
            edits.Items.AddRange(new ToolStripItem[] { pRotateLeft, pRotateRight, new ToolStripSeparator(), pEarlier, pLater, new ToolStripSeparator(), pDelete });

            var actions = PageTools();
            var insert = new ToolStripDropDownButton("Insert") { ToolTipText = "Insert pages after the selection" };
            insert.DropDownItems.Add(MI("Pages from File…", null, () => PageAction(InsertFromFileDialog)));
            insert.DropDownItems.Add(MI("Images…", null, () => PageAction(() => ImportImages(null, true, PastePoint(false)))));
            insert.DropDownItems.Add(MI("Blank Page", null, () => PageAction(InsertBlank)));
            var more = new ToolStripDropDownButton("More") { ToolTipText = "More page actions" };
            pCut = MI("Cut Pages", "Ctrl+X", () => PageAction(CutPages));
            pCopy = MI("Copy Pages", "Ctrl+C", () => PageAction(CopyPages));
            pPaste = MI("Paste Pages", "Ctrl+V", () => PageAction(() => PastePages(false)));
            pDuplicate = MI("Duplicate Pages", "Ctrl+D", () => PageAction(DuplicatePages));
            pExtract = MI("Extract to New Tab", null, () => PageAction(ExtractPages));
            pSave = MI("Save Pages As…", null, () => PageAction(SaveSelectedPagesAs));
            more.DropDownItems.AddRange(new ToolStripItem[] { pCut, pCopy, pPaste, pDuplicate, new ToolStripSeparator(), pExtract, pSave,
                new ToolStripSeparator(), MI("Select All Pages", "Ctrl+A", () => PageAction(() => organizer.SelectAll())),
                MI("Open Full Organizer", "Ctrl+E", () => SetOrganize(true)) });
            actions.Items.AddRange(new ToolStripItem[] { insert, more });
            pageSelection = new Label { Dock = DockStyle.Top, Height = S(28), Padding = new Padding(S(10), 0, 0, 0),
                TextAlign = ContentAlignment.MiddleLeft, ForeColor = Theme.SubtleText };
            var hint = new Label { Text = "Drag to reorder · Ctrl / Shift to select", Dock = DockStyle.Bottom,
                Height = S(42), Padding = new Padding(S(8), S(4), S(8), S(4)), ForeColor = Theme.SubtleText,
                TextAlign = ContentAlignment.MiddleLeft };
            pagePanel.Controls.Add(pageBody);
            pagePanel.Controls.Add(hint);
            pagePanel.Controls.Add(pageSelection);
            pagePanel.Controls.Add(actions);
            pagePanel.Controls.Add(edits);
            pagePanel.Controls.Add(header);
            pagePanel.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 1, BackColor = Theme.ChromeBorder });

            pageRail = new Panel { Dock = DockStyle.Right, Width = S(34), BackColor = Color.White, Visible = false };
            var railTools = PageTools();
            railTools.Padding = Padding.Empty;
            var expand = Btn("\uE8A5", "Show pages (Ctrl+Shift+E)", TogglePagePanel);
            expand.AccessibleName = "Show pages";
            railTools.Items.Add(expand);
            pageRail.Controls.Add(railTools);
            pageRail.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 1, BackColor = Theme.ChromeBorder });
            pageSplitter = new Splitter { Dock = DockStyle.Right, Width = S(4), BackColor = Theme.ChromeBorder,
                MinSize = S(196), MinExtra = S(220), Visible = false };
            pageSplitter.SplitterMoved += delegate
            {
                settings.PagePanelWidth = (int)Math.Round(pagePanel.Width * 96f / dpi);
                ApplySidebar();
            };
        }

        private bool PageTargets { get { return Organizing || (pagePanel.Visible && pageSelectionActive); } }

        private void SyncPageSelection()
        {
            if (syncingPages || Organizing || active == null || organizer.Document != active.Doc || view.Document != active.Doc) return;
            if (view.CurrentPage != organizer.FocusIndex) pageSelectionActive = false;
            if (PageTargets) return;
            syncingPages = true;
            try { organizer.SelectPage(view.CurrentPage); }
            finally { syncingPages = false; }
        }

        private void TogglePagePanel()
        {
            if (active == null || presentationMode) return;
            bool wasVisible = pagePanel.Visible;
            settings.ShowPages = true;
            settings.ShowBookmarks = !wasVisible;
            if (Organizing) SetOrganize(false);
            pageSelectionActive = false;
            ShowMode();
            SyncPageSelection();
            UpdateUI();
            if (pagePanel.Visible) organizer.Focus(); else view.Focus();
        }

        private ToolStripButton Btn(string glyph, string tip, Action click, string text = null, string font = null, bool mirror = false)
        {
            var b = new ToolStripButton
            {
                Image = Theme.Glyph(glyph, S(20), Theme.Text, font, mirror),
                Text = text ?? tip,
                ToolTipText = tip,
                AutoToolTip = false,
                DisplayStyle = text == null ? ToolStripItemDisplayStyle.Image : ToolStripItemDisplayStyle.ImageAndText,
                Margin = new Padding(S(1), S(1), S(1), S(2)),
                Padding = new Padding(S(3)),
            };
            b.Click += delegate { Guard(click); };
            return b;
        }

        private void BuildToolbar()
        {
            toolbar = new ToolStrip
            {
                GripStyle = ToolStripGripStyle.Hidden,
                Renderer = new FlatRenderer(),
                ImageScalingSize = new Size(S(16), S(16)),
                Padding = new Padding(S(6), S(3), S(6), S(3)),
                BackColor = Color.White,
                Stretch = true,
                Font = new Font("Segoe UI", 9f),
            };
            bOpen = Btn("", "Open (Ctrl+O)", OpenDialog);
            bSave = Btn("", "Save (Ctrl+S)", () => { if (active != null) Save(active); });
            bPrint = Btn("", "Print (Ctrl+P)", Print);
            bUndo = Btn("", "Undo (Ctrl+Z)", Undo);
            bRedo = Btn("", "Redo (Ctrl+Y)", Redo);
            bSidebar = Btn("", "Show or hide sidebar (F12)", ToggleSidebar);
            bOrganize = Btn("", "Organize pages: reorder, cut, copy, paste, rotate, insert (Ctrl+E)", () => SetOrganize(true), "Organize");

            bPrev = Btn("", "Previous page (P)", () => view.PrevPage());
            pageBox = new PageNumberField { AutoSize = false, Size = new Size(S(44), S(32)), ToolTipText = "Go to page (Ctrl+G)" };
            pageBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;
                int n;
                if (int.TryParse(pageBox.Text.Trim(), out n)) view.GoToPage(n - 1);
                FocusCanvas();
            };
            pageBox.LostFocus += delegate { UpdateUI(); };
            pageOf = new ToolStripLabel("of 0") { ForeColor = Theme.SubtleText, Margin = new Padding(S(2), 0, S(2), 0) };
            bNext = Btn("", "Next page (N)", () => view.NextPage());
            bZoomOut = Btn("", "Zoom out (-)", () => view.ZoomStep(-1));
            zoomBox = new ToolStripComboBox { AutoSize = false, Width = S(96), DropDownStyle = ComboBoxStyle.DropDown, ToolTipText = "Zoom", FlatStyle = FlatStyle.System };
            zoomBox.Items.AddRange(new object[] { "Fit Page", "Fit Width", "Actual Size", "50%", "75%", "100%", "125%", "150%", "200%", "300%", "400%", "800%" });
            zoomBox.SelectedIndexChanged += delegate { if (!updating) { ApplyZoomText(zoomBox.Text); FocusCanvas(); } };
            zoomBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;
                ApplyZoomText(zoomBox.Text);
                FocusCanvas();
            };
            zoomBox.LostFocus += delegate { UpdateUI(); };
            bZoomIn = Btn("", "Zoom in (+)", () => view.ZoomStep(1));
            bRotL = Btn("", "Rotate view left ([)", () => view.RotateView(-1), null, null, true);
            bRotR = Btn("", "Rotate view right (])", () => view.RotateView(1));

            bCut = Btn("", "Cut pages (Ctrl+X)", CutPages);
            bCopy = Btn("", "Copy pages (Ctrl+C)", CopyPages);
            bPaste = Btn("", "Paste pages after the selection (Ctrl+V)", () => PastePages(false));
            bDelete = Btn("", "Delete pages (Delete)", DeletePages);
            bPRotL = Btn("", "Rotate pages left ([)", () => RotatePages(-1), null, null, true);
            bPRotR = Btn("", "Rotate pages right (])", () => RotatePages(1));
            bInsert = new ToolStripDropDownButton("Insert", Theme.Glyph("", S(16), Theme.Text))
            {
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                ToolTipText = "Insert pages",
                AutoToolTip = false,
                Margin = new Padding(S(1), S(1), S(1), S(2)),
                Padding = new Padding(S(3)),
            };
            bInsert.DropDownItems.Add("Pages from file…", null, delegate { Guard(InsertFromFileDialog); });
            bInsert.DropDownItems.Add("Images…", null, delegate { Guard(() => ImportImages(null, true, PastePoint(false))); });
            bInsert.DropDownItems.Add("Blank page", null, delegate { Guard(InsertBlank); });
            bInsert.DropDownItems.Add("Paste before the selection", null, delegate { Guard(() => PastePages(true)); });
            bExtract = Btn("", "Copy the selected pages into a new tab", ExtractPages, "Extract");

            bMatchCase = new ToolStripButton("Aa") { CheckOnClick = true, ToolTipText = "Match case", AutoToolTip = false, Alignment = ToolStripItemAlignment.Right, Checked = settings.MatchCase, Font = new Font("Segoe UI Semibold", 9f), Margin = new Padding(S(1), S(1), S(4), S(2)) };
            bMatchCase.CheckedChanged += delegate { settings.MatchCase = bMatchCase.Checked; searchPanel.MatchCase = bMatchCase.Checked; QueueFind(); };
            bFindNext = Btn("", "Find next (F3)", () => FindNext(true));
            bFindPrev = Btn("", "Find previous (Shift+F3)", () => FindNext(false));
            bFindNext.Alignment = bFindPrev.Alignment = ToolStripItemAlignment.Right;
            findBox = new ToolStripTextBox { AutoSize = false, Width = S(190), Alignment = ToolStripItemAlignment.Right, BorderStyle = BorderStyle.FixedSingle, ToolTipText = "Find text (Ctrl+F). Enter for next, Shift+Enter for previous." };
            findBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;
                FindNext(!e.Shift);
            };
            findBox.TextChanged += delegate { searchPanel.Query = findBox.Text; QueueFind(); };

            var d1 = new ToolStripSeparator();
            var d2 = new ToolStripSeparator();
            docGroup.AddRange(new ToolStripItem[] { bSave, bPrint, d1, bUndo, bRedo, d2, bSidebar, bOrganize });
            toolbar.Items.Add(bOpen);
            toolbar.Items.AddRange(docGroup.ToArray());
            var s1 = new ToolStripSeparator();
            var s2 = new ToolStripSeparator();
            var s3 = new ToolStripSeparator();
            readGroup.AddRange(new ToolStripItem[] { s1, bPrev, pageBox, pageOf, bNext, s2, bZoomOut, zoomBox, bZoomIn, s3, bRotL, bRotR });
            var e1 = new ToolStripSeparator();
            var e2 = new ToolStripSeparator();
            var e3 = new ToolStripSeparator();
            editGroup.AddRange(new ToolStripItem[] { e1, bCut, bCopy, bPaste, bDelete, e2, bPRotL, bPRotR, e3, bInsert, bExtract });
            findGroup.AddRange(new ToolStripItem[] { bMatchCase, bFindNext, bFindPrev, findBox });
            toolbar.Items.AddRange(readGroup.ToArray());
            toolbar.Items.AddRange(editGroup.ToArray());
            toolbar.Items.AddRange(findGroup.ToArray());
        }

        private ToolStripMenuItem MI(string text, string keys, Action click)
        {
            var m = new ToolStripMenuItem(text);
            if (keys != null) m.ShortcutKeyDisplayString = keys;
            if (click != null) m.Click += delegate { Guard(click); };
            return m;
        }

        private void FillForm(int pageIndex, int fieldIndex)
        {
            if (active == null || pageIndex < 0 || pageIndex >= active.Doc.Pages.Count) return;
            var page = active.Doc.Pages[pageIndex];
            try
            {
                List<PdfFormField> fields;
                lock (Pdfium.Lock) fields = page.Src == null ? new List<PdfFormField>() : page.Src.GetFormFields(page.Index);
                if (fields.Count == 0) { Toast("No form fields on this page."); return; }
                using (var editor = new FormEditor(fields, pageIndex, fieldIndex))
                    if (editor.ShowDialog(this) == DialogResult.OK) active.Doc.FillForm(pageIndex, editor.Edits);
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Could not fill form", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            FocusCanvas();
        }

        private void BuildMenu()
        {
            menu = new MenuStrip { Renderer = new FlatRenderer(), BackColor = Theme.Chrome, Padding = new Padding(S(4), S(2), 0, S(2)), Font = new Font("Segoe UI", 9f) };

            var file = new ToolStripMenuItem("&File");
            miRecent = new ToolStripMenuItem("Open &Recent");
            miRecent.DropDownItems.Add("(none)");
            miRecent.DropDownOpening += delegate { FillRecentMenu(); };
            miSave = MI("&Save", "Ctrl+S", () => { if (active != null) Save(active); });
            miSaveAs = MI("Save &As…", "Ctrl+Shift+S", () => { if (active != null) SaveAs(active); });
            miSavePages = MI("Save Selected Pages As…", null, SaveSelectedPagesAs);
            miPrint = MI("&Print…", "Ctrl+P", Print);
            miClose = MI("&Close Tab", "Ctrl+W", () => CloseTab(active));
            miExport = MI("&Export Pages…", null, ExportPages);
            file.DropDownItems.AddRange(new ToolStripItem[]
            {
                MI("&Open…", "Ctrl+O", OpenDialog), miRecent, MI("Co&mbine PDFs…", null, CombineFiles), new ToolStripSeparator(),
                MI("Create PDF from &Images…", null, () => ImportImages(null, false, 0)), new ToolStripSeparator(),
                miSave, miSaveAs, miSavePages, miExport, new ToolStripSeparator(), miPrint, new ToolStripSeparator(), miClose, MI("E&xit", "Alt+F4", Close),
            });

            var edit = new ToolStripMenuItem("&Edit");
            miUndo = MI("&Undo", "Ctrl+Z", Undo);
            miRedo = MI("&Redo", "Ctrl+Y", Redo);
            miCopyText = MI("&Copy Text", "Ctrl+C", CopyText);
            miSelectAll = MI("Select &All Text", "Ctrl+A", () => view.SelectAll());
            miFind = MI("&Find…", "Ctrl+F", FocusFind);
            miFindNext = MI("Find &Next", "F3", () => FindNext(true));
            miFindPrev = MI("Find Pre&vious", "Shift+F3", () => FindNext(false));
            edit.DropDownItems.AddRange(new ToolStripItem[] { miUndo, miRedo, new ToolStripSeparator(), miCopyText, miSelectAll, new ToolStripSeparator(), miFind, miFindNext, miFindPrev,
                new ToolStripSeparator(), MI("Fill &Form…", "Ctrl+Shift+F", () => FillForm(view.CurrentPage, -1)) });

            var viewM = new ToolStripMenuItem("&View");
            miOrganize = MI("&Organize Pages", "Ctrl+E", ToggleOrganize);
            miSingle = MI("&Single Page", "Ctrl+6", () => view.SetLayout(PageLayout.Single));
            miFacing = MI("&Facing", "Ctrl+7", () => view.SetLayout(PageLayout.Facing));
            miBook = MI("&Book View", "Ctrl+8", () => view.SetLayout(PageLayout.Book));
            miContinuous = MI("&Continuous", "C", () => view.SetContinuous(!view.Continuous));
            miFitPage = MI("Fit &Page", "Ctrl+0", () => view.SetZoomMode(ZoomMode.FitPage));
            miFitWidth = MI("Fit &Width", "Ctrl+2", () => view.SetZoomMode(ZoomMode.FitWidth));
            miActual = MI("&Actual Size", "Ctrl+1", () => view.SetZoom(1));
            miZoomIn = MI("Zoom &In", "+", () => view.ZoomStep(1));
            miZoomOut = MI("Zoom &Out", "-", () => view.ZoomStep(-1));
            miRotL = MI("Rotate View &Left", "[", () => view.RotateView(-1));
            miRotR = MI("Rotate View &Right", "]", () => view.RotateView(1));
            miBookmarks = MI("&Bookmarks", "F12", ToggleSidebar);
            miPagePanel = MI("&Pages Panel", "Ctrl+Shift+E", TogglePagePanel);
            miFullscreen = MI("F&ull Screen", "F11", ToggleFullscreen);
            miPresentation = MI("Pr&esentation", "F5", TogglePresentation);
            viewM.DropDownItems.AddRange(new ToolStripItem[]
            {
                miOrganize, new ToolStripSeparator(), miSingle, miFacing, miBook, miContinuous, new ToolStripSeparator(),
                miFitPage, miFitWidth, miActual, miZoomIn, miZoomOut, new ToolStripSeparator(), miRotL, miRotR, new ToolStripSeparator(),
                miBookmarks, miPagePanel, miFullscreen, miPresentation,
            });

            var go = new ToolStripMenuItem("&Go");
            miNext = MI("&Next Page", "N", () => view.NextPage());
            miPrev = MI("&Previous Page", "P", () => view.PrevPage());
            miFirst = MI("&First Page", "Home", () => view.FirstPage());
            miLast = MI("&Last Page", "End", () => view.LastPage());
            miGoto = MI("&Go to Page…", "Ctrl+G", FocusPageBox);
            miBack = MI("&Back", "Alt+Left", () => view.GoBack());
            miForward = MI("F&orward", "Alt+Right", () => view.GoForward());
            go.DropDownItems.AddRange(new ToolStripItem[] { miNext, miPrev, miFirst, miLast, miGoto, new ToolStripSeparator(), miBack, miForward });

            var pagesM = new ToolStripMenuItem("&Pages");
            miCut = MI("Cu&t Pages", "Ctrl+X", CutPages);
            miCopy = MI("&Copy Pages", "Ctrl+C", CopyPages);
            miPaste = MI("&Paste Pages After", "Ctrl+V", () => PastePages(false));
            miPasteBefore = MI("Paste Pages &Before", "Ctrl+Shift+V", () => PastePages(true));
            miDelete = MI("&Delete Pages", "Del", DeletePages);
            miDuplicate = MI("D&uplicate Pages", "Ctrl+D", DuplicatePages);
            miPRotL = MI("Rotate Pages &Left", "Ctrl+[", () => RotatePages(-1));
            miPRotR = MI("Rotate Pages &Right", "Ctrl+]", () => RotatePages(1));
            miMoveEarlier = MI("Move Pages &Earlier", "Alt+Left", () => MoveBy(-1));
            miMoveLater = MI("Move Pages La&ter", "Alt+Right", () => MoveBy(1));
            miInsertBlank = MI("Insert Bla&nk Page", null, InsertBlank);
            miInsertFile = MI("&Insert Pages from File…", null, InsertFromFileDialog);
            miInsertImages = MI("Insert &Images…", null, () => ImportImages(null, true, PastePoint(false)));
            miExtract = MI("E&xtract Pages to New Tab", null, ExtractPages);
            miSelectAllPages = MI("Select &All Pages", "Ctrl+A", () => PageAction(() => organizer.SelectAll()));
            pagesM.DropDownItems.AddRange(new ToolStripItem[]
            {
                miCut, miCopy, miPaste, miPasteBefore, miDelete, miDuplicate, new ToolStripSeparator(), miPRotL, miPRotR,
                miMoveEarlier, miMoveLater, new ToolStripSeparator(), miInsertBlank, miInsertFile, miInsertImages, new ToolStripSeparator(),
                miExtract, miSelectAllPages,
            });

            var help = new ToolStripMenuItem("&Help");
            var palette = MI("&Command Palette…", "Ctrl+Shift+P", ShowCommandPalette);
            palette.Tag = "palette";
            help.DropDownItems.Add(palette);
            help.DropDownItems.AddRange(new ToolStripItem[] { MI("&Keyboard Shortcuts", "F1", () => Dialogs.Shortcuts(this)), MI("&About Gravity", null, ShowAbout) });

            menu.Items.AddRange(new ToolStripItem[] { file, edit, viewM, go, pagesM, help });
            foreach (ToolStripMenuItem top in menu.Items) top.DropDownOpening += delegate { UpdateUI(); };
        }

        private void BuildContextMenus()
        {
            viewMenu = new ContextMenuStrip { Renderer = new FlatRenderer() };
            var vCopy = MI("Copy Text", "Ctrl+C", CopyText);
            var vSelAll = MI("Select All Text", "Ctrl+A", () => view.SelectAll());
            var vPaste = MI("Paste Pages After This Page", "Ctrl+V", () => PastePages(false));
            viewMenu.Items.AddRange(new ToolStripItem[]
            {
                vCopy, vSelAll, new ToolStripSeparator(),
                MI("Organize Pages", "Ctrl+E", ToggleOrganize), MI("Go to Page…", "Ctrl+G", FocusPageBox), new ToolStripSeparator(),
                MI("Cut This Page", null, CutPages), MI("Copy This Page", null, CopyPages), vPaste, MI("Delete This Page", null, DeletePages),
                MI("Rotate This Page Left", "Ctrl+[", () => RotatePages(-1)), MI("Rotate This Page Right", "Ctrl+]", () => RotatePages(1)),
            });
            viewMenu.Opening += delegate
            {
                vCopy.Enabled = view.HasSelection;
                vPaste.Enabled = clipboard.Count > 0;
            };
            view.ContextMenuStrip = viewMenu;

            organizerMenu = new ContextMenuStrip { Renderer = new FlatRenderer() };
            var oPaste = MI("Paste After", "Ctrl+V", () => PastePages(false));
            var oPasteBefore = MI("Paste Before", "Ctrl+Shift+V", () => PastePages(true));
            organizerMenu.Items.AddRange(new ToolStripItem[]
            {
                MI("Cut", "Ctrl+X", CutPages), MI("Copy", "Ctrl+C", CopyPages), oPaste, oPasteBefore, MI("Delete", "Del", DeletePages),
                MI("Duplicate", "Ctrl+D", DuplicatePages), new ToolStripSeparator(),
                MI("Rotate Left", "[", () => RotatePages(-1)), MI("Rotate Right", "]", () => RotatePages(1)), new ToolStripSeparator(),
                MI("Insert Blank Page", null, InsertBlank), MI("Insert Pages from File…", null, InsertFromFileDialog), new ToolStripSeparator(),
                MI("Extract to New Tab", null, ExtractPages), MI("Save Pages As…", null, SaveSelectedPagesAs), new ToolStripSeparator(),
                MI("Select All", "Ctrl+A", () => organizer.SelectAll()), MI("Open in Reading View", "Enter", () =>
                {
                    int f = organizer.FocusIndex;
                    SetOrganize(false);
                    if (f >= 0) view.GoToPage(f, false);
                    view.Focus();
                }),
            });
            organizerMenu.Opening += delegate
            {
                oPaste.Enabled = oPasteBefore.Enabled = clipboard.Count > 0;
            };
            organizer.ContextMenuStrip = organizerMenu;
        }

        private void RestoreWindow()
        {
            var b = settings.WindowBounds;
            bool onScreen = !b.IsEmpty && Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(new Rectangle(b.X + 40, b.Y + 10, Math.Max(1, b.Width - 80), 40)));
            if (onScreen)
            {
                StartPosition = FormStartPosition.Manual;
                Bounds = b;
                if (settings.Maximized) WindowState = FormWindowState.Maximized;
            }
            else
            {
                var wa = Screen.PrimaryScreen.WorkingArea;
                Size = new Size(Math.Min(S(1180), wa.Width - S(40)), Math.Min(S(860), wa.Height - S(40)));
                StartPosition = FormStartPosition.CenterScreen;
            }
        }

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string appName, string idList);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, string lParam);

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            SendMessage(findBox.TextBox.Handle, 0x1501, (IntPtr)1, "Find");
            home.SetRecent(settings.Recent);
            List<string> files;
            int page;
            ParseArgs(startupArgs, out files, out page);
            if (files.Count > 0) OpenFiles(files, page);
            UpdateUI();
            if (ScreenshotPath != null) BeginScreenshot();
        }

        public static void ParseArgs(string[] args, out List<string> files, out int page)
        {
            files = new List<string>();
            page = -1;
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if ((a == "-page" || a == "--page") && i + 1 < args.Length)
                {
                    int p;
                    if (int.TryParse(args[++i], out p)) page = p - 1;
                }
                else if (a == "--screenshot" || a == "--size") i++;
                else if (a.StartsWith("--")) { }
                else files.Add(a);
            }
        }

        /// <summary>Called (on the UI thread) when another launch of Gravity forwards its arguments.</summary>
        public void OpenFromOtherInstance(string[] args)
        {
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            List<string> files;
            int page;
            ParseArgs(args, out files, out page);
            if (files.Count > 0) OpenFiles(files, page);
            Activate();
            BringToFront();
        }

        // =====================================================================
        // Tabs and documents
        // =====================================================================

        private bool Organizing { get { return active != null && active.Organize; } }

        private void OpenDialog()
        {
            using (var dlg = new OpenFileDialog { Filter = "PDF files (*.pdf)|*.pdf|All files (*.*)|*.*", Multiselect = true, Title = "Open" })
            {
                if (active != null && active.Doc.FilePath != null) dlg.InitialDirectory = Path.GetDirectoryName(active.Doc.FilePath);
                if (dlg.ShowDialog(this) == DialogResult.OK) OpenFiles(dlg.FileNames, -1);
            }
        }

        public void OpenFiles(IEnumerable<string> paths, int page)
        {
            DocTab last = null;
            foreach (var raw in paths)
            {
                string path;
                try { path = Path.GetFullPath(raw.Trim('"')); }
                catch (Exception) { continue; }
                var existing = tabs.FirstOrDefault(t => t.Doc.FilePath != null && string.Equals(t.Doc.FilePath, path, StringComparison.OrdinalIgnoreCase));
                if (existing != null) { last = existing; continue; }
                if (!File.Exists(path))
                {
                    MessageBox.Show(this, "The file could not be found:\n" + path, "Gravity", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    continue;
                }
                PdfSource src;
                if (Preloaded.TryGetValue(path, out src)) Preloaded.Remove(path);
                else src = LoadSource(path);
                if (src == null) continue;
                var tab = new DocTab { Doc = new EditDoc(src), View = settings.StateFor(path) };
                AddTab(tab);
                last = tab;
            }
            if (last != null)
            {
                ActivateTab(last);
                if (page >= 0) view.GoToPage(page, false);
            }
            home.SetRecent(settings.Recent);
        }

        /// <summary>Loads a PDF, asking for a password if needed. Returns null (after telling the user) on failure.</summary>
        private PdfSource LoadSource(string path)
        {
            string pw = null;
            while (true)
            {
                try
                {
                    Cursor = Cursors.WaitCursor;
                    return PdfSource.Open(path, pw);
                }
                catch (PdfPasswordException ex)
                {
                    Cursor = Cursors.Default;
                    pw = Dialogs.Password(this, Path.GetFileName(path), ex.WasWrong);
                    if (pw == null) return null;
                }
                catch (Exception ex)
                {
                    Cursor = Cursors.Default;
                    MessageBox.Show(this, "Could not open “" + Path.GetFileName(path) + "”.\n\n" + ex.Message, "Gravity", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return null;
                }
                finally
                {
                    Cursor = Cursors.Default;
                }
            }
        }

        private List<PageRef> LoadPages(IEnumerable<string> paths)
        {
            var refs = new List<PageRef>();
            foreach (var p in paths)
            {
                var src = LoadSource(p);
                if (src != null) refs.AddRange(EditDoc.AllPages(src));
            }
            return refs;
        }

        private void AddTab(DocTab tab)
        {
            tab.Doc.Changed += OnDocChanged;
            tabs.Add(tab);
            WatchFile(tab);
            if (tab.Doc.FilePath != null) settings.Remember(tab.Doc.FilePath, tab.View);
            UpdateTabs();
        }

        private void SaveActiveState()
        {
            if (active == null) return;
            active.View = view.GetState();
            active.OrganizerFocus = organizer.FocusIndex;
        }

        private void ActivateTab(DocTab tab)
        {
            if (tab == active && tab != null) { UpdateUI(); return; }
            SaveActiveState();
            CloseFind();
            pageSelectionActive = false;
            active = tab;
            ShowMode();
            if (tab == null)
            {
                view.SetDocument(null, null);
                organizer.SetDocument(null, 0);
            }
            else
            {
                view.SetDocument(tab.Doc, tab.View);
                organizer.SetDocument(tab.Doc, tab.Organize ? tab.OrganizerFocus : tab.View.Page);
            }
            LoadToc();
            ApplySidebar();
            UpdateUI();
            FocusCanvas();
        }

        private void ShowMode()
        {
            bool has = active != null, org = has && active.Organize;
            var parent = org ? host : pageBody;
            if (organizer.Parent != parent) { parent.Controls.Add(organizer); parent.Controls.SetChildIndex(organizer, 0); }
            organizer.SidebarMode = !org;
            home.Visible = !has;
            if (!has || org) CloseFind();
            view.Visible = has && !org;
            pagePanel.Visible = has && !org && !fullscreen && settings.ShowPages && settings.ShowBookmarks;
            pageRail.Visible = false;
            pageSplitter.Visible = false;
            organizer.Visible = org || pagePanel.Visible;
            ApplySidebar();
            // Drop queued renders for whichever view is now hidden.
            if (!view.Visible) renderer.Request(RenderService.ClientView, new List<TileKey>());
            if (!organizer.Visible) renderer.Request(RenderService.ClientThumbs, new List<TileKey>());
        }

        private void FocusCanvas()
        {
            if (Organizing) organizer.Focus();
            else if (view.Visible) view.Focus();
            else home.Focus();
        }

        private void CycleTab(int dir)
        {
            if (tabs.Count < 2 || active == null) return;
            int i = (tabs.IndexOf(active) + dir + tabs.Count) % tabs.Count;
            ActivateTab(tabs[i]);
        }

        private bool CloseTab(DocTab tab)
        {
            if (tab == null) return true;
            if (tab.Doc.IsModified)
            {
                if (tab != active) ActivateTab(tab);
                var r = MessageBox.Show(this, "Save changes to “" + tab.Doc.DisplayName + "” before closing?", "Gravity",
                    MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                if (r == DialogResult.Cancel) return false;
                if (r == DialogResult.Yes && !Save(tab)) return false;
            }
            if (tab == active) SaveActiveState();
            if (tab.Doc.FilePath != null && File.Exists(tab.Doc.FilePath)) settings.Remember(tab.Doc.FilePath, tab.View);
            StopWatching(tab);
            tab.Doc.Changed -= OnDocChanged;
            int idx = tabs.IndexOf(tab);
            tabs.RemoveAt(idx);
            if (tab == active)
            {
                active = null;
                ActivateTab(tabs.Count > 0 ? tabs[Math.Min(idx, tabs.Count - 1)] : null);
            }
            UpdateTabs();
            CollectSources();
            home.SetRecent(settings.Recent);
            UpdateUI();
            return true;
        }

        private void CollectSources()
        {
            var live = new HashSet<PdfSource>();
            foreach (var t in tabs) foreach (var s in t.Doc.Sources()) live.Add(s);
            foreach (var p in clipboard) if (p.Src != null) live.Add(p.Src);
            foreach (var dead in PdfSource.Collect(live)) renderer.Purge(dead);
        }

        private void OnDocChanged(object sender, EventArgs e)
        {
            var doc = (EditDoc)sender;
            if (active == null || doc != active.Doc) { UpdateTabs(); return; }
            if (searchPanel.Visible) QueueFind();
            syncingPages = true;
            try
            {
                view.OnDocumentChanged();
                organizer.OnDocumentChanged(pendingSelection);
                if (PageTargets && !Organizing && organizer.FocusIndex >= 0)
                    view.GoToPage(organizer.FocusIndex, false);
            }
            finally { syncingPages = false; }
            pendingSelection = null;
            SyncPageSelection();
            if (doc.Base != tocBase) { LoadToc(); ApplySidebar(); }
            UpdateUI();
        }

        private void UpdateTabs()
        {
            tabStrip.Tabs.Clear();
            foreach (var t in tabs)
                tabStrip.Tabs.Add(new TabStrip.Tab
                {
                    Title = t.Doc.DisplayName,
                    ToolTip = t.Doc.FilePath ?? t.Doc.DisplayName + " (not saved yet)",
                    Modified = t.Doc.IsModified,
                });
            tabStrip.SelectedIndex = active != null ? tabs.IndexOf(active) : -1;
            tabStrip.Visible = tabs.Count > 1 && !fullscreen;
            tabStrip.Invalidate();
        }

        private void FillRecentMenu()
        {
            miRecent.DropDownItems.Clear();
            int n = 0;
            foreach (var r in settings.Recent)
            {
                if (n >= 15) break;
                string path = r.Path;
                var item = new ToolStripMenuItem((n < 9 ? "&" + (n + 1) + "  " : "    ") + Path.GetFileName(path)) { ToolTipText = path };
                item.Enabled = File.Exists(path);
                item.Click += delegate { OpenFiles(new[] { path }, -1); };
                miRecent.DropDownItems.Add(item);
                n++;
            }
            if (n == 0) miRecent.DropDownItems.Add(new ToolStripMenuItem("(no recent files)") { Enabled = false });
            else
            {
                miRecent.DropDownItems.Add(new ToolStripSeparator());
                miRecent.DropDownItems.Add("Clear list", null, delegate { settings.Recent.Clear(); home.SetRecent(settings.Recent); });
            }
        }

        // =====================================================================
        // Organize mode and page operations
        // =====================================================================

        private void ToggleOrganize() { SetOrganize(!Organizing); }

        private void SetOrganize(bool on)
        {
            if (active == null || presentationMode || on == active.Organize) return;
            if (on)
            {
                int page = view.CurrentPage;
                active.Organize = true;
                ShowMode();
                organizer.SetDocument(active.Doc, page);
            }
            else
            {
                int focus = organizer.FocusIndex;
                active.Organize = false;
                ShowMode();
                if (focus >= 0 && focus != view.CurrentPage) view.GoToPage(focus, false);
            }
            UpdateUI();
            FocusCanvas();
        }

        private List<PageRef> Pages { get { return active != null ? active.Doc.Pages : new List<PageRef>(); } }

        /// <summary>Use the active thumbnail selection, or the current reading page.</summary>
        private List<int> Targets()
        {
            if (active == null || active.Doc.Pages.Count == 0) return new List<int>();
            return PageTargets ? organizer.SelectedIndices() : new List<int> { view.CurrentPage };
        }

        private int PastePoint(bool before)
        {
            int n = Pages.Count;
            if (PageTargets) return organizer.InsertionPoint(before);
            if (n == 0) return 0;
            return Math.Max(0, Math.Min(n, view.CurrentPage + (before ? 0 : 1)));
        }

        private static string Count(int n) { return n == 1 ? "1 page" : n + " pages"; }

        private void ApplyEdit(List<PageRef> newPages, List<PageRef> select, string message)
        {
            if (active == null) return;
            pendingSelection = select;
            active.Doc.SetPages(newPages);
            if (message != null) Toast(message);
        }

        private static List<PageRef> Without(List<PageRef> pages, List<int> indices)
        {
            var drop = new HashSet<int>(indices);
            var list = new List<PageRef>(pages.Count);
            for (int i = 0; i < pages.Count; i++) if (!drop.Contains(i)) list.Add(pages[i]);
            return list;
        }

        private void SetClipboard(List<PageRef> pages)
        {
            clipboard = pages;
            CollectSources();
        }

        private void CutPages()
        {
            var idx = Targets();
            if (idx.Count == 0) return;
            if (idx.Count >= Pages.Count) { Toast("A PDF needs at least one page — use Copy instead."); return; }
            SetClipboard(idx.Select(i => Pages[i]).ToList());
            ApplyEdit(Without(Pages, idx), null, Count(idx.Count) + " cut — paste with Ctrl+V, here or in another tab");
        }

        private void CopyPages()
        {
            var idx = Targets();
            if (idx.Count == 0) return;
            SetClipboard(idx.Select(i => Pages[i]).ToList());
            Toast(Count(idx.Count) + " copied — paste with Ctrl+V, here or in another tab");
            UpdateUI();
        }

        private void PastePages(bool before)
        {
            if (active == null) return;
            if (clipboard.Count == 0) { Toast("Nothing to paste — cut or copy some pages first."); return; }
            int at = PastePoint(before);
            var clones = clipboard.Select(p => p.Clone()).ToList();
            var list = new List<PageRef>(Pages);
            list.InsertRange(at, clones);
            ApplyEdit(list, clones, Count(clones.Count) + " pasted");
        }

        private void DeletePages()
        {
            var idx = Targets();
            if (idx.Count == 0) return;
            if (idx.Count >= Pages.Count) { Toast("A PDF needs at least one page."); return; }
            ApplyEdit(Without(Pages, idx), null, Count(idx.Count) + " deleted (Ctrl+Z to undo)");
        }

        private void DuplicatePages()
        {
            var idx = Targets();
            if (idx.Count == 0) return;
            var clones = idx.Select(i => Pages[i].Clone()).ToList();
            var list = new List<PageRef>(Pages);
            list.InsertRange(idx[idx.Count - 1] + 1, clones);
            ApplyEdit(list, clones, Count(clones.Count) + " duplicated");
        }

        private void RotatePages(int quarterTurns)
        {
            var idx = Targets();
            if (idx.Count == 0) return;
            var set = new HashSet<int>(idx);
            var list = new List<PageRef>(Pages.Count);
            var sel = new List<PageRef>();
            for (int i = 0; i < Pages.Count; i++)
            {
                if (!set.Contains(i)) { list.Add(Pages[i]); continue; }
                var r = Pages[i].Rotated(quarterTurns);
                list.Add(r);
                sel.Add(r);
            }
            ApplyEdit(list, sel, Count(idx.Count) + " rotated");
        }

        private void MovePages(List<PageRef> moving, int at)
        {
            if (active == null || moving.Count == 0) return;
            var set = new HashSet<PageRef>(moving, RefComparer<PageRef>.Instance);
            var pages = Pages;
            at = Math.Max(0, Math.Min(pages.Count, at));
            var list = pages.Take(at).Where(p => !set.Contains(p))
                .Concat(moving)
                .Concat(pages.Skip(at).Where(p => !set.Contains(p))).ToList();
            bool same = true;
            for (int i = 0; i < list.Count; i++) if (!ReferenceEquals(list[i], pages[i])) { same = false; break; }
            if (same) return;
            ApplyEdit(list, moving, Count(moving.Count) + " moved");
        }

        private void MoveBy(int dir)
        {
            var idx = Targets();
            if (idx.Count == 0) return;
            if (dir < 0 && idx[0] == 0) return;
            if (dir > 0 && idx[idx.Count - 1] == Pages.Count - 1) return;
            var moving = idx.Select(i => Pages[i]).ToList();
            MovePages(moving, dir < 0 ? idx[0] - 1 : idx[idx.Count - 1] + 2);
        }

        private void InsertBlank()
        {
            if (active == null) return;
            int at = PastePoint(false);
            double w = 612, h = 792;
            if (Pages.Count > 0)
            {
                var near = Pages[Math.Max(0, Math.Min(Pages.Count - 1, at - 1))];
                w = near.BaseWidth;
                h = near.BaseHeight;
                if ((near.Rotation & 1) == 1) { var t = w; w = h; h = t; }
            }
            var blank = PageRef.Blank(w, h);
            var list = new List<PageRef>(Pages);
            list.Insert(at, blank);
            ApplyEdit(list, new List<PageRef> { blank }, "Blank page inserted");
        }

        private void InsertFromFileDialog()
        {
            if (active == null) return;
            using (var dlg = new OpenFileDialog { Filter = "PDF files (*.pdf)|*.pdf", Multiselect = true, Title = "Insert pages from" })
                if (dlg.ShowDialog(this) == DialogResult.OK) InsertFiles(dlg.FileNames, PastePoint(false));
        }

        private void InsertFiles(string[] files, int at)
        {
            if (files.Any(ImageImport.Supports))
            {
                if (files.All(ImageImport.Supports)) { ImportImages(files, active != null, at); return; }
                Toast("Drop PDFs and images separately to choose image page sizes.");
                return;
            }
            if (active == null) { OpenFiles(files, -1); return; }
            var refs = LoadPages(files);
            if (refs.Count == 0) return;
            var list = new List<PageRef>(Pages);
            at = Math.Max(0, Math.Min(list.Count, at));
            list.InsertRange(at, refs);
            ApplyEdit(list, refs, Count(refs.Count) + " inserted from " + (files.Length == 1 ? Path.GetFileName(files[0]) : files.Length + " files"));
        }

        private void ExtractPages()
        {
            var idx = Targets();
            if (idx.Count == 0) return;
            var clones = idx.Select(i => Pages[i].Clone()).ToList();
            string baseName = Path.GetFileNameWithoutExtension(active.Doc.DisplayName);
            var tab = new DocTab { Doc = new EditDoc(clones, baseName + " (extract).pdf"), View = new ViewState { Mode = settings.DefaultZoom } };
            AddTab(tab);
            ActivateTab(tab);
            Toast(Count(clones.Count) + " extracted to a new tab — Ctrl+S to save it");
        }

        private void SaveSelectedPagesAs()
        {
            var idx = Targets();
            if (idx.Count == 0) return;
            string baseName = Path.GetFileNameWithoutExtension(active.Doc.DisplayName);
            using (var dlg = new SaveFileDialog { Filter = "PDF files (*.pdf)|*.pdf", DefaultExt = "pdf", AddExtension = true, FileName = baseName + " (pages).pdf", Title = "Save " + Count(idx.Count) + " as" })
            {
                if (active.Doc.FilePath != null) dlg.InitialDirectory = Path.GetDirectoryName(active.Doc.FilePath);
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                var tmp = new EditDoc(idx.Select(i => Pages[i].Clone()).ToList(), "pages");
                Cursor = Cursors.WaitCursor;
                try { tmp.WriteFile(dlg.FileName); }
                finally { Cursor = Cursors.Default; }
                Toast("Saved " + Count(idx.Count) + " to " + Path.GetFileName(dlg.FileName));
            }
        }

        private void CombineFiles()
        {
            var files = Dialogs.Combine(this, null);
            if (files == null) return;
            var refs = LoadPages(files);
            if (refs.Count == 0) return;
            var tab = new DocTab { Doc = new EditDoc(refs, "Combined.pdf"), View = new ViewState { Mode = settings.DefaultZoom }, Organize = true };
            AddTab(tab);
            ActivateTab(tab);
            Toast("Combined " + files.Count + " files into " + Count(refs.Count) + " — Ctrl+S to save");
        }

        private void Undo()
        {
            if (active == null) return;
            if (active.Doc.Undo()) Toast("Undone");
        }

        private void Redo()
        {
            if (active == null) return;
            if (active.Doc.Redo()) Toast("Redone");
        }

        // =====================================================================
        // Saving and printing
        // =====================================================================

        private bool Save(DocTab tab)
        {
            if (tab.Doc.FilePath == null) return SaveAs(tab);
            if (!tab.Doc.IsModified) { Toast("No changes to save"); return true; }
            return SaveTo(tab, tab.Doc.FilePath);
        }

        private bool SaveAs(DocTab tab)
        {
            if (tab.Doc.Pages.Count == 0) return false;
            using (var dlg = new SaveFileDialog { Filter = "PDF files (*.pdf)|*.pdf", DefaultExt = "pdf", AddExtension = true, OverwritePrompt = true, FileName = tab.Doc.DisplayName, Title = "Save as" })
            {
                if (tab.Doc.FilePath != null) dlg.InitialDirectory = Path.GetDirectoryName(tab.Doc.FilePath);
                if (dlg.ShowDialog(this) != DialogResult.OK) return false;
                return SaveTo(tab, dlg.FileName);
            }
        }

        private bool SaveTo(DocTab tab, string path)
        {
            var oldPages = tab.Doc.Pages;
            Cursor = Cursors.WaitCursor;
            StopWatching(tab);
            try
            {
                tab.Doc.SaveAs(path);
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                WatchFile(tab);
                MessageBox.Show(this, "Could not save “" + Path.GetFileName(path) + "”.\n\n" + ex.Message +
                    "\n\nYour edits are still open. Close any application using the destination, then retry, or use Save As to choose another file.", "Gravity", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            Cursor = Cursors.Default;
            RemapCache(oldPages, tab.Doc.Pages);
            WatchFile(tab);
            settings.Remember(path, tab == active ? view.GetState() : tab.View);
            if (tab == active) Toast("Saved " + Path.GetFileName(path));
            UpdateUI();
            return true;
        }

        /// <summary>After a save the pages come from a new source; reuse already-rendered tiles to avoid a flash.</summary>
        private void RemapCache(List<PageRef> oldPages, List<PageRef> newPages)
        {
            if (oldPages.Count != newPages.Count) return;
            var map = new Dictionary<Tuple<PdfSource, int>, int>();
            for (int k = 0; k < oldPages.Count; k++)
            {
                var p = oldPages[k];
                if (p.Src == null) continue;
                var key = Tuple.Create(p.Src, p.Index);
                if (!map.ContainsKey(key)) map[key] = k;
            }
            renderer.Rekey(delegate (TileKey t, out TileKey moved)
            {
                moved = default(TileKey);
                int k;
                if (t.Src == null || !map.TryGetValue(Tuple.Create(t.Src, t.Index), out k)) return false;
                var np = newPages[k];
                moved = new TileKey(np.Src, np.Index, (t.Rot - oldPages[k].Rotation) & 3, t.W, t.H, t.TX, t.TY);
                return true;
            });
        }

        private void Print()
        {
            if (active == null || active.Doc.Pages.Count == 0) return;
            var pages = active.Doc.Pages;
            int n = pages.Count;
            using (var pd = new PrintDocument())
            using (var dlg = new PrintDialog { Document = pd, AllowSomePages = true, AllowCurrentPage = true, UseEXDialog = true })
            {
                pd.DocumentName = active.Doc.DisplayName;
                pd.PrinterSettings.MinimumPage = 1;
                pd.PrinterSettings.MaximumPage = n;
                pd.PrinterSettings.FromPage = 1;
                pd.PrinterSettings.ToPage = n;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                var ps = pd.PrinterSettings;
                int from = 0, to = n - 1;
                if (ps.PrintRange == PrintRange.SomePages) { from = Math.Max(1, ps.FromPage) - 1; to = Math.Min(n, ps.ToPage) - 1; }
                else if (ps.PrintRange == PrintRange.CurrentPage) from = to = Organizing ? Math.Max(0, organizer.FocusIndex) : view.CurrentPage;
                if (to < from) return;
                int next = from;
                pd.QueryPageSettings += delegate (object s, QueryPageSettingsEventArgs e)
                {
                    double w, h;
                    DisplaySize(pages[next], out w, out h);
                    e.PageSettings.Landscape = w > h;
                };
                pd.PrintPage += delegate (object s, PrintPageEventArgs e)
                {
                    var p = pages[next];
                    double w, h;
                    DisplaySize(p, out w, out h);
                    var area = e.Graphics.VisibleClipBounds;  // hundredths of an inch
                    double pw = w / 72 * 100, ph = h / 72 * 100;
                    double sc = Math.Min(area.Width / pw, area.Height / ph);
                    float dw = (float)(pw * sc), dh = (float)(ph * sc);
                    var dest = new RectangleF(area.X + (area.Width - dw) / 2, area.Y + (area.Height - dh) / 2, dw, dh);
                    if (p.Src != null)
                    {
                        int res = e.PageSettings.PrinterResolution.X;
                        if (res <= 0 || res > 300) res = 300;
                        if (res < 150) res = 150;
                        using (var bmp = RenderService.RenderPage(p, (int)(dw / 100 * res), (int)(dh / 100 * res), Pdfium.FPDF_ANNOT | Pdfium.FPDF_PRINTING))
                            if (bmp != null) e.Graphics.DrawImage(bmp, dest);
                    }
                    next++;
                    e.HasMorePages = next <= to;
                };
                Cursor = Cursors.WaitCursor;
                try { pd.Print(); }
                catch (Exception ex) { MessageBox.Show(this, "Printing failed.\n\n" + ex.Message, "Gravity", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                finally { Cursor = Cursors.Default; }
            }
        }

        private static void DisplaySize(PageRef p, out double w, out double h)
        {
            w = p.BaseWidth;
            h = p.BaseHeight;
            if ((p.Rotation & 1) == 1) { var t = w; w = h; h = t; }
        }

        // =====================================================================
        // Reading view helpers
        // =====================================================================

        private void CopyText()
        {
            if (Organizing) { CopyPages(); return; }
            string text = view.GetSelectedText();
            if (string.IsNullOrEmpty(text)) return;
            try
            {
                Clipboard.SetText(text);
                Toast("Text copied");
            }
            catch (ExternalException) { Toast("The clipboard is busy — try again."); }
        }

        private void FocusFind()
        {
            if (active == null) return;
            if (Organizing) SetOrganize(false);
            if (presentationMode) TogglePresentation();
            if (fullscreen && !presentationMode) SetFullscreen(false);
            searchPanel.Visible = true;
            UpdateUI();
            searchPanel.FocusQuery();
        }

        private void FindNext(bool forward)
        {
            if (active == null || Organizing) return;
            string q = findBox.Text;
            if (string.IsNullOrEmpty(q)) { FocusFind(); return; }
            searchPanel.Visible = true;
            if (findTimer.Enabled || (!searchPanel.Complete && !searchPanel.Searching)) StartFind(forward);
            else if (searchPanel.Complete) searchPanel.Next(forward);
        }

        private void QueueFind()
        {
            bool typingInToolbar = findBox.Focused;
            findTimer.Stop(); searchPanel.Reset(); view.ClearHit();
            if (active == null || Organizing || string.IsNullOrEmpty(findBox.Text)) return;
            searchPanel.Visible = true; UpdateUI(); findTimer.Start();
            if (typingInToolbar) searchPanel.FocusQuery(false);
        }

        private void StartFind(bool forward)
        {
            findTimer.Stop();
            if (active == null || Organizing || !searchPanel.Visible) return;
            searchPanel.Search(Pages, findBox.Text, bMatchCase.Checked, view.CurrentPage, forward);
        }

        private void CloseFind()
        {
            findTimer.Stop(); searchPanel.Reset(); searchPanel.Visible = false;
            view.ClearHit();
            if (menu != null) UpdateUI();
        }

        private void ShowCommandPalette()
        {
            UpdateUI();
            FillRecentMenu();
            using (var palette = new CommandPalette(PaletteCommand.FromMenu(menu.Items)))
                if (palette.ShowDialog(this) == DialogResult.OK && palette.SelectedCommand != null)
                    palette.SelectedCommand.Item.PerformClick();
        }

        private void ExportPages()
        {
            if (active == null || Pages.Count == 0) return;
            var snapshot = new List<PageRef>(Pages);
            string name = Path.GetFileNameWithoutExtension(active.Doc.DisplayName);
            using (PdfSource.Retain(snapshot))
            using (var options = new ExportDialog(snapshot.Count, view.CurrentPage, Targets()))
            {
                if (options.ShowDialog(this) != DialogResult.OK) return;
                string destination;
                if (options.Format == 2)
                {
                    using (var save = new SaveFileDialog { Filter = "Text files (*.txt)|*.txt", DefaultExt = "txt", AddExtension = true, OverwritePrompt = true,
                        FileName = DocumentExport.SafeStem(name) + ".txt", Title = "Export text" })
                    {
                        if (save.ShowDialog(this) != DialogResult.OK) return;
                        destination = save.FileName;
                    }
                }
                else
                {
                    using (var folder = new FolderBrowserDialog { Description = "Choose a folder for the exported page images" })
                    {
                        if (folder.ShowDialog(this) != DialogResult.OK) return;
                        destination = folder.SelectedPath;
                    }
                }
                int format = options.Format, resolution = options.Resolution;
                var indices = options.Indices;
                {
                    bool done = WorkDialog.Run(this, "Export pages", (cancel, progress) =>
                    {
                        if (format == 2) DocumentExport.Text(snapshot, indices, destination, cancel, progress);
                        else DocumentExport.Images(snapshot, indices, destination, name, resolution, format == 1, cancel, progress);
                    });
                    Toast(done ? "Exported " + Count(indices.Count) : format == 2 ? "Text export cancelled. The destination was not changed." : "Export cancelled. Completed images remain in the export folder.");
                }
            }
        }

        private void ImportImages(IEnumerable<string> initial, bool insert, int at)
        {
            if (insert && active == null) return;
            var target = active;
            using (var options = new ImagesDialog(initial, insert))
            {
                if (options.ShowDialog(this) != DialogResult.OK) return;
                var files = options.Files; var layout = options.Options;
                byte[] bytes = null;
                if (!WorkDialog.Run(this, "Import images", (cancel, progress) => bytes = ImageImport.Create(files, layout, cancel, progress))) return;
                var source = PdfSource.FromBytes(bytes, null);
                var refs = Enumerable.Range(0, source.PageCount).Select(i => new PageRef(source, i, 0)).ToList();
                if (insert && target == active)
                {
                    var list = new List<PageRef>(Pages);
                    list.InsertRange(Math.Max(0, Math.Min(at, list.Count)), refs);
                    ApplyEdit(list, refs, Count(refs.Count) + " inserted from images");
                }
                else
                {
                    var tab = new DocTab { Doc = new EditDoc(refs, "Images.pdf"), View = new ViewState { Mode = settings.DefaultZoom }, Organize = true };
                    AddTab(tab); ActivateTab(tab);
                    Toast("Created " + Count(refs.Count) + " — reorder pages here, then Ctrl+S to save");
                }
            }
        }

        private void FocusPageBox()
        {
            if (active == null) return;
            if (Organizing) SetOrganize(false);
            if (!toolbar.Visible)
            {
                var s = Dialogs.Input(this, "Go to page", "Page number (1–" + Pages.Count + "):", (view.CurrentPage + 1).ToString());
                int n;
                if (s != null && int.TryParse(s.Trim(), out n)) view.GoToPage(n - 1);
                return;
            }
            pageBox.Focus();
            pageBox.SelectAll();
        }

        private void ApplyZoomText(string text)
        {
            if (active == null) return;
            text = (text ?? "").Trim();
            if (text.Equals("Fit Page", StringComparison.OrdinalIgnoreCase)) view.SetZoomMode(ZoomMode.FitPage);
            else if (text.Equals("Fit Width", StringComparison.OrdinalIgnoreCase)) view.SetZoomMode(ZoomMode.FitWidth);
            else if (text.Equals("Actual Size", StringComparison.OrdinalIgnoreCase)) view.SetZoom(1);
            else
            {
                double z;
                if (double.TryParse(text.TrimEnd('%', ' '), out z) && z > 0) view.SetZoom(z / 100.0);
            }
            UpdateUI();
        }

        private string ZoomText()
        {
            switch (view.Mode)
            {
                case ZoomMode.FitPage: return "Fit Page";
                case ZoomMode.FitWidth: return "Fit Width";
                default: return Math.Round(view.Zoom * 100) + "%";
            }
        }

        private void Toast(string text)
        {
            if (Organizing) organizer.ShowToast(text);
            else if (view.Visible) view.ShowToast(text);
        }

        // ---- bookmarks -------------------------------------------------------

        private void LoadToc()
        {
            outlineVersion = -1;
            toc.BeginUpdate();
            toc.Nodes.Clear();
            tocBase = active != null ? active.Doc.Base : null;
            if (tocBase != null)
            {
                List<OutlineItem> items;
                lock (Pdfium.Lock) items = tocBase.GetOutline();
                int budget = 20000;
                AddNodes(toc.Nodes, items, ref budget);
                if (toc.Nodes.Count <= 12) foreach (TreeNode n in toc.Nodes) n.Expand();
            }
            toc.EndUpdate();
            bool any = toc.Nodes.Count > 0;
            toc.Visible = any;
            tocEmpty.Visible = !any;
        }

        private static void AddNodes(TreeNodeCollection into, List<OutlineItem> items, ref int budget)
        {
            foreach (var item in items)
            {
                if (budget-- <= 0) return;
                var node = new TreeNode(string.IsNullOrEmpty(item.Title) ? "(untitled)" : item.Title) { Tag = item };
                into.Add(node);
                if (item.Children.Count > 0) AddNodes(node.Nodes, item.Children, ref budget);
            }
        }

        private void NavigateToc(TreeNode node)
        {
            var item = node != null ? node.Tag as OutlineItem : null;
            if (item == null || active == null) return;
            if (Organizing) SetOrganize(false);
            view.Navigate(item);
        }

        private void ToggleSidebar()
        {
            if (active == null) return;
            settings.ShowBookmarks = !settings.ShowBookmarks;
            ShowMode();
            UpdateUI();
        }

        private void ApplySidebar()
        {
            if (content == null || layingOutPanels) return;
            layingOutPanels = true;
            try
            {
                int available = content.ClientSize.Width;
                int limit = available < S(600) ? 160 : available < S(760) ? 190 : available < S(1000) ? 210 : 400;
                sidebar.Width = Math.Min(S(Math.Max(160, Math.Min(limit, settings.SidebarWidth))), Math.Max(S(160), available - S(300)));
                bool show = settings.ShowBookmarks && active != null && !fullscreen && available >= S(420);
                sidebar.Visible = show;
                splitter.Visible = show;
                pagePanel.Visible = show && !Organizing && settings.ShowPages;
                pageRail.Visible = pageSplitter.Visible = false;
                toc.Visible = show && !pagePanel.Visible && toc.Nodes.Count > 0;
                tocEmpty.Visible = show && !pagePanel.Visible && toc.Nodes.Count == 0;
                organizer.Visible = Organizing || pagePanel.Visible;
            }
            finally { layingOutPanels = false; }
        }

        // ---- full screen / presentation -------------------------------------------

        private void ToggleFullscreen()
        {
            if (presentationMode) { TogglePresentation(); return; }
            SetFullscreen(!fullscreen);
        }

        private void SetFullscreen(bool on)
        {
            if (on == fullscreen) return;
            SuspendLayout();
            if (on)
            {
                savedBorder = FormBorderStyle;
                savedState = WindowState;
                savedBounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
                fullscreen = true;
                menu.Visible = toolbar.Visible = tabStrip.Visible = false;
                ApplySidebar();
                if (WindowState != FormWindowState.Normal) WindowState = FormWindowState.Normal;
                FormBorderStyle = FormBorderStyle.None;
                Bounds = Screen.FromControl(this).Bounds;
            }
            else
            {
                fullscreen = false;
                FormBorderStyle = savedBorder;
                Bounds = savedBounds;
                WindowState = savedState;
                menu.Visible = toolbar.Visible = true;
                UpdateTabs();
                ApplySidebar();
            }
            ResumeLayout(true);
            ShowMode();
            UpdateUI();
        }

        private void TogglePresentation()
        {
            if (active == null) return;
            if (!presentationMode)
            {
                if (active.Organize) SetOrganize(false);
                presentationMode = true;
                SetFullscreen(true);
                view.ContextMenuStrip = null;
                view.SetPresentation(true);
                view.Focus();
            }
            else
            {
                presentationMode = false;
                view.SetPresentation(false);
                view.ContextMenuStrip = viewMenu;
                SetFullscreen(false);
            }
            UpdateUI();
        }

        // ---- reload when the file changes on disk -----------------------------------

        private void WatchFile(DocTab tab)
        {
            StopWatching(tab);
            string path = tab.Doc.FilePath;
            if (path == null) return;
            try
            {
                var w = new FileSystemWatcher(Path.GetDirectoryName(path), Path.GetFileName(path))
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.CreationTime,
                    SynchronizingObject = this,
                };
                FileSystemEventHandler h = delegate
                {
                    tab.ReloadPending = true;
                    tab.ReloadTries = 0;
                    reloadTimer.Stop();
                    reloadTimer.Start();
                };
                w.Changed += h;
                w.Created += h;
                w.Renamed += (s, e) => h(s, null);
                w.EnableRaisingEvents = true;
                tab.Watcher = w;
            }
            catch (Exception)
            {
                // Watching is best-effort (e.g. some network drives don't support it).
            }
        }

        private static void StopWatching(DocTab tab)
        {
            if (tab.Watcher == null) return;
            tab.Watcher.EnableRaisingEvents = false;
            tab.Watcher.Dispose();
            tab.Watcher = null;
        }

        private void OnReloadTimer(object sender, EventArgs e)
        {
            reloadTimer.Stop();
            bool retry = false;
            foreach (var tab in tabs.ToList())
            {
                if (!tab.ReloadPending) continue;
                string path = tab.Doc.FilePath;
                var b = tab.Doc.Base;
                if (path == null || b == null) { tab.ReloadPending = false; continue; }
                FileInfo fi;
                try
                {
                    fi = new FileInfo(path);
                    if (!fi.Exists) { tab.ReloadPending = false; continue; }
                    if (fi.LastWriteTimeUtc == b.FileStamp && fi.Length == b.FileLength) { tab.ReloadPending = false; continue; }
                }
                catch (Exception) { retry = true; continue; }
                if (tab.Doc.IsModified)
                {
                    tab.ReloadPending = false;
                    if (tab == active) Toast("This file changed on disk. Your unsaved edits are kept.");
                    continue;
                }
                try
                {
                    var fresh = PdfSource.Open(path, b.Password);
                    tab.ReloadPending = false;
                    if (tab == active) SaveActiveState();
                    tab.Doc.Reload(fresh);
                    CollectSources();
                }
                catch (PdfPasswordException) { tab.ReloadPending = false; }
                catch (Exception)
                {
                    // Probably still being written; try again shortly.
                    if (++tab.ReloadTries < 20) retry = true; else tab.ReloadPending = false;
                }
            }
            if (retry) reloadTimer.Start();
        }

        private void ReloadActive()
        {
            if (active == null || active.Doc.FilePath == null) return;
            if (active.Doc.IsModified &&
                MessageBox.Show(this, "Discard your changes and reload the file from disk?", "Gravity", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
                return;
            var fresh = LoadSource(active.Doc.FilePath);
            if (fresh == null) return;
            SaveActiveState();
            active.Doc.Reload(fresh);
            CollectSources();
            Toast("Reloaded");
        }

        // =====================================================================
        // Keyboard
        // =====================================================================

        private bool TextFocused { get { return findBox.Focused || searchPanel.QueryFocused || pageBox.Focused || zoomBox.Focused || (customZoom != null && customZoom.Focused); } }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            try
            {
                if (HandleKey(keyData)) return true;
            }
            catch (Exception ex)
            {
                ShowError(ex);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private bool HandleKey(Keys k)
        {
            bool has = active != null, org = Organizing || (pagePanel.Visible && pageSelectionActive && !view.Focused);
            bool text = TextFocused, tree = toc.Focused;
            switch (k)
            {
                case Keys.Control | Keys.O: OpenDialog(); return true;
                case Keys.Control | Keys.Shift | Keys.P: ShowCommandPalette(); return true;
                case Keys.Control | Keys.S: if (has) Save(active); return true;
                case Keys.Control | Keys.Shift | Keys.S: if (has) SaveAs(active); return true;
                case Keys.Control | Keys.P: Print(); return true;
                case Keys.Control | Keys.W:
                case Keys.Control | Keys.F4: CloseTab(active); return true;
                case Keys.Control | Keys.E: ToggleOrganize(); return true;
                case Keys.Control | Keys.Shift | Keys.E: TogglePagePanel(); return true;
                case Keys.Control | Keys.F: FocusFind(); return true;
                case Keys.Control | Keys.Shift | Keys.F: FillForm(view.CurrentPage, -1); return true;
                case Keys.F3: FindNext(true); return true;
                case Keys.Shift | Keys.F3: FindNext(false); return true;
                case Keys.Control | Keys.G: FocusPageBox(); return true;
                case Keys.F11: ToggleFullscreen(); return true;
                case Keys.F5:
                case Keys.Control | Keys.L: TogglePresentation(); return true;
                case Keys.F12: ToggleSidebar(); return true;
                case Keys.F1: Dialogs.Shortcuts(this); return true;
                case Keys.Control | Keys.Tab:
                case Keys.Control | Keys.PageDown: CycleTab(1); return true;
                case Keys.Control | Keys.Shift | Keys.Tab:
                case Keys.Control | Keys.PageUp: CycleTab(-1); return true;
                case Keys.Escape: return HandleEscape(text || tree);
            }
            if ((k & Keys.Modifiers) == Keys.Alt && (k & Keys.KeyCode) >= Keys.D1 && (k & Keys.KeyCode) <= Keys.D9)
            {
                int n = (k & Keys.KeyCode) == Keys.D9 ? tabs.Count - 1 : (int)((k & Keys.KeyCode) - Keys.D1);
                if (n >= 0 && n < tabs.Count) ActivateTab(tabs[n]);
                return true;
            }
            if (text || tree || searchPanel.ContainsFocus || !has) return false;

            switch (k)
            {
                case Keys.Control | Keys.Z: Undo(); return true;
                case Keys.Control | Keys.Y:
                case Keys.Control | Keys.Shift | Keys.Z: Redo(); return true;
                case Keys.Control | Keys.Shift | Keys.V: PastePages(true); return true;
                case Keys.Control | Keys.OemOpenBrackets: RotatePages(-1); return true;
                case Keys.Control | Keys.OemCloseBrackets: RotatePages(1); return true;
                case Keys.Control | Keys.D: DuplicatePages(); return true;
            }

            if (org)
            {
                switch (k)
                {
                    case Keys.Control | Keys.X: CutPages(); return true;
                    case Keys.Control | Keys.C: CopyPages(); return true;
                    case Keys.Control | Keys.V: PastePages(false); return true;
                    case Keys.Delete:
                    case Keys.Back: DeletePages(); return true;
                    case Keys.Control | Keys.A: organizer.SelectAll(); return true;
                    case Keys.OemOpenBrackets: RotatePages(-1); return true;
                    case Keys.OemCloseBrackets: RotatePages(1); return true;
                    case Keys.Alt | Keys.Left: MoveBy(-1); return true;
                    case Keys.Alt | Keys.Up: MoveBy(-1); return true;
                    case Keys.Alt | Keys.Right: MoveBy(1); return true;
                    case Keys.Alt | Keys.Down: MoveBy(1); return true;
                    case Keys.Oemplus:
                    case Keys.Shift | Keys.Oemplus:
                    case Keys.Add:
                    case Keys.Control | Keys.Oemplus:
                    case Keys.Control | Keys.Add:
                        organizer.ThumbSize = (int)(organizer.ThumbSize * 1.2); return true;
                    case Keys.OemMinus:
                    case Keys.Subtract:
                    case Keys.Control | Keys.OemMinus:
                    case Keys.Control | Keys.Subtract:
                        organizer.ThumbSize = (int)(organizer.ThumbSize / 1.2); return true;
                    case Keys.Control | Keys.D0: organizer.ThumbSize = 168; return true;
                }
                return organizer.HandleKey(k);
            }

            switch (k)
            {
                case Keys.Control | Keys.C: CopyText(); return true;
                case Keys.Control | Keys.A: view.SelectAll(); return true;
                case Keys.Control | Keys.V: PastePages(false); return true;
                case Keys.Alt | Keys.Left: view.GoBack(); return true;
                case Keys.Alt | Keys.Right: view.GoForward(); return true;
                case Keys.Control | Keys.D0:
                case Keys.Control | Keys.NumPad0: view.SetZoomMode(ZoomMode.FitPage); return true;
                case Keys.Control | Keys.D1:
                case Keys.Control | Keys.NumPad1: view.SetZoom(1); return true;
                case Keys.Control | Keys.D2:
                case Keys.Control | Keys.NumPad2: view.SetZoomMode(ZoomMode.FitWidth); return true;
                case Keys.Control | Keys.D6: view.SetLayout(PageLayout.Single); return true;
                case Keys.Control | Keys.D7: view.SetLayout(PageLayout.Facing); return true;
                case Keys.Control | Keys.D8: view.SetLayout(PageLayout.Book); return true;
                case Keys.Oemplus:
                case Keys.Shift | Keys.Oemplus:
                case Keys.Add:
                case Keys.Control | Keys.Oemplus:
                case Keys.Control | Keys.Add:
                    view.ZoomStep(1); return true;
                case Keys.OemMinus:
                case Keys.Subtract:
                case Keys.Control | Keys.OemMinus:
                case Keys.Control | Keys.Subtract:
                    view.ZoomStep(-1); return true;
                case Keys.Z: view.CycleFitMode(); return true;
                case Keys.C: view.SetContinuous(!view.Continuous); return true;
                case Keys.OemOpenBrackets:
                case Keys.Divide:
                case Keys.Control | Keys.Shift | Keys.OemMinus: view.RotateView(-1); return true;
                case Keys.OemCloseBrackets:
                case Keys.Multiply:
                case Keys.Control | Keys.Shift | Keys.Oemplus: view.RotateView(1); return true;
                case Keys.G: FocusPageBox(); return true;
                case Keys.OemQuestion: FocusFind(); return true;
                case Keys.R: ReloadActive(); return true;
            }
            return view.HandleKey(k);
        }

        private bool HandleEscape(bool inText)
        {
            if (presentationMode) { TogglePresentation(); return true; }
            if (fullscreen) { SetFullscreen(false); return true; }
            if (searchPanel.Visible) { CloseFind(); FocusCanvas(); return true; }
            if (inText) { UpdateUI(); FocusCanvas(); return true; }
            if (active != null && !Organizing)
            {
                view.ClearSelection();
                view.ClearHit();
                return true;
            }
            return false;
        }

        // =====================================================================
        // UI state
        // =====================================================================

        private void UpdateUI()
        {
            if (updating) return;
            updating = true;
            try
            {
                bool has = active != null;
                var doc = has ? active.Doc : null;
                bool org = has && active.Organize;
                int n = has ? doc.Pages.Count : 0;
                Text = has ? doc.DisplayName + (doc.IsModified ? " •" : "") + " – Gravity" : "Gravity";
                UpdateTabs();

                foreach (var it in docGroup) it.Visible = has;
                foreach (var it in readGroup) it.Visible = has && !org;
                foreach (var it in editGroup) it.Visible = has && org;
                foreach (var it in findGroup) it.Visible = has && !org && !searchPanel.Visible;
                bool pageTargets = PageTargets;
                int selCount = !has ? 0 : pageTargets ? organizer.SelectedIndices().Count : (n > 0 ? 1 : 0);
                var panelIndices = organizer.SelectedIndices();
                int panelCount = has ? panelIndices.Count : 0;
                pageSelection.Text = panelCount + " selected · " + Count(n);
                pRotateLeft.Enabled = pRotateRight.Enabled = pCopy.Enabled = pDuplicate.Enabled = pExtract.Enabled = pSave.Enabled = panelCount > 0;
                pDelete.Enabled = pCut.Enabled = panelCount > 0 && panelCount < n;
                pPaste.Enabled = has && clipboard.Count > 0;
                pEarlier.Enabled = panelCount > 0 && panelIndices[0] > 0;
                pLater.Enabled = panelCount > 0 && panelIndices[panelCount - 1] < n - 1;
                miPagePanel.Enabled = has && !presentationMode;
                miPagePanel.Checked = has && pagePanel.Visible;

                bSave.Enabled = has && n > 0;
                bPrint.Enabled = has && n > 0;
                bUndo.Enabled = has && doc.CanUndo;
                bRedo.Enabled = has && doc.CanRedo;
                bOrganize.Enabled = has && !presentationMode;
                bOrganize.Checked = org;
                bSidebar.Enabled = has;
                bSidebar.Checked = sidebar.Visible;
                bCut.Enabled = bDelete.Enabled = selCount > 0 && selCount < n;
                bCopy.Enabled = bPRotL.Enabled = bPRotR.Enabled = bExtract.Enabled = selCount > 0;
                bPaste.Enabled = has && clipboard.Count > 0;
                if (has && !org)
                {
                    if (!pageBox.Focused) pageBox.Text = n > 0 ? (view.CurrentPage + 1).ToString() : "";
                    pageOf.Text = "of " + n;
                    if (!zoomBox.Focused) zoomBox.Text = ZoomText();
                    bPrev.Enabled = view.CurrentPage > 0;
                    bNext.Enabled = view.CurrentPage < n - 1;
                }

                miSave.Enabled = bSave.Enabled;
                miSaveAs.Enabled = has && n > 0;
                miExport.Enabled = has && n > 0;
                miSavePages.Enabled = has && selCount > 0;
                miPrint.Enabled = bPrint.Enabled;
                miClose.Enabled = has;
                miUndo.Enabled = bUndo.Enabled;
                miRedo.Enabled = bRedo.Enabled;
                bool read = has && !org;
                miCopyText.Enabled = read && view.HasSelection;
                miSelectAll.Enabled = miFind.Enabled = miFindNext.Enabled = miFindPrev.Enabled = read;
                miOrganize.Enabled = has && !presentationMode;
                miOrganize.Checked = org;
                foreach (var m in new[] { miSingle, miFacing, miBook, miContinuous, miFitPage, miFitWidth, miActual, miZoomIn, miZoomOut, miRotL, miRotR, miNext, miPrev, miFirst, miLast, miGoto })
                    m.Enabled = read && n > 0;
                miSingle.Checked = view.LayoutMode == PageLayout.Single;
                miFacing.Checked = view.LayoutMode == PageLayout.Facing;
                miBook.Checked = view.LayoutMode == PageLayout.Book;
                miContinuous.Checked = view.Continuous;
                miFitPage.Checked = view.Mode == ZoomMode.FitPage;
                miFitWidth.Checked = view.Mode == ZoomMode.FitWidth;
                miActual.Checked = view.Mode == ZoomMode.Custom && Math.Abs(view.Zoom - 1) < 0.001;
                miBookmarks.Enabled = bSidebar.Enabled;
                miBookmarks.Checked = sidebar.Visible;
                miFullscreen.Checked = fullscreen && !presentationMode;
                miPresentation.Checked = presentationMode;
                miFullscreen.Enabled = miPresentation.Enabled = has;
                miBack.Enabled = read && view.CanGoBack;
                miForward.Enabled = read && view.CanGoForward;

                miCut.Enabled = miDelete.Enabled = selCount > 0 && selCount < n;
                miCopy.Enabled = miDuplicate.Enabled = miPRotL.Enabled = miPRotR.Enabled = miExtract.Enabled = selCount > 0;
                miMoveEarlier.Enabled = miMoveLater.Enabled = selCount > 0 && n > 1;
                miPaste.Enabled = miPasteBefore.Enabled = has && clipboard.Count > 0;
                miInsertBlank.Enabled = miInsertFile.Enabled = has;
                miInsertImages.Enabled = has;
                miSelectAllPages.Enabled = org || pagePanel.Visible;
                // Some keys mean different things in the two modes; show them where they apply.
                miCopyText.ShortcutKeyDisplayString = pageTargets ? "" : "Ctrl+C";
                miSelectAll.ShortcutKeyDisplayString = pageTargets ? "" : "Ctrl+A";
                miCopy.ShortcutKeyDisplayString = pageTargets ? "Ctrl+C" : "";
                miCut.ShortcutKeyDisplayString = pageTargets ? "Ctrl+X" : "";
                miDelete.ShortcutKeyDisplayString = pageTargets ? "Del" : "";
                miSelectAllPages.ShortcutKeyDisplayString = pageTargets ? "Ctrl+A" : "";
                miMoveEarlier.ShortcutKeyDisplayString = pageTargets ? "Alt+Left" : "";
                miMoveLater.ShortcutKeyDisplayString = pageTargets ? "Alt+Right" : "";
                miCut.Text = pageTargets ? "Cu&t Pages" : "Cu&t Current Page";
                miCopy.Text = pageTargets ? "&Copy Pages" : "&Copy Current Page";
                miDelete.Text = pageTargets ? "&Delete Pages" : "&Delete Current Page";
                miPaste.Text = pageTargets ? "&Paste Pages After Selection" : "&Paste Pages After Current Page";
                UpdateLucideShell(has, org, n, selCount);
            }
            finally
            {
                updating = false;
            }
        }

        // =====================================================================
        // Misc
        // =====================================================================

        private void OnFileDragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void OnFileDragDrop(object sender, DragEventArgs e)
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null) return;
            BeginInvoke((Action)delegate
            {
                Guard(() =>
                {
                    var images = files.Where(ImageImport.Supports).ToArray();
                    if (images.Length > 0) ImportImages(images, false, 0);
                    var pdfs = files.Where(f => !ImageImport.Supports(f)).ToArray();
                    if (pdfs.Length > 0) OpenFiles(pdfs, -1);
                });
            });
        }

        private void Guard(Action a)
        {
            try { a(); }
            catch (Exception ex) { ShowError(ex); }
        }

        private void ShowError(Exception ex)
        {
            Cursor = Cursors.Default;
            MessageBox.Show(this, ex.Message, "Gravity", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void ShowAbout()
        {
            string ver = typeof(MainForm).Assembly.GetName().Version.ToString(3);
            MessageBox.Show(this,
                "Gravity " + ver + "\n\nA fast, lightweight PDF viewer and page organizer for Windows.\n" +
                "Released under the MIT License.\n\n" +
                "PDF rendering by PDFium (BSD-3-Clause / Apache-2.0).\nSee the licenses folder next to Gravity.exe.",
                "About Gravity", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            foreach (var tab in tabs.ToList())
            {
                if (!tab.Doc.IsModified) continue;
                ActivateTab(tab);
                var r = MessageBox.Show(this, "Save changes to “" + tab.Doc.DisplayName + "” before closing?", "Gravity",
                    MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                if (r == DialogResult.Cancel || (r == DialogResult.Yes && !Save(tab)))
                {
                    e.Cancel = true;
                    return;
                }
            }
            SaveActiveState();
            for (int i = tabs.Count - 1; i >= 0; i--)
                if (tabs[i].Doc.FilePath != null && File.Exists(tabs[i].Doc.FilePath)) settings.Remember(tabs[i].Doc.FilePath, tabs[i].View);
            if (active != null && active.Doc.FilePath != null && File.Exists(active.Doc.FilePath)) settings.Remember(active.Doc.FilePath, active.View);
            var b = fullscreen ? savedBounds : (WindowState == FormWindowState.Normal ? Bounds : RestoreBounds);
            settings.WindowBounds = b;
            settings.Maximized = fullscreen ? savedState == FormWindowState.Maximized : WindowState == FormWindowState.Maximized;
            settings.ThumbSize = organizer.ThumbSize;
            if (ScreenshotPath == null && !Ephemeral) settings.Save();
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            foreach (var t in tabs) StopWatching(t);
            findTimer.Stop(); findTimer.Dispose(); searchPanel.Cancel();
            renderer.Dispose();
            base.OnFormClosed(e);
        }

        // ---- developer screenshot ---------------------------------------------------

        private void BeginScreenshot()
        {
            if (ScreenshotOrganize && active != null) SetOrganize(true);
            using (var preview = new Bitmap(Width, Height)) DrawToBitmap(preview, new Rectangle(Point.Empty, Size));
            var start = DateTime.UtcNow;
            var t = new Timer { Interval = 150 };
            t.Tick += delegate
            {
                if ((DateTime.UtcNow - start).TotalMilliseconds < 900) return;
                if (!renderer.IsIdle && (DateTime.UtcNow - start).TotalSeconds < 15) return;
                t.Stop();
                t.Dispose();
                Refresh();
                Application.DoEvents();
                using (var bmp = new Bitmap(Width, Height))
                {
                    // Capture only this window, even when it is hidden or covered by another app.
                    DrawToBitmap(bmp, new Rectangle(Point.Empty, Size));
                    bmp.Save(ScreenshotPath, System.Drawing.Imaging.ImageFormat.Png);
                }
                Close();
            };
            t.Start();
        }
    }

    /// <summary>Flat, light colors for menus and toolbars.</summary>
    internal sealed class FlatColors : ProfessionalColorTable
    {
        private static readonly Color Hover = Color.FromArgb(0xE8, 0xEF, 0xF9);
        private static readonly Color HoverBorder = Color.FromArgb(0xC6, 0xD7, 0xEE);
        private static readonly Color Pressed = Color.FromArgb(0xD5, 0xE3, 0xF6);

        public override Color ToolStripGradientBegin { get { return Color.White; } }
        public override Color ToolStripGradientMiddle { get { return Color.White; } }
        public override Color ToolStripGradientEnd { get { return Color.White; } }
        public override Color ToolStripBorder { get { return Theme.ChromeBorder; } }
        public override Color MenuStripGradientBegin { get { return Theme.Chrome; } }
        public override Color MenuStripGradientEnd { get { return Theme.Chrome; } }
        public override Color ButtonSelectedHighlight { get { return Hover; } }
        public override Color ButtonSelectedHighlightBorder { get { return HoverBorder; } }
        public override Color ButtonSelectedGradientBegin { get { return Hover; } }
        public override Color ButtonSelectedGradientMiddle { get { return Hover; } }
        public override Color ButtonSelectedGradientEnd { get { return Hover; } }
        public override Color ButtonSelectedBorder { get { return HoverBorder; } }
        public override Color ButtonPressedGradientBegin { get { return Pressed; } }
        public override Color ButtonPressedGradientMiddle { get { return Pressed; } }
        public override Color ButtonPressedGradientEnd { get { return Pressed; } }
        public override Color ButtonPressedHighlight { get { return Pressed; } }
        public override Color ButtonPressedHighlightBorder { get { return HoverBorder; } }
        public override Color ButtonPressedBorder { get { return HoverBorder; } }
        public override Color ButtonCheckedGradientBegin { get { return Theme.AccentSoft; } }
        public override Color ButtonCheckedGradientMiddle { get { return Theme.AccentSoft; } }
        public override Color ButtonCheckedGradientEnd { get { return Theme.AccentSoft; } }
        public override Color ButtonCheckedHighlight { get { return Theme.AccentSoft; } }
        public override Color ButtonCheckedHighlightBorder { get { return HoverBorder; } }
        public override Color CheckBackground { get { return Theme.AccentSoft; } }
        public override Color CheckSelectedBackground { get { return Hover; } }
        public override Color CheckPressedBackground { get { return Pressed; } }
        public override Color MenuItemSelected { get { return Hover; } }
        public override Color MenuItemBorder { get { return HoverBorder; } }
        public override Color MenuItemSelectedGradientBegin { get { return Hover; } }
        public override Color MenuItemSelectedGradientEnd { get { return Hover; } }
        public override Color MenuItemPressedGradientBegin { get { return Color.White; } }
        public override Color MenuItemPressedGradientMiddle { get { return Color.White; } }
        public override Color MenuItemPressedGradientEnd { get { return Color.White; } }
        public override Color ToolStripDropDownBackground { get { return Color.White; } }
        public override Color ImageMarginGradientBegin { get { return Color.White; } }
        public override Color ImageMarginGradientMiddle { get { return Color.White; } }
        public override Color ImageMarginGradientEnd { get { return Color.White; } }
        public override Color MenuBorder { get { return Color.FromArgb(0xC9, 0xCB, 0xCF); } }
        public override Color SeparatorDark { get { return Theme.ChromeBorder; } }
        public override Color SeparatorLight { get { return Color.White; } }
        public override Color OverflowButtonGradientBegin { get { return Color.White; } }
        public override Color OverflowButtonGradientMiddle { get { return Color.White; } }
        public override Color OverflowButtonGradientEnd { get { return Color.White; } }
    }

    internal sealed class FlatRenderer : RoundedRenderer
    {
        public FlatRenderer() { RoundedEdges = true; }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            if (e.ToolStrip is ToolStripDropDown) base.OnRenderToolStripBorder(e);
        }
    }
}
