using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace Gravity
{
    internal sealed partial class MainForm
    {
        private DocumentHeader shellHeader;
        private CommandBar commandBar;
        private ToolStrip modeTools, contextTools, navigationTools, openingTools;
        private ToolStripButton bRead, bFind, bMenu, bExpandedMenu, bOutline, bPages, bDuplicate, bEarlier, bLater;
        private ToolStripLabel selectionSummary;
        private ToolStripButton selectAllPages;
        private ToolStripDropDownButton contextMore;
        private ToolStripDropDownButton zoomChoice;
        private ToolStripTextBox customZoom;
        private ToolStripControlHost selectionHost;
        private Label selectionText;
        private LinkLabel selectAllLink;
        private ToolStripSeparator moveDivider;
        private StatusStrip documentStatus;
        private ToolStripStatusLabel positionStatus, saveStatus;
        private Label navigationTitle, navigationPosition, navigationCount;
        private bool menuExpanded, shellLayout, contextWrapped;
        private EditDoc outlineDocument;
        private int outlineVersion = -1;
        private readonly List<KeyValuePair<int, TreeNode>> outlineLocations = new List<KeyValuePair<int, TreeNode>>();

        private void StyleShellItem(ToolStripItem item, int width, int height, string text = null)
        {
            item.AutoSize = false;
            item.Size = new Size(S(width), S(height));
            item.Margin = new Padding(S(2));
            item.Padding = new Padding(S(8), 0, S(8), 0);
            item.ForeColor = Theme.Text;
            if (text != null) { item.Text = text; item.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText; }
            item.AccessibleName = item.ToolTipText.Split('(')[0].Trim();
        }

        private ToolStrip ShellTools(int height)
        {
            return new ToolStrip
            {
                AutoSize = false, Height = S(height), GripStyle = ToolStripGripStyle.Hidden,
                Renderer = new FlatRenderer(), BackColor = Theme.Chrome, ForeColor = Theme.Text,
                ImageScalingSize = new Size(S(20), S(20)), Font = new Font("Segoe UI", 9.5f),
                Padding = new Padding(S(10), S(3), S(10), S(3)), TabStop = true,
                LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow,
            };
        }

        private void BuildLucideShell()
        {
            SuspendLayout(); content.SuspendLayout(); sidebar.SuspendLayout();
            sidebar.BackColor = Theme.Chrome;
            toc.BackColor = tocEmpty.BackColor = Theme.Chrome;
            toc.ForeColor = Theme.Text;
            toc.ItemHeight = S(36); toc.Font = new Font("Segoe UI", 9f);
            toc.Indent = S(14); toc.ShowLines = false; toc.HideSelection = false;
            toc.DrawMode = TreeViewDrawMode.OwnerDrawText; toc.DrawNode += DrawOutlineNode;
            toc.AccessibleName = "Document outline";
            foreach (var label in sidebar.Controls.OfType<Label>().Where(c => c.Dock == DockStyle.Top).ToArray())
            { sidebar.Controls.Remove(label); label.Dispose(); }
            pagePanel.Dock = DockStyle.Fill;
            sidebar.Controls.Add(pagePanel); sidebar.Controls.SetChildIndex(pagePanel, 0);
            pagePanel.BackColor = Theme.Chrome;
            foreach (Control control in pagePanel.Controls) if (control != pageBody) control.Visible = false;
            navigationTools = ShellTools(58); navigationTools.Dock = DockStyle.Top;
            navigationTools.Renderer = new RoundedRenderer { NavigationSelection = true, CornerRadius = 5 };
            navigationTools.Padding = new Padding(S(16), S(14), S(16), S(8));
            bOutline = new ToolStripButton("Outline") { ToolTipText = "Document outline", AutoToolTip = false };
            bPages = new ToolStripButton("Pages") { ToolTipText = "Page thumbnails", AutoToolTip = false };
            StyleShellItem(bOutline, 96, 36); StyleShellItem(bPages, 96, 36);
            bOutline.Font = bPages.Font = new Font("Segoe UI Semibold", 9f);
            bOutline.Margin = bPages.Margin = Padding.Empty;
            bOutline.Click += delegate { ShowNavigation(false); };
            bPages.Click += delegate { ShowNavigation(true); };
            navigationTools.Items.AddRange(new ToolStripItem[] { bOutline, bPages });
            var metadata = new Panel { Dock = DockStyle.Top, Height = S(42), Padding = new Padding(S(20), 0, S(20), 0) };
            navigationTitle = new SingleLineLabel { Dock = DockStyle.Fill, ForeColor = Theme.SubtleText, Font = new Font("Segoe UI Semibold", 8.25f),
                TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
            navigationCount = new Label { Dock = DockStyle.Right, Width = S(65), ForeColor = Theme.SubtleText, Font = new Font("Segoe UI", 8.25f), TextAlign = ContentAlignment.MiddleRight };
            metadata.Controls.Add(navigationTitle); metadata.Controls.Add(navigationCount);
            navigationPosition = new Label { Dock = DockStyle.Bottom, Height = S(44), Padding = new Padding(S(44), 0, S(12), 0),
                ForeColor = Theme.SubtleText, Font = new Font("Segoe UI", 8.25f), TextAlign = ContentAlignment.MiddleLeft };
            navigationPosition.Paint += delegate(object sender, PaintEventArgs e) {
                using (var pen = new Pen(Theme.ChromeBorder)) e.Graphics.DrawLine(pen, 0, 0, navigationPosition.Width, 0);
                LucideIcons.Draw(e.Graphics, "book-open", new Rectangle(S(20), S(14), S(16), S(16)), Theme.SubtleText);
            };
            sidebar.Controls.Add(metadata); sidebar.Controls.Add(navigationTools); sidebar.Controls.Add(navigationPosition);
            sidebar.Resize += delegate { bOutline.Width = bPages.Width = Math.Max(S(64), (sidebar.ClientSize.Width - S(34)) / 2); };

            // Reuse command objects and handlers in the new task-specific rows.
            toolbar.Items.Clear(); toolbar.AutoSize = false; toolbar.Height = S(58); toolbar.Dock = DockStyle.Top;
            toolbar.Padding = new Padding(S(12), S(4), S(12), S(5)); toolbar.BackColor = Theme.Chrome;
            toolbar.ImageScalingSize = new Size(S(20), S(20)); toolbar.Font = new Font("Segoe UI", 9.5f); toolbar.TabStop = true;
            bFind = Btn("search", "Find in document (Ctrl+F)", FocusFind);
            bMenu = Btn("ellipsis", "Show application menus", delegate { SetApplicationMenus(!menuExpanded, true); });
            bExpandedMenu = Btn("ellipsis", "Hide application menus", delegate { SetApplicationMenus(false, true); });
            foreach (var item in new[] { bSidebar, bOpen, bSave, bPrint, bUndo, bRedo, bFind, bMenu, bExpandedMenu }) StyleShellItem(item, 44, 44);
            StyleShellItem(bOpen, 86, 44, "Open"); StyleShellItem(bSave, 86, 44, "Save");
            foreach (var item in new[] { bSave, bPrint, bUndo, bFind }) item.Alignment = ToolStripItemAlignment.Right;
            toolbar.Items.AddRange(new ToolStripItem[] { bSave, bPrint, bUndo, bFind });
            commandBar = new CommandBar { Dock = DockStyle.Top, Height = S(60), BackColor = Theme.Chrome };
            commandBar.Paint += delegate(object sender, PaintEventArgs e) { using (var pen = new Pen(Theme.ChromeBorder)) e.Graphics.DrawLine(pen, 0, commandBar.Height - 1, commandBar.Width, commandBar.Height - 1); };
            toolbar.Dock = DockStyle.None; toolbar.Width = S(268); toolbar.LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow;
            commandBar.Controls.Add(toolbar);
            openingTools = ShellTools(58); openingTools.Dock = DockStyle.None; openingTools.Width = S(214);
            openingTools.Items.AddRange(new ToolStripItem[] { bSidebar, bOpen, bMenu }); commandBar.Controls.Add(openingTools);
            modeTools = new SegmentStrip { AutoSize = false, Dock = DockStyle.None, GripStyle = ToolStripGripStyle.Hidden,
                Padding = new Padding(S(3)), CanOverflow = false, TabStop = true, ImageScalingSize = new Size(S(20), S(20)),
                Font = new Font("Segoe UI Semibold", 9.75f), Renderer = new RoundedRenderer { WhiteSelection = true }, LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow };
            bRead = Btn("book-open", "Read document", () => SetOrganize(false), "Read");
            StyleShellItem(bRead, 116, 38, "Read"); StyleShellItem(bOrganize, 116, 38, "Organize");
            bRead.Margin = bOrganize.Margin = Padding.Empty;
            modeTools.Items.AddRange(new ToolStripItem[] { bRead, bOrganize });
            commandBar.Controls.Add(modeTools); modeTools.BringToFront();
            menu.Dock = DockStyle.None; menu.AutoSize = false; menu.CanOverflow = false;
            menu.LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow;
            menu.Renderer = new RoundedRenderer(); menu.Font = openingTools.Font;
            menu.ImageScalingSize = openingTools.ImageScalingSize;
            menu.Padding = new Padding(S(10), S(3), S(10), S(3));
            string[] menuIcons = { "file-text", "pencil", "eye", "navigation", "files", "circle-question-mark" };
            for (int i = 0; i < menu.Items.Count; i++)
            {
                var item = menu.Items[i];
                item.Image = LucideIcons.Create(menuIcons[i], S(20), Theme.Text);
                item.ToolTipText = item.Text.Replace("&", "") + " menu";
                StyleShellItem(item, 94, 44);
                item.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
            }
            menu.Items.Add(bExpandedMenu);
            commandBar.Controls.Add(menu);
            commandBar.Resize += delegate { LayoutLucideShell(); };

            contextTools = ShellTools(57); contextTools.Dock = DockStyle.Top;
            contextTools.Padding = new Padding(S(20), S(6), S(20), S(6));
            contextTools.Paint += delegate(object sender, PaintEventArgs e) { using (var pen = new Pen(Theme.ChromeBorder)) e.Graphics.DrawLine(pen, 0, contextTools.Height - 1, contextTools.Width, contextTools.Height - 1); };
            contextTools.CanOverflow = false;
            contextMore = new ToolStripDropDownButton("More") { Alignment = ToolStripItemAlignment.Right,
                ToolTipText = "More page and view actions", AccessibleName = "More page and view actions" };
            StyleShellItem(contextMore, 82, 44);
            contextMore.DropDownOpening += delegate { PopulateContextMenu(); };
            contextTools.Items.Add(contextMore);
            contextTools.Resize += delegate { if (!updating) UpdateUI(); };
            selectionSummary = new ToolStripLabel { AutoSize = false, Width = S(142), Font = new Font("Segoe UI Semibold", 9f), TextAlign = ContentAlignment.MiddleLeft };
            selectAllPages = new ToolStripButton("Select all") { ToolTipText = "Select all pages (Ctrl+A)", ForeColor = Theme.Accent };
            selectAllPages.Click += delegate { PageAction(() => organizer.SelectAll()); };
            var selectionPanel = new Panel { Size = new Size(S(150), S(44)), BackColor = Theme.Chrome };
            selectionText = new Label { Dock = DockStyle.Top, Height = S(22), Font = new Font("Segoe UI Semibold", 9f), ForeColor = Theme.Text };
            selectAllLink = new LinkLabel { Dock = DockStyle.Fill, Text = "Select all", Font = new Font("Segoe UI", 8.25f), LinkColor = Theme.Accent,
                ActiveLinkColor = Theme.Accent, VisitedLinkColor = Theme.Accent, LinkBehavior = LinkBehavior.AlwaysUnderline, TabStop = true };
            selectAllLink.LinkClicked += delegate { PageAction(() => organizer.SelectAll()); };
            selectionPanel.Controls.Add(selectAllLink); selectionPanel.Controls.Add(selectionText);
            selectionHost = new ToolStripControlHost(selectionPanel) { AutoSize = false, Size = selectionPanel.Size, Margin = Padding.Empty };
            bDuplicate = Btn("copy", "Duplicate selected pages (Ctrl+D)", DuplicatePages, "Duplicate");
            bEarlier = Btn("arrow-up", "Move selected pages earlier (Alt+Up)", () => MoveBy(-1));
            bLater = Btn("arrow-down", "Move selected pages later (Alt+Down)", () => MoveBy(1));
            foreach (var item in new[] { bPrev, bNext, bZoomOut, bZoomIn, bRotL, bRotR, bCut, bCopy, bPaste, bDelete, bPRotL, bPRotR, bExtract, bEarlier, bLater, bDuplicate })
                StyleShellItem(item, 44, 44);
            bPrev.Image.Dispose(); bPrev.Image = LucideIcons.Create("chevron-left", S(20), Theme.Text);
            bNext.Image.Dispose(); bNext.Image = LucideIcons.Create("chevron-right", S(20), Theme.Text);
            bZoomOut.Image.Dispose(); bZoomOut.Image = LucideIcons.Create("minus", S(20), Theme.Text);
            bZoomIn.Image.Dispose(); bZoomIn.Image = LucideIcons.Create("plus", S(20), Theme.Text);
            StyleShellItem(bPRotR, 88, 40, "Rotate"); StyleShellItem(bDuplicate, 102, 40, "Duplicate");
            StyleShellItem(bDelete, 84, 40, "Delete"); StyleShellItem(bExtract, 90, 40, "Extract"); StyleShellItem(bInsert, 86, 40);
            pageBox.Size = new Size(S(44), S(32)); pageBox.Font = new Font("Segoe UI", 9f); pageBox.Margin = new Padding(S(4), S(6), S(4), S(6));
            zoomBox.Width = S(110); zoomBox.Font = new Font("Segoe UI", 9.5f); zoomBox.Margin = new Padding(S(4), S(10), S(4), S(8));
            zoomChoice = new ToolStripDropDownButton("Fit page") { ToolTipText = "Zoom", AccessibleName = "Zoom" };
            StyleShellItem(zoomChoice, 104, 44); zoomChoice.Font = new Font("Segoe UI", 9.75f);
            foreach (string value in new[] { "Fit page", "Fit width", "Actual size", "50%", "75%", "100%", "125%", "150%", "200%", "300%", "400%", "800%" })
            { string zoom = value; zoomChoice.DropDownItems.Add(value, null, delegate { ApplyZoomText(zoom); FocusCanvas(); }); }
            customZoom = new ToolStripTextBox { ToolTipText = "Type a zoom percentage and press Enter", AccessibleName = "Custom zoom percentage" };
            customZoom.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; ApplyZoomText(customZoom.Text); zoomChoice.HideDropDown(); FocusCanvas(); } };
            zoomChoice.DropDownItems.Add(new ToolStripSeparator()); zoomChoice.DropDownItems.Add(new ToolStripLabel("Custom percentage")); zoomChoice.DropDownItems.Add(customZoom);
            foreach (var item in new ToolStripItem[] { bZoomIn, zoomChoice, bZoomOut }) item.Alignment = ToolStripItemAlignment.Right;
            foreach (var item in new ToolStripItem[] { bLater, bEarlier, bDelete, bDuplicate, bPRotR }) item.Alignment = ToolStripItemAlignment.Right;
            moveDivider = new ToolStripSeparator { Alignment = ToolStripItemAlignment.Right, Margin = new Padding(S(8), S(10), S(8), S(10)) };
            foreach (var item in new ToolStripItem[] { bCut, bCopy, bPaste, bPRotL, bRotL, bRotR, bInsert, bExtract }) item.Overflow = ToolStripItemOverflow.Always;
            contextTools.Items.AddRange(new ToolStripItem[] { selectionHost, bPrev, pageBox, pageOf, bNext,
                bZoomIn, zoomChoice, bZoomOut, bLater, bEarlier, moveDivider, bDelete, bDuplicate, bPRotR });
            editGroup.AddRange(new ToolStripItem[] { selectionHost, bDuplicate, bEarlier, bLater, moveDivider });
            host.Controls.Add(contextTools);
            documentStatus = new StatusStrip { Dock = DockStyle.Bottom, SizingGrip = false, AutoSize = false, Height = S(31),
                ShowItemToolTips = true, ImageScalingSize = new Size(S(14), S(14)),
                BackColor = Color.FromArgb(0xF2, 0xF4, 0xF7), ForeColor = Theme.SubtleText, Padding = new Padding(S(18), 0, S(18), 0), Font = new Font("Segoe UI", 8.5f) };
            positionStatus = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            saveStatus = new ToolStripStatusLabel { TextAlign = ContentAlignment.MiddleRight,
                ImageAlign = ContentAlignment.MiddleLeft, TextImageRelation = TextImageRelation.ImageBeforeText };
            saveStatus.Disposed += delegate { if (saveStatus.Image != null) saveStatus.Image.Dispose(); };
            documentStatus.Items.AddRange(new ToolStripItem[] { positionStatus, saveStatus }); host.Controls.Add(documentStatus);
            shellHeader = new DocumentHeader { Dock = DockStyle.Top, Height = S(55) };
            shellHeader.BrandAction.Width = S(144);
            shellHeader.BrandAction.Click += delegate { SetApplicationMenus(!menuExpanded, true); };
            BuildWindowChrome();
            tabStrip.Dock = DockStyle.Top; tabStrip.Height = S(34); Controls.Add(tabStrip);
            Controls.Add(commandBar); Controls.Add(shellHeader);
            Controls.SetChildIndex(content, 0); Controls.SetChildIndex(commandBar, 1); Controls.SetChildIndex(tabStrip, 2); Controls.SetChildIndex(shellHeader, 3);
            menu.Visible = false; menu.ForeColor = Theme.Text;
            sidebar.ResumeLayout(true); content.ResumeLayout(true); ResumeLayout(true); LayoutLucideShell();
        }

        private void LayoutLucideShell()
        {
            if (commandBar == null || shellLayout) return;
            shellLayout = true;
            try
            {
                bool compact = ClientSize.Width < S(800);
                toolbar.Width = S(ClientSize.Width < S(700) ? 220 : 268);
                toolbar.Bounds = new Rectangle(commandBar.Width - toolbar.Width, 0, toolbar.Width, S(54));
                openingTools.Bounds = new Rectangle(0, 0, S(214), S(54));
                commandBar.Height = S(compact ? 106 : 60);
                modeTools.Bounds = new Rectangle((commandBar.Width - S(238)) / 2, S(compact ? 57 : 5), S(238), S(44));
                menu.Bounds = new Rectangle(0, 0, commandBar.Width, S(54));
                var entries = menu.Items.OfType<ToolStripMenuItem>().ToArray();
                int menuWidth = Math.Min(S(94), Math.Max(1, (menu.DisplayRectangle.Width - S(48) - S(4) * entries.Length) / entries.Length));
                foreach (ToolStripItem item in entries) item.Width = menuWidth;
            }
            finally { shellLayout = false; }
        }

        private void SetApplicationMenus(bool expanded, bool animate)
        {
            foreach (var item in menu.Items.OfType<ToolStripMenuItem>()) item.HideDropDown();
            if (menuExpanded == expanded) return;
            bool returnFocus = menu.ContainsFocus || openingTools.ContainsFocus || toolbar.ContainsFocus || modeTools.ContainsFocus;
            commandBar.Transition(delegate
            {
                menuExpanded = expanded;
                UpdateApplicationMenus(active != null);
                LayoutLucideShell();
            }, animate && !fullscreen && CommandBar.AnimationsEnabled,
                expanded ? 250 : 180);
            if (returnFocus) shellHeader.BrandAction.Focus();
        }

        private void UpdateApplicationMenus(bool has)
        {
            menu.Visible = menuExpanded && !fullscreen;
            openingTools.Visible = toolbar.Visible = !menuExpanded && !fullscreen;
            modeTools.Visible = has && !menuExpanded && !fullscreen;
            bMenu.Checked = bExpandedMenu.Checked = menuExpanded;
            bMenu.ToolTipText = bMenu.AccessibleName = menuExpanded ? "Hide application menus" : "Show application menus";
            shellHeader.BrandAction.AccessibleName = "Gravity: " + bMenu.AccessibleName;
        }

        private void ShowNavigation(bool pages)
        {
            if (active == null) return;
            settings.ShowBookmarks = true; settings.ShowPages = pages;
            if (pages && Organizing) SetOrganize(false);
            pageSelectionActive = false;
            ShowMode(); SyncPageSelection(); UpdateUI();
            if (pages) organizer.Focus(); else toc.Focus();
        }

        private void UpdateLucideShell(bool has, bool organizing, int count, int selectedCount)
        {
            if (shellHeader == null) return;
            shellHeader.Visible = commandBar.Visible = !fullscreen;
            if (fullscreen) commandBar.FinishTransition();
            UpdateApplicationMenus(has);
            contextTools.Visible = documentStatus.Visible = has && !fullscreen;
            bRead.Checked = has && !organizing; bRead.Enabled = has && !presentationMode; bFind.Enabled = has;
            bOutline.Checked = !pagePanel.Visible; bPages.Checked = pagePanel.Visible; bOutline.Enabled = bPages.Enabled = has;
            bDuplicate.Enabled = selectedCount > 0;
            var selection = organizer.SelectedIndices();
            bEarlier.Enabled = selectedCount > 0 && selection.Count > 0 && selection[0] > 0;
            bLater.Enabled = selectedCount > 0 && selection.Count > 0 && selection[selection.Count - 1] < count - 1;
            selectionSummary.Text = Count(selectedCount) + " selected";
            foreach (var item in findGroup) item.Visible = false;
            navigationTitle.Text = has ? System.IO.Path.GetFileNameWithoutExtension(active.Doc.DisplayName) : "";
            navigationCount.Text = has ? Count(count) : "";
            positionStatus.Text = organizing ? Count(count) + " · " + Count(selectedCount) + " selected" : "Page " + (count > 0 ? view.CurrentPage + 1 : 0) + " of " + count;
            positionStatus.ToolTipText = organizing ? "Ctrl click to select · Double-click to read" : "";
            UpdateSaveStatus(has ? active.Doc : null);
            shellHeader.DocumentTitle = has ? active.Doc.DisplayName : "";
            shellHeader.DocumentState = has ? Count(count) + " · " + (active.Doc.SaveState == DocumentSaveState.Unsaved ? "Unsaved changes" : saveStatus.Text) : "PDF reader and organizer";
            shellHeader.AccessibleName = shellHeader.DocumentTitle + ", " + shellHeader.DocumentState;
            shellHeader.Invalidate();
            bPrint.Visible = has && ClientSize.Width >= S(700); bRedo.Visible = false;
            bSidebar.Checked = false;
            int width = contextTools.Width;
            foreach (var item in new ToolStripItem[] { bCut, bCopy, bPaste, bPRotL, bRotL, bRotR, bInsert, bExtract }) item.Visible = false;
            zoomBox.Visible = false;
            foreach (var item in new ToolStripItem[] { bZoomIn, bZoomOut, zoomChoice }) item.Visible = has && !organizing;
            zoomChoice.Text = ZoomText().Replace("Page", "page").Replace("Width", "width").Replace("Size", "size");
            bPrev.Visible = bNext.Visible = has && !organizing && width >= S(410);
            selectAllPages.Visible = selectionSummary.Visible = false;
            selectionText.Text = selectionSummary.Text;
            bPRotR.Visible = bDuplicate.Visible = bDelete.Visible = has && organizing;
            bEarlier.Visible = bLater.Visible = moveDivider.Visible = has && organizing && width >= S(650);
            contextMore.Visible = has && organizing && width < S(800);
            LayoutContextActions(organizing && width < S(700));
            LayoutLucideShell();
            SyncOutlinePosition(organizing ? organizer.FocusIndex : view.CurrentPage);
            int section = outlineLocations.FindIndex(location => location.Value == toc.SelectedNode);
            navigationPosition.Text = section >= 0 ? "Section " + (section + 1) + " of " + outlineLocations.Count : "Page " + (count > 0 ? view.CurrentPage + 1 : 0) + " of " + count;
            toc.Invalidate();
        }

        private void UpdateSaveStatus(EditDoc doc)
        {
            string label = doc == null ? "" : doc.SaveState.ToString();
            if (saveStatus.Text != label)
            {
                var previous = saveStatus.Image;
                string icon = label == "Unsaved" ? "pencil" : label == "Saved" ? "check" : "file-text";
                saveStatus.Image = doc == null ? null : LucideIcons.Create(icon, S(14), Theme.SubtleText);
                saveStatus.Text = label;
                saveStatus.AccessibleName = doc == null ? "" : "Save status: " + label;
                if (previous != null) previous.Dispose();
            }
            saveStatus.ToolTipText = doc == null ? "" : doc.SaveState == DocumentSaveState.Original
                ? "Unchanged since opening."
                : doc.SaveState == DocumentSaveState.Saved ? "All changes saved."
                : doc.FilePath == null ? "This document has not been saved. Press Ctrl+S to save."
                : "Changes have not been saved. Press Ctrl+S to save.";
        }

        private void LayoutContextActions(bool wrap)
        {
            if (contextWrapped != wrap)
            {
                contextWrapped = wrap; contextTools.SuspendLayout();
                contextTools.Items.Clear();
                contextTools.LayoutStyle = wrap ? ToolStripLayoutStyle.Flow : ToolStripLayoutStyle.HorizontalStackWithOverflow;
                contextTools.Items.AddRange(wrap
                    ? new ToolStripItem[] { selectionHost, bPRotR, bDuplicate, bDelete, bEarlier, bLater, contextMore, moveDivider, bPrev, pageBox, pageOf, bNext, bZoomIn, zoomChoice, bZoomOut }
                    : new ToolStripItem[] { contextMore, selectionHost, bPrev, pageBox, pageOf, bNext, bZoomIn, zoomChoice, bZoomOut, bLater, bEarlier, moveDivider, bDelete, bDuplicate, bPRotR });
                if (wrap) ((FlowLayoutSettings)contextTools.LayoutSettings).SetFlowBreak(selectionHost, true);
                contextTools.ResumeLayout(true);
            }
            contextTools.Height = S(wrap ? 98 : 57);
            int inset = wrap ? 12 : contextTools.Width < S(500) ? 8 : 20;
            contextTools.Padding = new Padding(S(inset), S(6), S(inset), S(6));
            bool narrowRead = !wrap && contextTools.Width < S(410);
            bZoomIn.Width = bZoomOut.Width = S(narrowRead ? 36 : 44);
            bZoomIn.Margin = bZoomOut.Margin = new Padding(S(narrowRead ? 0 : 2));
            zoomChoice.Width = S(narrowRead ? 86 : 104);
            selectionHost.Size = new Size(wrap ? Math.Max(S(120), contextTools.Width - S(28)) : S(150), S(40));
            bool small = wrap && contextTools.Width < S(390);
            StyleShellItem(bPRotR, small ? 74 : 88, 44, "Rotate");
            StyleShellItem(bDuplicate, small ? 44 : 102, 44, "Duplicate");
            StyleShellItem(bDelete, small ? 44 : 84, 44, "Delete");
            if (small) { bDuplicate.DisplayStyle = bDelete.DisplayStyle = ToolStripItemDisplayStyle.Image; }
            contextMore.Width = S(small ? 68 : 82);
        }

        private void PopulateContextMenu()
        {
            foreach (ToolStripItem item in contextMore.DropDownItems.Cast<ToolStripItem>().ToArray()) item.Dispose();
            contextMore.DropDownItems.Clear();
            var commands = new[] { miPRotL, miPRotR, miDuplicate, miDelete, miCut, miCopy, miPaste, miMoveEarlier, miMoveLater,
                miSelectAllPages, miInsertBlank, miInsertFile, miInsertImages, miExtract, miSavePages };
            foreach (var command in commands)
            {
                var source = command;
                var item = new ToolStripMenuItem(source.Text) { Enabled = source.Enabled, Checked = source.Checked,
                    ShortcutKeyDisplayString = source.ShortcutKeyDisplayString };
                item.Click += delegate { source.PerformClick(); };
                contextMore.DropDownItems.Add(item);
            }
        }

        private void SyncOutlinePosition(int page)
        {
            if (active == null) return;
            var doc = active.Doc;
            if (outlineDocument != doc || outlineVersion != doc.Version)
            {
                outlineDocument = doc; outlineVersion = doc.Version; outlineLocations.Clear();
                var positions = new Dictionary<int, int>();
                for (int i = 0; i < doc.Pages.Count; i++)
                    if (doc.Pages[i].Src == tocBase && !positions.ContainsKey(doc.Pages[i].Index)) positions.Add(doc.Pages[i].Index, i);
                foreach (TreeNode node in FlattenOutline(toc.Nodes))
                {
                    var item = node.Tag as OutlineItem; int position;
                    if (item != null && positions.TryGetValue(item.PageIndex, out position)) outlineLocations.Add(new KeyValuePair<int, TreeNode>(position, node));
                }
            }
            TreeNode current = null; int nearest = -1;
            foreach (var location in outlineLocations)
                if (location.Key <= page && location.Key >= nearest) { nearest = location.Key; current = location.Value; }
            if (toc.SelectedNode != current) toc.SelectedNode = current;
        }

        private static IEnumerable<TreeNode> FlattenOutline(TreeNodeCollection nodes)
        {
            foreach (TreeNode node in nodes)
            {
                yield return node;
                foreach (var child in FlattenOutline(node.Nodes)) yield return child;
            }
        }

        private void DrawOutlineNode(object sender, DrawTreeNodeEventArgs e)
        {
            bool selected = e.Node == toc.SelectedNode;
            var row = new Rectangle(S(10), e.Bounds.Y, Math.Max(1, toc.ClientSize.Width - S(20)), toc.ItemHeight - 1);
            using (var background = new SolidBrush(Theme.Chrome)) e.Graphics.FillRectangle(background, new Rectangle(0, e.Bounds.Y, toc.Width, toc.ItemHeight));
            if (selected)
                using (var path = Theme.RoundRect(row, S(6)))
                using (var brush = new SolidBrush(Theme.AccentSoft)) e.Graphics.FillPath(brush, path);
            int indent = e.Node.Level * S(14);
            var text = new Rectangle(S(48) + indent, e.Bounds.Y, Math.Max(1, toc.Width - S(60) - indent), toc.ItemHeight);
            var item = e.Node.Tag as OutlineItem;
            int page = item != null && active != null ? active.Doc.Find(tocBase, item.PageIndex) : -1;
            using (var font = new Font(selected ? "Segoe UI Semibold" : "Segoe UI", 9f))
            {
                TextRenderer.DrawText(e.Graphics, e.Node.Text, font, text, selected ? Theme.OutlineSelectedText : Theme.Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                TextRenderer.DrawText(e.Graphics, page >= 0 ? (page + 1).ToString() : "", font,
                    new Rectangle(S(20) + indent, e.Bounds.Y, S(16), toc.ItemHeight), selected ? Theme.OutlineSelectedText : Theme.SubtleText,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
            if (e.Node.Nodes.Count > 0) LucideIcons.Draw(e.Graphics, e.Node.IsExpanded ? "chevron-down" : "chevron-right",
                new Rectangle(Math.Max(0, e.Bounds.X - S(16)), e.Bounds.Y + S(12), S(12), S(12)), Theme.SubtleText);
            if (selected && toc.Focused) ControlPaint.DrawFocusRectangle(e.Graphics, row, Theme.Accent, Theme.Chrome);
        }
    }

    internal class RoundedRenderer : ToolStripProfessionalRenderer
    {
        public bool WhiteSelection;
        public bool NavigationSelection;
        public Color SelectionColor { get { return NavigationSelection ? Theme.SidebarSelection : Theme.AccentSoft; } }
        public float CornerRadius = 6;
        public RoundedRenderer() : base(new FlatColors()) { RoundedEdges = true; }
        private void DrawButton(ToolStripItemRenderEventArgs e)
        {
            var button = e.Item as ToolStripButton;
            bool selected = button != null && button.Checked;
            var rect = new RectangleF(1, 1, Math.Max(1, e.Item.Width - 2), Math.Max(1, e.Item.Height - 2));
            float scale = e.Graphics.DpiX / 96f;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Theme.RoundRect(rect, CornerRadius * scale))
            {
                if (selected && WhiteSelection)
                {
                    var shadow = rect; shadow.Offset(0, scale);
                    using (var shadowPath = Theme.RoundRect(shadow, 6 * scale))
                    using (var shadowBrush = new SolidBrush(Color.FromArgb(24, 32, 43, 60))) e.Graphics.FillPath(shadowBrush, shadowPath);
                    e.Graphics.FillPath(Brushes.White, path);
                }
                else if (selected || e.Item.Selected || e.Item.Pressed)
                    using (var fill = new SolidBrush(e.Item.Pressed ? Theme.Pressed : selected ? SelectionColor : Theme.Hover)) e.Graphics.FillPath(fill, path);
                if (e.Item.Selected && e.Item.Owner != null && e.Item.Owner.ContainsFocus)
                    using (var pen = new Pen(Theme.Accent, Math.Max(2, 2 * scale))) e.Graphics.DrawPath(pen, path);
                else if (e.Item.Text == "Save")
                    using (var pen = new Pen(Color.FromArgb(0x7C, 0x87, 0x95))) e.Graphics.DrawPath(pen, path);
            }
        }
        protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e) { DrawButton(e); }
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            if (e.ToolStrip is ToolStripDropDown) base.OnRenderToolStripBorder(e);
        }
        protected override void OnRenderDropDownButtonBackground(ToolStripItemRenderEventArgs e) { DrawButton(e); }
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Owner is MenuStrip) DrawButton(e); else base.OnRenderMenuItemBackground(e);
        }
        private static bool PairedContent(ToolStripItem item) { return (item is ToolStripButton || item.Owner is MenuStrip) && item.DisplayStyle == ToolStripItemDisplayStyle.ImageAndText && item.Image != null; }
        private static Rectangle PairedImageBounds(Graphics graphics, ToolStripItem item)
        {
            int icon = item.Owner.ImageScalingSize.Width, gap = (int)Math.Round(8 * graphics.DpiX / 96f);
            int text = TextRenderer.MeasureText(graphics, item.Text, item.Font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
            return new Rectangle((item.Width - icon - gap - text) / 2, (item.Height - icon) / 2, icon, icon);
        }
        protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
        {
            if (PairedContent(e.Item)) e = new ToolStripItemImageRenderEventArgs(e.Graphics, e.Item, e.Image, PairedImageBounds(e.Graphics, e.Item));
            var button = e.Item as ToolStripButton;
            if (e.Item.Enabled && e.Image != null && (button != null && button.Checked || e.Item.Selected || e.Item.Pressed))
                Theme.DrawTintedImage(e.Graphics, e.Image, e.ImageRectangle, Theme.Accent);
            else base.OnRenderItemImage(e);
        }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (PairedContent(e.Item))
            {
                var icon = PairedImageBounds(e.Graphics, e.Item);
                int gap = (int)Math.Round(8 * e.Graphics.DpiX / 96f);
                e.TextRectangle = new Rectangle(icon.Right + gap, 0, Math.Max(1, e.Item.Width - icon.Right - gap), e.Item.Height);
                e.TextFormat = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            }
            var button = e.Item as ToolStripButton;
            e.TextColor = !e.Item.Enabled ? SystemColors.GrayText : button != null && button.Checked || e.Item.Selected || e.Item.Pressed ? Theme.Accent : e.Item.ForeColor;
            base.OnRenderItemText(e);
        }
    }
}
