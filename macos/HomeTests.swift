import AppKit
import PDFKit

enum HomeTests {
    static func run(check: (Bool, String) throws -> Void) throws {
        let controller = NSDocumentController.shared
        try check(controller is GravityDocumentController, "native document lifecycle uses Gravity controller")
        let previousDelegate = NSApp.delegate
        let delegate = AppDelegate()
        NSApp.delegate = delegate
        defer { delegate.homeWindow?.close(); NSApp.delegate = previousDelegate }
        delegate.applicationDidFinishLaunching(Notification(name: NSApplication.didFinishLaunchingNotification))
        guard let home = delegate.homeWindow, let window = home.window else { throw PDFError.invalid }
        try check(window.isVisible && controller.documents.isEmpty, "launch without a document shows Home")
        try check(NSApp.modalWindow == nil && !NSApp.windows.contains { $0 is NSOpenPanel && $0.isVisible },
                  "launch does not display an Open dialog")
        try check(!delegate.applicationShouldOpenUntitledFile(NSApp), "launch does not create a blank PDF")
        window.orderOut(nil)
        _ = delegate.applicationShouldHandleReopen(NSApp, hasVisibleWindows: false)
        try check(window.isVisible && delegate.homeWindow === home, "Dock reopen reuses Home without a file picker")

        let document = GravityDocument()
        controller.addDocument(document)
        document.makeWindowControllers()
        document.showWindows()
        try check(!window.isVisible && document.windowControllers.first?.window?.isVisible == true,
                  "opening a PDF hides Home and shows the document")
        let fileLaunch = AppDelegate()
        fileLaunch.applicationDidFinishLaunching(Notification(name: NSApplication.didFinishLaunchingNotification))
        try check(fileLaunch.homeWindow == nil, "launch with an opened PDF does not cover it with Home")
        document.close()
        let closeDeadline = Date().addingTimeInterval(1)
        while (!controller.documents.isEmpty || !window.isVisible) && Date() < closeDeadline {
            RunLoop.main.run(until: Date(timeIntervalSinceNow: 0.01))
        }
        try check(controller.documents.isEmpty && window.isVisible, "closing the last PDF returns to Home")

        let fixtureDirectory = FileManager.default.temporaryDirectory.appendingPathComponent("gravity-home-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: fixtureDirectory, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: fixtureDirectory) }
        let first = fixtureDirectory.appendingPathComponent("First.pdf")
        let second = fixtureDirectory.appendingPathComponent("Second.PDF")
        let model = PDFModel()
        try model.data().write(to: first)
        try model.rotate(IndexSet(integer: 0), by: 90)
        try model.data().write(to: second)
        let original = try Data(contentsOf: first)
        let combined = try HomeWindow.combinedDocument(from: [first, second])
        try check(combined.model.pdf.pageCount == 2 && combined.model.pdf.page(at: 0)?.rotation == 0 &&
                  combined.model.pdf.page(at: 1)?.rotation == 90, "Combine preserves selected file and page order")
        try check(combined.fileURL == nil && combined.isDocumentEdited, "combined PDF is a new unsaved document")
        try check(try Data(contentsOf: first) == original, "Combine leaves original PDFs unchanged")
        let invalid = fixtureDirectory.appendingPathComponent("Invalid.pdf")
        try Data("not a PDF".utf8).write(to: invalid)
        var rejected = false
        do { _ = try HomeWindow.combinedDocument(from: [first, invalid]) } catch { rejected = true }
        try check(rejected && controller.documents.isEmpty, "failed Combine never opens a partial document")

        let pasteboard = NSPasteboard.withUniqueName()
        defer { pasteboard.releaseGlobally() }
        pasteboard.writeObjects([first as NSURL, second as NSURL])
        try check(HomeDropView.pdfURLs(from: pasteboard) == [first, second], "drop accepts multiple PDFs and uppercase extensions")
        pasteboard.clearContents()
        pasteboard.writeObjects([first as NSURL, fixtureDirectory.appendingPathComponent("note.txt") as NSURL])
        try check(HomeDropView.pdfURLs(from: pasteboard).isEmpty, "drop rejects mixed unsupported files")

        var recentURLs: [URL] = []
        var opened: [URL] = []
        let recentHome = HomeWindow(recentDocuments: { recentURLs }, openDocument: { opened.append($0) })
        defer { recentHome.close() }
        guard let recentWindow = recentHome.window, let root = recentWindow.contentView else { throw PDFError.invalid }
        func descendants(_ view: NSView) -> [NSView] { [view] + view.subviews.flatMap(descendants) }
        guard let scroll = descendants(root).first(where: { $0.identifier?.rawValue == "homeRecents" }) as? NSScrollView else { throw PDFError.invalid }
        try check(scroll.isHidden, "empty Home displays the drop hint without an empty recent list")
        recentURLs = [first, second]
        recentHome.refreshRecents()
        guard let rows = scroll.documentView?.subviews.compactMap({ $0 as? NSButton }) else { throw PDFError.invalid }
        try check(!scroll.isHidden && rows.map(\.title) == ["First.pdf", "Second.PDF"], "Home shows native recent PDFs in order")
        rows[1].performClick(nil)
        try check(opened == [second], "clicking a recent file opens its exact URL")
        recentHome.openFiles([first, second])
        try check(opened == [second, first, second], "dropped PDFs all reach the document opener")
        for width in [CGFloat(1100), 560] {
            recentWindow.setContentSize(NSSize(width: width, height: 440))
            root.layoutSubtreeIfNeeded()
            let buttons = descendants(root).compactMap { $0 as? NSButton }.filter { ["homeOpen", "homeCombine"].contains($0.identifier?.rawValue ?? "") }
            try check(buttons.count == 2 && buttons.allSatisfy { root.bounds.contains($0.frame) }, "Home actions fit at width \(Int(width))")
            try check(!buttons[0].frame.intersects(buttons[1].frame), "Home actions do not overlap at width \(Int(width))")
            try check(root.bounds.contains(scroll.frame), "recent list fits at width \(Int(width))")
            let toolbar = descendants(root).compactMap { $0 as? NSButton }.filter { ($0.identifier?.rawValue ?? "").hasPrefix("homeToolbar") }
            try check(toolbar.count == 7 && toolbar.allSatisfy { $0.superview!.bounds.contains($0.frame) }, "Windows-style Home toolbar fits at width \(Int(width))")
            try check(toolbar.filter(\.isEnabled).count == 2, "Home enables Open and menus while document commands wait for a PDF")
            try check(toolbar.enumerated().allSatisfy { index, button in toolbar.dropFirst(index + 1).allSatisfy { !button.frame.intersects($0.frame) } },
                      "Home toolbar buttons do not overlap at width \(Int(width))")
            let commandBar = toolbar[0].superview!
            guard let header = descendants(root).first(where: { $0.identifier?.rawValue == "homeHeader" }),
                  let closeButton = recentWindow.standardWindowButton(.closeButton) else { throw PDFError.invalid }
            let headerFrame = header.convert(header.bounds, to: nil)
            let closeFrame = closeButton.convert(closeButton.bounds, to: nil)
            try check(commandBar.frame.maxY <= 84 && headerFrame.maxY <= closeFrame.minY,
                      "Home keeps its compact header below the native window controls")
            let save = toolbar.first { $0.identifier?.rawValue == "homeToolbarSave" } as! WorkspaceButton
            let saveContent = save.contentFrames
            try check(saveContent.image.minX >= 8 && save.bounds.width - saveContent.title.maxX >= 8 &&
                      saveContent.title.minX - saveContent.image.maxX == 6,
                      "Save icon and label stay grouped with padding inside the border")
            recentHome.toggleApplicationMenus(nil)
            root.layoutSubtreeIfNeeded()
            let menus = descendants(root).compactMap { $0 as? WorkspaceMenus }.first!
            try check(!menus.isHidden && toolbar.allSatisfy(\.isHidden), "Home menus expand in place of the command bar")
            try check(menus.subviews.allSatisfy { menus.bounds.contains($0.frame) }, "expanded Home menus fit at width \(Int(width))")
            for case let button as WorkspaceButton in menus.subviews where button.imagePosition == .imageLeading {
                try check(button.contentFrames.title.minX - button.contentFrames.image.maxX == 6,
                          "menu icon and label use the compact shared gap")
            }
            recentHome.toggleApplicationMenus(nil)
            try check(menus.isHidden && toolbar.allSatisfy { !$0.isHidden }, "closing Home menus restores the command bar")
        }
        recentURLs = Array(repeating: first, count: 30)
        recentHome.refreshRecents()
        root.layoutSubtreeIfNeeded()
        try check(scroll.documentView!.bounds.height > scroll.contentSize.height, "long recent lists scroll without hiding actions")
        let accent = DocumentTheme.keyColor.usingColorSpace(.sRGB)!
        try check(accent.redComponent == CGFloat(11) / 255 && accent.greenComponent == CGFloat(87) / 255 && accent.blueComponent == CGFloat(208) / 255,
                  "default key color is Windows brand blue 0B57D0")
        try check(DocumentTheme.lockup != nil && DocumentTheme.brandMark != nil, "Home and workspace use bundled Gravity artwork")
        guard let logo = DocumentTheme.lockup?.representations.first as? NSBitmapImageRep else { throw PDFError.invalid }
        var logoPixel = [Int](repeating: 0, count: 4)
        logo.getPixel(&logoPixel, atX: 20, y: 20)
        try check(logoPixel == [11, 87, 208, 255], "default Gravity logo tile uses exact 0B57D0 sRGB pixels")
        var colors: [UInt32] = [0x0b57d0, 0x777777, 0x808080, 0xff8800]
        for r: UInt32 in [0, 85, 170, 255] {
            for g: UInt32 in [0, 85, 170, 255] {
                for b: UInt32 in [0, 85, 170, 255] { colors.append(r << 16 | g << 8 | b) }
            }
        }
        for hex in colors {
            let palette = AccentPalette(seed: DocumentTheme.color(hex))
            for (name, background) in [("normal", palette.action), ("hover", palette.actionHover), ("pressed", palette.actionPressed)] {
                try check(AccentPalette.contrast(AccentPalette.text(on: background), background) >= 4.5,
                          "adaptive button text meets 4.5:1 contrast for \(String(hex, radix: 16)), \(name)")
                try check(AccentPalette.contrast(background, DocumentTheme.chrome) >= 3 ||
                          AccentPalette.contrast(palette.foreground, DocumentTheme.chrome) >= 3,
                          "button fill or outline stays visible for \(String(hex, radix: 16)), \(name)")
            }
            let surfaces: [NSColor] = [.white, DocumentTheme.chrome, DocumentTheme.canvas, palette.hover, palette.pressed, palette.navigation, palette.selection]
            try check(surfaces.allSatisfy {
                AccentPalette.contrast(palette.foreground, $0) >= 4.5 &&
                AccentPalette.contrast(DocumentTheme.ink, $0) >= 4.5 &&
                AccentPalette.contrast(DocumentTheme.muted, $0) >= 4.5
            }, "colored text stays readable on all surfaces for \(String(hex, radix: 16))")
        }
        let red = AccentPalette(seed: DocumentTheme.color(0xff0000))
        let redFill = red.action.usingColorSpace(.sRGB)!
        try check(redFill.redComponent > 0.9 && redFill.redComponent < 1 && redFill.greenComponent == 0 && redFill.blueComponent == 0,
                  "red buttons remain a close, slightly darker shade of the logo")
        try check(AccentPalette.text(on: red.action) == .white, "red action keeps readable white text")
        let yellow = AccentPalette(seed: DocumentTheme.color(0xffff00))
        let yellowFill = yellow.action.usingColorSpace(.sRGB)!
        try check(yellowFill.redComponent > 0.9 && yellowFill.greenComponent > 0.9 && yellowFill.blueComponent == 0,
                  "bright yellow remains yellow instead of becoming a dark contrast color")
        try check([yellow.action, yellow.actionHover, yellow.actionPressed].allSatisfy { AccentPalette.text(on: $0) == .black },
                  "yellow actions use black text in every state")
        for title in ["File", "Edit", "View", "Pages", "Window", "Gravity"] {
            try check(NSApp.mainMenu?.items.contains(where: { $0.title == title && $0.submenu != nil }) == true,
                      "in-window \(title) menu has real native commands")
        }

        if let flag = CommandLine.arguments.firstIndex(of: "--ui-snapshots"), flag + 1 < CommandLine.arguments.count {
            let directory = URL(fileURLWithPath: CommandLine.arguments[flag + 1], isDirectory: true)
            try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
            func capture(_ name: String) throws {
                root.layoutSubtreeIfNeeded()
                root.displayIfNeeded()
                // Include the native title bar so excess top spacing and overlap
                // with the traffic-light controls are visible during visual QA.
                let surface = root.superview ?? root
                surface.layoutSubtreeIfNeeded()
                guard let bitmap = surface.bitmapImageRepForCachingDisplay(in: surface.bounds) else { throw PDFError.write }
                surface.cacheDisplay(in: surface.bounds, to: bitmap)
                guard let png = bitmap.representation(using: .png, properties: [:]) else { throw PDFError.write }
                try png.write(to: directory.appendingPathComponent(name + ".png"))
            }
            recentWindow.setContentSize(NSSize(width: 1100, height: 760))
            recentURLs = []
            recentHome.refreshRecents()
            try capture("home")
            recentHome.toggleApplicationMenus(nil)
            try capture("home-menus")
            recentHome.toggleApplicationMenus(nil)
            recentURLs = [first, second]
            recentHome.refreshRecents()
            try capture("home-recent")
            recentWindow.setContentSize(NSSize(width: 560, height: 440))
            try capture("home-narrow")
        }
    }
}
