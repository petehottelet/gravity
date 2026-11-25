import AppKit
import PDFKit
import UniformTypeIdentifiers

final class FormPDFView: PDFView {
    var editField: ((PDFAnnotation) -> Void)?

    override func mouseDown(with event: NSEvent) {
        let point = convert(event.locationInWindow, from: nil)
        if let page = page(for: point, nearest: false),
           let field = page.annotation(at: convert(point, to: page)),
           PDFModel.isWidget(field) {
            editField?(field)
            return
        }
        super.mouseDown(with: event)
    }

    override func keyDown(with event: NSEvent) {
        // Keep widget editing in the form sheet, where it participates in undo.
        if event.keyCode == 48 {
            NSApp.sendAction(#selector(DocumentWindow.fillForm(_:)), to: nil, from: self)
            return
        }
        super.keyDown(with: event)
    }

    override func perform(_ action: PDFAction) {
        if let link = action as? PDFActionURL {
            guard let url = link.url, let scheme = url.scheme?.lowercased(),
                  ["https", "http", "mailto"].contains(scheme) else { return }
            NSWorkspace.shared.open(url)
        } else if action is PDFActionGoTo {
            super.perform(action)
        }
    }
}

final class DocumentWindow: NSWindowController, NSCollectionViewDataSource, NSCollectionViewDelegate,
                            NSOutlineViewDataSource, NSOutlineViewDelegate, NSMenuItemValidation {
    private let pdfView = FormPDFView()
    private let pages = PageCollectionView()
    private let organizer = PageCollectionView()
    private let outline = NSOutlineView()
    private let search = NSSearchField()
    private let root = WorkspaceSurface()
    private let header = WorkspaceSurface()
    private let commands = WorkspaceSurface()
    private let modeTrack = WorkspaceSurface()
    private let sidebar = WorkspaceSurface()
    private let workspace = WorkspaceSurface()
    private let context = WorkspaceSurface()
    private let searchBar = WorkspaceSurface()
    private let status = WorkspaceSurface()
    private let pageScroll = NSScrollView()
    private let outlineScroll = NSScrollView()
    private let organizerScroll = NSScrollView()
    private let brand = DocumentTheme.label("Gravity", size: 14, weight: .bold)
    private let brandMark = NSImageView()
    private let titleLabel = DocumentTheme.label(size: 13, weight: .semibold)
    private let documentState = DocumentTheme.label(size: 11, color: DocumentTheme.muted)
    private let navigationTitle = DocumentTheme.label(size: 11, weight: .semibold, color: DocumentTheme.muted)
    private let navigationCount = DocumentTheme.label(size: 11, color: DocumentTheme.muted)
    private let navigationPosition = DocumentTheme.label(size: 11, color: DocumentTheme.muted)
    private let outlineEmpty = DocumentTheme.label("No bookmarks in this PDF", size: 12, color: DocumentTheme.muted)
    private let position = DocumentTheme.label(size: 11, color: DocumentTheme.muted)
    private let guidance = DocumentTheme.label(size: 11, color: DocumentTheme.muted)
    private let pageField = NSTextField()
    private let pageCount = DocumentTheme.label(size: 12, color: DocumentTheme.muted)
    private let selectionCount = DocumentTheme.label(size: 12, weight: .semibold)
    private let searchFeedback = DocumentTheme.label(size: 11, color: DocumentTheme.muted)
    private var controls: [String: WorkspaceButton] = [:]
    private var observers: [NSObjectProtocol] = []
    private let thumbnailCache = NSCache<NSNumber, NSImage>()
    private var pageTitles: [Int: String] = [:]
    private var outlineLocations: [(page: Int, item: PDFOutline)] = []
    private var organizing = false
    private var showingPages = false
    private var sidebarVisible = true
    private var searching = false
    private var updating = false
    private unowned let gravity: GravityDocument
    private var model: PDFModel { gravity.model }
    private static var pageClipboard: Data?

    init(document: GravityDocument) {
        gravity = document
        let window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 1100, height: 780),
                              styleMask: [.titled, .closable, .miniaturizable, .resizable], backing: .buffered, defer: false)
        window.minSize = NSSize(width: 560, height: 440)
        window.tabbingMode = .preferred
        window.titleVisibility = .hidden
        window.titlebarAppearsTransparent = true
        window.backgroundColor = DocumentTheme.chrome
        // The approved light workspace includes PDFKit and native field editors.
        window.appearance = NSAppearance(named: .aqua)
        window.center()
        super.init(window: window)
        shouldCascadeWindows = true
        window.setFrameAutosaveName("GravityDocument")

        window.contentView = root
        root.arrange = { [weak self] in self?.layoutWorkspace() }
        for view in [header, commands, sidebar, workspace, status] { root.addSubview(view) }
        for view in [context, searchBar, pdfView, organizerScroll] { workspace.addSubview(view) }
        for view in [brandMark, brand, titleLabel, documentState] { header.addSubview(view) }
        for view in [navigationTitle, navigationCount, navigationPosition, outlineScroll, pageScroll, outlineEmpty] {
            sidebar.addSubview(view)
        }
        status.addSubview(position)
        status.addSubview(guidance)
        titleLabel.alignment = .center
        documentState.alignment = .center
        navigationCount.alignment = .right
        guidance.alignment = .right
        if let url = Bundle.main.url(forResource: "g-mark", withExtension: "png") {
            brandMark.image = NSImage(contentsOf: url)
        }
        brandMark.imageScaling = .scaleProportionallyDown
        commands.bottomRule = true
        context.bottomRule = true
        sidebar.rightRule = true
        searchBar.bottomRule = true
        status.topRule = true
        status.fill = DocumentTheme.status
        modeTrack.fill = DocumentTheme.track
        modeTrack.cornerRadius = 8
        commands.addSubview(modeTrack)

