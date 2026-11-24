# Gravity UI comparison

The selected direction is the original Lucide redesign in `draft.html`: Read/Organize controls, Outline/Pages sidebar, labeled page actions, and restrained document chrome. Open `index.html` to compare this selected design with the existing saved reference. The files work offline, including the icons. Select Reading or Organizing, then Existing, Side by side, or Selected design for a full-width view. The styling-only alternative is retained in `cleanup.html` for reference.

This is an optional interface concept, not an implementation in the native app. No PDF files are opened, written, or printed. Changes last until the draft reloads. Reading and organizing have equal scope.

The selected prototype supports outline and thumbnail navigation, a chapter filter, zoom, selection, rotation, duplication, deletion, moving a single page earlier or later, and undo. Ctrl/Command-click or Shift-click selects multiple pages; Space toggles a focused page's selection. File-operation controls explain their prototype limits when used.

## Comparison limits

The existing screenshots are the repository's `docs/reading.png` and `docs/organize.png`. They are saved references, not fresh captures, and predate the collapsible Pages panel now in the native source. The comparison identifies this explicitly. Both sides are displayed at the same 1264 × 780 logical viewport, scaled equally in the comparison. The selected design has an Outline/Pages sidebar.

The first document page is a full-page crop; other available pages use the saved organizer's small thumbnails. They are adequate for composition, not for evaluating PDF text-rendering quality. Pages 13 and 14 use explicit unavailable-preview placeholders. Sample chapter labels are illustrative. The document's existing sample chart is unchanged; the redesign applies to application chrome.

The draft targets WCAG AAA text contrast and takes interaction guidance from Apple's Human Interface Guidelines. “AAA+” is not used as a certification. This is not a full WCAG, VoiceOver, or native accessibility audit. The comparison's scaled screenshots are visual references, not accessible replacements for the underlying document.

## Assets and licenses

- `vendor/icons.js`: 32 unmodified SVG definitions from `lucide-static` 1.79.0, downloaded from the official npm package. The rendering layer adds decorative accessibility attributes and sets a consistent stroke weight. See `vendor/LICENSE` for the upstream ISC license and notices.
- `assets/g-mark.png`: copied unchanged from the repository's `assets/g-mark.png`. The UI applies a CSS brightness filter to make the light mark legible on the light toolbar.
- `assets/page-1.png`: crop `(506,108,1016,770)` from `docs/reading.png`.
- `assets/thumb-1.png` through `thumb-12.png`: crops from `docs/organize.png`; see the embedded PNG `Source` metadata for each source rectangle.
- No remote fonts, tracking, runtime downloads, or network calls are used by the draft itself.

## Design references

- [Lucide](https://lucide.dev/guide/)
- [Apple accessibility guidance](https://developer.apple.com/design/human-interface-guidelines/accessibility)
- [WCAG enhanced contrast](https://www.w3.org/WAI/WCAG22/Understanding/contrast-enhanced.html)
- [Edward Tufte: notebooks and information design](https://www.edwardtufte.com/notebook/)
