import AppKit

final class AppDelegate: NSObject, NSApplicationDelegate {
    private(set) var homeWindow: HomeWindow?
    private var terminating = false
    private var keyColorWindow: KeyColorWindow?
    private var fileAssociationsWindow: FileAssociationsWindow?
    private var appearanceObserver: NSObjectProtocol?

    func applicationShouldOpenUntitledFile(_ sender: NSApplication) -> Bool { false }

    func applicationDidFinishLaunching(_ notification: Notification) {
        if !CommandLine.arguments.contains("--self-test") {
            AppAppearance.shared.updateDockIcon()
            appearanceObserver = NotificationCenter.default.addObserver(forName: AppAppearance.didChange, object: AppAppearance.shared, queue: .main) { _ in
                AppAppearance.shared.updateDockIcon()
            }
        }
        if #available(macOS 14, *) { NSApp.activate() }
        else { NSApp.activate(ignoringOtherApps: true) }
        showHomeIfNeeded()
    }

    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool {
        if !flag { showHome(nil) }
        return true
    }

    @objc func showHome(_ sender: Any?) {
        guard !terminating else { return }
        if homeWindow == nil { homeWindow = HomeWindow() }
        homeWindow?.showWindow(sender)
    }

    @objc func combinePDFs(_ sender: Any?) {
        showHome(sender)
        homeWindow?.combinePDFs(sender)
    }

    @objc func createPDFFromImages(_ sender: Any?) {
        let panel = NSOpenPanel()
        panel.allowedContentTypes = [.png, .jpeg, .tiff, .bmp, .heic]
        panel.allowsMultipleSelection = true
        panel.canChooseDirectories = false
        panel.prompt = "Create PDF"
        panel.message = "Choose images to combine in filename order. Reorder pages in Organize before saving."
        panel.begin { response in
            guard response == .OK else { return }
            do {
                let urls = panel.urls.sorted { $0.lastPathComponent.localizedStandardCompare($1.lastPathComponent) == .orderedAscending }
                let document = GravityDocument()
                try document.read(from: FileCommands.imagePDF(from: urls), ofType: "com.adobe.pdf")
                document.updateChangeCount(.changeDone)
                NSDocumentController.shared.addDocument(document)
                document.makeWindowControllers()
                document.showWindows()
                (document.windowControllers.first as? DocumentWindow)?.showOrganize(nil)
            } catch { NSApp.presentError(error) }
        }
    }

    func showHomeIfNeeded() {
        if NSDocumentController.shared.documents.isEmpty { showHome(nil) }
    }

    func documentOpened() { homeWindow?.window?.orderOut(nil) }
    func applicationWillTerminate(_ notification: Notification) { terminating = true }
    deinit { if let appearanceObserver = appearanceObserver { NotificationCenter.default.removeObserver(appearanceObserver) } }

    @objc func chooseKeyColor(_ sender: Any?) {
        if keyColorWindow == nil { keyColorWindow = KeyColorWindow() }
        keyColorWindow?.showWindow(sender)
    }

    @objc func chooseFileAssociations(_ sender: Any?) {
        if fileAssociationsWindow == nil { fileAssociationsWindow = FileAssociationsWindow() }
        fileAssociationsWindow?.showWindow(sender)
    }
}

final class GravityDocumentController: NSDocumentController {
    override func addDocument(_ document: NSDocument) {
        super.addDocument(document)
        (NSApp.delegate as? AppDelegate)?.documentOpened()
    }

    override func removeDocument(_ document: NSDocument) {
        super.removeDocument(document)
        DispatchQueue.main.async { (NSApp.delegate as? AppDelegate)?.showHomeIfNeeded() }
    }
}

@main
enum GravityApp {
    static func main() {
        let app = NSApplication.shared
        app.setActivationPolicy(.regular)
        let documents = GravityDocumentController()
        let delegate = AppDelegate()
        app.delegate = delegate
        buildMenus()
        if CommandLine.arguments.contains("--self-test") {
            exit(MacTests.run())
        }
        withExtendedLifetime((delegate, documents)) { app.run() }
    }

