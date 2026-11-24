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
        private Panel shellHeader, commandBar;
        private ToolStrip modeTools, contextTools, navigationTools, openingTools;
        private ToolStripButton bRead, bFind, bMenu, bOutline, bPages, bDuplicate, bEarlier, bLater;
        private ToolStripLabel selectionSummary;
        private ToolStripButton selectAllPages;
        private ToolStripDropDownButton contextMore;
        private StatusStrip documentStatus;
        private ToolStripStatusLabel positionStatus, saveStatus;
        private Label navigationTitle, navigationPosition;
        private bool menuExpanded, shellLayout;
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
            toc.ItemHeight = S(36); toc.Font = new Font("Segoe UI", 9.5f);
            toc.DrawMode = TreeViewDrawMode.OwnerDrawText; toc.DrawNode += DrawOutlineNode;
            toc.AccessibleName = "Document outline";
            foreach (var label in sidebar.Controls.OfType<Label>().Where(c => c.Dock == DockStyle.Top).ToArray())
            { sidebar.Controls.Remove(label); label.Dispose(); }
            pagePanel.Dock = DockStyle.Fill;
            sidebar.Controls.Add(pagePanel); sidebar.Controls.SetChildIndex(pagePanel, 0);
            pagePanel.BackColor = Theme.Chrome;
            foreach (var tools in pagePanel.Controls.OfType<ToolStrip>())
            {
                tools.BackColor = Theme.Chrome;
                if (tools.Items.OfType<ToolStripLabel>().Any(l => l.Text == "Pages")) tools.Visible = false;
            }
            navigationTools = ShellTools(52); navigationTools.Dock = DockStyle.Top;
            navigationTools.Padding = new Padding(S(12), S(8), S(12), S(4));
            bOutline = new ToolStripButton("Outline") { ToolTipText = "Document outline", AutoToolTip = false };
            bPages = new ToolStripButton("Pages") { ToolTipText = "Page thumbnails", AutoToolTip = false };
            StyleShellItem(bOutline, 94, 34); StyleShellItem(bPages, 94, 34);
            bOutline.Click += delegate { ShowNavigation(false); };
            bPages.Click += delegate { ShowNavigation(true); };
            navigationTools.Items.AddRange(new ToolStripItem[] { bOutline, bPages });
            navigationTitle = new Label { Dock = DockStyle.Top, Height = S(36), Padding = new Padding(S(18), 0, S(12), 0),
                ForeColor = Theme.SubtleText, Font = new Font("Segoe UI Semibold", 8.5f), TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
            navigationPosition = new Label { Dock = DockStyle.Bottom, Height = S(34), Padding = new Padding(S(18), 0, S(12), 0),
                ForeColor = Theme.SubtleText, Font = new Font("Segoe UI", 8.5f), TextAlign = ContentAlignment.MiddleLeft };
            sidebar.Controls.Add(navigationTitle); sidebar.Controls.Add(navigationTools); sidebar.Controls.Add(navigationPosition);

            // Reuse command objects and handlers in the new task-specific rows.
            toolbar.Items.Clear(); toolbar.AutoSize = false; toolbar.Height = S(58); toolbar.Dock = DockStyle.Top;
            toolbar.Padding = new Padding(S(12), S(4), S(12), S(5)); toolbar.BackColor = Theme.Chrome;
            toolbar.ImageScalingSize = new Size(S(20), S(20)); toolbar.Font = new Font("Segoe UI", 9.5f); toolbar.TabStop = true;
            bFind = Btn("search", "Find in document (Ctrl+F)", FocusFind);
            bMenu = Btn("ellipsis", "Show application menus", delegate { menuExpanded = !menuExpanded; menu.Visible = menuExpanded && !fullscreen; });
            foreach (var item in new[] { bSidebar, bOpen, bSave, bPrint, bUndo, bRedo, bFind, bMenu }) StyleShellItem(item, 44, 44);
            StyleShellItem(bOpen, 86, 44, "Open"); StyleShellItem(bSave, 86, 44, "Save");
            foreach (var item in new[] { bSave, bMenu, bPrint, bRedo, bUndo, bFind }) item.Alignment = ToolStripItemAlignment.Right;
            toolbar.Items.AddRange(new ToolStripItem[] { bSave, bMenu, bPrint, bRedo, bUndo, bFind });
            commandBar = new Panel { Dock = DockStyle.Top, Height = S(62), BackColor = Theme.Chrome };
            toolbar.Dock = DockStyle.None; toolbar.Width = S(358); toolbar.LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow;
            commandBar.Controls.Add(toolbar);
            openingTools = ShellTools(58); openingTools.Dock = DockStyle.None; openingTools.Width = S(166);
            openingTools.Items.AddRange(new ToolStripItem[] { bSidebar, bOpen }); commandBar.Controls.Add(openingTools);
            modeTools = ShellTools(48); modeTools.Dock = DockStyle.None; modeTools.Padding = new Padding(S(3));
            modeTools.BackColor = Color.FromArgb(0xE5, 0xE8, 0xED); modeTools.CanOverflow = false;
            bRead = Btn("book-open", "Read document", () => SetOrganize(false), "Read");
            StyleShellItem(bRead, 110, 38, "Read"); StyleShellItem(bOrganize, 110, 38, "Organize");
            modeTools.Items.AddRange(new ToolStripItem[] { bRead, bOrganize });
            commandBar.Controls.Add(modeTools); modeTools.BringToFront();
            commandBar.Resize += delegate { LayoutLucideShell(); };

            contextTools = ShellTools(56); contextTools.Dock = DockStyle.Top;
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
            bDuplicate = Btn("copy", "Duplicate selected pages (Ctrl+D)", DuplicatePages, "Duplicate");
            bEarlier = Btn("arrow-up", "Move selected pages earlier (Alt+Up)", () => MoveBy(-1));
            bLater = Btn("arrow-down", "Move selected pages later (Alt+Down)", () => MoveBy(1));
            foreach (var item in new[] { bPrev, bNext, bZoomOut, bZoomIn, bRotL, bRotR, bCut, bCopy, bPaste, bDelete, bPRotL, bPRotR, bExtract, bEarlier, bLater, bDuplicate })
                StyleShellItem(item, 38, 40);
            bPrev.Image.Dispose(); bPrev.Image = LucideIcons.Create("chevron-left", S(20), Theme.Text);
            bNext.Image.Dispose(); bNext.Image = LucideIcons.Create("chevron-right", S(20), Theme.Text);
            StyleShellItem(bPRotR, 88, 40, "Rotate"); StyleShellItem(bDuplicate, 102, 40, "Duplicate");
            StyleShellItem(bDelete, 84, 40, "Delete"); StyleShellItem(bExtract, 90, 40, "Extract"); StyleShellItem(bInsert, 86, 40);
            pageBox.Width = S(44); pageBox.Font = new Font("Segoe UI", 10f); pageBox.Margin = new Padding(S(4), S(11), S(4), S(8));
            zoomBox.Width = S(110); zoomBox.Font = new Font("Segoe UI", 9.5f); zoomBox.Margin = new Padding(S(4), S(10), S(4), S(8));
            foreach (var item in new ToolStripItem[] { bZoomIn, zoomBox, bZoomOut }) item.Alignment = ToolStripItemAlignment.Right;
            foreach (var item in new ToolStripItem[] { bCut, bCopy, bPaste, bPRotL, bRotL, bRotR, bInsert, bExtract }) item.Overflow = ToolStripItemOverflow.Always;
            contextTools.Items.AddRange(new ToolStripItem[] { selectionSummary, selectAllPages, bPrev, pageBox, pageOf, bNext,
                bZoomIn, zoomBox, bZoomOut, bPRotR, bDuplicate, bDelete, bEarlier, bLater, bCut, bCopy, bPaste, bPRotL, bRotL, bRotR, bInsert, bExtract });
            editGroup.AddRange(new ToolStripItem[] { selectionSummary, selectAllPages, bDuplicate, bEarlier, bLater });
            host.Controls.Add(contextTools);
            documentStatus = new StatusStrip { Dock = DockStyle.Bottom, SizingGrip = false, AutoSize = false, Height = S(31),
                BackColor = Color.FromArgb(0xF2, 0xF4, 0xF7), ForeColor = Theme.SubtleText, Padding = new Padding(S(18), 0, S(18), 0), Font = new Font("Segoe UI", 8.5f) };
            positionStatus = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            saveStatus = new ToolStripStatusLabel { TextAlign = ContentAlignment.MiddleRight };
            documentStatus.Items.AddRange(new ToolStripItem[] { positionStatus, saveStatus }); host.Controls.Add(documentStatus);
            shellHeader = new Panel { Dock = DockStyle.Top, Height = S(48), BackColor = Theme.Chrome };
            var brand = new Label { Text = "Gravity", Dock = DockStyle.Left, Width = S(160), Padding = new Padding(S(22), 0, 0, 0),
                Font = new Font("Segoe UI Semibold", 13f), ForeColor = Theme.Text, TextAlign = ContentAlignment.MiddleLeft };
            tabStrip.Dock = DockStyle.Fill; shellHeader.Controls.Add(tabStrip); shellHeader.Controls.Add(brand);
            Controls.Add(commandBar); Controls.Add(shellHeader);
            Controls.SetChildIndex(content, 0); Controls.SetChildIndex(commandBar, 1); Controls.SetChildIndex(shellHeader, 2); Controls.SetChildIndex(menu, 3);
            menu.Visible = false; menu.ForeColor = Theme.Text;
            sidebar.ResumeLayout(true); content.ResumeLayout(true); ResumeLayout(true); LayoutLucideShell();
        }

        private void LayoutLucideShell()
        {
            if (commandBar == null || shellLayout) return;
            shellLayout = true;
            try
            {
                bool compact = ClientSize.Width < S(1000);
                toolbar.Width = S(ClientSize.Width < S(620) ? 262 : ClientSize.Width < S(700) ? 310 : 358);
                toolbar.Bounds = new Rectangle(commandBar.Width - toolbar.Width, 0, toolbar.Width, S(58));
                openingTools.Bounds = new Rectangle(0, 0, S(166), S(58));
                commandBar.Height = S(compact ? 110 : 62);
                modeTools.Bounds = new Rectangle((commandBar.Width - S(234)) / 2, S(compact ? 60 : 5), S(234), S(46));
            }
            finally { shellLayout = false; }
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
            shellHeader.Visible = commandBar.Visible = !fullscreen; menu.Visible = menuExpanded && !fullscreen;
            contextTools.Visible = documentStatus.Visible = has && !fullscreen; modeTools.Visible = has;
            bRead.Checked = has && !organizing; bRead.Enabled = has && !presentationMode; bFind.Enabled = has;
            bOutline.Checked = !pagePanel.Visible; bPages.Checked = pagePanel.Visible; bOutline.Enabled = bPages.Enabled = has;
            bMenu.Checked = menu.Visible; bDuplicate.Enabled = selectedCount > 0;
            var selection = organizer.SelectedIndices();
            bEarlier.Enabled = selectedCount > 0 && selection.Count > 0 && selection[0] > 0;
            bLater.Enabled = selectedCount > 0 && selection.Count > 0 && selection[selection.Count - 1] < count - 1;
            selectionSummary.Text = Count(selectedCount) + " selected";
            foreach (var item in findGroup) item.Visible = false;
            navigationTitle.Text = has ? active.Doc.DisplayName : ""; navigationPosition.Text = has ? Count(count) : "";
            positionStatus.Text = organizing ? Count(count) + " · " + Count(selectedCount) + " selected" : "Page " + (count > 0 ? view.CurrentPage + 1 : 0) + " of " + count;
            saveStatus.Text = has && active.Doc.IsModified ? "Unsaved changes" : "All changes saved";
            if (has && active.Doc.FilePath == null) saveStatus.Text = "New document · not saved";
            bPrint.Visible = has && ClientSize.Width >= S(700); bRedo.Visible = has && ClientSize.Width >= S(620);
            int width = contextTools.Width;
            foreach (var item in new ToolStripItem[] { bCut, bCopy, bPaste, bPRotL, bRotL, bRotR, bInsert, bExtract }) item.Visible = false;
            foreach (var item in new ToolStripItem[] { bZoomIn, bZoomOut, zoomBox }) item.Visible = has && !organizing && width >= S(500);
            bPrev.Visible = bNext.Visible = has && !organizing && width >= S(330);
            selectAllPages.Visible = has && organizing && width >= S(480);
            bPRotR.Visible = has && organizing && width >= S(480);
            bDuplicate.Visible = has && organizing && width >= S(600);
            bDelete.Visible = has && organizing && width >= S(700);
            bEarlier.Visible = bLater.Visible = has && organizing && width >= S(800);
            LayoutLucideShell();
            SyncOutlinePosition(organizing ? organizer.FocusIndex : view.CurrentPage);
            toc.Invalidate();
        }

        private void PopulateContextMenu()
        {
            foreach (ToolStripItem item in contextMore.DropDownItems.Cast<ToolStripItem>().ToArray()) item.Dispose();
            contextMore.DropDownItems.Clear();
            var commands = Organizing
                ? new[] { miPRotL, miPRotR, miDuplicate, miDelete, miCut, miCopy, miPaste, miMoveEarlier, miMoveLater,
                    miSelectAllPages, miInsertBlank, miInsertFile, miInsertImages, miExtract, miSavePages }
                : new[] { miPrev, miNext, miGoto, miFitPage, miFitWidth, miActual, miZoomIn, miZoomOut, miRotL, miRotR,
                    miSingle, miFacing, miBook, miContinuous };
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
                    if (doc.Pages[i].Src == doc.Base && !positions.ContainsKey(doc.Pages[i].Index)) positions.Add(doc.Pages[i].Index, i);
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
            var row = new Rectangle(6, e.Bounds.Y, Math.Max(1, toc.ClientSize.Width - 12), toc.ItemHeight - 1);
            using (var background = new SolidBrush(Theme.Chrome)) e.Graphics.FillRectangle(background, new Rectangle(0, e.Bounds.Y, toc.Width, toc.ItemHeight));
            if (selected)
                using (var path = Theme.RoundRect(row, S(6)))
                using (var brush = new SolidBrush(Theme.AccentSoft)) e.Graphics.FillPath(brush, path);
            var text = new Rectangle(Math.Max(S(12), e.Bounds.X), e.Bounds.Y, Math.Max(1, toc.Width - Math.Max(S(12), e.Bounds.X) - S(12)), toc.ItemHeight);
            TextRenderer.DrawText(e.Graphics, e.Node.Text, toc.Font, text, selected ? Theme.Accent : Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            if (selected && toc.Focused) ControlPaint.DrawFocusRectangle(e.Graphics, row, Theme.Accent, Theme.Chrome);
        }
    }

    internal class RoundedRenderer : ToolStripProfessionalRenderer
    {
        public RoundedRenderer() : base(new FlatColors()) { RoundedEdges = true; }
        private void DrawButton(ToolStripItemRenderEventArgs e)
        {
            var button = e.Item as ToolStripButton;
            bool selected = button != null && button.Checked;
            var rect = new RectangleF(1, 1, Math.Max(1, e.Item.Width - 2), Math.Max(1, e.Item.Height - 2));
            float scale = e.Graphics.DpiX / 96f;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Theme.RoundRect(rect, 6 * scale))
            {
                if (selected || e.Item.Selected || e.Item.Pressed)
                    using (var fill = new SolidBrush(e.Item.Pressed ? Color.FromArgb(0xD4, 0xE1, 0xF1) : selected ? Theme.AccentSoft : Color.FromArgb(0xE2, 0xE8, 0xF0))) e.Graphics.FillPath(fill, path);
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
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            var button = e.Item as ToolStripButton;
            e.TextColor = !e.Item.Enabled ? SystemColors.GrayText : button != null && button.Checked ? Theme.Accent : e.Item.ForeColor;
            base.OnRenderItemText(e);
        }
    }
}
