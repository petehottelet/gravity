import AppKit

// Build with macos/AppIcon.swift so Finder and the live Dock icon share padding.
@main
enum MakeMacIcon {
    static func main() throws {
        guard CommandLine.arguments.count == 3,
              let source = NSImage(contentsOfFile: CommandLine.arguments[1]) else {
            throw NSError(domain: "MakeMacIcon", code: 1, userInfo: [
                NSLocalizedDescriptionKey: "Usage: MakeMacIcon source.png output.iconset"
            ])
        }
        let folder = URL(fileURLWithPath: CommandLine.arguments[2], isDirectory: true)
        try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        for size in [16, 32, 128, 256, 512] {
            for scale in [1, 2] {
                let pixels = size * scale
                guard let bitmap = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: pixels, pixelsHigh: pixels,
                                                    bitsPerSample: 8, samplesPerPixel: 4, hasAlpha: true,
                                                    isPlanar: false, colorSpaceName: .deviceRGB,
                                                    bytesPerRow: pixels * 4, bitsPerPixel: 32),
                      let context = NSGraphicsContext(bitmapImageRep: bitmap) else {
                    throw NSError(domain: "MakeMacIcon", code: 2)
                }
                NSGraphicsContext.saveGraphicsState()
                NSGraphicsContext.current = context
                let bounds = NSRect(x: 0, y: 0, width: pixels, height: pixels)
                NSColor.clear.setFill()
                bounds.fill(using: .copy)
                AppIcon.image(from: source, size: CGFloat(pixels)).draw(in: bounds)
                NSGraphicsContext.restoreGraphicsState()
                let suffix = scale == 2 ? "@2x" : ""
                guard let png = bitmap.representation(using: .png, properties: [:]) else {
                    throw NSError(domain: "MakeMacIcon", code: 3)
                }
                try png.write(to: folder.appendingPathComponent("icon_\(size)x\(size)\(suffix).png"))
            }
        }
    }
}
