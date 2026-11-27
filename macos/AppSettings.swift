import AppKit
import UniformTypeIdentifiers

final class AppAppearance: NSObject {
    // Match Windows' IconColor.DefaultHex; custom colors belong in local preferences.
    static let defaultHex = "#0B57D0"
    static let didChange = Notification.Name("GravityAppearanceDidChange")
    static let shared = AppAppearance(defaults: CommandLine.arguments.contains("--self-test") ? nil : .standard)
    private let defaults: UserDefaults?
    private(set) var hex: String
    private(set) var palette: AccentPalette
    var color: NSColor { Self.color(for: hex)! }

    init(defaults: UserDefaults?) {
        self.defaults = defaults
        hex = Self.normalize(defaults?.string(forKey: "appKeyColor") ?? "") ?? Self.defaultHex
        palette = AccentPalette(seed: Self.color(for: hex)!)
        super.init()
    }

    static func normalize(_ text: String) -> String? {
        var digits = text.trimmingCharacters(in: .whitespacesAndNewlines)
        if digits.hasPrefix("#") { digits.removeFirst() }
        guard [3, 6].contains(digits.count), digits.utf8.allSatisfy({
            (48...57).contains($0) || (65...70).contains($0) || (97...102).contains($0)
        }) else { return nil }
        if digits.count == 3 { digits = digits.map { "\($0)\($0)" }.joined() }
        return "#" + digits.uppercased()
    }

    static func color(for text: String) -> NSColor? {
        guard let hex = normalize(text), let value = UInt32(hex.dropFirst(), radix: 16) else { return nil }
        return DocumentTheme.color(value)
    }

    @discardableResult
    func apply(_ text: String) -> Bool {
        guard let value = Self.normalize(text) else { return false }
        hex = value
        palette = AccentPalette(seed: color)
        defaults?.set(value, forKey: "appKeyColor")
        NotificationCenter.default.post(name: Self.didChange, object: self)
        return true
    }

    func updateDockIcon() {
        guard let artwork = DocumentTheme.artwork("gravity-icon-1024", tint: color) else { return }
        NSApp.applicationIconImage = AppIcon.image(from: artwork)
    }
}

final class KeyColorWindow: NSWindowController, NSTextFieldDelegate {
    let hexField = NSTextField()
    let colorWell = NSColorWell()
    private let feedback = NSTextField(wrappingLabelWithString: "")
    private let appearance: AppAppearance

    init(appearance: AppAppearance = .shared) {
        self.appearance = appearance
        let window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 380, height: 104),
                              styleMask: [.titled, .closable], backing: .buffered, defer: false)
        window.title = "App Key Color"
        window.appearance = NSAppearance(named: .aqua)
        window.isReleasedWhenClosed = false
        super.init(window: window)
        guard let content = window.contentView else { return }
        let label = NSTextField(labelWithString: "App color")
        label.frame = NSRect(x: 20, y: 74, width: 120, height: 18)
        content.addSubview(label)
        hexField.frame = NSRect(x: 76, y: 40, width: 128, height: 24)
        hexField.setAccessibilityLabel("Hex color")
        hexField.target = self
        hexField.action = #selector(commitHexColor(_:))
        hexField.delegate = self
        content.addSubview(hexField)
        colorWell.frame = NSRect(x: 20, y: 38, width: 44, height: 28)
        // The minimal well opens the system picker at the swatch instead of
        // activating the separate Colors panel. Keep macOS 12 compatibility.
        if #available(macOS 13, *) { colorWell.colorWellStyle = .minimal }
        if #available(macOS 14, *) { colorWell.supportsAlpha = false }
        colorWell.target = self
        colorWell.action = #selector(pickedColor(_:))
        colorWell.setAccessibilityLabel("Choose app key color")
        content.addSubview(colorWell)
        let reset = NSButton(title: "Reset to Blue", target: self, action: #selector(resetColor(_:)))
        reset.frame = NSRect(x: 216, y: 36, width: 144, height: 32)
        reset.bezelStyle = .rounded
        content.addSubview(reset)
        feedback.frame = NSRect(x: 20, y: 10, width: 340, height: 18)
        feedback.font = .systemFont(ofSize: 11)
        content.addSubview(feedback)
        window.initialFirstResponder = hexField
        window.center()
        loadColor()
    }

    required init?(coder: NSCoder) { fatalError("Use init()") }
    override func showWindow(_ sender: Any?) {
        if window?.isVisible != true { loadColor() }
        super.showWindow(sender)
        window?.makeKeyAndOrderFront(sender)
    }
    private func loadColor() {
        hexField.stringValue = appearance.hex
        colorWell.color = appearance.color
        validate()
    }
    func controlTextDidChange(_ notification: Notification) { validate() }
    func controlTextDidEndEditing(_ notification: Notification) { commitHexColor(nil) }
    private func validate() {
        let valid = AppAppearance.normalize(hexField.stringValue) != nil
        feedback.stringValue = valid ? "Choose a color, or enter a hex value and press Return." : "Enter 3 or 6 hex digits, such as #F80 or #FF8800."
        feedback.textColor = valid ? .secondaryLabelColor : .systemRed
    }
    @objc func commitHexColor(_ sender: Any?) {
        guard let hex = AppAppearance.normalize(hexField.stringValue) else { validate(); return }
        if hex != appearance.hex { appearance.apply(hex) }
        loadColor()
    }
    @objc func resetColor(_ sender: Any?) {
        appearance.apply(AppAppearance.defaultHex)
        loadColor()
    }
    @objc func pickedColor(_ sender: Any?) {
        guard let color = colorWell.color.usingColorSpace(.sRGB) else { return }
        hexField.stringValue = String(format: "#%02X%02X%02X", Int((color.redComponent * 255).rounded()),
                                      Int((color.greenComponent * 255).rounded()), Int((color.blueComponent * 255).rounded()))
        commitHexColor(sender)
    }
}

