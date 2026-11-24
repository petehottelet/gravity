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

final class DocumentWindow: NSWindowController, NSTableViewDataSource, NSTableViewDelegate {
    private let pdfView = FormPDFView()
    private let pages = NSTableView()
    private let search = NSSearchField()
    private let pageLabel = NSTextField(labelWithString: "")
    private var pageObserver: NSObjectProtocol?
    private var updating = false
    private unowned let gravity: GravityDocument
    private var model: PDFModel { gravity.model }
    private static var pageClipboard: Data?

    init(document: GravityDocument) {
        gravity = document
        let window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 1100, height: 780),
                              styleMask: [.titled, .closable, .miniaturizable, .resizable], backing: .buffered, defer: false)
        window.minSize = NSSize(width: 900, height: 480)
        window.tabbingMode = .preferred
        window.center()
        super.init(window: window)
        shouldCascadeWindows = true
        window.setFrameAutosaveName("GravityDocument")

        let controls = NSStackView()
        controls.orientation = .horizontal
        controls.spacing = 8
        controls.edgeInsets = NSEdgeInsets(top: 8, left: 12, bottom: 8, right: 12)
        controls.addArrangedSubview(button("Previous", #selector(previousPage(_:))))
        controls.addArrangedSubview(button("Next", #selector(nextPage(_:))))
        controls.addArrangedSubview(pageLabel)
        controls.addArrangedSubview(button("Zoom Out", #selector(zoomOut(_:))))
        controls.addArrangedSubview(button("Zoom In", #selector(zoomIn(_:))))
        controls.addArrangedSubview(button("Fit Page", #selector(fitPage(_:))))
        controls.addArrangedSubview(button("Fill Form", #selector(fillForm(_:))))
        search.placeholderString = "Find in document"
        search.target = self
        search.action = #selector(findNext(_:))
        search.setAccessibilityLabel("Find in document")
        search.widthAnchor.constraint(greaterThanOrEqualToConstant: 150).isActive = true
        controls.addArrangedSubview(search)

        let split = NSSplitView()
        split.isVertical = true
        split.dividerStyle = .thin
        let scroll = NSScrollView()
        scroll.hasVerticalScroller = true
        let column = NSTableColumn(identifier: NSUserInterfaceItemIdentifier("Page"))
        column.width = 160
        pages.addTableColumn(column)
        pages.headerView = nil
        pages.rowHeight = 150
        pages.allowsMultipleSelection = true
        pages.dataSource = self
        pages.delegate = self
        pages.setAccessibilityLabel("Pages")
        scroll.documentView = pages
        scroll.widthAnchor.constraint(greaterThanOrEqualToConstant: 140).isActive = true
        split.addArrangedSubview(scroll)
        split.addArrangedSubview(pdfView)
        pdfView.autoScales = true
        pdfView.displayMode = .singlePageContinuous
        pdfView.editField = { [weak self] field in self?.edit(field) }
        let content = NSView()
        window.contentView = content
        for child in [controls, split] {
            child.translatesAutoresizingMaskIntoConstraints = false
            content.addSubview(child)
        }
        NSLayoutConstraint.activate([
            controls.topAnchor.constraint(equalTo: content.topAnchor),
            controls.leadingAnchor.constraint(equalTo: content.leadingAnchor),
            controls.trailingAnchor.constraint(equalTo: content.trailingAnchor),
            split.topAnchor.constraint(equalTo: controls.bottomAnchor),
            split.leadingAnchor.constraint(equalTo: content.leadingAnchor),
            split.trailingAnchor.constraint(equalTo: content.trailingAnchor),
            split.bottomAnchor.constraint(equalTo: content.bottomAnchor)
        ])
        split.setPosition(180, ofDividerAt: 0)
        pageObserver = NotificationCenter.default.addObserver(forName: .PDFViewPageChanged, object: pdfView, queue: .main) { [weak self] _ in self?.pageChanged() }
        refresh()
    }

    required init?(coder: NSCoder) { fatalError("Use init(document:)") }
    deinit { if let observer = pageObserver { NotificationCenter.default.removeObserver(observer) } }

    private func button(_ title: String, _ action: Selector) -> NSButton {
        let button = NSButton(title: title, target: self, action: action)
        button.bezelStyle = .rounded
        return button
    }

    private var currentIndex: Int {
        guard let page = pdfView.currentPage, let pdf = pdfView.document else { return 0 }
        let index = pdf.index(for: page)
        return index == NSNotFound ? 0 : index
    }

    private var selected: IndexSet {
        return pages.selectedRowIndexes.isEmpty ? IndexSet(integer: currentIndex) : pages.selectedRowIndexes
    }

    func refresh() {
        let index = min(currentIndex, model.pdf.pageCount - 1)
        updating = true
        pdfView.document = model.pdf
        pages.reloadData()
        if let page = model.pdf.page(at: index) { pdfView.go(to: page) }
        pages.selectRowIndexes(IndexSet(integer: index), byExtendingSelection: false)
        updating = false
        pageChanged()
    }

    private func pageChanged() {
        pageLabel.stringValue = "\(currentIndex + 1) of \(model.pdf.pageCount)"
        if !updating && window?.firstResponder !== pages {
            updating = true
            pages.selectRowIndexes(IndexSet(integer: currentIndex), byExtendingSelection: false)
            pages.scrollRowToVisible(currentIndex)
            updating = false
        }
    }

    func numberOfRows(in tableView: NSTableView) -> Int { model.pdf.pageCount }

    func tableView(_ tableView: NSTableView, viewFor tableColumn: NSTableColumn?, row: Int) -> NSView? {
        let cell = NSTableCellView()
        let image = NSImageView(frame: NSRect(x: 14, y: 24, width: 132, height: 120))
        image.imageScaling = .scaleProportionallyUpOrDown
        image.image = model.pdf.page(at: row)?.thumbnail(of: NSSize(width: 132, height: 120), for: .cropBox)
        let label = NSTextField(labelWithString: "Page \(row + 1)")
        label.frame = NSRect(x: 14, y: 3, width: 132, height: 18)
        label.alignment = .center
        cell.addSubview(image)
        cell.addSubview(label)
        cell.imageView = image
        cell.textField = label
        return cell
    }

    func tableViewSelectionDidChange(_ notification: Notification) {
        guard !updating, let index = pages.selectedRowIndexes.first, let page = model.pdf.page(at: index) else { return }
        updating = true
        pdfView.go(to: page)
        updating = false
    }

    @objc func previousPage(_ sender: Any?) { pdfView.goToPreviousPage(sender) }
    @objc func nextPage(_ sender: Any?) { pdfView.goToNextPage(sender) }
    @objc func zoomIn(_ sender: Any?) { pdfView.zoomIn(sender) }
    @objc func zoomOut(_ sender: Any?) { pdfView.zoomOut(sender) }
    @objc func fitPage(_ sender: Any?) { pdfView.autoScales = true }
    @objc func find(_ sender: Any?) { window?.makeFirstResponder(search) }
    @objc func findNext(_ sender: Any?) {
        guard !search.stringValue.isEmpty else { return }
        let result = model.pdf.findString(search.stringValue, fromSelection: pdfView.currentSelection, withOptions: .caseInsensitive)
            ?? model.pdf.findString(search.stringValue, fromSelection: nil, withOptions: .caseInsensitive)
        if let result = result { pdfView.setCurrentSelection(result, animate: true); pdfView.scrollSelectionToVisible(sender) }
        else { NSSound.beep() }
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
        pages.selectRowIndexes(IndexSet(indices.map { $0 + offset }), byExtendingSelection: false)
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
        let at = currentIndex + 1
        gravity.edit("Insert Blank Page") { try model.insert(PDFModel().data(), at: at) }
    }
    @objc func insertFiles(_ sender: Any?) {
        guard let window = window else { return }
        let panel = NSOpenPanel()
        panel.allowedContentTypes = [.pdf]
        panel.allowsMultipleSelection = true
        panel.beginSheetModal(for: window) { [weak self] response in
            guard response == .OK, let self = self else { return }
            self.gravity.edit("Insert PDFs") {
                var index = self.currentIndex + 1
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
