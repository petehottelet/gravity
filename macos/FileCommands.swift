import AppKit
import PDFKit
import UniformTypeIdentifiers

enum FileCommands {
    static func saveSelectedPDF(_ data: Data, to destination: URL, source: URL?) throws {
        if let source = source, try sameFile(destination, source) {
            throw NSError(domain: "Gravity", code: 1, userInfo: [NSLocalizedDescriptionKey:
                "Choose a different filename to save selected pages without replacing the open PDF."])
        }
        try data.write(to: destination, options: .atomic)
    }

    private static func sameFile(_ first: URL, _ second: URL) throws -> Bool {
        let first = first.resolvingSymlinksInPath().standardizedFileURL
        let second = second.resolvingSymlinksInPath().standardizedFileURL
        if first == second { return true }
        // File identity also covers case variants on case-insensitive volumes
        // and multiple names for the same file. A new destination has no identity.
        do {
            let keys: Set<URLResourceKey> = [.fileResourceIdentifierKey]
            let firstID = try first.resourceValues(forKeys: keys).fileResourceIdentifier as? NSObject
            let secondID = try second.resourceValues(forKeys: keys).fileResourceIdentifier as? NSObject
            return firstID != nil && firstID?.isEqual(secondID) == true
        } catch let error as CocoaError where error.code == .fileReadNoSuchFile || error.code == .fileNoSuchFile {
            return false
        }
    }

    static func imagePDF(from urls: [URL]) throws -> Data {
        guard !urls.isEmpty else { throw PDFError.invalid }
        let pdf = PDFDocument()
        for url in urls {
            guard let image = NSImage(contentsOf: url), image.isValid,
                  let page = PDFPage(image: image) else { throw PDFError.invalid }
            pdf.insert(page, at: pdf.pageCount)
        }
        guard let data = pdf.dataRepresentation() else { throw PDFError.write }
        return data
    }

    static func text(from pdf: PDFDocument) -> String {
        (0..<pdf.pageCount).map { pdf.page(at: $0)?.string ?? "" }.joined(separator: "\n\u{c}\n")
    }

    // Stage an entire export before making it visible. Never overwrite an
    // existing folder or leave a partially exported set among the user's files.
    static func exportImages(_ pdf: PDFDocument, to destination: URL, format: NSBitmapImageRep.FileType) throws {
        let fileManager = FileManager.default
        let staging = destination.deletingLastPathComponent().appendingPathComponent(".gravity-export-" + UUID().uuidString)
        try fileManager.createDirectory(at: staging, withIntermediateDirectories: false)
        defer { try? fileManager.removeItem(at: staging) }
        for index in 0..<pdf.pageCount {
            try autoreleasepool {
                guard let page = pdf.page(at: index) else { throw PDFError.invalid }
                var size = page.bounds(for: .cropBox).size
                if page.rotation % 180 != 0 { size = NSSize(width: size.height, height: size.width) }
                // 144 dpi, with a bounded bitmap for oversized PDF pages.
                guard size.width.isFinite, size.height.isFinite, size.width > 0, size.height > 0 else { throw PDFError.invalid }
                let scale = min(2, 8192 / max(size.width, size.height))
                size = NSSize(width: ceil(size.width * scale), height: ceil(size.height * scale))
                let thumbnail = page.thumbnail(of: size, for: .cropBox)
                guard let bitmap = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: Int(size.width), pixelsHigh: Int(size.height),
                                                    bitsPerSample: 8, samplesPerPixel: 4, hasAlpha: true, isPlanar: false,
                                                    colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 32),
                      let context = NSGraphicsContext(bitmapImageRep: bitmap) else { throw PDFError.write }
                NSGraphicsContext.saveGraphicsState()
                NSGraphicsContext.current = context
                NSColor.white.setFill()
                NSRect(origin: .zero, size: size).fill()
                thumbnail.draw(in: NSRect(origin: .zero, size: size), from: .zero, operation: .sourceOver, fraction: 1)
                NSGraphicsContext.restoreGraphicsState()
                guard let data = bitmap.representation(using: format, properties: [.compressionFactor: 0.92]) else { throw PDFError.write }
                let name = String(format: "Page-%03d", index + 1) + (format == .jpeg ? ".jpg" : ".png")
                try data.write(to: staging.appendingPathComponent(name), options: .atomic)
            }
        }
        try fileManager.moveItem(at: staging, to: destination)
    }

    static func unusedExportFolder(in parent: URL, name: String) -> URL {
        var url = parent.appendingPathComponent(name, isDirectory: true)
        var suffix = 2
        while FileManager.default.fileExists(atPath: url.path) {
            url = parent.appendingPathComponent("\(name) \(suffix)", isDirectory: true)
            suffix += 1
        }
        return url
    }
}

final class ExportOptions {
    let alert = NSAlert()
    let format = NSPopUpButton()
    let scope = NSPopUpButton()
    init(selectedCount: Int, totalCount: Int) {
        alert.messageText = "Export Pages"
        alert.informativeText = "Save pages as images or export their selectable text."
        alert.addButton(withTitle: "Continue…")
        alert.addButton(withTitle: "Cancel")
        let content = NSView(frame: NSRect(x: 0, y: 0, width: 360, height: 88))
        for (title, popup, y) in [("Format", format, CGFloat(48)), ("Pages", scope, CGFloat(8))] {
            let label = NSTextField(labelWithString: title)
            label.frame = NSRect(x: 0, y: y + 4, width: 70, height: 22)
            content.addSubview(label)
            popup.frame = NSRect(x: 80, y: y, width: 280, height: 28)
            popup.setAccessibilityLabel(title)
            content.addSubview(popup)
        }
        format.addItems(withTitles: ["PNG images", "JPEG images", "Plain text (.txt)"])
        scope.addItems(withTitles: ["Selected pages (\(selectedCount))", "All pages (\(totalCount))"])
        alert.accessoryView = content
    }
}
