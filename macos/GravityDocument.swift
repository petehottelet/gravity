import AppKit
import PDFKit

@objc(GravityDocument)
final class GravityDocument: NSDocument {
    var model = PDFModel()
    private var originalData: Data?
    private var password: String?

    override init() {
        super.init()
        fileType = "com.adobe.pdf"
        hasUndoManager = true
        undoManager?.levelsOfUndo = 100
    }

    override class var autosavesInPlace: Bool { false }

    override func read(from data: Data, ofType typeName: String) throws {
        do { model = try PDFModel(data: data) }
        catch PDFError.locked {
            let alert = NSAlert()
            alert.messageText = "Password required"
            alert.informativeText = "Enter the password to open this PDF."
            let input = NSSecureTextField(frame: NSRect(x: 0, y: 0, width: 300, height: 24))
            alert.accessoryView = input
            alert.addButton(withTitle: "Open")
            alert.addButton(withTitle: "Cancel")
            alert.window.initialFirstResponder = input
            guard alert.runModal() == .alertFirstButtonReturn else { throw CocoaError(.userCancelled) }
            password = input.stringValue
            model = try PDFModel(data: data, password: password)
        }
        originalData = data
    }

    override func data(ofType typeName: String) throws -> Data {
        // Preserve an unchanged file byte for byte, including its signatures.
        if !isDocumentEdited, let originalData = originalData { return originalData }
        return try model.data()
    }

    override func makeWindowControllers() {
        addWindowController(DocumentWindow(document: self))
    }

    func edit(_ name: String, operation: () throws -> Void) {
        do {
            let before = try model.data()
            do { try operation() }
            catch {
                model = try PDFModel(data: before, password: password)
                refresh()
                throw error
            }
            if try model.data() == before { return }
            originalData = nil
            undoManager?.registerUndo(withTarget: self) { $0.restore(before, name: name) }
            undoManager?.setActionName(name)
            updateChangeCount(.changeDone)
            refresh()
        } catch { presentError(error) }
    }

    private func restore(_ data: Data, name: String) {
        do {
            let inverse = try model.data()
            model = try PDFModel(data: data, password: password)
            undoManager?.registerUndo(withTarget: self) { $0.restore(inverse, name: name) }
            undoManager?.setActionName(name)
            refresh()
        } catch { presentError(error) }
    }

    private func refresh() {
        for case let controller as DocumentWindow in windowControllers { controller.refresh() }
    }

    override func printOperation(withSettings printSettings: [NSPrintInfo.AttributeKey: Any]) throws -> NSPrintOperation {
        guard model.pdf.allowsPrinting else { throw PDFError.readOnly }
        guard let operation = model.pdf.printOperation(for: printInfo, scalingMode: .pageScaleToFit, autoRotate: true) else { throw PDFError.write }
        return operation
    }
}
