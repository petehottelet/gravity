"""Generate Gravity's brand assets from the G mark.

    python tools/make_brand.py                   rebuild every asset from assets/g-mark.png
    python tools/make_brand.py --extract IMAGE   first re-extract the mark from a two-color
                                                 image (a white G on a solid background)

Outputs:
    src/Gravity.ico                  application icon (embedded in Gravity.exe)
    src/GravityPdf.ico               document icon for PDF files associated with Gravity
    src/Lockup.png                   icon + wordmark shown on the start page
    assets/gravity-icon-1024.png     high-resolution app icon
    assets/gravity-pdf-512.png       high-resolution document icon
    docs/logo-light.png, docs/logo-dark.png   README logo for light and dark themes

Requires Pillow, the ONRAMP font (installed, or pass --font) for the wordmark, and
Segoe UI Bold (included with Windows) for the PDF label. ONRAMP is a licensed font:
only the rendered images are kept in the repository, never the font file.
"""

from __future__ import annotations

import argparse
import io
import os
import struct
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parent.parent
MARK_PATH = ROOT / "assets" / "g-mark.png"

BLUE = (0x0B, 0x57, 0xD0)
WHITE = (255, 255, 255)
INK_LIGHT = (0x1B, 0x1B, 0x1F)
PAGE = (0xE6, 0xE9, 0xED)
PAGE_FOLD = (0xC9, 0xCF, 0xD6)

WORDMARK = "GRAVITY"     # ONRAMP has capitals only
WORDMARK_FACE = "ONRAMP"
WORDMARK_CAP = 0.50      # capital height of the wordmark as a fraction of the icon height
WORDMARK_TRACKING = 50   # extra letter spacing in 1/1000 em, like tracking in design apps

ICON_SIZES = (16, 20, 24, 32, 40, 48, 64, 128, 256)
TILE_RADIUS = 0.20       # corner radius as a fraction of the tile
GLYPH_FILL = 0.78        # glyph width as a fraction of the tile (matches the source art)
GLYPH_DROP = 0.017       # the source art sits the G slightly below center
STROKE_FRACTION = 0.047  # stroke width of the G relative to its own width
MIN_STROKE_PX = 1.45     # thicken the G at small sizes so it stays visible


# --------------------------------------------------------------------------- mark


