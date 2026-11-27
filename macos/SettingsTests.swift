import AppKit
import PDFKit

enum SettingsTests {
    static func run(check: (Bool, String) throws -> Void) throws {
        for name in ["Gravity", "GravityPdf"] {
            let url = Bundle.main.url(forResource: name, withExtension: "icns")
            try check(url.flatMap { NSImage(contentsOf: $0) }?.isValid == true, "bundle includes a readable \(name) icon")
        }
        try check(DocumentTheme.symbol("save")?.isValid == true, "bundled Save artwork is readable")
        guard let licenseURL = Bundle.main.url(forResource: "Lucide", withExtension: "txt", subdirectory: "licenses") else {
            throw PDFError.invalid
        }
        let iconLicense = try String(contentsOf: licenseURL, encoding: .utf8)
        try check(iconLicense.contains("ISC License") && iconLicense.contains("Permission to use, copy, modify"),
                  "bundled icon retains its copyright and permission notice")
        let fileMenu = NSApp.mainMenu!.items.first { $0.title == "File" }!.submenu!
        for title in ["App Key Color…", "File Associations…", "Create PDF from Images…", "Save Selected Pages As…", "Export Pages…", "Quit Gravity"] {
            let item = fileMenu.items.first { $0.title == title }
            try check(item?.action != nil, "File menu provides \(title)")
        }
        for title in ["App Key Color…", "File Associations…", "Create PDF from Images…"] {
            let item = fileMenu.items.first { $0.title == title }!
            try check(NSApp.target(forAction: item.action!, to: item.target, from: item) is AppDelegate,
                      "\(title) works without an open document")
        }
        try check(AppAppearance.normalize("  f80 ") == "#FF8800" && AppAppearance.normalize("#abcdef") == "#ABCDEF",
                  "key color accepts short and long hex input")
        for invalid in ["", "#12", "#12345G", "##123456", "#12345678", "１２３"] {
            try check(AppAppearance.normalize(invalid) == nil, "invalid hex input is rejected")
        }
        let suite = "org.gravitypdf.settings-tests." + UUID().uuidString
        let defaults = UserDefaults(suiteName: suite)!
        defer { defaults.removePersistentDomain(forName: suite) }
        let stored = AppAppearance(defaults: defaults)
        try check(stored.hex == "#0B57D0", "a fresh installation defaults to Windows brand blue")
        try check(stored.apply("#FF0000") && AppAppearance(defaults: defaults).hex == "#FF0000",
                  "this installation's red preference survives a restart")
        let otherSuite = "org.gravitypdf.settings-tests." + UUID().uuidString
        let otherDefaults = UserDefaults(suiteName: otherSuite)!
        defer { otherDefaults.removePersistentDomain(forName: otherSuite) }
        try check(AppAppearance(defaults: otherDefaults).hex == "#0B57D0" && AppAppearance(defaults: nil).hex == "#0B57D0",
                  "a local red preference does not change other installations or the built-in default")
        try check(stored.apply("#38A") && AppAppearance(defaults: defaults).hex == "#3388AA", "key color persists when settings are reloaded")
        defaults.set("broken", forKey: "appKeyColor")
        try check(AppAppearance(defaults: defaults).hex == "#0B57D0", "invalid saved color falls back to brand blue")

        func descendants(_ view: NSView) -> [NSView] { [view] + view.subviews.flatMap(descendants) }
        let shared = AppAppearance.shared
        let original = shared.hex
        defer { shared.apply(original) }
        let home = HomeWindow(recentDocuments: { [] })
        let first = GravityDocument(), second = GravityDocument()
        first.makeWindowControllers(); second.makeWindowControllers()
        let controllers = [first, second].map { $0.windowControllers.first as! DocumentWindow }
        defer { home.close(); controllers.forEach { $0.close() } }
        for controller in controllers { controller.showOrganize(nil); controller.window!.contentView!.layoutSubtreeIfNeeded() }
        let homeViews = descendants(home.window!.contentView!)
        let homeLogo = homeViews.compactMap { $0 as? NSImageView }.first { $0.image != nil }!
        let previousLogo = homeLogo.image
        let dialog = KeyColorWindow()
        defer { dialog.close() }
        try check(descendants(dialog.window!.contentView!).compactMap { $0 as? NSButton }.contains { $0.title == "Reset to Blue" },
                  "color dialog identifies blue as the reset default")
        let previousAccent = DocumentTheme.accent
        dialog.hexField.stringValue = "#00AA66"
        dialog.controlTextDidChange(Notification(name: NSControl.textDidChangeNotification))
        try check(shared.hex == original, "typing a hex value does not save an unfinished edit")
        dialog.commitHexColor(nil)
        try check(shared.hex == "#00AA66" && DocumentTheme.accent != previousAccent, "committing a hex value changes the shared accent palette at runtime")
        try check(homeLogo.image !== previousLogo, "an already-open Home window updates its logo")
        for controller in controllers {
            let views = descendants(controller.window!.contentView!)
            let buttons = views.compactMap { $0 as? WorkspaceButton }.filter(\.selected)
            try check(!buttons.isEmpty && buttons.allSatisfy { $0.contentTintColor == DocumentTheme.accent },
                      "already-open document controls adopt the new key color")
            let grid = views.compactMap { $0 as? NSCollectionView }.first { !$0.isHidden && !$0.visibleItems().isEmpty }
            if let item = grid?.visibleItems().first as? PageThumbnailItem {
                item.isSelected = true
                let surfaces = descendants(item.view).compactMap { $0 as? WorkspaceSurface }
                try check(surfaces.contains { $0.fill == DocumentTheme.selection }, "thumbnail selection uses the changed key color")
            }
        }
        try check(DocumentTheme.palette.action.greenComponent > DocumentTheme.palette.action.redComponent,
                  "Home action fill follows the selected green hue")
        let green = shared.hex
        dialog.hexField.stringValue = "invalid"
        dialog.controlTextDidChange(Notification(name: NSControl.textDidChangeNotification))
        dialog.commitHexColor(nil)
        try check(shared.hex == green, "invalid color cannot replace the saved key color")
        dialog.colorWell.color = DocumentTheme.color(0x0b57d0)
        dialog.colorWell.sendAction(dialog.colorWell.action, to: dialog.colorWell.target)
        try check(shared.hex == "#0B57D0" && dialog.hexField.stringValue == "#0B57D0", "choosing a swatch saves its RGB value immediately and synchronizes the hex field")
        dialog.hexField.stringValue = "#38A"
        dialog.controlTextDidEndEditing(Notification(name: NSControl.textDidEndEditingNotification))
        try check(shared.hex == "#3388AA" && dialog.hexField.stringValue == shared.hex, "leaving the hex field saves and normalizes a valid edit")
        dialog.close()
        try check(shared.hex == "#3388AA", "closing color settings keeps the chosen color")

        // The same windows stay alive as the palette changes; capture the
        // rendered controls to catch stale NSImage/layer colors as well as values.
        for hex in ["#FF0000", "#0B57D0", "#FFFF00", "#777777"] {
            shared.apply(hex)
            for surface in [DocumentTheme.hover, DocumentTheme.pressed, DocumentTheme.navigation, DocumentTheme.selection] {
                try check(AccentPalette.contrast(DocumentTheme.accent, surface) >= 4.5, "changed accents remain readable on selection and hover fills")
            }
            try check(AccentPalette.contrast(AccentPalette.text(on: DocumentTheme.palette.action), DocumentTheme.palette.action) >= 4.5,
                      "filled action text remains readable after changing key color")
            if let flag = CommandLine.arguments.firstIndex(of: "--ui-snapshots"), flag + 1 < CommandLine.arguments.count {
                let directory = URL(fileURLWithPath: CommandLine.arguments[flag + 1])
                try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
                for (name, window) in [("home", home.window!), ("document", controllers[0].window!)] {
                    let root = window.contentView!
                    root.layoutSubtreeIfNeeded()
                    guard let bitmap = root.bitmapImageRepForCachingDisplay(in: root.bounds) else { throw PDFError.write }
                    root.cacheDisplay(in: root.bounds, to: bitmap)
                    try bitmap.representation(using: .png, properties: [:])!.write(to: directory.appendingPathComponent("key-color-\(hex.dropFirst())-\(name).png"))
                }
            }
        }
        shared.apply("#FF0000")
        dialog.resetColor(nil)
        try check(dialog.hexField.stringValue == "#0B57D0" && shared.hex == "#0B57D0", "Reset to Blue restores the brand palette immediately")

        let appURL = URL(fileURLWithPath: "/Applications/Gravity.app")
        var current: URL? = URL(fileURLWithPath: "/System/Applications/Preview.app")
        var requests: [URL] = []
        var completion: ((Error?) -> Void)?
        let associations = FileAssociationsWindow(appURL: appURL, currentDefault: { current }, setDefault: { url, callback in
            requests.append(url); completion = callback
        })
        defer { associations.close() }
        try check(associations.status.stringValue.contains("Preview") && associations.chooseButton.isEnabled,
                  "file associations show the actual current default app")
        associations.chooseDefault(nil)
        associations.chooseDefault(nil)
        try check(requests == [appURL] && !associations.chooseButton.isEnabled, "only an explicit action requests a PDF association change, once")
        current = appURL
        completion?(nil)
        RunLoop.current.run(until: Date(timeIntervalSinceNow: 0.05))
        try check(!associations.chooseButton.isEnabled && associations.status.stringValue.contains("Gravity is the default"),
                  "successful association change refreshes the status")
        current = nil
        associations.refreshStatus()
        associations.chooseDefault(nil)
        completion?(CocoaError(.userCancelled))
        RunLoop.current.run(until: Date(timeIntervalSinceNow: 0.05))
        try check(associations.chooseButton.isEnabled && associations.status.stringValue.contains("not changed"),
                  "cancelled or failed association changes remain retryable")
        associations.chooseDefault(nil)
        completion?(nil)
        RunLoop.current.run(until: Date(timeIntervalSinceNow: 0.05))
        try check(associations.status.stringValue.contains("not changed"), "association result is checked instead of assuming success")
        if let flag = CommandLine.arguments.firstIndex(of: "--ui-snapshots"), flag + 1 < CommandLine.arguments.count {
            let directory = URL(fileURLWithPath: CommandLine.arguments[flag + 1])
            current = appURL
            associations.refreshStatus()
            for (name, window) in [("key-color-settings", dialog.window!), ("file-associations-settings", associations.window!)] {
                window.makeKeyAndOrderFront(nil)
                let root = window.contentView!
                root.layoutSubtreeIfNeeded()
                window.displayIfNeeded()
                guard let bitmap = root.bitmapImageRepForCachingDisplay(in: root.bounds) else { throw PDFError.write }
                root.cacheDisplay(in: root.bounds, to: bitmap)
                try bitmap.representation(using: .png, properties: [:])!.write(to: directory.appendingPathComponent(name + ".png"))
                let capture = Process()
                capture.executableURL = URL(fileURLWithPath: "/usr/sbin/screencapture")
                capture.arguments = ["-x", "-o", "-l", String(window.windowNumber), directory.appendingPathComponent(name + "-window.png").path]
                capture.standardError = FileHandle.nullDevice
                try capture.run()
                capture.waitUntilExit()
                window.orderOut(nil)
            }
        }
    }
}

