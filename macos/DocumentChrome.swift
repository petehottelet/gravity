import AppKit
import PDFKit

enum WorkspaceMetrics {
    static let headerHeight: CGFloat = 40
    static let commandHeight: CGFloat = 44
    static let buttonHeight: CGFloat = 32
    static let iconSize: CGFloat = 18
    static let iconGap: CGFloat = 6
}

// Shared visual vocabulary for the native document workspace. All sizes are points.
enum DocumentTheme {
    static func color(_ value: UInt32) -> NSColor {
        NSColor(srgbRed: CGFloat((value >> 16) & 255) / 255,
                green: CGFloat((value >> 8) & 255) / 255,
                blue: CGFloat(value & 255) / 255, alpha: 1)
    }

    static let ink = color(0x262626)
    static let muted = color(0x484848)
    static var keyColor: NSColor { AppAppearance.shared.color }
    static var palette: AccentPalette { AppAppearance.shared.palette }
    static var accent: NSColor { palette.foreground }
    static let chrome = color(0xf8f8f8)
    static let canvas = color(0xebebeb)
    static let rule = color(0xd3d3d3)
    static let controlRule = color(0x858585)
    static var hover: NSColor { palette.hover }
    static var pressed: NSColor { palette.pressed }
    static let track = color(0xe8e8e8)
    static var navigation: NSColor { palette.navigation }
    static var selection: NSColor { palette.selection }
    static let status = color(0xf4f4f4)

    static func refresh(_ view: NSView) {
        (view as? WorkspaceButton)?.refreshAppearance()
        view.needsDisplay = true
        view.subviews.forEach(refresh)
    }

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
        if name == "save", let url = Bundle.main.url(forResource: "save", withExtension: "svg") {
            let image = NSImage(contentsOf: url)
            image?.isTemplate = true
            return image
        }
        return NSImage(systemSymbolName: name, accessibilityDescription: nil)?
            .withSymbolConfiguration(NSImage.SymbolConfiguration(pointSize: 16, weight: .regular))
    }

    static var lockup: NSImage? { artwork("Lockup", tint: keyColor) }
    static var brandMark: NSImage? { brandMark(tint: keyColor) }
    static func brandMark(tint: NSColor) -> NSImage? {
        guard let url = Bundle.main.url(forResource: "g-mark", withExtension: "png"),
              let mark = NSImage(contentsOf: url) else { return nil }
        return NSImage(size: NSSize(width: 96, height: 96), flipped: false) { rect in
            let scale = min(rect.width / mark.size.width, rect.height / mark.size.height)
            let size = NSSize(width: mark.size.width * scale, height: mark.size.height * scale)
            let frame = NSRect(x: rect.midX - size.width / 2, y: rect.midY - size.height / 2,
                               width: size.width, height: size.height)
            mark.draw(in: frame, from: .zero, operation: .sourceOver, fraction: 1)
            tint.setFill()
            rect.fill(using: .sourceAtop)
            return true
        }
    }

    // Use the same original artwork and blue-coverage tint as the Windows app.
    static func artwork(_ name: String, tint: NSColor) -> NSImage? {
        guard let url = Bundle.main.url(forResource: name, withExtension: "png"),
              let source = NSBitmapImageRep(data: (try? Data(contentsOf: url)) ?? Data()),
              let tinted = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: source.pixelsWide, pixelsHigh: source.pixelsHigh,
                                            bitsPerSample: 8, samplesPerPixel: 4, hasAlpha: true, isPlanar: false,
                                            colorSpaceName: .deviceRGB, bytesPerRow: source.pixelsWide * 4, bitsPerPixel: 32),
              let color = tint.usingColorSpace(.sRGB) else { return nil }
        // Work in the asset's original sRGB bytes. AppKit colorAt() can interpret
        // an untagged PNG as calibrated RGB and shift the exact brand color.
        var pixel = [Int](repeating: 0, count: source.samplesPerPixel)
        for y in 0..<source.pixelsHigh {
            for x in 0..<source.pixelsWide {
                source.getPixel(&pixel, atX: x, y: y)
                let r = CGFloat(pixel[0]), g = CGFloat(pixel[1]), b = CGFloat(pixel[2])
                let coverage = b - r > 16 && b - g > 8 ? min(1, max(0, min((b - r) / 197, (b - g) / 121))) : 0
                func tint(_ original: CGFloat, _ target: CGFloat, _ base: CGFloat) -> Int {
                    Int(min(255, max(0, original + coverage * (target * 255 - base))).rounded())
                }
                var result = [tint(r, color.redComponent, 11), tint(g, color.greenComponent, 87),
                              tint(b, color.blueComponent, 208), source.hasAlpha ? pixel[3] : 255]
                tinted.setPixel(&result, atX: x, y: y)
            }
        }
        let image = NSImage(size: source.size)
        image.addRepresentation(tinted.retagging(with: .sRGB) ?? tinted)
        return image
    }
}

