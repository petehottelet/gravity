import AppKit
import PDFKit

@objc(GravityDocument)
final class GravityDocument: NSDocument {
    var model = PDFModel()
    private var preservedData: Data?
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
        preservedData = data
    }

    override func data(ofType typeName: String) throws -> Data {
        // These bytes describe the current snapshot, even after undoing a saved edit.
        // Preserve them byte for byte, including signatures, until the next edit.
        if let preservedData = preservedData { return preservedData }
        return try model.data()
    }

    override func makeWindowControllers() {
        addWindowController(DocumentWindow(document: self))
    }

    func edit(_ name: String, operation: () throws -> Void) {
        do {
            let before = try model.data()
            let snapshot = preservedData ?? before
            do { try operation() }
            catch {
                model = try PDFModel(data: snapshot, password: password)
                refresh()
                throw error
            }
            if try model.data() == before { return }
            preservedData = nil
            // NSDocument tracks change counts from its undo manager's groups.
            undoManager?.registerUndo(withTarget: self) { $0.restore(snapshot, name: name) }
            undoManager?.setActionName(name)
            refresh()
        } catch { presentError(error) }
    }

    private func restore(_ data: Data, name: String) {
        do {
            let inverse = try preservedData ?? model.data()
            model = try PDFModel(data: data, password: password)
            preservedData = data
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
