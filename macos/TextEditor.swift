import AppKit
import PDFKit

// A sheet keeps text changes inside the document's snapshot-based undo history.
final class TextEditor: NSObject, NSTextViewDelegate, NSTextFieldDelegate {
    let alert = NSAlert()
    let text = NSTextView()
    private let fonts = NSPopUpButton()
    private let size = NSTextField(string: "16")
    private let x = NSTextField(), y = NSTextField(), width = NSTextField(), height = NSTextField()
    private let feedback = DocumentTheme.label(size: 11, color: DocumentTheme.muted)
    private let crop: NSRect
    private let existing: Bool
    private var fontNames = ["Helvetica", "Helvetica-Bold", "Times-Roman", "Courier"]

    init(page: PDFPage, point: NSPoint, annotation: PDFAnnotation?) {
        crop = page.bounds(for: .cropBox)
        existing = annotation != nil
        super.init()
        alert.messageText = existing ? "Edit Text" : "Add Text"
        alert.informativeText = "Text stays editable after saving. Double-click it to edit again."
        alert.addButton(withTitle: "Apply")
        alert.addButton(withTitle: "Cancel")
        if existing { alert.addButton(withTitle: "Delete Text") }
        let panel = NSView(frame: NSRect(x: 0, y: 0, width: 420, height: 310))
        let scroll = NSScrollView(frame: NSRect(x: 0, y: 168, width: 420, height: 142))
        scroll.borderType = .bezelBorder
        scroll.hasVerticalScroller = true
        text.frame = scroll.bounds
        text.isRichText = false
        text.isAutomaticQuoteSubstitutionEnabled = false
        text.isAutomaticDashSubstitutionEnabled = false
        text.isVerticallyResizable = true
        text.autoresizingMask = [.width]
        text.textContainer?.widthTracksTextView = true
        text.textContainerInset = NSSize(width: 6, height: 6)
        text.string = annotation?.contents ?? ""
        text.delegate = self
        text.setAccessibilityLabel("Text to add to the PDF")
        scroll.documentView = text
        panel.addSubview(scroll)
        let font = annotation?.font ?? NSFont(name: "Helvetica", size: 16)!
        if !fontNames.contains(font.fontName) { fontNames.append(font.fontName) }
        fonts.addItems(withTitles: fontNames)
        fonts.selectItem(withTitle: font.fontName)
        fonts.frame = NSRect(x: 0, y: 123, width: 240, height: 28)
        fonts.target = self
        fonts.action = #selector(changed(_:))
        fonts.setAccessibilityLabel("Font")
        panel.addSubview(fonts)
        size.doubleValue = Double(font.pointSize)
        size.frame = NSRect(x: 326, y: 125, width: 94, height: 24)
        panel.addSubview(size)
        let sizeLabel = DocumentTheme.label("Size")
        sizeLabel.frame = NSRect(x: 282, y: 127, width: 40, height: 20)
        panel.addSubview(sizeLabel)
        let w = min(260, crop.width), h = min(80, crop.height)
        let initial = annotation?.bounds ?? NSRect(x: min(max(crop.minX, point.x), crop.maxX - w),
                                                   y: min(max(crop.minY, point.y - h), crop.maxY - h), width: w, height: h)
        let geometry: [(String, NSTextField, CGFloat)] = [("X", x, initial.minX), ("Y", y, initial.minY),
                                                       ("Width", width, initial.width), ("Height", height, initial.height)]
        for (index, entry) in geometry.enumerated() {
            let left = CGFloat(index) * 108
            let label = DocumentTheme.label(entry.0)
            label.frame = NSRect(x: left, y: 91, width: 96, height: 18)
            panel.addSubview(label)
            entry.1.frame = NSRect(x: left, y: 64, width: 96, height: 24)
            entry.1.stringValue = String(format: "%.2f", Double(entry.2))
            entry.1.setAccessibilityLabel(entry.0 + " in PDF points")
            panel.addSubview(entry.1)
        }
        for field in [size, x, y, width, height] { field.delegate = self }
        size.setAccessibilityLabel("Font size in points")
        let note = DocumentTheme.label("Position is in points from the page’s bottom-left corner.", size: 11, color: DocumentTheme.muted)
        note.frame = NSRect(x: 0, y: 36, width: 420, height: 18)
        panel.addSubview(note)
        feedback.frame = NSRect(x: 0, y: 5, width: 420, height: 24)
        panel.addSubview(feedback)
        alert.accessoryView = panel
        alert.window.initialFirstResponder = text
        validate()
    }

    var textBounds: NSRect {
        NSRect(x: x.doubleValue, y: y.doubleValue, width: width.doubleValue, height: height.doubleValue)
    }
    var font: NSFont {
        let points = size.doubleValue.isFinite ? max(6, min(144, size.doubleValue)) : 16
        return NSFont(name: fonts.titleOfSelectedItem ?? "Helvetica", size: CGFloat(points)) ?? .systemFont(ofSize: CGFloat(points))
    }
    @objc private func changed(_ sender: Any?) { validate() }
    func textDidChange(_ notification: Notification) { validate() }
    func controlTextDidChange(_ notification: Notification) { validate() }
    private func validate() {
        let numbers = [size, x, y, width, height].compactMap { Double($0.stringValue) }
        var error = ""
        if numbers.count != 5 || !numbers.allSatisfy({ $0.isFinite }) { error = "Enter a number in each size and position field." }
        else if !(6...144).contains(size.doubleValue) { error = "Choose a font size from 6 to 144 points." }
        else if textBounds.width <= 0 || textBounds.height <= 0 || !crop.contains(textBounds) { error = "Keep the text box inside the page." }
        else if !existing && text.string.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty { error = "Enter the text you want to add." }
        if error.isEmpty && !text.string.isEmpty {
            let measured = (text.string as NSString).boundingRect(with: NSSize(width: max(1, textBounds.width - 4), height: .greatestFiniteMagnitude),
                                                                options: [.usesLineFragmentOrigin, .usesFontLeading], attributes: [.font: font])
            if measured.height + 4 > textBounds.height { error = "Increase the box height or reduce the font size to fit the text." }
        }
        feedback.stringValue = error
        alert.buttons.first?.isEnabled = error.isEmpty
        if numbers.count == 5 && numbers.allSatisfy({ $0.isFinite }) { text.font = font }
    }
}
