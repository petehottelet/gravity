import AppKit
import PDFKit

enum MacTests {
    static func run() -> Int32 {
        var count = 0
        func check(_ condition: @autoclosure () -> Bool, _ name: String) throws {
            guard condition() else { throw NSError(domain: "GravityTests", code: 1, userInfo: [NSLocalizedDescriptionKey: name]) }
            count += 1
            print("ok: \(name)")
        }
        do {
            let model = PDFModel()
            let page = model.pdf.page(at: 0)!
            let text = PDFAnnotation(bounds: CGRect(x: 50, y: 700, width: 250, height: 30), forType: .widget, withProperties: nil)
            text.widgetFieldType = .text
            text.fieldName = "Name"
            text.widgetStringValue = "Initial"
            text.font = .systemFont(ofSize: 12)
            page.addAnnotation(text)
            let checkBox = PDFAnnotation(bounds: CGRect(x: 50, y: 650, width: 24, height: 24), forType: .widget, withProperties: nil)
            checkBox.widgetFieldType = .button
            checkBox.widgetControlType = .checkBoxControl
            checkBox.fieldName = "Agree"
            checkBox.buttonWidgetState = .offState
            page.addAnnotation(checkBox)
            try model.fill(text, text: "Renée Example")
            try model.fill(checkBox, checked: true)
            let reopened = try PDFModel(data: model.data())
            try check(reopened.widgets(on: 0).first { $0.fieldName == "Name" }?.widgetStringValue == "Renée Example", "text survives serialization")
            try check(reopened.widgets(on: 0).first { $0.fieldName == "Agree" }?.buttonWidgetState == .onState, "checkbox survives serialization")
            text.isReadOnly = true
            var rejected = false
            do { try model.fill(text, text: "Changed") } catch PDFError.readOnly { rejected = true }
            try check(rejected && text.widgetStringValue == "Renée Example", "read-only field is unchanged")
            try model.insert(PDFModel().data(), at: 1)
            try model.rotate(IndexSet(integer: 0), by: 90)
            try model.move(IndexSet(integer: 0), by: 1)
            try check(model.pdf.pageCount == 2 && model.pdf.page(at: 1)?.rotation == 90, "page movement and rotation")
            let extracted = try PDFModel(data: model.extract(IndexSet(integer: 1)))
            try check(extracted.widgets(on: 0).contains { $0.widgetStringValue == "Renée Example" }, "extraction preserves form value")
            try model.delete(IndexSet(integer: 0))
            rejected = false
            do { try model.delete(IndexSet(integer: 0)) } catch PDFError.lastPage { rejected = true }
            try check(rejected && model.pdf.pageCount == 1, "last page cannot be deleted")
            let document = GravityDocument()
            let originalBytes = try reopened.data()
            try document.read(from: originalBytes, ofType: "com.adobe.pdf")
            let original = try document.data(ofType: "com.adobe.pdf")
            try check(original == originalBytes, "unchanged bytes are preserved")
            document.undoManager?.beginUndoGrouping()
            document.edit("Fill Form") { try document.model.fill(document.model.widgets(on: 0)[0], text: "Undo test") }
            document.undoManager?.endUndoGrouping()
            try check(document.isDocumentEdited, "form edit marks document modified")
            document.undoManager?.undo()
            try check(document.model.widgets(on: 0)[0].widgetStringValue == "Renée Example", "undo restores field")
            document.undoManager?.redo()
            try check(document.model.widgets(on: 0)[0].widgetStringValue == "Undo test", "redo restores edit")
            document.makeWindowControllers()
            try check(document.windowControllers.first?.window != nil, "native window is constructed")
            document.windowControllers.forEach { $0.close() }
            print("All \(count) macOS checks passed.")
            return 0
        } catch {
            // Test names and generic errors only; never print fixture contents or local paths.
            print("FAILED: \(error.localizedDescription)")
            return 1
        }
    }
}
