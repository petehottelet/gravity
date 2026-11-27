import AppKit
import PDFKit

enum EditingTests {
    static func run(check: (Bool, String) throws -> Void) throws {
        let model = PDFModel()
        for _ in 1..<6 { try model.insert(PDFModel().data(), at: model.pdf.pageCount) }
        let original = (0..<6).map { model.pdf.page(at: $0)! }
        let bookmark = PDFOutline()
        bookmark.destination = PDFDestination(page: original[1], at: .zero)
        let root = PDFOutline()
        root.insertChild(bookmark, at: 0)
        model.pdf.outlineRoot = root
        let form = PDFAnnotation(bounds: NSRect(x: 20, y: 20, width: 80, height: 20), forType: .widget, withProperties: nil)
        form.widgetFieldType = .text
        form.fieldName = "Moved field"
        form.widgetStringValue = "Retained"
        original[1].addAnnotation(form)
        let selection = try model.reorder(IndexSet([1, 3]), before: 6)
        try check(selection == IndexSet([4, 5]) && [0, 2, 4, 5, 1, 3].enumerated().allSatisfy { model.pdf.page(at: $0.offset) === original[$0.element] },
                  "nonadjacent dragged pages retain their order at the end")
        try check(bookmark.destination?.page === original[1] && model.widgets(on: 4).first?.widgetStringValue == "Retained",
                  "reordering retains bookmark targets and form fields")
        try model.reorder(IndexSet([4, 5]), before: 0)
        try check(model.pdf.page(at: 0) === original[1] && model.pdf.page(at: 1) === original[3], "dragging a group to the beginning")
        let revision = model.revision
        try model.reorder(IndexSet([0, 1]), before: 1)
        try model.reorder(IndexSet(integersIn: 0..<6), before: 6)
        try check(model.revision == revision, "dropping within a contiguous selection or moving all pages is a no-op")
        var rejected = false
        do { try model.reorder(IndexSet(integer: 6), before: 0) } catch PDFError.invalid { rejected = true }
        try check(rejected && model.revision == revision && model.pdf.pageCount == 6, "invalid page drags leave the document intact")

        let document = GravityDocument()
        try document.read(from: PDFModel().data(), ofType: "com.adobe.pdf")
        document.undoManager?.groupsByEvent = false
        let before = try document.data(ofType: "com.adobe.pdf")
        let font = NSFont(name: "Helvetica", size: 18)!
        let bounds = NSRect(x: 50, y: 600, width: 300, height: 100)
        var annotation: PDFAnnotation?
        document.edit("Add Text") {
            annotation = try document.model.setText(on: 0, text: "New text — Renée\nSecond line", font: font, bounds: bounds)
        }
        try check(annotation != nil && document.isDocumentEdited && document.undoManager?.canUndo == true, "adding multiline text is undoable and marks the PDF modified")
        let reopened = try PDFModel(data: document.data(ofType: "com.adobe.pdf"))
        let savedText = reopened.pdf.page(at: 0)!.annotations.first(where: PDFModel.isFreeText)
        try check(savedText?.contents == "New text — Renée\nSecond line", "saved text retains Unicode and line breaks")
        try check(savedText?.bounds == bounds, "saved text retains its position and dimensions")
        try check(savedText?.font?.pointSize == 18, "saved text retains font size")
        try check(savedText?.shouldPrint == true, "saved text remains printable")
        document.undoManager?.undo()
        try check(!document.isDocumentEdited && document.model.pdf.page(at: 0)!.annotations.isEmpty, "undo removes added text and returns to clean")
        let restored = try document.data(ofType: "com.adobe.pdf")
        try check(restored == before, "text undo restores original PDF bytes")
        document.undoManager?.redo()
        let restoredText = document.model.pdf.page(at: 0)!.annotations.first(where: PDFModel.isFreeText)!
        document.edit("Edit Text") {
            try document.model.setText(on: 0, annotation: restoredText, text: "Revised", font: font,
                                       bounds: bounds.offsetBy(dx: 10, dy: -20))
        }
        try check(restoredText.contents == "Revised" && restoredText.bounds.minX == 60, "existing added text can be edited and repositioned")
        document.edit("Delete Text") {
            try document.model.setText(on: 0, annotation: restoredText, text: "", font: font, bounds: bounds)
        }
        try check(document.model.pdf.page(at: 0)!.annotations.isEmpty, "clearing text removes its annotation")
        document.undoManager?.undo()
        try check(document.model.pdf.page(at: 0)!.annotations.first(where: PDFModel.isFreeText)?.contents == "Revised", "deleting text supports undo")
        rejected = false
        do { try reopened.setText(on: 0, text: "Outside", font: font, bounds: bounds.offsetBy(dx: 700, dy: 0)) }
        catch PDFError.invalid { rejected = true }
        try check(rejected && reopened.pdf.page(at: 0)!.annotations.count == 1, "text cannot be placed outside the page")
        savedText!.isReadOnly = true
        rejected = false
        do { try reopened.setText(on: 0, annotation: savedText, text: "Forbidden", font: font, bounds: bounds) }
        catch PDFError.readOnly { rejected = true }
        try check(rejected && savedText?.contents == "New text — Renée\nSecond line", "read-only text annotations remain unchanged")
        for rotation in [0, 90, 180, 270] {
            let page = PDFPage()
            page.setBounds(NSRect(x: 20, y: 30, width: 420, height: 560), for: .mediaBox)
            page.rotation = rotation
            let editor = TextEditor(page: page, point: NSPoint(x: 435, y: 35), annotation: nil)
            try check(page.bounds(for: .cropBox).contains(editor.textBounds), "text placement stays in offset page bounds at rotation \(rotation)")
        }
        for color in [DocumentTheme.chrome, DocumentTheme.canvas] {
            let rgb = color.usingColorSpace(.sRGB)!
            try check(rgb.redComponent == rgb.greenComponent && rgb.greenComponent == rgb.blueComponent, "workspace base surfaces use neutral grays")
        }
    }
}