// Keep filled actions close to the logo. Text and icons on pale surfaces need
// their own darker shade; text on filled actions adapts to the actual fill.
struct AccentPalette {
    let foreground: NSColor
    let action: NSColor
    let actionHover: NSColor
    let actionPressed: NSColor
    let hover: NSColor
    let pressed: NSColor
    let navigation: NSColor
    let selection: NSColor

    init(seed: NSColor) {
        let tone = Self.readable(seed, against: [.white], minimum: 2)
        hover = Self.mix(tone, .white, 0.87)
        pressed = Self.mix(tone, .white, 0.77)
        navigation = Self.mix(tone, .white, 0.85)
        selection = Self.mix(tone, .white, 0.83)
        foreground = Self.readable(seed, against: [.white, DocumentTheme.chrome, DocumentTheme.canvas, hover, pressed, navigation, selection], minimum: 4.5)
        action = Self.mix(seed, .black, 0.08)
        actionHover = Self.mix(action, .black, 0.04)
        actionPressed = Self.mix(action, .black, 0.08)
    }

    static func text(on background: NSColor) -> NSColor {
        contrast(.black, background) > contrast(.white, background) ? .black : .white
    }

    static func mix(_ a: NSColor, _ b: NSColor, _ amount: CGFloat) -> NSColor {
        let a = a.usingColorSpace(.sRGB)!, b = b.usingColorSpace(.sRGB)!
        func channel(_ x: CGFloat, _ y: CGFloat) -> CGFloat { ((x + (y - x) * amount) * 255).rounded() / 255 }
        return NSColor(srgbRed: channel(a.redComponent, b.redComponent), green: channel(a.greenComponent, b.greenComponent),
                       blue: channel(a.blueComponent, b.blueComponent), alpha: 1)
    }
    private static func readable(_ seed: NSColor, against surfaces: [NSColor], minimum: CGFloat) -> NSColor {
        for step in 0...100 {
            let candidate = mix(seed, .black, CGFloat(step) / 100)
            if surfaces.allSatisfy({ contrast(candidate, $0) >= minimum }) { return candidate }
        }
        return .black
    }
    static func contrast(_ a: NSColor, _ b: NSColor) -> CGFloat {
        func luminance(_ color: NSColor) -> CGFloat {
            let rgb = color.usingColorSpace(.sRGB)!
            func linear(_ value: CGFloat) -> CGFloat { value <= 0.04045 ? value / 12.92 : pow((value + 0.055) / 1.055, 2.4) }
            return 0.2126 * linear(rgb.redComponent) + 0.7152 * linear(rgb.greenComponent) + 0.0722 * linear(rgb.blueComponent)
        }
        let x = luminance(a), y = luminance(b)
        return (max(x, y) + 0.05) / (min(x, y) + 0.05)
    }
}

final class WorkspaceSurface: NSView {
    var fill = DocumentTheme.chrome { didSet { needsDisplay = true } }
    var bottomRule = false
    var topRule = false
    var rightRule = false
    var cornerRadius: CGFloat = 0
    var arrange: (() -> Void)?
    var draggable = false
    override var isFlipped: Bool { true }
    override var mouseDownCanMoveWindow: Bool { draggable }
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
    var showsHoverBackground = true
    var iconSize = WorkspaceMetrics.iconSize
    private let tintsImage: Bool
    private var hovered = false
    private var tracking: NSTrackingArea?

