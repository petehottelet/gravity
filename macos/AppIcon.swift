import AppKit

enum AppIcon {
    // Keep the tile inside the same visual bounds as neighboring macOS icons.
    // The source artwork stays full bleed for use on other platforms.
    static func image(from artwork: NSImage, size: CGFloat = 1024) -> NSImage {
        NSImage(size: NSSize(width: size, height: size), flipped: false) { bounds in
            NSGraphicsContext.current?.imageInterpolation = .high
            // Notes and QuickTime use a 25 px inset on a 256 px canvas.
            let inset: CGFloat = 100.0 / 1024.0
            let frame = bounds.insetBy(dx: bounds.width * inset, dy: bounds.height * inset)
            artwork.draw(in: frame, from: .zero, operation: .sourceOver, fraction: 1)
            return true
        }
    }
}
