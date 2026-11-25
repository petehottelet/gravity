import AppKit
import PDFKit

enum WorkspaceTests {
    static func run(check: (Bool, String) throws -> Void) throws {
        let fixture = PDFDocument()
        for number in 1...6 {
            let page = WorkspaceFixturePage(frame: NSRect(x: 0, y: 0, width: 612, height: 792))
            page.number = number
            guard let source = PDFDocument(data: page.dataWithPDF(inside: page.bounds)), let pdfPage = source.page(at: 0) else {
                throw PDFError.invalid
            }
            fixture.insert(pdfPage, at: fixture.pageCount)
        }
        let bookmarks = PDFOutline()
        for (index, title) in ["Welcome", "Observations", "Next steps"].enumerated() {
            let item = PDFOutline()
            item.label = title
            item.destination = PDFDestination(page: fixture.page(at: index * 2)!, at: NSPoint(x: 0, y: 792))
            bookmarks.insertChild(item, at: index)
        }
        fixture.outlineRoot = bookmarks
        guard let bytes = fixture.dataRepresentation() else { throw PDFError.write }
        let document = GravityDocument()
        try document.read(from: bytes, ofType: "com.adobe.pdf")
        document.undoManager?.groupsByEvent = false
        document.makeWindowControllers()
        guard let controller = document.windowControllers.first as? DocumentWindow,
              let window = controller.window, let root = window.contentView else { throw PDFError.invalid }
        defer { controller.close() }
        window.setContentSize(NSSize(width: 1100, height: 760))
        root.layoutSubtreeIfNeeded()

        func descendants(_ view: NSView) -> [NSView] { [view] + view.subviews.flatMap { descendants($0) } }
        func view<T: NSView>(_ id: String, as type: T.Type) throws -> T {
            guard let result = descendants(root).first(where: { $0.identifier?.rawValue == id }) as? T else {
                throw NSError(domain: "GravityTests", code: 1, userInfo: [NSLocalizedDescriptionKey: "Missing workspace control: \(id)"])
            }
            return result
        }
        func button(_ id: String) throws -> WorkspaceButton { try view(id, as: WorkspaceButton.self) }
        let read = try button("read"), organize = try button("organize")
        let grid = try view("organizer", as: PageCollectionView.self)
        let rail = try view("pageRail", as: PageCollectionView.self)
        let field = try view("pageNumber", as: NSTextField.self)
        let state = try view("documentState", as: NSTextField.self)
        guard let pdfView = descendants(root).compactMap({ $0 as? PDFView }).first,
              let outline = descendants(root).compactMap({ $0 as? NSOutlineView }).first,
              let search = descendants(root).compactMap({ $0 as? NSSearchField }).first else { throw PDFError.invalid }

        try check(read.selected && !organize.selected && !pdfView.isHidden, "workspace starts in Read")
        try check(state.stringValue.contains("Not saved yet"), "untitled document never reports Saved")
        try check(outline.numberOfRows == 3, "outline displays real PDF bookmarks")
        try check(rail.numberOfItems(inSection: 0) == 6 && grid.numberOfItems(inSection: 0) == 6,
                  "sidebar and organizer contain all pages")
        try check(Bundle.main.url(forResource: "g-mark", withExtension: "png") != nil, "native bundle contains Gravity artwork")
        for control in descendants(root).compactMap({ $0 as? WorkspaceButton }) where control.imagePosition == .imageOnly {
            try check(control.image != nil && !(control.toolTip ?? "").isEmpty, "icon action has a native symbol and a name")
        }

        outline.selectRowIndexes(IndexSet(integer: 1), byExtendingSelection: false)
        controller.outlineViewSelectionDidChange(Notification(name: NSOutlineView.selectionDidChangeNotification, object: outline))
        try check(pdfView.currentPage === document.model.pdf.page(at: 2), "bookmark selection navigates to its page")
        field.stringValue = "6"
        controller.goToPage(field)
        try check(pdfView.currentPage === document.model.pdf.page(at: 5), "page field navigates to last page")
        field.stringValue = "999"
        controller.goToPage(field)
        try check(field.integerValue == 6 && pdfView.currentPage === document.model.pdf.page(at: 5), "invalid page restores current position")
        controller.showOrganize(nil)
        try check(organize.selected && !read.selected && pdfView.isHidden, "Organize replaces reader with grid")
        try check(grid.selectionIndexPaths == [IndexPath(item: 5, section: 0)], "organizer begins at current reading page")
        grid.selectionIndexPaths = [IndexPath(item: 1, section: 0), IndexPath(item: 2, section: 0)]
        controller.collectionView(grid, didSelectItemsAt: grid.selectionIndexPaths)
        controller.rotateRight(nil)
        try check(document.model.pdf.page(at: 1)?.rotation == 90 && document.model.pdf.page(at: 2)?.rotation == 90,
                  "rotation applies to every selected page")
        try check(grid.selectionIndexPaths.count == 2 && state.stringValue.contains("Unsaved changes"),
                  "editing preserves multiselection and updates save state")
        controller.undoEdit(nil)
        try check(document.model.pdf.page(at: 1)?.rotation == 0 && !document.isDocumentEdited,
                  "organizer undo restores document and clean state")
        try check(state.stringValue.contains("Not saved yet"), "undo refreshes header state")
        controller.moveLater(nil)
        try check(grid.selectionIndexPaths == [IndexPath(item: 2, section: 0), IndexPath(item: 3, section: 0)],
                  "move keeps selection on moved pages")
        controller.undoEdit(nil)
        controller.duplicatePages(nil)
        try check(document.model.pdf.pageCount == 8, "duplicate adds selected pages")
        controller.undoEdit(nil)
        grid.selectionIndexPaths = [IndexPath(item: 0, section: 0)]
        controller.insertBlank(nil)
        try check(document.model.pdf.pageCount == 7 && (document.model.pdf.page(at: 1)?.string ?? "").isEmpty,
                  "blank page inserts after organizer selection rather than former reading position")
        controller.undoEdit(nil)
        controller.selectAllPages(nil)
        try check(grid.selectionIndexPaths.count == 6, "Select all selects whole document")
        let deleteItem = NSMenuItem(title: "Delete", action: #selector(DocumentWindow.deletePages(_:)), keyEquivalent: "")
        try check(!controller.validateMenuItem(deleteItem), "cannot delete every page through menu")
        try check(try !button("delete").isEnabled, "cannot delete every page through toolbar")

        for width in [CGFloat(1100), 820, 819, 700, 699, 560] {
            window.setContentSize(NSSize(width: width, height: 500))
            root.layoutSubtreeIfNeeded()
            let primary = try ["rotate", "duplicate", "delete"].map { try button($0) }
            for action in primary {
                try check(!action.isHidden && action.superview!.bounds.contains(action.frame),
                          "primary page action fits at width \(Int(width))")
            }
            try check(!primary[0].frame.intersects(primary[1].frame) && !primary[1].frame.intersects(primary[2].frame),
                      "organizer actions do not overlap at width \(Int(width))")
            try check(read.superview!.bounds.contains(read.frame) && organize.superview!.bounds.contains(organize.frame),
                      "both modes remain visible at width \(Int(width))")
            controller.showRead(nil)
            root.layoutSubtreeIfNeeded()
            let previous = try button("previous"), next = try button("next")
            let minus = try button("minus"), zoom = try button("zoom"), plus = try button("plus")
            for control in [previous, next, minus, zoom, plus] {
                try check(!control.isHidden && control.superview!.bounds.contains(control.frame),
                          "reading control fits at width \(Int(width))")
            }
            try check(next.frame.maxX <= minus.frame.minX && zoom.frame.maxX <= plus.frame.minX,
                      "page and zoom controls do not overlap at width \(Int(width))")
            controller.showOrganize(nil)
            root.layoutSubtreeIfNeeded()
        }
        window.setContentSize(NSSize(width: 1100, height: 760))
        controller.showPages(nil)
        root.layoutSubtreeIfNeeded()
        try check(read.selected && !rail.isHiddenOrHasHiddenAncestor, "Pages returns to reading with thumbnails")
        rail.selectionIndexPaths = [IndexPath(item: 4, section: 0)]
        controller.collectionView(rail, didSelectItemsAt: rail.selectionIndexPaths)
        try check(pdfView.currentPage === document.model.pdf.page(at: 4), "page rail navigates reader")
        controller.showOrganize(nil)
        controller.find(nil)
        try check(read.selected && !search.isHiddenOrHasHiddenAncestor, "Find opens reader and search field from organizer")
        search.stringValue = "Observation"
        controller.findNext(nil)
        try check(pdfView.currentSelection != nil, "find selects actual PDF text")
        search.stringValue = "unmatched-fixture-query"
        controller.findNext(nil)
        try check(pdfView.currentSelection == nil, "no-match search clears previous highlight")
        controller.closeFind(nil)
        try check(search.isHiddenOrHasHiddenAncestor, "closing Find restores canvas space")
        controller.showOrganize(nil)
        grid.selectionIndexPaths = [IndexPath(item: 2, section: 0)]
        controller.showRead(nil)
        try check(pdfView.currentPage === document.model.pdf.page(at: 2), "Read follows selected organizer page")
        controller.showOrganize(nil)
        grid.openPage?(3)
        try check(read.selected && pdfView.currentPage === document.model.pdf.page(at: 3), "opening organizer thumbnail reads its page")
        document.updateChangeCount(.changeDone)
        try check(state.stringValue.contains("Unsaved changes"), "external change-count updates reach header")
        document.updateChangeCount(.changeCleared)
        try check(!state.stringValue.contains("Unsaved changes"), "saved checkpoint refreshes header")

        // Optional local captures use only generated fixtures, never an opened user PDF.
        if let flag = CommandLine.arguments.firstIndex(of: "--ui-snapshots"), flag + 1 < CommandLine.arguments.count {
            let directory = URL(fileURLWithPath: CommandLine.arguments[flag + 1], isDirectory: true)
            try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
            func capture(_ name: String) throws {
                root.layoutSubtreeIfNeeded()
                root.displayIfNeeded()
                guard let bitmap = root.bitmapImageRepForCachingDisplay(in: root.bounds) else { throw PDFError.write }
                root.cacheDisplay(in: root.bounds, to: bitmap)
                guard let png = bitmap.representation(using: .png, properties: [:]) else { throw PDFError.write }
                try png.write(to: directory.appendingPathComponent(name + ".png"))
            }
            controller.showOutline(nil)
            controller.showRead(nil)
            try capture("reading")
            controller.showPages(nil)
            try capture("pages")
            controller.showOrganize(nil)
            grid.selectionIndexPaths = [IndexPath(item: 1, section: 0), IndexPath(item: 2, section: 0)]
            controller.collectionView(grid, didSelectItemsAt: grid.selectionIndexPaths)
            try capture("organizing")
            window.setContentSize(NSSize(width: 560, height: 500))
            try capture("organizing-narrow")
        }
    }
}

private final class WorkspaceFixturePage: NSView {
    var number = 1
    override func draw(_ dirtyRect: NSRect) {
        NSColor.white.setFill()
        bounds.fill()
        let title = number == 1 ? "Field notes" : "Observation \(number)"
        (title as NSString).draw(at: NSPoint(x: 54, y: 658), withAttributes: [
            .font: NSFont.systemFont(ofSize: 32, weight: .semibold), .foregroundColor: DocumentTheme.ink
        ])
        let paragraphs = ["A small collection of ideas, observations, and next steps.",
                          "Use the outline to find a section. Open Pages to explore.",
                          "In Organize, select pages to rotate, duplicate, or move them."]
        for (line, text) in paragraphs.enumerated() {
            (text as NSString).draw(at: NSPoint(x: 54, y: CGFloat(602 - line * 30)), withAttributes: [
                .font: NSFont.systemFont(ofSize: 14), .foregroundColor: DocumentTheme.muted
            ])
        }
        DocumentTheme.rule.setFill()
        NSRect(x: 54, y: 76, width: 504, height: 1).fill()
        ("\(number)" as NSString).draw(at: NSPoint(x: 54, y: 48), withAttributes: [.font: NSFont.systemFont(ofSize: 11)])
    }
}