    init(_ title: String, symbol: String? = nil, label: String? = nil, target: AnyObject?, action: Selector) {
        tintsImage = symbol != nil
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
    func refreshAppearance() { updateTint(); needsDisplay = true }

    override var isFlipped: Bool { true }

    // AppKit's imageLeading layout pins the image to the edge and centers the
    // title independently. Keep each icon/label pair together, as on Windows.
    var contentFrames: (image: NSRect, title: NSRect) {
        let hasImage = image != nil && imagePosition != .noImage
        let hasTitle = !title.isEmpty && imagePosition != .imageOnly
        let imageWidth = hasImage ? iconSize : 0
        let gap = hasImage && hasTitle ? WorkspaceMetrics.iconGap : 0
        let textSize = hasTitle ? (title as NSString).size(withAttributes: [.font: font ?? .systemFont(ofSize: 13)]) : .zero
        let textWidth = min(ceil(textSize.width), max(0, bounds.width - 16 - imageWidth - gap))
        let x = floor((bounds.width - imageWidth - gap - textWidth) / 2)
        let trailing = imagePosition == .imageTrailing || imagePosition == .imageRight
        let imageRect = hasImage ? NSRect(x: trailing ? x + textWidth + gap : x,
                                         y: floor((bounds.height - iconSize) / 2), width: iconSize, height: iconSize) : .zero
        let titleRect = hasTitle ? NSRect(x: trailing ? x : x + imageWidth + gap,
                                         y: floor((bounds.height - ceil(textSize.height)) / 2), width: textWidth, height: ceil(textSize.height)) : .zero
        return (imageRect, titleRect)
    }

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
        if selected || (showsHoverBackground && isEnabled && (hovered || isHighlighted)) {
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
        if outlined {
            DocumentTheme.controlRule.withAlphaComponent(isEnabled ? 1 : 0.5).setStroke()
            shape.lineWidth = 1
            shape.stroke()
        }
        let tint = isEnabled ? (selected || hovered || isHighlighted ? DocumentTheme.accent : DocumentTheme.ink) : DocumentTheme.muted
        let opacity: CGFloat = isEnabled ? 1 : 0.5
        let frames = contentFrames
        if let image = image, !frames.image.isEmpty {
            let drawing = tintsImage ? NSImage(size: image.size, flipped: false) { rect in
                image.draw(in: rect, from: .zero, operation: .sourceOver, fraction: 1)
                tint.setFill()
                rect.fill(using: .sourceAtop)
                return true
            } : image
            // Symbols such as ellipsis and minus are naturally short; fit them
            // inside the icon slot without stretching their aspect ratio.
            let scale = min(frames.image.width / max(1, drawing.size.width), frames.image.height / max(1, drawing.size.height))
            let size = NSSize(width: drawing.size.width * scale, height: drawing.size.height * scale)
            let rect = NSRect(x: frames.image.midX - size.width / 2, y: frames.image.midY - size.height / 2,
                              width: size.width, height: size.height)
            drawing.draw(in: rect, from: .zero, operation: .sourceOver, fraction: opacity, respectFlipped: true, hints: nil)
        }
        if !frames.title.isEmpty {
            let paragraph = NSMutableParagraphStyle()
            paragraph.lineBreakMode = .byTruncatingTail
            (title as NSString).draw(in: frames.title, withAttributes: [
                .font: font ?? .systemFont(ofSize: 13), .foregroundColor: tint.withAlphaComponent(opacity), .paragraphStyle: paragraph
            ])
        }
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
    static let pageDragType = NSPasteboard.PasteboardType("com.gravity.pdf-pages")
    var openPage: ((Int) -> Void)?
    var deleteSelection: (() -> Void)?
    var copySelection: (() -> Void)?
    var cutSelection: (() -> Void)?
    var pasteSelection: (() -> Void)?
    var moveSelection: ((Int) -> Void)?
    var pageMenu: (() -> NSMenu)?

    override func menu(for event: NSEvent) -> NSMenu? {
        window?.makeFirstResponder(self)
        if let path = indexPathForItem(at: convert(event.locationInWindow, from: nil)), !selectionIndexPaths.contains(path) {
            selectionIndexPaths = [path]
        }
        delegate?.collectionView?(self, didSelectItemsAt: selectionIndexPaths)
        return pageMenu?()
    }

    override func mouseDown(with event: NSEvent) {
        super.mouseDown(with: event)
        if event.clickCount == 2, let path = indexPathForItem(at: convert(event.locationInWindow, from: nil)) {
            openPage?(path.item)
        }
    }
    override func keyDown(with event: NSEvent) {
        if event.modifierFlags.intersection(.deviceIndependentFlagsMask) == .option && [125, 126].contains(event.keyCode) {
            moveSelection?(event.keyCode == 126 ? -1 : 1)
            return
        }
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
    func refreshAppearance() { updateSelection() }
    private func updateSelection() {
        tile.fill = isSelected ? DocumentTheme.selection : .clear
        tile.layer?.borderWidth = isSelected ? 2 : 0
        tile.layer?.borderColor = DocumentTheme.accent.cgColor
        tile.layer?.cornerRadius = 6
        check.isHidden = !isSelected || !showsCheck
        check.contentTintColor = DocumentTheme.accent
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