def extract_mark(source: Path) -> None:
    """Pull a white glyph off a solid colored background into a white-on-transparent PNG."""
    img = Image.open(source).convert("RGB")
    w, h = img.size
    green = img.getchannel("G")
    corners = [green.getpixel(p) for p in ((4, 4), (w - 5, 4), (4, h - 5), (w - 5, h - 5))]
    bg = sorted(corners)[len(corners) // 2]
    span = max(1, 255 - bg)

    def coverage(v: int) -> int:
        a = round((v - bg) * 255 / span)
        if a < 10:
            return 0
        if a > 245:
            return 255
        return a

    alpha = green.point([coverage(v) for v in range(256)])
    bbox = alpha.point(lambda v: 255 if v > 128 else 0).getbbox()
    if bbox is None:
        raise SystemExit("No light glyph found in " + str(source))
    pad = round(max(w, h) * 0.01)
    box = (max(0, bbox[0] - pad), max(0, bbox[1] - pad), min(w, bbox[2] + pad), min(h, bbox[3] + pad))
    alpha = alpha.crop(box)
    mark = Image.new("RGBA", alpha.size, WHITE + (0,))
    mark.putalpha(alpha)
    MARK_PATH.parent.mkdir(parents=True, exist_ok=True)
    mark.save(MARK_PATH, optimize=True)
    print("extracted", MARK_PATH.relative_to(ROOT), mark.size)


def glyph(mark: Image.Image, width: int, target_px: float) -> Image.Image:
    """The mark scaled to `width`, thickened if it would be thinner than MIN_STROKE_PX at `target_px`."""
    height = round(mark.height * width / mark.width)
    alpha = mark.getchannel("A").resize((width, height), Image.LANCZOS)
    stroke_at_target = STROKE_FRACTION * target_px
    if stroke_at_target < MIN_STROKE_PX:
        grow = (MIN_STROKE_PX - stroke_at_target) * width / target_px
        k = int(round(grow)) | 1
        if k >= 3:
            alpha = alpha.filter(ImageFilter.MaxFilter(k))
    # Rebuild as solid white so no transparent-pixel color (resampling leaves black there)
    # can show through once the strokes are thickened.
    g = Image.new("RGBA", alpha.size, WHITE + (0,))
    g.putalpha(alpha)
    return g


# --------------------------------------------------------------------------- shapes


def shrink(img: Image.Image, size: int) -> Image.Image:
    """Downscale a master render. Area averaging for small icons avoids the ringing
    (dark specks between thin strokes) that sharpening filters produce."""
    return img.resize((size, size), Image.BOX if size <= 48 else Image.LANCZOS)


def master_size(size: int) -> int:
    return max(256, min(1024, size * 16))


def tile_master(mark: Image.Image, m: int, target_px: float, fill: float) -> Image.Image:
    """A blue rounded tile with the white G, rendered at m x m pixels."""
    img = Image.new("RGBA", (m, m), (0, 0, 0, 0))
    ImageDraw.Draw(img).rounded_rectangle((0, 0, m - 1, m - 1), radius=round(m * TILE_RADIUS), fill=BLUE + (255,))
    g = glyph(mark, round(m * fill), target_px * fill)
    x = (m - g.width) // 2
    y = (m - g.height) // 2 + round(m * GLYPH_DROP)
    img.alpha_composite(g, (x, y))
    return img


def tile(mark: Image.Image, size: int) -> Image.Image:
    fill = GLYPH_FILL if size >= 48 else (0.84 if size >= 32 else 0.9)
    m = master_size(size)
    return shrink(tile_master(mark, m, size, fill), size)


def document(mark: Image.Image, size: int, label_font: Path) -> Image.Image:
    """A flat file icon: gray page with a folded corner, the blue G, and a blue PDF band."""
    m = master_size(size)
    img = Image.new("RGBA", (m, m), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    small = size < 40
    pw, ph = m * (0.72 if small else 0.70), m * 0.90
    x0 = m * (0.16 if small else 0.17)
    y0 = (m - ph) / 2
    x1, y1 = x0 + pw, y0 + ph
    fold = pw * 0.24
    radius = m * 0.035

    # Page with the top-right corner cut off, then the folded flap.
    d.rounded_rectangle((x0, y0, x1, y1), radius=radius, fill=PAGE + (255,))
    d.polygon([(x1 - fold, y0 - 1), (x1 + 1, y0 - 1), (x1 + 1, y0 + fold)], fill=(0, 0, 0, 0))
    d.polygon([(x1 - fold, y0), (x1, y0 + fold), (x1 - fold, y0 + fold)], fill=PAGE_FOLD + (255,))

    # Label band, overhanging the page on the left.
    band_top, band_bottom = y0 + ph * (0.63 if not small else 0.66), y0 + ph * (0.86 if not small else 0.84)
    band_left = x0 - m * (0.07 if not small else 0.05)
    d.rounded_rectangle((band_left, band_top, x1, band_bottom), radius=m * 0.018, fill=BLUE + (255,))
    if not small:
        bh = band_bottom - band_top
        font = ImageFont.truetype(str(label_font), round(bh * 0.78))
        left, top, right, bottom = font.getbbox("PDF")
        cx, cy = (band_left + x1) / 2, (band_top + band_bottom) / 2
        d.text((cx - (left + right) / 2, cy - (top + bottom) / 2), "PDF", font=font, fill=WHITE + (255,))

    # The G in brand blue, centered above the band.
    gw = round(pw * (0.66 if small else 0.54))
    g = glyph(mark, gw, gw * size / m)
    blue = Image.new("RGBA", g.size, BLUE + (255,))
    blue.putalpha(g.getchannel("A"))
    gx = round(x0 + (pw - gw) / 2)
    gy = round(y0 + (band_top - y0) * 0.52 - g.height / 2)
    img.alpha_composite(blue, (gx, gy))
    return shrink(img, size)


def lockup(mark: Image.Image, height: int, ink: tuple[int, int, int], font_path: Path) -> Image.Image:
    """The icon followed by the "Gravity" wordmark, its capital height centered on the icon."""
    icon = tile_master(mark, master_size(height), height, GLYPH_FILL).resize((height, height), Image.LANCZOS)
    cap_target = height * WORDMARK_CAP
    size = round(height)
    for _ in range(8):  # converge on the font size whose capitals are cap_target tall
        font = ImageFont.truetype(str(font_path), size)
        top, bottom = font.getbbox("H")[1], font.getbbox("H")[3]
        size = max(8, round(size * cap_target / (bottom - top)))
    font = ImageFont.truetype(str(font_path), size)
    cap_top, baseline = font.getbbox("H")[1], font.getbbox("H")[3]
    left, _, _, below = font.getbbox(WORDMARK)
    # Letter positions keep the font's own kerning (prefix widths) and add uniform tracking.
    tracking = size * WORDMARK_TRACKING / 1000
    xs = [font.getlength(WORDMARK[:i]) + i * tracking for i in range(len(WORDMARK))]
    right = xs[-1] + font.getbbox(WORDMARK[-1])[2]
    gap = round(height * 0.2)
    y = (height - (baseline - cap_top)) / 2 - cap_top
    canvas_h = max(height, int(y + below) + 2)  # room for descenders, if the font has any
    canvas = Image.new("RGBA", (height + gap + round(right - left) + round(height * 0.04), canvas_h), (0, 0, 0, 0))
    canvas.alpha_composite(icon, (0, 0))
    d = ImageDraw.Draw(canvas)
    for ch, x in zip(WORDMARK, xs):
        d.text((height + gap - left + x, y), ch, font=font, fill=ink + (255,))
    return canvas


# --------------------------------------------------------------------------- files


def bmp_frame(img: Image.Image) -> bytes:
    """A 32-bit DIB icon frame (BGRA rows bottom-up, then the AND mask)."""
    w, h = img.size
    px = img.load()
    rows = bytearray()
    mask_stride = ((w + 31) // 32) * 4
    mask = bytearray()
    for yy in range(h - 1, -1, -1):
        mrow = bytearray(mask_stride)
        for xx in range(w):
            r, g, b, a = px[xx, yy]
            rows += bytes((b, g, r, a))
            if a == 0:
                mrow[xx // 8] |= 0x80 >> (xx % 8)
        mask += mrow
    header = struct.pack("<IiiHHIIiiII", 40, w, h * 2, 1, 32, 0, len(rows) + len(mask), 0, 0, 0, 0)
    return header + bytes(rows) + bytes(mask)


def png_bytes(img: Image.Image) -> bytes:
    buf = io.BytesIO()
    img.save(buf, format="PNG", optimize=True)
    return buf.getvalue()


def write_ico(path: Path, frames: list[Image.Image]) -> None:
    """Classic DIB frames for window-icon sizes (widest compatibility), PNG for 128 px and up."""
    blobs = [png_bytes(f) if f.width >= 128 else bmp_frame(f) for f in frames]
    out = bytearray(struct.pack("<HHH", 0, 1, len(frames)))
    offset = 6 + 16 * len(frames)
    for f, blob in zip(frames, blobs):
        dim = 0 if f.width >= 256 else f.width
        out += struct.pack("<BBBBHHII", dim, dim, 0, 0, 1, 32, len(blob), offset)
        offset += len(blob)
    for blob in blobs:
        out += blob
    path.write_bytes(bytes(out))
    print("wrote", path.relative_to(ROOT), "(%d sizes)" % len(frames))


def find_font(file_name: str, explicit: str | None = None) -> Path:
    candidates = [Path(explicit)] if explicit else []
    windir = os.environ.get("WINDIR", r"C:\Windows")
    local = os.environ.get("LOCALAPPDATA", "")
    candidates.append(Path(windir) / "Fonts" / file_name)
    if local:
        candidates.append(Path(local) / "Microsoft" / "Windows" / "Fonts" / file_name)
    for c in candidates:
        if c.is_file():
            return c
    raise SystemExit(file_name + " not found; install the font or pass its path")


def find_installed_font(face: str, explicit: str | None = None) -> Path:
    """Locate an installed font by its registered face name (font files often carry
    install-specific names, e.g. from a font manager)."""
    if explicit:
        return Path(explicit)
    import winreg

    windir = os.environ.get("WINDIR", r"C:\Windows")
    keys = [(winreg.HKEY_CURRENT_USER, r"Software\Microsoft\Windows NT\CurrentVersion\Fonts"),
            (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts")]
    for hive, sub in keys:
        try:
            key = winreg.OpenKey(hive, sub)
        except OSError:
            continue
        with key:
            i = 0
            while True:
                try:
                    name, value, _ = winreg.EnumValue(key, i)
                except OSError:
                    break
                i += 1
                if name.split(" (")[0] == face:
                    path = Path(value) if os.path.isabs(value) else Path(windir) / "Fonts" / value
                    if path.is_file():
                        return path
    raise SystemExit("Font '" + face + "' is not installed; install it or pass --font PATH")


def save_png(img: Image.Image, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    img.save(path, optimize=True)
    print("wrote", path.relative_to(ROOT), img.size)


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--extract", metavar="IMAGE", help="re-extract assets/g-mark.png from this image first")
    ap.add_argument("--font", metavar="PATH", help="wordmark font file (default: installed " + WORDMARK_FACE + ")")
    args = ap.parse_args()
    if args.extract:
        extract_mark(Path(args.extract))
    mark = Image.open(MARK_PATH).convert("RGBA")
    font = find_installed_font(WORDMARK_FACE, args.font)
    label = find_font("segoeuib.ttf")

    write_ico(ROOT / "src" / "Gravity.ico", [tile(mark, s) for s in ICON_SIZES])
    write_ico(ROOT / "src" / "GravityPdf.ico", [document(mark, s, label) for s in ICON_SIZES])
    save_png(tile_master(mark, 1024, 1024, GLYPH_FILL), ROOT / "assets" / "gravity-icon-1024.png")
    save_png(document(mark, 512, label), ROOT / "assets" / "gravity-pdf-512.png")
    save_png(lockup(mark, 96, INK_LIGHT, font), ROOT / "src" / "Lockup.png")
    save_png(lockup(mark, 240, INK_LIGHT, font), ROOT / "docs" / "logo-light.png")
    save_png(lockup(mark, 240, WHITE, font), ROOT / "docs" / "logo-dark.png")


if __name__ == "__main__":
    main()
