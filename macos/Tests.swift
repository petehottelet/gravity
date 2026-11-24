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
            let note = PDFAnnotation(bounds: CGRect(x: 350, y: 700, width: 24, height: 24), forType: .text, withProperties: nil)
            page.addAnnotation(note)
            let hidden = PDFAnnotation(bounds: CGRect(x: 350, y: 650, width: 24, height: 24), forType: .widget, withProperties: nil)
            hidden.widgetFieldType = .text
            hidden.fieldName = "Hidden"
            hidden.shouldDisplay = false
            page.addAnnotation(hidden)
            try check(model.widgets(on: 0).count == 2 && PDFModel.isWidget(text) && !PDFModel.isWidget(note),
                      "visible widget discovery excludes notes and hidden fields")
            try model.fill(text, text: "Renée Example")
            try model.fill(checkBox, checked: true)
            try check(text.widgetStringValue == "Renée Example" && checkBox.buttonWidgetState == .onState,
                      "form editing updates both field values in memory")
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
            var originalBytes = try reopened.data()
            // A valid trailing comment distinguishes the input from PDFKit's reserialization.
            originalBytes.append(Data("\n% Original undo fixture\n".utf8))
            try document.read(from: originalBytes, ofType: "com.adobe.pdf")
            guard let undo = document.undoManager else { throw PDFError.invalid }
            // The self-tests run without the app event loop; close each group explicitly.
            undo.groupsByEvent = false
            func edit(_ name: String, operation: () throws -> Void) {
                undo.beginUndoGrouping()
                document.edit(name, operation: operation)
                undo.endUndoGrouping()
            }
            let original = try document.data(ofType: "com.adobe.pdf")
            try check(!document.isDocumentEdited && original == originalBytes, "opened document is clean and preserves unchanged bytes")
            edit("No Change") {}
            try check(!document.isDocumentEdited && !undo.canUndo, "no-op edit stays clean without undo history")
            edit("Same Form Value") { try document.model.fill(document.model.widgets(on: 0)[0], text: "Renée Example") }
            edit("No Rotation") { try document.model.rotate(IndexSet(integer: 0), by: 360) }
            edit("No Move") { try document.model.move(IndexSet(integer: 0), by: 0) }
            edit("Empty Deletion") { try document.model.delete(IndexSet()) }
            try check(!document.isDocumentEdited && !undo.canUndo, "unchanged form and page operations do not create edits")
            edit("Fill Form") { try document.model.fill(document.model.widgets(on: 0)[0], text: "Undo test") }
            try check(document.isDocumentEdited, "form edit marks document modified")
            undo.undo()
            try check(document.model.widgets(on: 0)[0].widgetStringValue == "Renée Example", "undo restores field")
            try check(!document.isDocumentEdited, "undoing the only form edit clears modified state")
            let undoneBytes = try document.data(ofType: "com.adobe.pdf")
            try check(undoneBytes == originalBytes, "undo preserves the original PDF byte for byte")
            undo.redo()
            try check(document.model.widgets(on: 0)[0].widgetStringValue == "Undo test", "redo restores edit")
            try check(document.isDocumentEdited, "redo marks the form modified again")
            undo.undo()
            try check(!document.isDocumentEdited, "redo then undo returns to clean")

            edit("Insert Blank Page") { try document.model.insert(PDFModel().data(), at: 1) }
            edit("Rotate Pages") { try document.model.rotate(IndexSet(integer: 0), by: 90) }
            try check(document.isDocumentEdited && document.model.pdf.pageCount == 2 && document.model.pdf.page(at: 0)?.rotation == 90,
                      "separate page edits change the document")
            undo.undo()
            try check(document.isDocumentEdited && document.model.pdf.pageCount == 2 && document.model.pdf.page(at: 0)?.rotation == 0,
                      "partial undo keeps remaining page edits marked modified")
            undo.undo()
            try check(!document.isDocumentEdited && document.model.pdf.pageCount == 1, "undoing all page edits clears modified state")
            let pageUndoBytes = try document.data(ofType: "com.adobe.pdf")
            try check(pageUndoBytes == originalBytes, "undoing page edits preserves original bytes")
            undo.redo()
            undo.redo()
            try check(document.isDocumentEdited && document.model.pdf.pageCount == 2 && document.model.pdf.page(at: 0)?.rotation == 90,
                      "redo restores both page edits and modified state")
            undo.undo()
            undo.undo()

            undo.beginUndoGrouping()
            document.edit("Insert Blank Page") { try document.model.insert(PDFModel().data(), at: 1) }
            document.edit("Rotate Pages") { try document.model.rotate(IndexSet(integer: 0), by: 90) }
            undo.endUndoGrouping()
            try check(document.isDocumentEdited, "grouped page edits mark the document modified")
            undo.undo()
            try check(!document.isDocumentEdited && document.model.pdf.pageCount == 1 && document.model.pdf.page(at: 0)?.rotation == 0,
                      "one undo clears all edits in a group")
            undo.redo()
            try check(document.isDocumentEdited && document.model.pdf.pageCount == 2 && document.model.pdf.page(at: 0)?.rotation == 90,
                      "redo restores the whole group")

            // Establish the same change-count baseline as a completed save.
            document.updateChangeCount(.changeCleared)
            try check(!document.isDocumentEdited && undo.canUndo, "saved checkpoint is clean and retains undo history")
            undo.undo()
            try check(document.isDocumentEdited && document.model.pdf.pageCount == 1, "undo past saved checkpoint is modified")
            let beforeSaveBytes = try document.data(ofType: "com.adobe.pdf")
            try check(beforeSaveBytes == originalBytes, "undo past saved checkpoint still preserves the restored bytes")
            undo.redo()
            try check(!document.isDocumentEdited, "redo to saved checkpoint clears modified state")
            let atCheckpoint = try PDFModel(data: document.data(ofType: "com.adobe.pdf"))
            try check(atCheckpoint.pdf.pageCount == 2 && atCheckpoint.pdf.page(at: 0)?.rotation == 90,
                      "clean saved checkpoint returns edited content instead of original bytes")
            edit("Fill Form") { try document.model.fill(document.model.widgets(on: 0)[0], text: "After checkpoint") }
            undo.undo()
            try check(!document.isDocumentEdited && document.model.widgets(on: 0)[0].widgetStringValue == "Renée Example",
                      "undoing a later edit returns to the saved checkpoint")
            undo.undo()
            edit("Fill Form") { try document.model.fill(document.model.widgets(on: 0)[0], text: "New branch") }
            try check(document.isDocumentEdited && !undo.canRedo, "new edit after undo stays modified when replacing saved history")
            let branched = try PDFModel(data: document.data(ofType: "com.adobe.pdf"))
            try check(branched.pdf.pageCount == 1 && branched.widgets(on: 0)[0].widgetStringValue == "New branch",
                      "editing a restored snapshot invalidates its preserved bytes")
            document.makeWindowControllers()
            try check(document.windowControllers.count == 1, "document retains its window controller")
            try check(document.windowControllers.first?.document === document, "window controller belongs to the document")
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
