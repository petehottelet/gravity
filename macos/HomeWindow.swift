import AppKit
import PDFKit
import UniformTypeIdentifiers

final class HomeWindow: NSWindowController {
    private let root = HomeDropView()
    private let header = WorkspaceSurface()
    private let commands = WorkspaceSurface()
    private let applicationMenus = WorkspaceMenus()
    private let headerSubtitle = DocumentTheme.label("PDF reader and organizer", size: 12, color: DocumentTheme.muted)
    private var brandButton: WorkspaceButton!
    private var toolbarControls: [String: WorkspaceButton] = [:]
    private var menusExpanded = false
    private let logo = NSImageView()
    private let subtitle = DocumentTheme.label("Fast PDF viewer and page organizer", size: 14, color: DocumentTheme.muted)
    private let recentTitle = DocumentTheme.label("RECENT", size: 12, weight: .semibold, color: DocumentTheme.muted)
    private let hint = DocumentTheme.label("Drop PDF files here, or open one to get started.", size: 13, color: DocumentTheme.muted)
    private let scroll = NSScrollView()
    private let recentList = WorkspaceSurface()
    private let recentDocuments: () -> [URL]
    private let openDocument: (URL) -> Void
    private var recentURLs: [URL] = []
    private var rows: [RecentPDFButton] = []
    private var openButton: HomeActionButton!
    private var combineButton: HomeActionButton!
    private var appearanceObserver: NSObjectProtocol?

