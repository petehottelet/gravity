import AppKit

// The in-window menu row uses the real macOS commands and their validation.
// It mirrors the expandable application menus in the Windows command bar.
final class WorkspaceMenus: NSView {
    var closeMenus: (() -> Void)?
    private let titles = ["File", "Edit", "View", "Pages", "Window", "Gravity"]
    private var buttons: [WorkspaceButton] = []
    private var closeButton: WorkspaceButton!
    override var isFlipped: Bool { true }

    override init(frame: NSRect) {
        super.init(frame: frame)
        let symbols = ["doc", "pencil", "eye", "doc.on.doc", "macwindow", "info.circle"]
        for (index, title) in titles.enumerated() {
            let button = WorkspaceButton(title, symbol: symbols[index], label: "\(title) menu", target: self, action: #selector(openMenu(_:)))
            button.tag = index
            button.identifier = NSUserInterfaceItemIdentifier("applicationMenu\(title)")
            buttons.append(button)
            addSubview(button)
        }
        closeButton = WorkspaceButton("", symbol: "ellipsis", label: "Hide application menus", target: self, action: #selector(close(_:)))
        closeButton.identifier = NSUserInterfaceItemIdentifier("hideApplicationMenus")
        addSubview(closeButton)
        identifier = NSUserInterfaceItemIdentifier("applicationMenus")
        isHidden = true
    }
    required init?(coder: NSCoder) { fatalError("Use init(frame:)") }
    override func layout() {
        super.layout()
        let availableWidth = max(0, bounds.width - 64)
        let gap = min(8, availableWidth / CGFloat(buttons.count - 1))
        let buttonSpace = max(0, availableWidth - gap * CGFloat(buttons.count - 1))
        let textWidths = buttons.map {
            ceil(($0.title as NSString).size(withAttributes: [.font: $0.font ?? .systemFont(ofSize: 13)]).width)
        }
        let iconWidth = WorkspaceMetrics.iconSize + WorkspaceMetrics.iconGap
        let showsIcons = textWidths.reduce(0, +) + CGFloat(buttons.count) * (iconWidth + 16) <= buttonSpace
        // Size each target to its content so every icon/label pair has the same
        // side padding, regardless of the length of the menu title.
        let widths = textWidths.map { $0 + 16 + (showsIcons ? iconWidth : 0) }
        let scale = min(1, buttonSpace / widths.reduce(0, +))
        var x: CGFloat = 12
        for (index, button) in buttons.enumerated() {
            let width = widths[index] * scale
            button.frame = NSRect(x: x, y: 6, width: width, height: WorkspaceMetrics.buttonHeight)
            button.imagePosition = showsIcons ? .imageLeading : .noImage
            x += width + gap
        }
        closeButton.frame = NSRect(x: bounds.width - 44, y: 6, width: 32, height: WorkspaceMetrics.buttonHeight)
    }
    override func setFrameSize(_ size: NSSize) { super.setFrameSize(size); needsLayout = true }
    @objc private func openMenu(_ sender: NSButton) {
        guard titles.indices.contains(sender.tag),
              let menu = NSApp.mainMenu?.items.first(where: { $0.title == titles[sender.tag] })?.submenu?.copy() as? NSMenu else { return }
        menu.popUp(positioning: nil, at: NSPoint(x: sender.frame.minX, y: sender.frame.maxY), in: self)
    }
    @objc private func close(_ sender: Any?) { closeMenus?() }
}