    private static func buildMenus() {
        let main = NSMenu()
        func menu(_ title: String) -> NSMenu {
            let item = NSMenuItem(title: title, action: nil, keyEquivalent: "")
            let submenu = NSMenu(title: title)
            item.submenu = submenu
            main.addItem(item)
            return submenu
        }
        func add(_ menu: NSMenu, _ title: String, _ selector: Selector, _ key: String = "", _ modifiers: NSEvent.ModifierFlags = .command) {
            let item = NSMenuItem(title: title, action: selector, keyEquivalent: key)
            item.keyEquivalentModifierMask = modifiers
            menu.addItem(item)
        }
        let app = menu("Gravity")
        add(app, "About Gravity", #selector(NSApplication.orderFrontStandardAboutPanel(_:)))
        app.addItem(.separator())
        add(app, "Hide Gravity", #selector(NSApplication.hide(_:)), "h")
        add(app, "Quit Gravity", #selector(NSApplication.terminate(_:)), "q")
        let file = menu("File")
        add(file, "New", #selector(NSDocumentController.newDocument(_:)), "n")
        add(file, "Open…", #selector(NSDocumentController.openDocument(_:)), "o")
        let home = NSMenuItem(title: "Home", action: #selector(AppDelegate.showHome(_:)), keyEquivalent: "h")
        home.keyEquivalentModifierMask = [.command, .shift]
        home.target = NSApp.delegate
        file.addItem(home)
        let combine = NSMenuItem(title: "Combine PDFs…", action: #selector(AppDelegate.combinePDFs(_:)), keyEquivalent: "")
        combine.target = NSApp.delegate
        file.addItem(combine)
        let images = NSMenuItem(title: "Create PDF from Images…", action: #selector(AppDelegate.createPDFFromImages(_:)), keyEquivalent: "")
        images.target = NSApp.delegate
        file.addItem(images)
        file.addItem(.separator())
        add(file, "Close", #selector(NSWindow.performClose(_:)), "w")
        add(file, "Save", #selector(NSDocument.save(_:)), "s")
        add(file, "Save As…", #selector(NSDocument.saveAs(_:)), "s", [.command, .shift])
        add(file, "Save Selected Pages As…", #selector(DocumentWindow.saveSelectedPages(_:)))
        add(file, "Export Pages…", #selector(DocumentWindow.exportPages(_:)))
        add(file, "Print…", #selector(NSDocument.printDocument(_:)), "p")
        file.addItem(.separator())
        for (title, action) in [("App Key Color…", #selector(AppDelegate.chooseKeyColor(_:))),
                                ("File Associations…", #selector(AppDelegate.chooseFileAssociations(_:)))] {
            let item = NSMenuItem(title: title, action: action, keyEquivalent: "")
            item.target = NSApp.delegate
            file.addItem(item)
        }
        file.addItem(.separator())
        add(file, "Quit Gravity", #selector(NSApplication.terminate(_:)))
        let edit = menu("Edit")
        add(edit, "Undo", Selector(("undo:")), "z")
        add(edit, "Redo", Selector(("redo:")), "z", [.command, .shift])
        edit.addItem(.separator())
        add(edit, "Cut", #selector(NSText.cut(_:)), "x")
        add(edit, "Copy", #selector(NSText.copy(_:)), "c")
        add(edit, "Paste", #selector(NSText.paste(_:)), "v")
        add(edit, "Select All", #selector(NSText.selectAll(_:)), "a")
        edit.addItem(.separator())
        add(edit, "Find…", #selector(DocumentWindow.find(_:)), "f")
        add(edit, "Find Next", #selector(DocumentWindow.findNext(_:)), "g")
        add(edit, "Fill Form…", #selector(DocumentWindow.fillForm(_:)), "f", [.command, .shift])
        add(edit, "Add Text", #selector(DocumentWindow.addText(_:)), "t", [.command, .shift])
        let view = menu("View")
        add(view, "Read", #selector(DocumentWindow.showRead(_:)), "1")
        add(view, "Organize", #selector(DocumentWindow.showOrganize(_:)), "2")
        add(view, "Show Sidebar", #selector(DocumentWindow.toggleSidebar(_:)), "s", [.command, .control])
        add(view, "Outline", #selector(DocumentWindow.showOutline(_:)))
        add(view, "Page Thumbnails", #selector(DocumentWindow.showPages(_:)))
        view.addItem(.separator())
        add(view, "Previous Page", #selector(DocumentWindow.previousPage(_:)), "\u{F72C}", [])
        add(view, "Next Page", #selector(DocumentWindow.nextPage(_:)), "\u{F72D}", [])
        add(view, "Zoom In", #selector(DocumentWindow.zoomIn(_:)), "+")
        add(view, "Zoom Out", #selector(DocumentWindow.zoomOut(_:)), "-")
        add(view, "Fit Page", #selector(DocumentWindow.fitPage(_:)), "0")
        add(view, "Enter Full Screen", #selector(NSWindow.toggleFullScreen(_:)), "f", [.command, .control])
        let pages = menu("Pages")
        add(pages, "Select All Pages", #selector(DocumentWindow.selectAllPages(_:)), "a", [.command, .shift])
        pages.addItem(.separator())
        add(pages, "Cut Pages", #selector(DocumentWindow.cutPages(_:)), "x", [.command, .shift])
        add(pages, "Copy Pages", #selector(DocumentWindow.copyPages(_:)), "c", [.command, .shift])
        add(pages, "Paste Pages", #selector(DocumentWindow.pastePages(_:)), "v", [.command, .shift])
        add(pages, "Duplicate Pages", #selector(DocumentWindow.duplicatePages(_:)), "d")
        add(pages, "Delete Pages", #selector(DocumentWindow.deletePages(_:)))
        pages.addItem(.separator())
        add(pages, "Rotate Left", #selector(DocumentWindow.rotateLeft(_:)), "[")
        add(pages, "Rotate Right", #selector(DocumentWindow.rotateRight(_:)), "]")
        add(pages, "Move Earlier", #selector(DocumentWindow.moveEarlier(_:)), "\u{F700}", [.option])
        add(pages, "Move Later", #selector(DocumentWindow.moveLater(_:)), "\u{F701}", [.option])
        pages.addItem(.separator())
        add(pages, "Insert Blank Page", #selector(DocumentWindow.insertBlank(_:)))
        add(pages, "Insert PDFs…", #selector(DocumentWindow.insertFiles(_:)))
        add(pages, "Extract to New Document", #selector(DocumentWindow.extractPages(_:)))
        let windows = menu("Window")
        add(windows, "Minimize", #selector(NSWindow.performMiniaturize(_:)), "m")
        add(windows, "Bring All to Front", #selector(NSApplication.arrangeInFront(_:)))
        NSApp.windowsMenu = windows
        NSApp.mainMenu = main
    }
}
