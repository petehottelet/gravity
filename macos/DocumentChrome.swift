import AppKit
import PDFKit

// Shared visual vocabulary for the native document workspace. All sizes are points.
enum DocumentTheme {
    static func color(_ value: UInt32) -> NSColor {
        NSColor(srgbRed: CGFloat((value >> 16) & 255) / 255,
                green: CGFloat((value >> 8) & 255) / 255,
                blue: CGFloat(value & 255) / 255, alpha: 1)
    }

    static let ink = color(0x222831)
    static let muted = color(0x414b57)
    static let accent = color(0x174b8b)
    static let chrome = color(0xf7f8fa)
    static let canvas = color(0xe8ebef)
    static let rule = color(0xcdd3db)
    static let controlRule = color(0x7c8795)
    static let hover = color(0xe2e8f0)
    static let pressed = color(0xd4e1f1)
    static let track = color(0xe5e8ed)
    static let navigation = color(0xe2eaf5)
    static let selection = color(0xdce8f7)
    static let status = color(0xf2f4f7)

    static func label(_ text: String = "", size: CGFloat = 12, weight: NSFont.Weight = .regular,
                      color: NSColor = ink) -> NSTextField {
        let label = NSTextField(labelWithString: text)
        label.font = .systemFont(ofSize: size, weight: weight)
        label.textColor = color
        label.lineBreakMode = .byTruncatingTail
        label.maximumNumberOfLines = 1
        return label
    }

    static func symbol(_ name: String) -> NSImage? {
        NSImage(systemSymbolName: name, accessibilityDescription: nil)?
            .withSymbolConfiguration(NSImage.SymbolConfiguration(pointSize: 16, weight: .regular))
    }
}

final class WorkspaceSurface: NSView {
    var fill = DocumentTheme.chrome { didSet { needsDisplay = true } }
    var bottomRule = false
    var topRule = false
    var rightRule = false
    var cornerRadius: CGFloat = 0
    var arrange: (() -> Void)?
    override var isFlipped: Bool { true }
    override func layout() { super.layout(); arrange?() }
    override func setFrameSize(_ newSize: NSSize) { super.setFrameSize(newSize); needsLayout = true }
    override func draw(_ dirtyRect: NSRect) {
        fill.setFill()
        NSBezierPath(roundedRect: bounds, xRadius: cornerRadius, yRadius: cornerRadius).fill()
        DocumentTheme.rule.setFill()
        if bottomRule { NSRect(x: 0, y: bounds.height - 1, width: bounds.width, height: 1).fill() }
        if topRule { NSRect(x: 0, y: 0, width: bounds.width, height: 1).fill() }
        if rightRule { NSRect(x: bounds.width - 1, y: 0, width: 1, height: bounds.height).fill() }
    }
}

final class WorkspaceButton: NSButton {
    enum SelectionStyle { case mode, navigation }
    var selectionStyle: SelectionStyle = .mode
    var selected = false { didSet { state = selected ? .on : .off; updateTint(); needsDisplay = true } }
    var outlined = false
    private var hovered = false
    private var tracking: NSTrackingArea?

    init(_ title: String, symbol: String? = nil, label: String? = nil, target: AnyObject?, action: Selector) {
        super.init(frame: .zero)
        self.title = title
        self.target = target
        self.action = action
        bezelStyle = .smallSquare
        isBordered = false
        setButtonType(.momentaryChange)
        font = .systemFont(ofSize: 13)
        imagePosition = title.isEmpty ? .imageOnly : .imageLeading
        if let symbol = symbol { image = DocumentTheme.symbol(symbol) }
        imageScaling = .scaleProportionallyDown
        focusRingType = .exterior
        let name = label ?? title
        setAccessibilityLabel(name)
        toolTip = name
        updateTint()
    }

    required init?(coder: NSCoder) { fatalError("Use init(title:symbol:label:target:action:)") }

    private func updateTint() { contentTintColor = selected ? DocumentTheme.accent : DocumentTheme.ink }

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
        if selected || (isEnabled && (hovered || isHighlighted)) {
            NSGraphicsContext.saveGraphicsState()
            if selected && selectionStyle == .mode {
                let shadow = NSShadow()
                shadow.shadowColor = NSColor.black.withAlphaComponent(0.12)
                shadow.shadowBlurRadius = 3
                shadow.shadowOffset = NSSize(width: 0, height: -1)
                shadow.set()
            }
            let fill: NSColor = isHighlighted && isEnabled ? DocumentTheme.pressed
                : selected ? (selectionStyle == .mode ? .white : DocumentTheme.navigation) : DocumentTheme.hover
            fill.setFill()
            shape.fill()
            NSGraphicsContext.restoreGraphicsState()
        }
        if outlined { DocumentTheme.controlRule.setStroke(); shape.lineWidth = 1; shape.stroke() }
        super.draw(dirtyRect)
    }
    override var focusRingMaskBounds: NSRect { bounds }
    override func drawFocusRingMask() {
        NSBezierPath(roundedRect: bounds.insetBy(dx: 1, dy: 1), xRadius: 6, yRadius: 6).fill()
    }
}