// Only PDFs are registered: importing an image creates a new PDF rather than
// making Gravity an image editor or taking over image file associations.
final class FileAssociationsWindow: NSWindowController {
    typealias SetDefault = (URL, @escaping (Error?) -> Void) -> Void
    let status = NSTextField(wrappingLabelWithString: "")
    let chooseButton = NSButton()
    private let appURL: URL
    private let currentDefault: () -> URL?
    private let setDefault: SetDefault
    private var changing = false

    init(appURL: URL = Bundle.main.bundleURL,
         currentDefault: @escaping () -> URL? = { NSWorkspace.shared.urlForApplication(toOpen: .pdf) },
         setDefault: @escaping SetDefault = { url, completion in
             NSWorkspace.shared.setDefaultApplication(at: url, toOpen: .pdf, completion: completion)
         }) {
        self.appURL = appURL
        self.currentDefault = currentDefault
        self.setDefault = setDefault
        let window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 480, height: 274),
                              styleMask: [.titled, .closable], backing: .buffered, defer: false)
        window.title = "File Associations"
        window.appearance = NSAppearance(named: .aqua)
        window.isReleasedWhenClosed = false
        super.init(window: window)
        guard let content = window.contentView else { return }
        let explanation = NSTextField(wrappingLabelWithString: "Choose Gravity as the default app for PDF documents. macOS may ask you to confirm the change.")
        explanation.frame = NSRect(x: 24, y: 204, width: 432, height: 48)
        content.addSubview(explanation)
        let type = NSTextField(labelWithString: "PDF document (.pdf)")
        type.font = .systemFont(ofSize: 14, weight: .semibold)
        type.frame = NSRect(x: 24, y: 170, width: 432, height: 22)
        content.addSubview(type)
        status.frame = NSRect(x: 24, y: 90, width: 432, height: 68)
        status.setAccessibilityLabel("Default PDF application")
        content.addSubview(status)
        chooseButton.title = "Make Gravity Default"
        chooseButton.target = self
        chooseButton.action = #selector(chooseDefault(_:))
        chooseButton.bezelStyle = .rounded
        chooseButton.frame = NSRect(x: 254, y: 20, width: 204, height: 32)
        content.addSubview(chooseButton)
        let closeButton = NSButton(title: "Close", target: self, action: #selector(dismiss(_:)))
        closeButton.bezelStyle = .rounded
        closeButton.keyEquivalent = "\u{1b}"
        closeButton.frame = NSRect(x: 154, y: 20, width: 96, height: 32)
        content.addSubview(closeButton)
        window.center()
        refreshStatus()
    }

    required init?(coder: NSCoder) { fatalError("Use init()") }
    override func showWindow(_ sender: Any?) {
        if !changing { refreshStatus() }
        super.showWindow(sender)
        window?.makeKeyAndOrderFront(sender)
    }
    private var isDefault: Bool {
        currentDefault()?.resolvingSymlinksInPath().standardizedFileURL == appURL.resolvingSymlinksInPath().standardizedFileURL
    }
    func refreshStatus() {
        if isDefault { status.stringValue = "Gravity is the default app for PDF documents." }
        else if let url = currentDefault() {
            status.stringValue = "Current default: \(FileManager.default.displayName(atPath: url.path))"
        } else { status.stringValue = "No default PDF app is currently selected." }
        chooseButton.isEnabled = !changing && !isDefault
    }
    @objc func chooseDefault(_ sender: Any?) {
        guard !changing else { return }
        changing = true
        chooseButton.isEnabled = false
        status.stringValue = "Waiting for macOS to update the default PDF app…"
        setDefault(appURL) { [weak self] error in
            let finish = {
                guard let self = self else { return }
                self.changing = false
                self.refreshStatus()
                if let error = error { self.status.stringValue = "The default app was not changed. \(error.localizedDescription)" }
                else if !self.isDefault { self.status.stringValue = "The default app was not changed. You can try again, or use Finder → Get Info → Open with → Change All." }
            }
            if Thread.isMainThread { finish() }
            else { DispatchQueue.main.async(execute: finish) }
        }
    }
    @objc private func dismiss(_ sender: Any?) { close() }
}