enum FileCommandTests {
    static func run(check: (Bool, String) throws -> Void) throws {
        let folder = FileManager.default.temporaryDirectory.appendingPathComponent("GravityFileTests-" + UUID().uuidString)
        try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: folder) }
        let image = NSImage(size: NSSize(width: 96, height: 64), flipped: false) { rect in
            NSColor.red.setFill(); rect.fill(); return true
        }
        let png = NSBitmapImageRep(data: image.tiffRepresentation!)!.representation(using: .png, properties: [:])!
        let source = folder.appendingPathComponent("source.png")
        try png.write(to: source)
        let data = try FileCommands.imagePDF(from: [source, source])
        let pdf = PDFDocument(data: data)!
        try check(pdf.pageCount == 2 && (try Data(contentsOf: source)) == png, "image import creates one PDF page per image and preserves originals")
        let document = GravityDocument()
        try document.read(from: data, ofType: "com.adobe.pdf")
        document.updateChangeCount(.changeDone)
        try check(document.fileURL == nil && document.isDocumentEdited, "image-created PDF is unsaved and cannot overwrite a source image")
        let extracted = try document.model.extract(IndexSet(integer: 1))
        let originalURL = folder.appendingPathComponent("Original.pdf")
        try data.write(to: originalURL)
        let selectedURL = folder.appendingPathComponent("selected.pdf")
        try FileCommands.saveSelectedPDF(extracted, to: selectedURL, source: originalURL)
        try check(PDFDocument(url: selectedURL)?.pageCount == 1 && document.model.pdf.pageCount == 2, "saving selected pages preserves the source document")
        let symbolicLink = folder.appendingPathComponent("linked.pdf")
        try FileManager.default.createSymbolicLink(at: symbolicLink, withDestinationURL: originalURL)
        let hardLink = folder.appendingPathComponent("hard-linked.pdf")
        try FileManager.default.linkItem(at: originalURL, to: hardLink)
        var aliases = [originalURL, symbolicLink, hardLink]
        let caseVariant = folder.appendingPathComponent("ORIGINAL.PDF")
        if FileManager.default.fileExists(atPath: caseVariant.path) { aliases.append(caseVariant) }
        for alias in aliases {
            var rejected = false
            do { try FileCommands.saveSelectedPDF(extracted, to: alias, source: originalURL) }
            catch { rejected = true }
            try check(rejected && (try Data(contentsOf: originalURL)) == data && (try Data(contentsOf: alias)) == data,
                      "selected-page saving rejects every alias of the open PDF without changing its bytes")
        }
        let exported = folder.appendingPathComponent("PNG pages")
        try FileCommands.exportImages(pdf, to: exported, format: .png)
        let files = try FileManager.default.contentsOfDirectory(at: exported, includingPropertiesForKeys: nil)
        try check(files.count == 2 && files.allSatisfy { NSImage(contentsOf: $0)?.isValid == true }, "PNG export writes readable images for every page")
        let jpeg = folder.appendingPathComponent("JPEG pages")
        pdf.page(at: 0)?.rotation = 90
        try FileCommands.exportImages(pdf, to: jpeg, format: .jpeg)
        let rotated = NSBitmapImageRep(data: try Data(contentsOf: jpeg.appendingPathComponent("Page-001.jpg")))!
        try check(rotated.pixelsHigh > rotated.pixelsWide, "JPEG export respects page rotation")
        var rejected = false
        do { try FileCommands.exportImages(pdf, to: exported, format: .png) } catch { rejected = true }
        try check(rejected && (try FileManager.default.contentsOfDirectory(atPath: exported.path)).count == 2,
                  "export never overwrites an existing output folder")
        try check(FileCommands.unusedExportFolder(in: folder, name: "PNG pages").lastPathComponent == "PNG pages 2",
                  "repeat exports choose a new folder")
        let fixture = ExportTextFixture(frame: NSRect(x: 0, y: 0, width: 300, height: 200))
        let textPDF = PDFDocument(data: fixture.dataWithPDF(inside: fixture.bounds))!
        try check(FileCommands.text(from: textPDF).contains("Gravity export"), "plain-text export includes selectable page text")
        try check(!(try FileManager.default.contentsOfDirectory(atPath: folder.path)).contains { $0.hasPrefix(".gravity-export-") },
                  "successful and failed exports clean up staging files")
    }
}

private final class ExportTextFixture: NSView {
    override func draw(_ dirtyRect: NSRect) {
        ("Gravity export" as NSString).draw(at: NSPoint(x: 20, y: 100), withAttributes: [.font: NSFont.systemFont(ofSize: 18)])
    }
}