        add("sidebar", "", "sidebar.left", "Show or hide sidebar", #selector(toggleSidebar(_:)), to: commands)
        add("open", "Open", "folder", "Open PDF (Command+O)", #selector(openDocument(_:)), to: commands)
        add("read", "Read", "book", "Read document", #selector(showRead(_:)), to: modeTrack)
        add("organize", "Organize", "square.grid.2x2", "Organize pages", #selector(showOrganize(_:)), to: modeTrack)
        add("find", "", "magnifyingglass", "Find in document (Command+F)", #selector(find(_:)), to: commands)
        add("undo", "", "arrow.uturn.backward", "Undo (Command+Z)", #selector(undoEdit(_:)), to: commands)
        add("print", "", "printer", "Print (Command+P)", #selector(printDocument(_:)), to: commands)
        add("save", "Save", "square.and.arrow.down", "Save PDF (Command+S)", #selector(saveDocument(_:)), to: commands)
        controls["save"]?.outlined = true
        add("outline", "Outline", nil, "Document outline", #selector(showOutline(_:)), to: sidebar)
        add("pages", "Pages", nil, "Page thumbnails", #selector(showPages(_:)), to: sidebar)
        controls["outline"]?.selectionStyle = .navigation
        controls["pages"]?.selectionStyle = .navigation
        add("previous", "", "chevron.left", "Previous page", #selector(previousPage(_:)), to: context)
        add("next", "", "chevron.right", "Next page", #selector(nextPage(_:)), to: context)
        add("minus", "", "minus", "Zoom out (Command+-)", #selector(zoomOut(_:)), to: context)
        add("zoom", "Fit page", "chevron.down", "Zoom options", #selector(chooseZoom(_:)), to: context)
        controls["zoom"]?.imagePosition = .imageTrailing
        add("plus", "", "plus", "Zoom in (Command++)", #selector(zoomIn(_:)), to: context)
        add("form", "Fill Form", "square.and.pencil", "Fill form (Command+Shift+F)", #selector(fillForm(_:)), to: context)
        add("selectAll", "Select all", nil, "Select all pages (Command+A)", #selector(selectAllPages(_:)), to: context)
        add("rotate", "Rotate", "rotate.right", "Rotate selected pages clockwise", #selector(rotateRight(_:)), to: context)
        add("duplicate", "Duplicate", "plus.square.on.square", "Duplicate selected pages", #selector(duplicatePages(_:)), to: context)
        add("delete", "Delete", "trash", "Delete selected pages", #selector(deletePages(_:)), to: context)
        add("earlier", "", "arrow.up", "Move selected pages earlier", #selector(moveEarlier(_:)), to: context)
        add("later", "", "arrow.down", "Move selected pages later", #selector(moveLater(_:)), to: context)
        add("more", "More", "ellipsis", "More page actions", #selector(morePageActions(_:)), to: context)
        for view in [pageField, pageCount, selectionCount] { context.addSubview(view) }
        pageField.alignment = .center
        pageField.font = .monospacedDigitSystemFont(ofSize: 12, weight: .regular)
        pageField.bezelStyle = .roundedBezel
        pageField.target = self
        pageField.action = #selector(goToPage(_:))
        pageField.identifier = NSUserInterfaceItemIdentifier("pageNumber")
        pageField.setAccessibilityLabel("Page number")

        search.placeholderString = "Find in document"
        search.target = self
        search.action = #selector(findNext(_:))
        search.sendsSearchStringImmediately = false
        search.sendsWholeSearchString = true
        search.setAccessibilityLabel("Find in document")
        searchBar.addSubview(search)
        searchBar.addSubview(searchFeedback)
        add("findNext", "Next", "chevron.right", "Find next (Command+G)", #selector(findNext(_:)), to: searchBar)
        add("closeFind", "", "xmark", "Close find", #selector(closeFind(_:)), to: searchBar)

        configureCollection(pages, in: pageScroll, rail: true)
        configureCollection(organizer, in: organizerScroll, rail: false)
        let column = NSTableColumn(identifier: NSUserInterfaceItemIdentifier("Outline"))
        outline.addTableColumn(column)
        outline.outlineTableColumn = column
        outline.headerView = nil
        outline.rowHeight = 36
        outline.indentationPerLevel = 14
        outline.style = .sourceList
        outline.backgroundColor = DocumentTheme.chrome
        outline.dataSource = self
        outline.delegate = self
        outline.setAccessibilityLabel("Document outline")
        outlineScroll.documentView = outline
        outlineScroll.hasVerticalScroller = true
        outlineScroll.drawsBackground = false
        outlineEmpty.alignment = .center
        thumbnailCache.countLimit = 120
        thumbnailCache.totalCostLimit = 32 * 1024 * 1024
        pdfView.autoScales = true
        pdfView.displayMode = .singlePageContinuous
        pdfView.backgroundColor = DocumentTheme.canvas
        pdfView.pageBreakMargins = NSEdgeInsets(top: 20, left: 24, bottom: 20, right: 24)
        pdfView.setAccessibilityLabel("PDF reading view")
        pdfView.editField = { [weak self] field in self?.edit(field) }
        observe(.PDFViewPageChanged, object: pdfView) { $0.pageChanged() }
        observe(.PDFViewScaleChanged, object: pdfView) { $0.updateChrome() }
        for name in [Notification.Name.NSUndoManagerDidUndoChange, .NSUndoManagerDidRedoChange,
                     .NSUndoManagerDidCloseUndoGroup] {
            observe(name, object: gravity.undoManager) { $0.updateChrome() }
        }
        root.layoutSubtreeIfNeeded()
        refresh()
    }

    required init?(coder: NSCoder) { fatalError("Use init(document:)") }
    deinit { observers.forEach { NotificationCenter.default.removeObserver($0) } }

    private func observe(_ name: Notification.Name, object: Any?, action: @escaping (DocumentWindow) -> Void) {
        observers.append(NotificationCenter.default.addObserver(forName: name, object: object, queue: .main) { [weak self] _ in
            if let self = self { action(self) }
        })
    }

    private func add(_ id: String, _ title: String, _ symbol: String?, _ label: String, _ action: Selector, to parent: NSView) {
        let button = WorkspaceButton(title, symbol: symbol, label: label, target: self, action: action)
        button.identifier = NSUserInterfaceItemIdentifier(id)
        controls[id] = button
        parent.addSubview(button)
    }

    private func configureCollection(_ collection: PageCollectionView, in scroll: NSScrollView, rail: Bool) {
        let layout = NSCollectionViewFlowLayout()
        layout.itemSize = rail ? NSSize(width: 144, height: 210) : NSSize(width: 150, height: 234)
        layout.minimumInteritemSpacing = 22
        layout.minimumLineSpacing = 24
        layout.sectionInset = NSEdgeInsets(top: 24, left: 24, bottom: 24, right: 24)
        collection.collectionViewLayout = layout
        collection.isSelectable = true
        collection.allowsMultipleSelection = !rail
        collection.allowsEmptySelection = false
        collection.backgroundColors = [rail ? DocumentTheme.chrome : DocumentTheme.canvas]
        collection.dataSource = self
        collection.delegate = self
        collection.register(PageThumbnailItem.self, forItemWithIdentifier: PageThumbnailItem.reuseID)
        collection.identifier = NSUserInterfaceItemIdentifier(rail ? "pageRail" : "organizer")
        collection.setAccessibilityLabel(rail ? "Page thumbnails" : "Organize pages")
        collection.openPage = { [weak self] index in self?.readPage(index) }
        collection.deleteSelection = { [weak self] in self?.deletePages(nil) }
        collection.copySelection = { [weak self] in self?.copyPages(nil) }
        collection.cutSelection = { [weak self] in self?.cutPages(nil) }
        collection.pasteSelection = { [weak self] in self?.pastePages(nil) }
        scroll.documentView = collection
        scroll.hasVerticalScroller = true
        scroll.autohidesScrollers = true
        scroll.drawsBackground = false
    }

    private func place(_ id: String, _ x: CGFloat, _ y: CGFloat, _ width: CGFloat, _ height: CGFloat = 40) {
        controls[id]?.frame = NSRect(x: x, y: y, width: width, height: height)
    }

    private func layoutWorkspace() {
        let width = root.bounds.width, height = root.bounds.height
        let compact = width < 820
        let commandHeight: CGFloat = compact ? 104 : 60
        let sidebarWidth: CGFloat = sidebarVisible && width >= 700 ? (width < 1000 ? 210 : 236) : 0
        let bodyY: CGFloat = 48 + commandHeight
        header.frame = NSRect(x: 0, y: 0, width: width, height: 48)
        brandMark.frame = NSRect(x: 20, y: 10, width: 24, height: 26)
        brand.frame = NSRect(x: 54, y: 15, width: 90, height: 22)
        titleLabel.frame = NSRect(x: 155, y: 7, width: max(0, width - 310), height: 18)
        documentState.frame = NSRect(x: 155, y: 27, width: max(0, width - 310), height: 16)
        commands.frame = NSRect(x: 0, y: 48, width: width, height: commandHeight)
        place("sidebar", 12, 8, 40, 44)
        place("open", 56, 8, 86, 44)
        place("save", width - 98, 8, 86, 44)
        place("print", width - 142, 8, 40, 44)
        place("undo", width - 186, 8, 40, 44)
        place("find", width - 230, 8, 40, 44)
        modeTrack.frame = NSRect(x: (width - 238) / 2, y: compact ? 56 : 8, width: 238, height: 44)
        place("read", 3, 3, 116, 38)
        place("organize", 119, 3, 116, 38)
        let bodyHeight = max(0, height - bodyY - 30)
        sidebar.isHidden = sidebarWidth == 0
        if sidebar.isHidden, let responder = window?.firstResponder as? NSView, responder.isDescendant(of: sidebar) {
            window?.makeFirstResponder(organizing ? organizer : pdfView)
        }
        sidebar.frame = NSRect(x: 0, y: bodyY, width: sidebarWidth, height: bodyHeight)
        place("outline", 12, 10, max(0, (sidebarWidth - 24) / 2), 36)
        place("pages", sidebarWidth / 2, 10, max(0, (sidebarWidth - 24) / 2), 36)
        navigationTitle.frame = NSRect(x: 20, y: 65, width: max(0, sidebarWidth - 95), height: 18)
        navigationCount.frame = NSRect(x: sidebarWidth - 75, y: 65, width: 55, height: 18)
        navigationPosition.frame = NSRect(x: 20, y: bodyHeight - 30, width: max(0, sidebarWidth - 40), height: 18)
        let navigationRect = NSRect(x: 0, y: 98, width: max(0, sidebarWidth - 1), height: max(0, bodyHeight - 138))
        outlineScroll.frame = navigationRect
        pageScroll.frame = navigationRect
        outlineEmpty.frame = NSRect(x: 14, y: 118, width: max(0, sidebarWidth - 28), height: 20)
        if let layout = pages.collectionViewLayout as? NSCollectionViewFlowLayout {
            let inset = max(8, (pageScroll.contentSize.width - 144) / 2)
            layout.sectionInset = NSEdgeInsets(top: 16, left: inset, bottom: 16, right: inset)
        }
        sizeCollection(pages, in: pageScroll)
        let available = width - sidebarWidth
        workspace.frame = NSRect(x: sidebarWidth, y: bodyY, width: available, height: bodyHeight)
        let wrap = organizing && available < 680
        let contextHeight: CGFloat = wrap ? 100 : 56
        context.frame = NSRect(x: 0, y: 0, width: available, height: contextHeight)
        let small = available < 540
        place("previous", 12, 8, 32)
        pageField.frame = NSRect(x: 48, y: 13, width: 46, height: 30)
        pageCount.frame = NSRect(x: 102, y: 19, width: 60, height: 20)
        place("next", 162, 8, 32)
        controls["form"]?.isHidden = organizing || small
        place("form", 204, 8, 110)
        place("minus", available - 188, 8, 32)
        place("zoom", available - 152, 8, 104)
        place("plus", available - 44, 8, 32)
        selectionCount.frame = NSRect(x: 20, y: 8, width: 130, height: 18)
        place("selectAll", 14, 27, 90, 24)
        let actionX: CGFloat = wrap ? 12 : 160
        let actionY: CGFloat = wrap ? 54 : 8
        place("rotate", actionX, actionY, 90)
        place("duplicate", actionX + 96, actionY, 112)
        place("delete", actionX + 214, actionY, 88)
        let showMovement = available >= 800
        controls["earlier"]?.isHidden = !organizing || !showMovement
        controls["later"]?.isHidden = !organizing || !showMovement
        place("earlier", 480, 8, 40)
        place("later", 524, 8, 40)
        place("more", available - 88, 8, 76)
        searchBar.isHidden = !searching
        searchBar.frame = NSRect(x: 0, y: contextHeight, width: available, height: searching ? 48 : 0)
        search.frame = NSRect(x: 16, y: 10, width: max(140, available - 280), height: 28)
        searchFeedback.frame = NSRect(x: available - 250, y: 15, width: 124, height: 20)
        place("findNext", available - 122, 4, 76)
        place("closeFind", available - 44, 4, 36)
        let canvasY = contextHeight + (searching ? 48 : 0)
        let canvas = NSRect(x: 0, y: canvasY, width: available, height: max(0, bodyHeight - canvasY))
        pdfView.frame = canvas
        organizerScroll.frame = canvas
        if let layout = organizer.collectionViewLayout as? NSCollectionViewFlowLayout {
            let inset: CGFloat = available < 650 ? 18 : 34
            layout.sectionInset = NSEdgeInsets(top: 26, left: inset, bottom: 26, right: inset)
        }
        sizeCollection(organizer, in: organizerScroll)
        status.frame = NSRect(x: 0, y: height - 30, width: width, height: 30)
        position.frame = NSRect(x: 20, y: 8, width: 180, height: 16)
        guidance.frame = NSRect(x: 210, y: 8, width: max(0, width - 230), height: 16)
    }

    private func sizeCollection(_ collection: NSCollectionView, in scroll: NSScrollView) {
        let width = scroll.contentSize.width
        guard width > 0 else { return }
        if collection.frame.width != width {
            collection.setFrameSize(NSSize(width: width, height: max(scroll.contentSize.height, collection.frame.height)))
            collection.collectionViewLayout?.invalidateLayout()
        }
    }

    private var currentIndex: Int {
        guard let page = pdfView.currentPage, let pdf = pdfView.document else { return 0 }
        let index = pdf.index(for: page)
        return index == NSNotFound ? 0 : index
    }

    private var selected: IndexSet {
        let indices = organizing ? IndexSet(organizer.selectionIndexPaths.map { $0.item }) : IndexSet(integer: currentIndex)
        return indices.isEmpty ? IndexSet(integer: currentIndex) : indices
    }

    func refresh() {
        let index = max(0, min(currentIndex, model.pdf.pageCount - 1))
        let selection = selected.intersection(IndexSet(integersIn: 0..<model.pdf.pageCount))
        updating = true
        thumbnailCache.removeAllObjects()
        searchFeedback.stringValue = ""
        pdfView.document = model.pdf
        rebuildOutline()
        pages.reloadData()
        organizer.reloadData()
        if let page = model.pdf.page(at: index) { pdfView.go(to: page) }
        pages.selectionIndexPaths = [IndexPath(item: index, section: 0)]
        organizer.selectionIndexPaths = Set((selection.isEmpty ? IndexSet(integer: index) : selection).map { IndexPath(item: $0, section: 0) })
        updating = false
        pageChanged()
    }

    private func pageChanged() {
        guard !updating else { return }
        updating = true
        let path = IndexPath(item: currentIndex, section: 0)
        pages.selectionIndexPaths = [path]
        if !organizing { organizer.selectionIndexPaths = [path] }
        if showingPages && !sidebar.isHidden { pages.scrollToItems(at: [path], scrollPosition: .nearestHorizontalEdge) }
        if let item = outlineLocations.last(where: { $0.page <= currentIndex })?.item {
            let row = outline.row(forItem: item)
            if row >= 0 { outline.selectRowIndexes(IndexSet(integer: row), byExtendingSelection: false) }
        }
        updating = false
        updateChrome()
    }

    func updateDocumentStatus() { updateChrome() }

    override func synchronizeWindowTitleWithDocumentName() {
        super.synchronizeWindowTitleWithDocumentName()
        updateChrome()
    }

    private func updateChrome() {
        titleLabel.stringValue = gravity.displayName
        titleLabel.toolTip = gravity.displayName
        let count = model.pdf.pageCount
        let countText = "\(count) \(count == 1 ? "page" : "pages")"
        let saveText = gravity.isDocumentEdited ? "Unsaved changes" : gravity.fileURL == nil ? "Not saved yet" : "Saved"
        documentState.stringValue = "\(countText) · \(saveText)"
        documentState.identifier = NSUserInterfaceItemIdentifier("documentState")
        navigationTitle.stringValue = (gravity.displayName as NSString).deletingPathExtension
        navigationCount.stringValue = countText
        navigationPosition.stringValue = "Page \(currentIndex + 1) of \(count)"
        if pageField.currentEditor() == nil { pageField.integerValue = currentIndex + 1 }
        pageCount.stringValue = "of \(count)"
        selectionCount.stringValue = "\(selected.count) selected"
        position.stringValue = organizing ? "\(selected.count) of \(count) selected" : navigationPosition.stringValue
        guidance.stringValue = organizing ? "Command-click to select · Double-click to read" : "Nothing is written until you save"
        controls["read"]?.selected = !organizing
        controls["organize"]?.selected = organizing
        controls["outline"]?.selected = !showingPages
        controls["pages"]?.selected = showingPages
        controls["find"]?.selected = searching
        controls["sidebar"]?.selected = sidebarVisible
        controls["sidebar"]?.selectionStyle = .navigation
        controls["find"]?.selectionStyle = .navigation
        controls["zoom"]?.title = pdfView.autoScales ? "Fit page" : "\(Int((pdfView.scaleFactor * 100).rounded()))%"
        controls["previous"]?.isEnabled = currentIndex > 0
        controls["next"]?.isEnabled = currentIndex + 1 < count
        controls["undo"]?.isEnabled = gravity.undoManager?.canUndo == true
        controls["save"]?.isEnabled = gravity.isDocumentEdited || gravity.fileURL == nil
        controls["print"]?.isEnabled = model.pdf.allowsPrinting
        let editable = model.pdf.allowsDocumentAssembly
        for id in ["rotate", "earlier", "later"] { controls[id]?.isEnabled = editable }
        controls["duplicate"]?.isEnabled = editable && model.pdf.allowsCopying
        controls["delete"]?.isEnabled = editable && selected.count < count
        controls["earlier"]?.isEnabled = editable && (selected.first ?? 0) > 0
        controls["later"]?.isEnabled = editable && (selected.last ?? 0) + 1 < count
        controls["form"]?.isEnabled = model.pdf.allowsFormFieldEntry && !model.widgets(on: currentIndex).isEmpty
        for id in ["previous", "next", "minus", "zoom", "plus", "form"] { controls[id]?.isHidden = organizing }
        for id in ["rotate", "duplicate", "delete", "selectAll", "earlier", "later", "more"] { controls[id]?.isHidden = !organizing }
        pageField.isHidden = organizing
        pageCount.isHidden = organizing
        selectionCount.isHidden = !organizing
        pdfView.isHidden = organizing
        organizerScroll.isHidden = !organizing
        pageScroll.isHidden = !showingPages
        outlineScroll.isHidden = showingPages || (model.pdf.outlineRoot?.numberOfChildren ?? 0) == 0
        outlineEmpty.isHidden = showingPages || (model.pdf.outlineRoot?.numberOfChildren ?? 0) > 0
        root.needsLayout = true
    }

    private func rebuildOutline() {
        pageTitles.removeAll()
        outlineLocations.removeAll()
        func visit(_ parent: PDFOutline) {
            for index in 0..<parent.numberOfChildren {
                guard let item = parent.child(at: index) else { continue }
                if let page = item.destination?.page {
                    let index = model.pdf.index(for: page)
                    if index != NSNotFound {
                        outlineLocations.append((index, item))
                        if let label = item.label, !label.isEmpty, pageTitles[index] == nil { pageTitles[index] = label }
                    }
                }
                visit(item)
            }
        }
        if let root = model.pdf.outlineRoot { visit(root) }
        outlineLocations.sort { $0.page < $1.page }
        outline.reloadData()
        outline.expandItem(nil, expandChildren: true)
    }

    func outlineView(_ outlineView: NSOutlineView, numberOfChildrenOfItem item: Any?) -> Int {
        ((item as? PDFOutline) ?? model.pdf.outlineRoot)?.numberOfChildren ?? 0
    }
    func outlineView(_ outlineView: NSOutlineView, child index: Int, ofItem item: Any?) -> Any {
        ((item as? PDFOutline) ?? model.pdf.outlineRoot)!.child(at: index)!
    }
    func outlineView(_ outlineView: NSOutlineView, isItemExpandable item: Any) -> Bool {
        ((item as? PDFOutline)?.numberOfChildren ?? 0) > 0
    }
    func outlineView(_ outlineView: NSOutlineView, rowViewForItem item: Any) -> NSTableRowView? { OutlineRowView() }
    func outlineView(_ outlineView: NSOutlineView, viewFor tableColumn: NSTableColumn?, item: Any) -> NSView? {
        guard let item = item as? PDFOutline else { return nil }
        let cell = NSTableCellView()
        let label = DocumentTheme.label(size: 12)
        let index = item.destination?.page.map { model.pdf.index(for: $0) }
        let prefix = index.flatMap { $0 == NSNotFound ? nil : "\($0 + 1)    " } ?? ""
        label.stringValue = prefix + (item.label ?? "Untitled bookmark")
        label.translatesAutoresizingMaskIntoConstraints = false
        cell.addSubview(label)
        cell.textField = label
        cell.toolTip = label.stringValue
        NSLayoutConstraint.activate([
            label.leadingAnchor.constraint(equalTo: cell.leadingAnchor, constant: 4),
            label.trailingAnchor.constraint(equalTo: cell.trailingAnchor, constant: -8),
            label.centerYAnchor.constraint(equalTo: cell.centerYAnchor)
        ])
        return cell
    }
    func outlineViewSelectionDidChange(_ notification: Notification) {
        guard !updating, let item = outline.item(atRow: outline.selectedRow) as? PDFOutline else { return }
        setOrganizing(false)
        if let action = item.action { pdfView.perform(action) }
        else if let destination = item.destination { pdfView.go(to: destination) }
    }

    func collectionView(_ collectionView: NSCollectionView, numberOfItemsInSection section: Int) -> Int { model.pdf.pageCount }
    func collectionView(_ collectionView: NSCollectionView, itemForRepresentedObjectAt indexPath: IndexPath) -> NSCollectionViewItem {
        let item = collectionView.makeItem(withIdentifier: PageThumbnailItem.reuseID, for: indexPath)
        if let item = item as? PageThumbnailItem {
            let key = NSNumber(value: indexPath.item)
            var image = thumbnailCache.object(forKey: key)
            if image == nil {
                image = model.pdf.page(at: indexPath.item)?.thumbnail(of: NSSize(width: 280, height: 340), for: .cropBox)
                if let image = image { thumbnailCache.setObject(image, forKey: key, cost: 280 * 340 * 4) }
            }
            item.configure(image: image, index: indexPath.item, title: pageTitles[indexPath.item], rail: collectionView === pages)
        }
        return item
    }
    func collectionView(_ collectionView: NSCollectionView, didSelectItemsAt indexPaths: Set<IndexPath>) {
        guard !updating else { return }
        if collectionView === pages, let index = indexPaths.first?.item { readPage(index) }
        else { updateChrome() }
    }
    func collectionView(_ collectionView: NSCollectionView, didDeselectItemsAt indexPaths: Set<IndexPath>) {
        if !updating { updateChrome() }
    }

    private func setOrganizing(_ value: Bool) {
        guard organizing != value else { return }
        let readingIndex = selected.first ?? currentIndex
        organizing = value
        if value {
            organizer.selectionIndexPaths = [IndexPath(item: currentIndex, section: 0)]
            closeFind(nil)
        } else if let page = model.pdf.page(at: readingIndex) {
            pdfView.go(to: page)
        }
        updateChrome()
        root.layoutSubtreeIfNeeded()
        if value {
            window?.makeFirstResponder(organizer)
            organizer.scrollToItems(at: organizer.selectionIndexPaths, scrollPosition: .nearestHorizontalEdge)
        } else { window?.makeFirstResponder(pdfView) }
    }
    private func readPage(_ index: Int) {
        guard let page = model.pdf.page(at: index) else { return }
        setOrganizing(false)
        pdfView.go(to: page)
        pageChanged()
    }
    @objc func showRead(_ sender: Any?) { setOrganizing(false) }
    @objc func showOrganize(_ sender: Any?) { setOrganizing(true) }
    @objc func toggleSidebar(_ sender: Any?) { sidebarVisible.toggle(); updateChrome() }
    @objc func showOutline(_ sender: Any?) { showingPages = false; sidebarVisible = true; updateChrome() }
    @objc func showPages(_ sender: Any?) { showingPages = true; sidebarVisible = true; setOrganizing(false); updateChrome() }
    @objc func selectAllPages(_ sender: Any?) {
        setOrganizing(true)
        organizer.selectAll(sender)
        updateChrome()
    }
    @objc func openDocument(_ sender: Any?) { NSDocumentController.shared.openDocument(sender) }
    @objc func saveDocument(_ sender: Any?) { gravity.save(sender) }
    @objc func printDocument(_ sender: Any?) { gravity.printDocument(sender) }
    @objc func undoEdit(_ sender: Any?) { gravity.undoManager?.undo(); updateChrome() }
    @objc func goToPage(_ sender: Any?) {
        guard let number = Int(pageField.stringValue.trimmingCharacters(in: .whitespacesAndNewlines)),
              number > 0, number <= model.pdf.pageCount else {
            pageField.integerValue = currentIndex + 1
            NSSound.beep()
            return
        }
        window?.makeFirstResponder(pdfView)
        readPage(number - 1)
    }
    @objc func chooseZoom(_ sender: Any?) {
        let menu = NSMenu()
        let fit = NSMenuItem(title: "Fit page", action: #selector(fitPage(_:)), keyEquivalent: "")
        fit.target = self
        fit.state = pdfView.autoScales ? .on : .off
        menu.addItem(fit)
        for percent in [50, 75, 100, 125, 150, 200, 300] {
            let item = NSMenuItem(title: "\(percent)%", action: #selector(setZoom(_:)), keyEquivalent: "")
            item.tag = percent
            item.target = self
            item.state = !pdfView.autoScales && Int((pdfView.scaleFactor * 100).rounded()) == percent ? .on : .off
            menu.addItem(item)
        }
        if let button = controls["zoom"] { menu.popUp(positioning: nil, at: NSPoint(x: 0, y: button.bounds.height), in: button) }
    }
    @objc private func setZoom(_ sender: NSMenuItem) {
        pdfView.autoScales = false
        pdfView.scaleFactor = CGFloat(sender.tag) / 100
        updateChrome()
    }
    @objc func morePageActions(_ sender: Any?) {
        let menu = NSMenu()
        let items: [(String, Selector)] = [
            ("Rotate Left", #selector(rotateLeft(_:))), ("Move Earlier", #selector(moveEarlier(_:))),
            ("Move Later", #selector(moveLater(_:))), ("Cut Pages", #selector(cutPages(_:))),
            ("Copy Pages", #selector(copyPages(_:))), ("Paste Pages", #selector(pastePages(_:))),
            ("Insert Blank Page", #selector(insertBlank(_:))), ("Insert PDFs…", #selector(insertFiles(_:))),
            ("Extract to New Document", #selector(extractPages(_:)))
        ]
        for (title, action) in items {
            let item = NSMenuItem(title: title, action: action, keyEquivalent: "")
            item.target = self
            menu.addItem(item)
        }
        if let button = controls["more"] { menu.popUp(positioning: nil, at: NSPoint(x: 0, y: button.bounds.height), in: button) }
    }

    func validateMenuItem(_ menuItem: NSMenuItem) -> Bool {
        let editable = model.pdf.allowsDocumentAssembly
        switch menuItem.action {
        case #selector(showRead(_:)): menuItem.state = organizing ? .off : .on
        case #selector(showOrganize(_:)): menuItem.state = organizing ? .on : .off
        case #selector(toggleSidebar(_:)): menuItem.state = sidebarVisible ? .on : .off
        case #selector(previousPage(_:)): return currentIndex > 0
        case #selector(nextPage(_:)): return currentIndex + 1 < model.pdf.pageCount
        case #selector(deletePages(_:)): return editable && selected.count < model.pdf.pageCount
        case #selector(cutPages(_:)): return editable && model.pdf.allowsCopying && selected.count < model.pdf.pageCount
        case #selector(copyPages(_:)), #selector(extractPages(_:)): return model.pdf.allowsCopying
        case #selector(duplicatePages(_:)): return editable && model.pdf.allowsCopying
        case #selector(pastePages(_:)): return editable && Self.pageClipboard != nil
        case #selector(moveEarlier(_:)): return editable && (selected.first ?? 0) > 0
        case #selector(moveLater(_:)): return editable && (selected.last ?? 0) + 1 < model.pdf.pageCount
        case #selector(rotateLeft(_:)), #selector(rotateRight(_:)), #selector(insertBlank(_:)), #selector(insertFiles(_:)): return editable
        case #selector(fillForm(_:)): return model.pdf.allowsFormFieldEntry
        default: break
        }
        return true
    }

    @objc func previousPage(_ sender: Any?) { setOrganizing(false); pdfView.goToPreviousPage(sender) }
    @objc func nextPage(_ sender: Any?) { setOrganizing(false); pdfView.goToNextPage(sender) }
    @objc func zoomIn(_ sender: Any?) { pdfView.zoomIn(sender) }
    @objc func zoomOut(_ sender: Any?) { pdfView.zoomOut(sender) }
    @objc func fitPage(_ sender: Any?) { pdfView.autoScales = true; updateChrome() }
    @objc func find(_ sender: Any?) {
        setOrganizing(false)
        searching = true
        updateChrome()
        root.layoutSubtreeIfNeeded()
        window?.makeFirstResponder(search)
    }
    @objc func closeFind(_ sender: Any?) {
        searching = false
        searchFeedback.stringValue = ""
        window?.makeFirstResponder(organizing ? organizer : pdfView)
        updateChrome()
    }
    @objc func findNext(_ sender: Any?) {
        guard !search.stringValue.isEmpty else {
            searchFeedback.stringValue = ""
            pdfView.clearSelection()
            return
        }
        if !searching { find(sender) }
        let result = model.pdf.findString(search.stringValue, fromSelection: pdfView.currentSelection, withOptions: .caseInsensitive)
            ?? model.pdf.findString(search.stringValue, fromSelection: nil, withOptions: .caseInsensitive)
        if let result = result {
            pdfView.setCurrentSelection(result, animate: false)
            pdfView.scrollSelectionToVisible(sender)
            searchFeedback.stringValue = "Match on page \(currentIndex + 1)"
        } else {
            pdfView.clearSelection()
            searchFeedback.stringValue = "No matches"
        }
    }

    @objc func rotateLeft(_ sender: Any?) { let indices = selected; gravity.edit("Rotate Pages") { try model.rotate(indices, by: -90) } }
    @objc func rotateRight(_ sender: Any?) { let indices = selected; gravity.edit("Rotate Pages") { try model.rotate(indices, by: 90) } }
    @objc func deletePages(_ sender: Any?) { let indices = selected; gravity.edit("Delete Pages") { try model.delete(indices) } }
    @objc func moveEarlier(_ sender: Any?) { move(-1) }
    @objc func moveLater(_ sender: Any?) { move(1) }
    private func move(_ offset: Int) {
        let indices = selected
        guard let first = indices.first, let last = indices.last, first + offset >= 0, last + offset < model.pdf.pageCount else { return }
        gravity.edit("Move Pages") { try model.move(indices, by: offset) }
        organizer.selectionIndexPaths = Set(indices.map { IndexPath(item: $0 + offset, section: 0) })
        updateChrome()
    }

    @objc func copyPages(_ sender: Any?) {
        do { Self.pageClipboard = try model.extract(selected) } catch { gravity.presentError(error) }
    }
    @objc func cutPages(_ sender: Any?) {
        guard selected.count < model.pdf.pageCount else { gravity.presentError(PDFError.lastPage); return }
        do { Self.pageClipboard = try model.extract(selected); deletePages(sender) } catch { gravity.presentError(error) }
    }
    @objc func pastePages(_ sender: Any?) {
        guard let data = Self.pageClipboard else { NSSound.beep(); return }
        let at = (selected.last ?? currentIndex) + 1
        gravity.edit("Paste Pages") { try model.insert(data, at: at) }
    }
    @objc func duplicatePages(_ sender: Any?) {
        do {
            let data = try model.extract(selected), at = (selected.last ?? currentIndex) + 1
            gravity.edit("Duplicate Pages") { try model.insert(data, at: at) }
        } catch { gravity.presentError(error) }
    }
    @objc func insertBlank(_ sender: Any?) {
        let at = (selected.last ?? currentIndex) + 1
        gravity.edit("Insert Blank Page") { try model.insert(PDFModel().data(), at: at) }
    }
    @objc func insertFiles(_ sender: Any?) {
        guard let window = window else { return }
        let insertionIndex = (selected.last ?? currentIndex) + 1
        let panel = NSOpenPanel()
        panel.allowedContentTypes = [.pdf]
        panel.allowsMultipleSelection = true
        panel.beginSheetModal(for: window) { [weak self] response in
            guard response == .OK, let self = self else { return }
            self.gravity.edit("Insert PDFs") {
                var index = insertionIndex
                for url in panel.urls {
                    let data = try Data(contentsOf: url), source = try PDFModel(data: data)
                    try self.model.insert(data, at: index)
                    index += source.pdf.pageCount
                }
            }
        }
    }
    @objc func extractPages(_ sender: Any?) {
        do {
            let document = GravityDocument()
            try document.read(from: model.extract(selected), ofType: "com.adobe.pdf")
            document.updateChangeCount(.changeDone)
            NSDocumentController.shared.addDocument(document)
            document.makeWindowControllers()
            document.showWindows()
        } catch { gravity.presentError(error) }
    }

    @objc func fillForm(_ sender: Any?) {
        guard let window = window else { return }
        let fields = model.widgets(on: currentIndex)
        let alert = NSAlert()
        if fields.isEmpty {
            alert.messageText = "No form fields on this page"
            alert.informativeText = "Go to a page with interactive fields, then choose Fill Form."
            alert.beginSheetModal(for: window)
            return
        }
        alert.messageText = "Fill form"
        alert.informativeText = "Choose a field on page \(currentIndex + 1). You can also click a field in the document."
        let picker = NSPopUpButton(frame: NSRect(x: 0, y: 0, width: 320, height: 28), pullsDown: false)
        for (index, field) in fields.enumerated() { picker.addItem(withTitle: "\(index + 1). \(field.fieldName ?? "Untitled field")") }
        alert.accessoryView = picker
        alert.addButton(withTitle: "Edit Field")
        alert.addButton(withTitle: "Cancel")
        alert.beginSheetModal(for: window) { [weak self] response in
            if response == .alertFirstButtonReturn { self?.edit(fields[picker.indexOfSelectedItem]) }
        }
    }

    private func edit(_ field: PDFAnnotation) {
        guard let window = window else { return }
        guard !field.isReadOnly, model.pdf.allowsFormFieldEntry else { gravity.presentError(PDFError.readOnly); return }
        let alert = NSAlert()
        alert.messageText = field.fieldName ?? "Fill form field"
        alert.informativeText = "Apply this value, then save the PDF to keep it."
        alert.addButton(withTitle: "Apply")
        alert.addButton(withTitle: "Cancel")
        var value: () -> String? = { nil }
        var checked: () -> Bool? = { nil }
        switch field.widgetFieldType {
        case .text:
            if field.isMultiline {
                let scroll = NSScrollView(frame: NSRect(x: 0, y: 0, width: 360, height: 140))
                let text = NSTextView(frame: scroll.bounds)
                text.isRichText = false
                text.string = field.widgetStringValue ?? ""
                text.font = .systemFont(ofSize: NSFont.systemFontSize)
                scroll.documentView = text
                scroll.hasVerticalScroller = true
                alert.accessoryView = scroll
                alert.window.initialFirstResponder = text
                value = { text.string }
            } else {
                let text = field.isPasswordField ? NSSecureTextField() : NSTextField()
                text.frame = NSRect(x: 0, y: 0, width: 360, height: 24)
                text.stringValue = field.widgetStringValue ?? ""
                alert.accessoryView = text
                alert.window.initialFirstResponder = text
                value = { text.stringValue }
            }
        case .choice:
            let flags = (field.value(forAnnotationKey: .widgetFieldFlags) as? NSNumber)?.intValue ?? 0
            guard flags & (1 << 21) == 0 else { gravity.presentError(PDFError.unsupported); return }
            let choices = field.choices ?? [], exports = field.values ?? choices
            if flags & (1 << 18) != 0 {
                let combo = NSComboBox(frame: NSRect(x: 0, y: 0, width: 360, height: 26))
                combo.addItems(withObjectValues: choices)
                let index = exports.firstIndex(of: field.widgetStringValue ?? "")
                combo.stringValue = index.flatMap { $0 < choices.count ? choices[$0] : nil } ?? field.widgetStringValue ?? ""
                alert.accessoryView = combo
                value = { if let index = choices.firstIndex(of: combo.stringValue), index < exports.count { return exports[index] }; return combo.stringValue }
            } else {
                guard !choices.isEmpty else { gravity.presentError(PDFError.unsupported); return }
                let picker = NSPopUpButton(frame: NSRect(x: 0, y: 0, width: 360, height: 28), pullsDown: false)
                picker.addItems(withTitles: choices)
                if let index = exports.firstIndex(of: field.widgetStringValue ?? "") { picker.selectItem(at: index) }
                alert.accessoryView = picker
                value = { let index = picker.indexOfSelectedItem; return index < exports.count ? exports[index] : choices[index] }
            }
        case .button:
            guard field.widgetControlType != .pushButtonControl else { gravity.presentError(PDFError.unsupported); return }
            let radio = field.widgetControlType == .radioButtonControl
            let box = NSButton(checkboxWithTitle: radio ? "Select this option" : "Checked", target: nil, action: nil)
            box.frame = NSRect(x: 0, y: 0, width: 360, height: 28)
            box.state = radio || field.buttonWidgetState == .onState ? .on : .off
            box.isEnabled = !radio
            alert.accessoryView = box
            checked = { box.state == .on }
        default: gravity.presentError(PDFError.unsupported); return
        }
        alert.beginSheetModal(for: window) { [weak self] response in
            guard response == .alertFirstButtonReturn, let self = self else { return }
            self.gravity.edit("Fill Form") { try self.model.fill(field, text: value(), checked: checked()) }
        }
    }
}