final class OutlineRowView: NSTableRowView {
    override func drawSelection(in dirtyRect: NSRect) {
        DocumentTheme.navigation.setFill()
        NSBezierPath(roundedRect: bounds.insetBy(dx: 6, dy: 2), xRadius: 6, yRadius: 6).fill()
    }
}

final class PageCollectionView: NSCollectionView {
    var openPage: ((Int) -> Void)?
    var deleteSelection: (() -> Void)?
    var copySelection: (() -> Void)?
    var cutSelection: (() -> Void)?
    var pasteSelection: (() -> Void)?

    override func mouseDown(with event: NSEvent) {
        super.mouseDown(with: event)
        if event.clickCount == 2, let path = indexPathForItem(at: convert(event.locationInWindow, from: nil)) {
            openPage?(path.item)
        }
    }
    override func keyDown(with event: NSEvent) {
        switch event.keyCode {
        case 36, 76:
            if let page = selectionIndexPaths.map({ $0.item }).min() { openPage?(page) }
        case 51, 117: deleteSelection?()
        default: super.keyDown(with: event)
        }
    }
    @objc func copy(_ sender: Any?) { copySelection?() }
    @objc func cut(_ sender: Any?) { cutSelection?() }
    @objc func paste(_ sender: Any?) { pasteSelection?() }
}

final class PageThumbnailItem: NSCollectionViewItem {
    static let reuseID = NSUserInterfaceItemIdentifier("PageThumbnail")
    private let tile = WorkspaceSurface()
    private let preview = NSImageView()
    private let number = DocumentTheme.label(size: 12, weight: .semibold)
    private let caption = DocumentTheme.label(size: 11, color: DocumentTheme.muted)
    private let check = NSImageView()
    var showsCheck = true

    override func loadView() {
        let root = WorkspaceSurface()
        root.fill = .clear
        view = root
        tile.fill = .clear
        tile.cornerRadius = 6
        tile.wantsLayer = true
        preview.imageScaling = .scaleProportionallyUpOrDown
        preview.wantsLayer = true
        preview.layer?.shadowColor = NSColor.black.cgColor
        preview.layer?.shadowOpacity = 0.16
        preview.layer?.shadowRadius = 4
        preview.layer?.shadowOffset = CGSize(width: 0, height: -2)
        check.image = DocumentTheme.symbol("checkmark.circle.fill")
        check.contentTintColor = DocumentTheme.accent
        number.alignment = .center
        caption.alignment = .center
        root.addSubview(tile)
        tile.addSubview(preview)
        tile.addSubview(check)
        root.addSubview(number)
        root.addSubview(caption)
        root.arrange = { [weak self] in self?.arrange() }
        updateSelection()
    }

    func configure(image: NSImage?, index: Int, title: String?, rail: Bool) {
        _ = view
        showsCheck = !rail
        preview.image = image
        number.stringValue = "Page \(index + 1)"
        caption.stringValue = title ?? ""
        let name = [number.stringValue, title].compactMap { $0 }.joined(separator: ", ")
        view.setAccessibilityLabel(name)
        view.toolTip = name
        updateSelection()
        view.needsLayout = true
    }

    override var isSelected: Bool { didSet { updateSelection() } }
    private func updateSelection() {
        tile.fill = isSelected ? DocumentTheme.selection : .clear
        tile.layer?.borderWidth = isSelected ? 2 : 0
        tile.layer?.borderColor = DocumentTheme.accent.cgColor
        tile.layer?.cornerRadius = 6
        check.isHidden = !isSelected || !showsCheck
        number.textColor = isSelected ? DocumentTheme.accent : DocumentTheme.ink
    }
    private func arrange() {
        let width = view.bounds.width, height = view.bounds.height - 44
        tile.frame = NSRect(x: 0, y: 0, width: width, height: height)
        preview.frame = tile.bounds.insetBy(dx: 12, dy: 12)
        check.frame = NSRect(x: width - 27, y: 7, width: 20, height: 20)
        number.frame = NSRect(x: 0, y: height + 7, width: width, height: 17)
        caption.frame = NSRect(x: 0, y: height + 25, width: width, height: 16)
    }
}
