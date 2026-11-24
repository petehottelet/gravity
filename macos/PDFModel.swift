import AppKit
import PDFKit

enum PDFError: LocalizedError {
    case invalid, locked, write, lastPage, readOnly, unsupported
    var errorDescription: String? {
        switch self {
        case .invalid: return "The file is not a readable PDF."
        case .locked: return "The PDF password is missing or incorrect."
        case .write: return "The PDF could not be saved."
        case .lastPage: return "Keep at least one page in the document."
        case .readOnly: return "This document or field does not allow editing."
        case .unsupported: return "This form field type is not supported."
        }
    }
}

final class PDFModel {
    private(set) var pdf: PDFDocument
    // PDFKit can serialize unchanged content to different bytes. Track actual edits instead.
    private(set) var revision: UInt64 = 0

    init(data: Data, password: String? = nil) throws {
        guard let document = PDFDocument(data: data) else { throw PDFError.invalid }
        if document.isLocked {
            guard let password = password, document.unlock(withPassword: password) else { throw PDFError.locked }
        }
        guard document.pageCount > 0 else { throw PDFError.invalid }
        pdf = document
    }

    init() {
        pdf = PDFDocument()
        let page = PDFPage()
        page.setBounds(CGRect(x: 0, y: 0, width: 612, height: 792), for: .mediaBox)
        pdf.insert(page, at: 0)
    }

    func data() throws -> Data {
        guard let data = pdf.dataRepresentation() else { throw PDFError.write }
        return data
    }

    func requirePageEditing() throws {
        guard pdf.allowsDocumentAssembly else { throw PDFError.readOnly }
    }

    func rotate(_ indices: IndexSet, by degrees: Int) throws {
        try requirePageEditing()
        for index in indices {
            guard let page = pdf.page(at: index) else { continue }
            let rotation = ((page.rotation + degrees) % 360 + 360) % 360
            if page.rotation != rotation {
                page.rotation = rotation
                revision &+= 1
            }
        }
    }

    func delete(_ indices: IndexSet) throws {
        try requirePageEditing()
        guard !indices.isEmpty else { return }
        guard indices.allSatisfy({ $0 >= 0 && $0 < pdf.pageCount }) else { throw PDFError.invalid }
        guard indices.count < pdf.pageCount else { throw PDFError.lastPage }
        for index in indices.reversed() { pdf.removePage(at: index) }
        revision &+= 1
    }

    func move(_ indices: IndexSet, by offset: Int) throws {
        try requirePageEditing()
        guard offset != 0 else { return }
        guard let first = indices.first, let last = indices.last,
              first + offset >= 0, last + offset < pdf.pageCount else { return }
        let order = offset > 0 ? Array(indices.reversed()) : Array(indices)
        for index in order {
            guard let page = pdf.page(at: index) else { continue }
            pdf.removePage(at: index)
            pdf.insert(page, at: index + offset)
        }
        revision &+= 1
    }

    func extract(_ indices: IndexSet) throws -> Data {
        guard pdf.allowsCopying else { throw PDFError.readOnly }
        let result = PDFDocument()
        for index in indices {
            guard let page = pdf.page(at: index)?.copy() as? PDFPage else { throw PDFError.invalid }
            result.insert(page, at: result.pageCount)
        }
        guard let data = result.dataRepresentation(), result.pageCount > 0 else { throw PDFError.write }
        return data
    }

    func insert(_ data: Data, at index: Int) throws {
        try requirePageEditing()
        guard index >= 0 && index <= pdf.pageCount else { throw PDFError.invalid }
        let source = try PDFModel(data: data)
        guard source.pdf.allowsCopying else { throw PDFError.readOnly }
        for offset in 0..<source.pdf.pageCount {
            guard let page = source.pdf.page(at: offset)?.copy() as? PDFPage else { throw PDFError.invalid }
            pdf.insert(page, at: index + offset)
        }
        revision &+= 1
    }

    static func isWidget(_ annotation: PDFAnnotation) -> Bool {
        // PDFAnnotation.type omits the PDF name's leading slash, unlike subtype raw values.
        return annotation.type?.trimmingCharacters(in: CharacterSet(charactersIn: "/")) == "Widget"
    }

    func widgets(on page: Int) -> [PDFAnnotation] {
        return pdf.page(at: page)?.annotations.filter { Self.isWidget($0) && $0.shouldDisplay } ?? []
    }

    func fill(_ field: PDFAnnotation, text: String? = nil, checked: Bool? = nil) throws {
        guard pdf.allowsFormFieldEntry, !field.isReadOnly else { throw PDFError.readOnly }
        let peers = (0..<pdf.pageCount).flatMap { widgets(on: $0) }.filter {
            $0 === field || (field.fieldName != nil && $0.fieldName == field.fieldName)
        }
        switch field.widgetFieldType {
        case .text, .choice:
            let value = text ?? ""
            if field.widgetFieldType == .text && field.maximumLength > 0 && value.count > field.maximumLength {
                throw NSError(domain: "Gravity", code: 1, userInfo: [NSLocalizedDescriptionKey: "This field allows at most \(field.maximumLength) characters."])
            }
            for peer in peers where (peer.widgetStringValue ?? "") != value {
                peer.widgetStringValue = value
                revision &+= 1
            }
        case .button:
            guard field.widgetControlType != .pushButtonControl else { throw PDFError.unsupported }
            let on = checked ?? false
            for peer in peers {
                let isOn = field.widgetControlType == .radioButtonControl ? on && peer === field : on
                let state: PDFWidgetCellState = isOn ? .onState : .offState
                if peer.buttonWidgetState != state {
                    peer.buttonWidgetState = state
                    revision &+= 1
                }
            }
        default: throw PDFError.unsupported
        }
    }
}