    init(recentDocuments: @escaping () -> [URL] = { NSDocumentController.shared.recentDocumentURLs },
         openDocument: @escaping (URL) -> Void = HomeWindow.openDocument) {
        self.recentDocuments = recentDocuments
        self.openDocument = openDocument
        let window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 1100, height: 760),
                              styleMask: [.titled, .closable, .miniaturizable, .resizable], backing: .buffered, defer: false)
        window.title = "Gravity"
        window.titleVisibility = .hidden
        window.titlebarAppearsTransparent = true
        window.titlebarSeparatorStyle = .none
        window.minSize = NSSize(width: 560, height: 440)
        window.appearance = NSAppearance(named: .aqua)
        window.backgroundColor = DocumentTheme.chrome
        window.tabbingMode = .disallowed
        window.isReleasedWhenClosed = false
        window.center()
        super.init(window: window)
        if !CommandLine.arguments.contains("--self-test") { window.setFrameAutosaveName("GravityHome") }
        window.contentView = root
        root.identifier = NSUserInterfaceItemIdentifier("home")
        root.arrange = { [weak self] in self?.arrange() }
        root.openFiles = { [weak self] in self?.openFiles($0) }
        configureToolbar()
        logo.image = DocumentTheme.lockup
        logo.imageScaling = .scaleProportionallyUpOrDown
        logo.imageAlignment = .alignLeft
        logo.setAccessibilityLabel("Gravity")
        openButton = HomeActionButton("Open PDF…", hint: "⌘O", primary: true, target: self, action: #selector(openPDF(_:)))
        combineButton = HomeActionButton("Combine PDFs…", target: self, action: #selector(combinePDFs(_:)))
        openButton.identifier = NSUserInterfaceItemIdentifier("homeOpen")
        combineButton.identifier = NSUserInterfaceItemIdentifier("homeCombine")
        scroll.identifier = NSUserInterfaceItemIdentifier("homeRecents")
        scroll.hasVerticalScroller = true
        scroll.drawsBackground = false
        scroll.borderType = .noBorder
        scroll.documentView = recentList
        recentList.fill = .white
        recentList.cornerRadius = 8
        scroll.wantsLayer = true
        scroll.layer?.cornerRadius = 8
        scroll.layer?.borderWidth = 1
        scroll.layer?.borderColor = DocumentTheme.rule.cgColor
        for view in [logo, subtitle, openButton!, combineButton!, recentTitle, scroll, hint] { root.addSubview(view) }
        window.initialFirstResponder = openButton
        refreshRecents()
        appearanceObserver = NotificationCenter.default.addObserver(forName: AppAppearance.didChange, object: AppAppearance.shared, queue: .main) { [weak self] _ in
            self?.logo.image = DocumentTheme.lockup
            self?.brandButton.image = DocumentTheme.brandMark
            if let root = self?.root { DocumentTheme.refresh(root) }
        }
    }

    required init?(coder: NSCoder) { fatalError("Use init()") }
    deinit { if let appearanceObserver = appearanceObserver { NotificationCenter.default.removeObserver(appearanceObserver) } }

    override func showWindow(_ sender: Any?) {
        refreshRecents()
        super.showWindow(sender)
        if window?.isMiniaturized == true { window?.deminiaturize(sender) }
        window?.makeKeyAndOrderFront(sender)
    }

    func refreshRecents() {
        recentURLs = recentDocuments().filter { $0.isFileURL && $0.pathExtension.lowercased() == "pdf" }
        rows.forEach { $0.removeFromSuperview() }
        rows = recentURLs.enumerated().map { index, url in
            let row = RecentPDFButton(url: url, target: self, action: #selector(openRecent(_:)))
            row.tag = index
            recentList.addSubview(row)
            return row
        }
        scroll.isHidden = rows.isEmpty
        recentTitle.isHidden = rows.isEmpty
        arrange()
    }

    private func arrange() {
        header.frame = NSRect(x: 0, y: 0, width: root.bounds.width, height: WorkspaceMetrics.headerHeight)
        brandButton.frame = NSRect(x: 12, y: 4, width: 104, height: WorkspaceMetrics.buttonHeight)
        headerSubtitle.frame = NSRect(x: 210, y: 11, width: max(0, root.bounds.width - 420), height: 18)
        headerSubtitle.isHidden = root.bounds.width < 700
        commands.frame = NSRect(x: 0, y: WorkspaceMetrics.headerHeight, width: root.bounds.width, height: WorkspaceMetrics.commandHeight)
        applicationMenus.frame = commands.bounds
        let toolbarWidth = commands.bounds.width
        let positions: [(String, CGFloat, CGFloat)] = [("sidebar", 12, 32), ("open", 48, 76), ("menu", 128, 32),
            ("find", toolbarWidth - 198, 32), ("undo", toolbarWidth - 162, 32),
            ("print", toolbarWidth - 126, 32), ("save", toolbarWidth - 90, 78)]
        for (id, x, width) in positions { toolbarControls[id]?.frame = NSRect(x: x, y: 6, width: width, height: WorkspaceMetrics.buttonHeight) }
        let width = min(620, max(0, root.bounds.width - 48))
        let chromeHeight = WorkspaceMetrics.headerHeight + WorkspaceMetrics.commandHeight
        let x = (root.bounds.width - width) / 2, y = chromeHeight + max(24, (root.bounds.height - chromeHeight - 160) / 9)
        logo.frame = NSRect(x: x, y: y, width: 461 * 46 / 96, height: 46)
        subtitle.frame = NSRect(x: x, y: y + 56, width: width, height: 22)
        openButton.frame = NSRect(x: x, y: y + 96, width: 190, height: 40)
        combineButton.frame = NSRect(x: x + 202, y: y + 96, width: 190, height: 40)
        recentTitle.frame = NSRect(x: x, y: y + 172, width: width, height: 18)
        let listY = y + 198
        let listHeight = min(CGFloat(rows.count) * 56, max(56, root.bounds.height - listY - 70))
        scroll.frame = NSRect(x: x, y: listY, width: width, height: listHeight)
        recentList.frame = NSRect(x: 0, y: 0, width: scroll.contentSize.width, height: CGFloat(rows.count) * 56)
        for (index, row) in rows.enumerated() {
            row.frame = NSRect(x: 0, y: CGFloat(index) * 56, width: recentList.bounds.width, height: 56)
        }
        hint.frame = NSRect(x: x, y: rows.isEmpty ? y + 172 : listY + listHeight + 22, width: width, height: 22)
    }

    private func configureToolbar() {
        root.addSubview(header)
        root.addSubview(commands)
        commands.bottomRule = true
        header.draggable = true
        header.identifier = NSUserInterfaceItemIdentifier("homeHeader")
        commands.identifier = NSUserInterfaceItemIdentifier("homeCommands")
        brandButton = WorkspaceButton("Gravity", label: "Gravity: Show application menus", target: self, action: #selector(toggleApplicationMenus(_:)))
        brandButton.image = DocumentTheme.brandMark
        brandButton.iconSize = 22
        brandButton.showsHoverBackground = false
        brandButton.imagePosition = .imageLeading
        brandButton.imageScaling = .scaleProportionallyDown
        brandButton.font = .systemFont(ofSize: 14, weight: .bold)
        headerSubtitle.alignment = .center
        header.addSubview(brandButton)
        header.addSubview(headerSubtitle)
        func add(_ id: String, _ title: String, _ symbol: String, _ label: String, action: Selector, enabled: Bool = false) {
            let button = WorkspaceButton(title, symbol: symbol, label: label, target: self, action: action)
            button.identifier = NSUserInterfaceItemIdentifier("homeToolbar\(id.capitalized)")
            button.isEnabled = enabled
            toolbarControls[id] = button
            commands.addSubview(button)
        }
        add("sidebar", "", "sidebar.left", "Show sidebar (open a PDF first)", action: #selector(DocumentWindow.toggleSidebar(_:)))
        add("open", "Open", "folder", "Open PDF (Command+O)", action: #selector(openPDF(_:)), enabled: true)
        add("menu", "", "ellipsis", "Show application menus", action: #selector(toggleApplicationMenus(_:)), enabled: true)
        add("find", "", "magnifyingglass", "Find in document (open a PDF first)", action: #selector(DocumentWindow.find(_:)))
        add("undo", "", "arrow.uturn.backward", "Undo (open a PDF first)", action: #selector(DocumentWindow.undoEdit(_:)))
        add("print", "", "printer", "Print (open a PDF first)", action: #selector(DocumentWindow.printDocument(_:)))
        add("save", "Save", "save", "Save PDF (open a PDF first)", action: #selector(DocumentWindow.saveDocument(_:)))
        toolbarControls["save"]?.outlined = true
        commands.addSubview(applicationMenus)
        applicationMenus.closeMenus = { [weak self] in self?.toggleApplicationMenus(nil) }
    }

    @objc func toggleApplicationMenus(_ sender: Any?) {
        menusExpanded.toggle()
        applicationMenus.isHidden = !menusExpanded
        toolbarControls.values.forEach { $0.isHidden = menusExpanded }
        brandButton.setAccessibilityLabel("Gravity: \(menusExpanded ? "Hide" : "Show") application menus")
        root.needsLayout = true
    }

    @objc private func openPDF(_ sender: Any?) { NSDocumentController.shared.openDocument(sender) }
    @objc private func openRecent(_ sender: NSButton) {
        guard recentURLs.indices.contains(sender.tag) else { return }
        openFiles([recentURLs[sender.tag]])
    }

    func openFiles(_ urls: [URL]) {
        urls.forEach(openDocument)
    }

    private static func openDocument(_ url: URL) {
        NSDocumentController.shared.openDocument(withContentsOf: url, display: true) { document, _, error in
            if let error = error { NSApp.presentError(error) }
            if document != nil { (NSApp.delegate as? AppDelegate)?.documentOpened() }
        }
    }

    @objc func combinePDFs(_ sender: Any?) {
        guard let window = window else { return }
        let panel = NSOpenPanel()
        panel.allowedContentTypes = [.pdf]
        panel.allowsMultipleSelection = true
        panel.canChooseDirectories = false
        panel.prompt = "Combine"
        panel.message = "Select PDFs to combine. You can reorder pages in Organize before saving."
        panel.beginSheetModal(for: window) { [weak self] response in
            guard response == .OK, let self = self else { return }
            do {
                let document = try Self.combinedDocument(from: panel.urls)
                NSDocumentController.shared.addDocument(document)
                document.makeWindowControllers()
                document.showWindows()
                (document.windowControllers.first as? DocumentWindow)?.showOrganize(nil)
                self.window?.orderOut(nil)
            } catch { NSApp.presentError(error) }
        }
    }

    static func combinedDocument(from urls: [URL]) throws -> GravityDocument {
        guard !urls.isEmpty else { throw PDFError.invalid }
        let combined = PDFDocument()
        for url in urls {
            // Reuse the document password prompt and enforce PDF copying permissions.
            let source = GravityDocument()
            try source.read(from: Data(contentsOf: url), ofType: UTType.pdf.identifier)
            guard source.model.pdf.allowsCopying else { throw PDFError.readOnly }
            for index in 0..<source.model.pdf.pageCount {
                guard let page = source.model.pdf.page(at: index)?.copy() as? PDFPage else { throw PDFError.invalid }
                combined.insert(page, at: combined.pageCount)
            }
        }
        guard let data = combined.dataRepresentation() else { throw PDFError.write }
        let document = GravityDocument()
        try document.read(from: data, ofType: UTType.pdf.identifier)
        document.displayName = "Combined.pdf"
        document.updateChangeCount(.changeDone)
        return document
    }
}

final class HomeDropView: NSView {
    var arrange: (() -> Void)?
    var openFiles: (([URL]) -> Void)?
    private var acceptingDrop = false { didSet { needsDisplay = true } }
    override var isFlipped: Bool { true }
    override init(frame: NSRect) {
        super.init(frame: frame)
        registerForDraggedTypes([.fileURL])
    }
    required init?(coder: NSCoder) { fatalError("Use init(frame:)") }
    override func layout() { super.layout(); arrange?() }
    override func setFrameSize(_ size: NSSize) { super.setFrameSize(size); needsLayout = true }
    override func draw(_ dirtyRect: NSRect) {
        DocumentTheme.chrome.setFill()
        bounds.fill()
        if acceptingDrop {
            DocumentTheme.accent.setStroke()
            let border = NSBezierPath(roundedRect: bounds.insetBy(dx: 12, dy: 12), xRadius: 10, yRadius: 10)
            border.lineWidth = 2
            border.stroke()
        }
    }
    static func pdfURLs(from pasteboard: NSPasteboard) -> [URL] {
        let urls = (pasteboard.readObjects(forClasses: [NSURL.self], options: [.urlReadingFileURLsOnly: true]) as? [URL]) ?? []
        return !urls.isEmpty && urls.allSatisfy { $0.pathExtension.lowercased() == "pdf" } ? urls : []
    }
    override func draggingEntered(_ sender: NSDraggingInfo) -> NSDragOperation {
        acceptingDrop = !Self.pdfURLs(from: sender.draggingPasteboard).isEmpty
        return acceptingDrop ? .copy : []
    }
    override func draggingExited(_ sender: NSDraggingInfo?) { acceptingDrop = false }
    override func performDragOperation(_ sender: NSDraggingInfo) -> Bool {
        let urls = Self.pdfURLs(from: sender.draggingPasteboard)
        acceptingDrop = false
        guard !urls.isEmpty else { return false }
        openFiles?(urls)
        return true
    }
}

private class HomeActionButton: NSButton {
    let primary: Bool
    let hint: String?
    private var hovered = false
    private var tracking: NSTrackingArea?
    init(_ title: String, hint: String? = nil, primary: Bool = false, target: AnyObject?, action: Selector) {
        self.primary = primary
        self.hint = hint
        super.init(frame: .zero)
        self.title = title
        self.target = target
        self.action = action
        isBordered = false
        setButtonType(.momentaryChange)
        setAccessibilityLabel(title)
        toolTip = title
    }
    required init?(coder: NSCoder) { fatalError("Use init(title:)") }
    override func updateTrackingAreas() {
        super.updateTrackingAreas()
        if let tracking = tracking { removeTrackingArea(tracking) }
        let area = NSTrackingArea(rect: .zero, options: [.mouseEnteredAndExited, .activeInKeyWindow, .inVisibleRect], owner: self)
        addTrackingArea(area)
        tracking = area
    }
    override func mouseEntered(with event: NSEvent) { hovered = true; needsDisplay = true }
    override func mouseExited(with event: NSEvent) { hovered = false; needsDisplay = true }
    override func draw(_ dirtyRect: NSRect) {
        let shape = NSBezierPath(roundedRect: bounds.insetBy(dx: 1, dy: 1), xRadius: 6, yRadius: 6)
        let fill: NSColor = primary ? (isHighlighted ? DocumentTheme.palette.actionPressed : hovered ? DocumentTheme.palette.actionHover : DocumentTheme.palette.action)
            : (isHighlighted ? DocumentTheme.pressed : hovered ? DocumentTheme.hover : .white)
        fill.setFill()
        shape.fill()
        if !primary { DocumentTheme.rule.setStroke(); shape.stroke() }
        else if AccentPalette.contrast(fill, DocumentTheme.chrome) < 3 {
            DocumentTheme.accent.setStroke(); shape.stroke()
        }
        let ink: NSColor = primary ? AccentPalette.text(on: fill) : DocumentTheme.ink
        let text = NSAttributedString(string: title, attributes: [.font: NSFont.systemFont(ofSize: 13, weight: .semibold), .foregroundColor: ink])
        text.draw(at: NSPoint(x: hint == nil ? (bounds.width - text.size().width) / 2 : 16, y: (bounds.height - text.size().height) / 2))
        if let hint = hint {
            let text = NSAttributedString(string: hint, attributes: [.font: NSFont.systemFont(ofSize: 12), .foregroundColor: ink])
            text.draw(at: NSPoint(x: bounds.width - text.size().width - 16, y: (bounds.height - text.size().height) / 2))
        }
    }
    override var focusRingMaskBounds: NSRect { bounds }
    override func drawFocusRingMask() { NSBezierPath(roundedRect: bounds, xRadius: 6, yRadius: 6).fill() }
}

private final class RecentPDFButton: NSButton {
    private let url: URL
    init(url: URL, target: AnyObject?, action: Selector) {
        self.url = url
        super.init(frame: .zero)
        title = url.lastPathComponent
        self.target = target
        self.action = action
        isBordered = false
        setButtonType(.momentaryChange)
        setAccessibilityLabel("Open \(url.lastPathComponent)")
        toolTip = url.path
    }
    required init?(coder: NSCoder) { fatalError("Use init(url:)") }
    override var isFlipped: Bool { true }
    override func draw(_ dirtyRect: NSRect) {
        (isHighlighted ? DocumentTheme.pressed : .white).setFill()
        bounds.fill()
        if tag > 0 {
            DocumentTheme.rule.setFill()
            NSRect(x: 14, y: 0, width: bounds.width - 28, height: 1).fill()
        }
        let exists = FileManager.default.fileExists(atPath: url.path)
        if let icon = DocumentTheme.symbol("doc")?.copy() as? NSImage {
            icon.lockFocus()
            (exists ? DocumentTheme.accent : DocumentTheme.muted).setFill()
            NSRect(origin: .zero, size: icon.size).fill(using: .sourceAtop)
            icon.unlockFocus()
            icon.draw(in: NSRect(x: 16, y: 18, width: 20, height: 20))
        }
        let style = NSMutableParagraphStyle()
        style.lineBreakMode = .byTruncatingMiddle
        let directory = url.deletingLastPathComponent().path
        (url.lastPathComponent as NSString).draw(in: NSRect(x: 48, y: 9, width: bounds.width - 64, height: 20), withAttributes: [
            .font: NSFont.systemFont(ofSize: 13), .foregroundColor: exists ? DocumentTheme.ink : DocumentTheme.muted, .paragraphStyle: style
        ])
        ((exists ? directory : "Missing: " + directory) as NSString).draw(in: NSRect(x: 48, y: 30, width: bounds.width - 64, height: 16), withAttributes: [
            .font: NSFont.systemFont(ofSize: 11), .foregroundColor: DocumentTheme.muted, .paragraphStyle: style
        ])
    }
}
