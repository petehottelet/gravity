# SumatraPDF feature dossier for Gravity

Reference date: November 24, 2026.

The Gravity comparisons below record the planning baseline before the first implementation increment. Reliable Windows saves, the command palette, Find results, text/image export and images-to-PDF have since been added. See the [implementation status](GRAVITY_ROADMAP.md#implemented-windows-increment) and [README](../README.md) for current behavior; the SumatraPDF reference inventory is unchanged.

Gravity should develop into a dependable PDF reader, form filler and page editor for Windows and macOS. SumatraPDF offers a useful benchmark for discoverability, reading flexibility and document utilities. Gravity already implements much of the everyday reading and page organization workflow; its most valuable next steps are reliable saving, explicit platform coverage, richer navigation, accessible controls, annotations and predictable export tools.

This dossier describes the functions documented by [SumatraPDF](https://www.sumatrapdfreader.org/docs/SumatraPDF-documentation), compares them with Gravity's current source, and identifies the product and engineering implications. The accompanying [Gravity roadmap](GRAVITY_ROADMAP.md) turns those implications into ordered work with acceptance criteria. The [reference index](SUMATRAPDF_REFERENCE_INDEX.md) and [machine readable catalog](sumatrapdf-reference-catalog.json) preserve the long command, setting and scripting inventories.

## Contents

- [Scope and interpretation](#scope-and-interpretation)
- [Gravity baseline](#gravity-baseline)
- [Product and installation](#product-and-installation)
- [Formats and specialist reading](#formats-and-specialist-reading)
- [Reading and navigation](#reading-and-navigation)
- [Working with documents](#working-with-documents)
- [Commands and customization](#commands-and-customization)
- [Automation and deployment](#automation-and-deployment)
- [Command line tools and scripting](#command-line-tools-and-scripting)
- [Support and privacy](#support-and-privacy)
- [Comparison conclusions](#comparison-conclusions)

## Scope and interpretation

The coverage includes 114 pages in the official documentation bundle plus three linked references for barcodes, debugging and WSL integration. The technical index records 392 command rows, 375 setting paths, 664 JavaScript API entries and 56 example scripts. An API entry may be a constructor, overload, method or property; these counts must not be interpreted as counts of separate user features.

The website is a moving reference, not a manual frozen to the stable release. The Windows download page currently identifies **3.6.1** as stable. The advanced settings reference explicitly describes the upcoming **3.7** release, and many feature pages label their contents prerelease. The separate `SumatraPDF cli` reference says it is available on macOS and Linux and coming to Windows. Treat these as distinct product surfaces. [Stable download](https://www.sumatrapdfreader.org/download-free-pdf-viewer), [Advanced settings](https://www.sumatrapdfreader.org/docs/Advanced-options-settings), [Cross-platform CLI](https://www.sumatrapdfreader.org/docs/Command-line-for-scripts-and-AI-agents).

Throughout this dossier:

- **Established** means the documentation identifies an older release or describes longstanding behavior. It does not certify every detail against a downloaded stable binary.
- **3.7** means the cited documentation marks the feature or relevant enhancement as 3.7 or prerelease.
- **Mixed** means a feature family contains both longstanding and newer behavior.
- **Source present** means Gravity contains implementation for the capability. It does not mean the behavior was exercised in this documentation task.
- **Partial** means Gravity has a narrower implementation or a meaningful platform difference.
- **Absent** means no corresponding user-facing implementation was identified in the inspected source.
- **Candidate** means a proposed Gravity feature, not a commitment or an implemented capability.

Gravity's baseline is the current working tree, including the new forms and macOS code. It is broader than a historical release or a clean checkout might be. Runtime, interoperability, accessibility and performance claims require the checks defined in the roadmap; no new runtime pass or performance benchmark is claimed here.

## Gravity baseline

### Product and architecture

The [README](../README.md) positions Gravity as a lightweight PDF viewer, form filler and page organizer. Windows uses C# and WinForms on .NET Framework 4.8 with PDFium. macOS uses Swift, AppKit and PDFKit. Both target PDF workflows, but they do not share the same document engine or UI implementation.

The Windows editing model is particularly useful: `PdfSource` owns loaded PDF data, and `PageRef` identifies a source page with an additional rotation. `EditDoc` represents an ordered collection of those references. Many page operations therefore modify a list before serialization rather than rewriting the PDF after every gesture. Form edits create independent document snapshots and enter the same undo history. See [Model.cs](../src/Model.cs), [PdfForms.cs](../src/PdfForms.cs) and [FormEditor.cs](../src/FormEditor.cs).

Windows rendering uses 1024-pixel tiles on a background worker and a least-recently-used bitmap cache. The configured bitmap budget is 320 MiB in a 64-bit process and 160 MiB in a 32-bit process. Those budgets do not bound total process memory: loaded PDFs, text data, native allocations and undo snapshots also consume memory. A single PDFium lock coordinates native access. See [Renderer.cs](../src/Renderer.cs) and [Pdfium.cs](../src/Pdfium.cs).

The macOS model operates on PDFKit documents, and undo records serialized document snapshots. AppKit supplies standard document windows, saving and printing. This provides native behavior, but it also creates different preservation, performance and undo characteristics that must be tested separately. See [PDFModel.swift](../macos/PDFModel.swift), [GravityDocument.swift](../macos/GravityDocument.swift) and [DocumentWindow.swift](../macos/DocumentWindow.swift).

### Current capability matrix

| Capability | Windows source | macOS source | Practical implication |
| --- | --- | --- | --- |
| Open PDF | Present, with password prompt | Present, with password prompt | Core input format on both platforms |
| Other document formats | Absent | Absent | Do not advertise general document-reader parity |
| Tabs and multiple documents | Custom tabs and per-user instance handoff | Native document windows and tabs | Similar user outcome, different lifecycle implementation |
| Layout | Single, facing, book and continuous combinations | PDFKit reading view | Full Windows layout parity on Mac needs explicit verification |
| Zoom | Fit page, fit width, actual size, free zoom and pointer anchoring | Native zoom and scaling | Test coordinates consistently with rotation and widgets |
| View rotation | Present | PDFKit behavior must be distinguished from saved rotation | Keep view changes separate from document edits |
| Bookmarks and internal links | Present | Windows-style bookmark sidebar absent | High-value Mac navigation gap |
| External links | Web and mail links; other schemes blocked | Native framework behavior needs an explicit application policy | Align link policy across platforms |
| Text selection and copy | Present | Present through PDFKit | Accessible text navigation is a separate requirement |
| Find | Forward and backward, optional case matching | Search field backed by PDFKit | No established shared all-results experience |
| Navigation history | Link, bookmark and page jumps | Equivalent workflow not established | Preserve return location when adding richer navigation |
| Recent files and reading position | Up to 20 recent entries, per-file view state | Remembered reading positions not implemented | Recent files are not a full restored session |
| File change reload | Watcher with dirty-document protection | Absent | Essential for generated PDFs and external editing |
| Full screen and presentation | Present | Dedicated presentation mode absent | Presentation parity can follow core navigation |
| Thumbnail page panel | Collapsible panel beside reading view | Multiple-selection thumbnail sidebar | Retain page actions in reading context |
| Full organizer | Grid with selection and drag reordering | No equivalent drag organizer | Windows has a stronger page-editing interaction |
| Page operations | Cut, copy, paste, delete, duplicate, rotate, insert, extract | Most operations present through menus | Test same outcomes rather than identical controls |
| Combine PDFs | Dedicated combine workflow | Insertion and cross-document page operations | A dedicated combined-file workflow is not established on Mac |
| Undo and redo | Up to 300 steps, including after save | Document undo manager configured for 100 levels | Memory and save semantics differ |
| Existing form fields | Text, multiline, checks, radio, choice and multiple-selection lists | Text, checks, radio and choices; multiple-selection lists missing | Forms are already present and should be stabilized |
| Form calculations and XFA | Unsupported | Unsupported | Detect unsupported forms and explain the limitation |
| Annotation editing | Absent | No application annotation editor identified | Major everyday workflow gap |
| Digital signing and verification | Absent | No application workflow identified | Opening a signed PDF is not verification |
| Printing | Raster rendering up to 300 DPI, fit-to-paper workflow | PDFKit print operation | Precise scale and cross-platform output need work |
| Text and page-image export | Internal engine primitives, no full export product | Framework primitives, no full export product | Existing capabilities are a starting point, not a finished feature |
| Crop, flatten, redact, compress and encrypt tools | No complete user-facing suite | No complete user-facing suite | Separate tools by their preservation and security consequences |
| Command palette and shortcut editor | Absent | Absent | A shared command model would help several future features |
| Accessibility | Keyboard shortcuts present; document accessibility unverified | Native controls help; complete workflow unverified | Do not infer screen-reader support from keyboard support |
| OCR, speech and AI | Absent | Absent | Optional later work after text and accessibility foundations |
| Deployment | Portable bundle and per-user install script | Universal source build and packaging script | Mac build/runtime validation and distribution remain distinct gates |

Evidence: [README](../README.md), [MainForm.cs](../src/MainForm.cs), [DocView.cs](../src/DocView.cs), [OrganizerView.cs](../src/OrganizerView.cs), [Settings.cs](../src/Settings.cs), [Program.cs](../src/Program.cs), [macOS sources](../macos/).

### Saving and preservation

An unchanged Windows document can be saved by copying its original bytes. For edited pages, Gravity selects a basis document, keeps its remaining pages, imports additional pages, orders them, applies rotations and serializes. Keeping the basis catalog is intended to preserve its metadata, outline and forms. It does not prove that every relationship survives every combination of imported pages, shared fields, deleted destinations and duplicate pages. Bookmarks from other input documents are explicitly outside the current preservation promise. [Model.cs](../src/Model.cs), [README limitations](../README.md#limitations).

`EditDoc.WriteFile` writes to a temporary file and attempts `File.Replace`. If replacement raises certain exceptions, it falls back to `File.Copy` over the destination. That overwrite path is not equivalent to an atomic replacement: interruption can leave a partial destination. The README's unconditional interrupted-save guarantee is therefore stronger than the implementation supports. The roadmap makes this a first-phase reliability item; it is not fixed by this dossier. [Model.cs](../src/Model.cs).

Opening encrypted PDFs, preserving encryption after editing, respecting assembly permissions, and validating digital signatures are four different requirements. Gravity's password prompt establishes the first. The others need explicit tests and product behavior. Similarly, unchanged-byte preservation protects an unchanged signed file, but ordinary edits may invalidate signatures.

### Existing verification assets

The Windows suite generates its own PDFs and covers opening, text, page movement, cross-document editing, duplication, blank pages, undo, save, unchanged bytes, links, outlines, rendering, malformed files and forms. UI tests drive a real window using synthetic input. macOS has separate PDFKit model and window-construction tests. Python tests cover icon generation. These are useful foundations for the roadmap's expanded fixtures. [SelfTest.cs](../tests/SelfTest.cs), [UiTest.cs](../tests/UiTest.cs), [FormTests.cs](../tests/FormTests.cs), [Tests.swift](../macos/Tests.swift), [test_make_brand.py](../tests/test_make_brand.py).

## Product and installation

### D01 Installation and portable operation

**SumatraPDF — mixed.** Users can choose an installer or a portable distribution, extract installer contents, select a destination and use a custom settings directory. The installed product adds optional operating-system components that a standalone viewer does not automatically provide. Portable packaging behavior changes in the 3.7 documentation, so single-file distribution should not be equated with never extracting runtime files. [Installation](https://www.sumatrapdfreader.org/docs/Installation), [Portable versus installer](https://www.sumatrapdfreader.org/docs/SumatraPDF-portable).

**Gravity implications.** Preserve the easy portable Windows bundle. Add a manifest that identifies the executable, engine and license files as one compatible unit. Test installation, updating an installed copy, removal and the absence of administrator rights. A smaller download is useful only if first launch is predictable and missing or mismatched engine files produce actionable errors. A portable-settings mode would be an additional feature: the existing portable binary still uses the normal user settings location.

### D02 Default application and shell integration

**SumatraPDF — mixed.** Registration makes supported extensions available to the operating system; the user chooses the default application through Windows. Installed components can provide Explorer previews, thumbnails and content indexing. The 3.7 installer separates preview and thumbnail choices. [Default viewer](https://www.sumatrapdfreader.org/docs/Set-as-default-pdf-viewer), [Installer arguments](https://www.sumatrapdfreader.org/docs/Installer-cmd-line-arguments).

**Gravity implications.** Existing PDF registration and the default-app handoff are a good baseline. Explorer preview, thumbnail and search handlers would be separate components with their own crash containment, update and uninstall requirements. They should follow reader reliability rather than delay it. On macOS, evaluate Finder/Quick Look integration separately instead of assuming a Windows handler architecture can be reused.

### D03 Platforms and formats of distribution

**SumatraPDF — evolving.** The platform page now describes a macOS port sharing document engines, commands and settings with Windows. Linux and browser versions are described as work in progress, while the separate CLI page already claims macOS/Linux availability. This is not evidence of full graphical parity across all platforms. [Platforms](https://www.sumatrapdfreader.org/docs/Why-only-Windows), [CLI](https://www.sumatrapdfreader.org/docs/Command-line-for-scripts-and-AI-agents).

**Gravity implications.** Its native Windows and Mac implementations can be an advantage in platform conventions, but every feature needs an explicit Windows/Mac status. A Mac source build should remain distinguishable from a validated, signed and notarized downloadable application. Keep release numbering separate from roadmap phase names.

## Formats and specialist reading

### D04 Supported document families

**SumatraPDF — mixed.** The format list includes PDF and PDF-compatible AI; EPUB, MOBI/unencrypted Kindle variants, FictionBook, LIT and Palm DOC; text, Markdown and RTF; CBZ/CBR/CBT/CB7 and image archives; DjVu, CHM, XPS/OXPS/XOD/DWFX, OFD and SVG. Raster support includes PNG, JPEG, GIF, TIFF, BMP, ICO, TGA, WebP, JPEG XR/2000, AVIF, JPEG XL and HEIF/HEIC. Some newer types are marked 3.7. RTF has omissions, and DRM-protected ebooks are not generally implied. [Supported formats](https://www.sumatrapdfreader.org/docs/Supported-document-formats).

**Gravity implications.** Treat multi-format support as a product expansion, not a file-extension change. PDFium and PDFKit do not provide SumatraPDF's combined codec coverage. Image-to-PDF import offers immediate value within Gravity's existing purpose. A general ebook reader requires reflow, font controls, logical reading positions and format-specific accessibility; it should be a later decision with a separate maintenance budget.

### D05 Converted and dependency-backed inputs

**SumatraPDF — mixed.** PostScript/EPS and PJL depend on Ghostscript. DVI can use a TeX conversion toolchain. CAJ/KDH/NH support on 64-bit Windows uses a downloadable converter and cached PDF output. These are conversion workflows, not identical native rendering paths. The source also describes built-in HEIC/AVIF decoding with an optional Windows fallback. [Supported formats](https://www.sumatrapdfreader.org/docs/Supported-document-formats).

**Gravity implications.** If converters are ever added, display the dependency, the source/output distinction and the cache lifecycle. Do not silently install an execution toolchain simply because a document asks for a particular format. Identify which conversions retain text and vectors, which rasterize, and which can change pagination. Keep optional converters outside the minimum PDF installation.

### D06 Comics and image collections

**SumatraPDF — mixed.** Comics and image folders are treated as ordered pages. The docs describe password-protected comic archives, chapter/bookmark metadata, right-to-left reading, continuous strips, wide pages spanning a two-page row, automatic wide-page rotation, zoom limits, file/image information and caching for network archives. [Comics and manga](https://www.sumatrapdfreader.org/docs/Comics-and-manga).

**Gravity implications.** A simple ordered image import can serve scans and photographs without creating a separate comic-reader product. If comic reading becomes a goal, specify natural filename sorting, archive limits, orientation handling, cover placement, page progression and missing/corrupt-image behavior. Wide-page spread detection must not accidentally reinterpret an ordinary landscape PDF page as two physical pages.

### D07 Ebooks and reflow

**SumatraPDF — mixed.** Reflowable documents support font, font size, margins, line spacing, layout dimensions and CSS choices. The newer settings dialog can apply overrides to one document or all ebooks. Text selection, search and outlines work through the ebook engine, but changing layout can change displayed page numbers. [Ebook UI](https://www.sumatrapdfreader.org/docs/Customize-eBook-UI).

**Gravity implications.** A PDF page number is a stable physical index; an ebook's rendered page number may not be. Generalizing Gravity's current `PageRef` abstraction directly to EPUB would conflate different identities. Any future ebook provider needs logical anchors for navigation and bookmarks, a separate pagination lifecycle and tests for embedded fonts, vertical writing and CSS failures.

### D08 Engineering drawings

**SumatraPDF — 3.7.** Drawing enhancements can strengthen fine strokes and darken gray lines, globally or for a PDF in the current session. A separate line-weight setting can display lines at one pixel. These display transformations have specific limits and do not all affect printing/export. [Engineering drawings](https://www.sumatrapdfreader.org/docs/CAD-Engineering-Drawings).

**Gravity implications.** First improve exact scale, large-page navigation and rendering at high zoom. Later drawing modes should make clear whether they change display, printing or saved content. Avoid promising measurement accuracy until page units, rotation, calibration and printer scaling have been tested. A ruler or measurement overlay should never silently alter the document.

### D09 OFD

**SumatraPDF — 3.7.** OFD support covers fixed-layout pages, templates, annotations, attachments, multi-document packages, password encryption, conversion and signature/seal checks. Certificate-encrypted packages are a stated limitation. Signature validation has OFD-specific algorithms and trust handling. [OFD documents](https://www.sumatrapdfreader.org/docs/OFD-documents).

**Gravity implications.** This is a separate format and trust-system investment with limited overlap with the current PDF-only product. Keep it deferred unless actual users require e-invoice or public-sector OFD workflows. A PDF export path could eventually provide interoperability without claiming native OFD editing or signature validation.

## Reading and navigation

### D10 Tabs and windows

**SumatraPDF — mixed.** Tab use and process reuse are independent. Navigation includes direct tab switching, an MRU switcher, duplicate views, new windows, closure actions and session restoration. Newer behavior adds lazy loading, missing-file tabs, colored collapsible tab groups and additional Home tabs. Saved collections of files and visual groups in the tab strip are separate concepts. [Tabs and windows](https://www.sumatrapdfreader.org/docs/Tabs-and-windows).

**Gravity implications.** Keep existing instance handoff and tabs. Prioritize reopen-closed, session restore, dirty-document recovery and explicit handling of missing or disconnected files. Restore inactive tabs without eagerly reading every large PDF. Visual grouping is useful after the basic session lifecycle is reliable. Test repeated open requests, non-ASCII filenames, multiple windows and the distinction between a document and a view of that document.

### D11 Page layouts and display transforms

**SumatraPDF — mixed.** Single, facing and book layouts combine with continuous scrolling. Newer controls include overlapping one-page spread steps, mirrored display, uniform page widths, visible-margin trimming, selection-based cropping, column reading and rotation-following scroll direction. These operations do not all modify the file, and saving cropped pages changes page boxes rather than removing hidden content. [Viewing modes](https://www.sumatrapdfreader.org/docs/Viewing-modes).

**Gravity implications.** Existing core layouts are substantial. Add fit-content and temporary trim before specialized transforms. Every transform must update text hit-testing, link rectangles, widget placement, search highlights and thumbnails through one consistent coordinate model. A display crop belongs to view state; a saved crop belongs to document history. A transform's effect on printing must be stated explicitly.

### D12 Scrolling and zoom

**SumatraPDF — mixed.** Input includes keys, wheel, touchpad, panning and pinch. Beyond normal fit modes, the docs describe fit-content, fit-visible, fit-height, shrink-only and orientation-aware fitting; free pan; retained offsets on page turns; and configurable wheel behavior. Automatic scrolling and reading aids appear in the wider command surface. [Scrolling and zooming](https://www.sumatrapdfreader.org/docs/Scrolling-and-zooming).

**Gravity implications.** Preserve pointer-anchored zoom and efficient tile reuse. Establish a consistent difference between scrolling within a page and turning a page. Test high-resolution touchpads independently from wheel notches. For large drawings, allow a corner to reach the center of the viewport. Avoid introducing many fit commands before their names and transitions are understandable.

### D13 Thumbnails and sidebars

**SumatraPDF — 3.7 for the documented thumbnail sidebar.** Thumbnails support mouse and keyboard navigation, current-page feedback and focus movement. Sidebar panels can show outlines, thumbnails and favorites in different combinations. Page rearrangement belongs to a separate merge window. [Thumbnails sidebar](https://www.sumatrapdfreader.org/docs/Thumbnails-sidebar).

**Gravity implications.** Gravity already places editable thumbnails beside the reading view and offers a larger organizer. This is a useful product strength. Improve selection counts, keyboard movement, page labels, context actions and drag feedback without forcing users to leave the document. On Mac, bring navigation and editing gestures closer to the established Windows workflow while retaining native shortcuts.

### D14 Outlines and chapter navigation

**SumatraPDF — mixed.** Outlines can be browsed, searched through the palette and expanded to useful depths. The TOC recipe distinguishes a reading-only outline inferred from headings from bookmarks actually written into the PDF. Scripted bookmark creation can supply a permanent outline. [Command palette](https://www.sumatrapdfreader.org/docs/Command-Palette), [Add a table of contents](https://www.sumatrapdfreader.org/docs/Add-table-of-contents-to-PDF).

**Gravity implications.** Deliver the Mac bookmark sidebar and search before automatic TOC generation. Add chapter navigation and current-section tracking. Later outline editing must define what happens when target pages are deleted, moved or duplicated. Generated headings should be previewed and editable; a heuristic must not be presented as an authoritative document structure.

### D15 Favorites and personal reading marks

**SumatraPDF — established with newer extensions.** Favorites save a document location, including scroll position, and can be reached across documents through a sidebar or palette. They are personal navigation state rather than necessarily bookmarks embedded in the PDF. [Managing favorites](https://www.sumatrapdfreader.org/docs/Managing-favorites).

**Gravity implications.** Personal reading marks are a relatively inexpensive addition to settings and navigation. Store a label, document identity, physical page and within-page location. Allow renaming, deletion and missing-file relocation. Keep the terminology distinct from document bookmarks so users know whether a shared PDF will include the mark.

### D16 PDF layers

**SumatraPDF — 3.7.** A layer sidebar exposes optional content groups, grouping labels, mutually exclusive alternatives and locked layers. Visibility is remembered, while printing also observes PDF print-specific layer behavior. Toggling display does not rewrite the PDF. [PDF layers](https://www.sumatrapdfreader.org/docs/PDF-layers).

**Gravity implications.** Useful for maps, engineering and multilingual documents, but it needs an engine capability investigation on both platforms. Start with detection and an honest status if interactive visibility control is unavailable. Ensure displayed layers and printed layers cannot diverge without explanation. Do not treat hiding a layer as removal of confidential content.

### D17 Page information and document properties

**SumatraPDF — mixed.** Page information can show page labels, physical indices, progress, dimensions, rotation and zoom. Richer bars include format-specific metadata. Document properties add metadata, security and font information; lower-level reports expose PDF objects and page boxes. [Page info](https://www.sumatrapdfreader.org/docs/Page-info), [PDF information](https://www.sumatrapdfreader.org/docs/Show-PDF-info).

**Gravity implications.** Add a compact Properties dialog and distinguish the physical page index from a printed label such as `xii`. Show dimensions and units, encryption and signature presence, interactive forms and engine version. This helps both users and support. A signature-presence badge must not imply cryptographic validation.

### D18 Search

**SumatraPDF — mixed.** Find includes next/previous matches, selection-based queries, a match count, a floating all-results list with snippets and page numbers, and optional page-range restriction. Search behavior is distinct from multi-file CLI grep and sending selected text to a web service. [Finding text](https://www.sumatrapdfreader.org/docs/Finding-text).

**Gravity implications.** Extend the existing forward/backward search into cancellable incremental search with results and context. Keep a per-document query and expose whole-word and case options. Define Unicode and diacritic behavior. Searching a scanned page with no text should explain the absence of searchable content rather than quietly suggest that the word is not present. Test edits and reloads while a search is running.

### D19 Links and navigation history

**SumatraPDF — mixed.** The app follows embedded links and can detect URL, email and DOI text. Back/forward navigation returns to prior locations. Auto-detection can be disabled independently of embedded links, and restricted-use policies can limit protocols. [Hyperlinks](https://www.sumatrapdfreader.org/docs/Hyperlinks).

**Gravity implications.** Keep the existing Windows scheme restrictions as the minimum cross-platform policy. Add keyboard link navigation, a visible destination and context-menu copy. Treat local-file launches and executable actions separately from web URLs. When link support expands, test destination coordinates after rotation, crop and page reorder. History should return to an actual view position, not merely a page number.

### D20 Citation and footnote previews

**SumatraPDF — 3.7.** Hover previews can display an internal destination without moving the main view; the popup supports scrolling and zoom. The docs also describe inferred plain-text citations and EPUB-specific footnote popups. [Citation hover preview](https://www.sumatrapdfreader.org/docs/Citation-hover-preview).

**Gravity implications.** Begin with explicit internal PDF links, whose destination is known. Add keyboard access and a delay setting. Reuse cached rendering without blocking reading or confusing main-view scroll gestures. Heuristic bibliography matching can follow later, with a clear distinction between an authored link and an inferred citation target.

### D21 Full screen and presentations

**SumatraPDF — mixed.** Presentation mode fits one slide to the screen and provides slide movement, blank black/white screens, cursor hiding and a newer laser pointer. Full screen can preserve the reading layout instead of behaving as a slide show. [Present a PDF](https://www.sumatrapdfreader.org/docs/Present-a-PDF).

**Gravity implications.** Windows already has both modes. Restore the exact prior view on exit, support presenter keys, and bring a dedicated presentation flow to Mac. Blank-screen and pointer controls are useful additions with little document-engine work. Test multiple monitors, changing display DPI and focus restoration before adding automatic slide timing.

### D22 Accessibility and reading assistance

**SumatraPDF — mixed.** The accessibility page describes Windows UI Automation for document text and keyboard use, with more complete text-range navigation in 3.7. It notes a Narrator focus problem, scan limitations and missing UIA exposure in embedded plugin mode. Built-in speech is a separate feature. [Accessibility](https://www.sumatrapdfreader.org/docs/Accessibility).

**Gravity implications.** Keyboard shortcuts alone do not establish accessibility. Expose document, page, text, link, form and thumbnail semantics; provide visible focus and readable labels; announce errors and progress; and test without a mouse. Windows custom canvas controls need particular attention. PDFKit's native behavior is a foundation on Mac, not proof that every surrounding control and page-edit operation is accessible.

## Working with documents

### D23 Annotation creation

**SumatraPDF — mixed.** The annotation workflow includes text/area highlights, underline, strikeout, squiggles, notes, free text, ink, shapes, lines, stamps and images. Newer editing controls add repeat highlighting, annotation search, movement, clipboard operations, undo and explicit save/discard behavior. [Editing annotations](https://www.sumatrapdfreader.org/docs/Editing-annotations).

**Gravity implications.** Prioritize highlight, underline, strikeout and notes, then free text, shapes, ink and stamps. Build an annotation model that survives save, reopen, undo and page organization. Use actual PDF annotations with portable appearances. An annotation is not an edit to the original paragraph's font or wording; avoid promising general text-layout editing through a markup toolbar.

### D24 Annotation management and interoperability

**Gravity requirements.** Add a searchable annotation list showing page, type and note, with stable selection after edits. Color and opacity need meaningful defaults and keyboard access. Preserve annotations created by other tools even when Gravity cannot edit their subtype. Support multiple quadrilaterals for text spanning lines, rotated pages and duplicate pages. Compare the saved result in independent viewers. A hidden-annotations view should not silently change export or printing.

The detailed reference supplies less prominent annotation commands, including polygons, polylines, carets, links, attachments and popup-related operations. Presence in that reference does not guarantee that all subtypes have equally complete editing UX. Use the [command inventory](SUMATRAPDF_REFERENCE_INDEX.md#command-identifiers) to track breadth while keeping Gravity's first annotation release focused.

### D25 Fillable forms

**SumatraPDF — 3.7.** Forms support text entry, multiline/password constraints, checks, radios and choices, field highlighting, undo and saving. The docs describe field calculations through PDF JavaScript and a switch to disable it. Keyboard traversal has limitations: the described Tab sequence is not an all-controls traversal. [Fill PDF forms](https://www.sumatrapdfreader.org/docs/Fill-PDF-forms).

**Gravity implications.** Forms are already implemented. Focus next on interactive affordances, shared-name field behavior, export values versus display labels, radio-group consistency, long Unicode text, appearances, keyboard access and reopen compatibility. Keep unsupported XFA and calculation/submission behavior explicit. A read-only field, a document assembly restriction and a form-entry permission are distinct constraints.

### D26 Signature images and inserted images

**SumatraPDF — 3.7 enhancements.** Images from a file or clipboard can be placed as stamp annotations, moved and resized. The image workflow also documents replacing a PDF image and handing it to another editor. A pasted handwritten signature is explicitly different from a certificate-based signature. [Add an image](https://www.sumatrapdfreader.org/docs/Add-an-image-to-a-PDF).

**Gravity implications.** Image stamps are a practical second annotation milestone. Label a handwriting picture as a signature image. Keep original aspect ratio by default, support transparency, and set useful placement size independent of current zoom. External image editing requires a managed temporary file and a clear import step; it should not replace content when a tool merely opens the image.

### D27 Digital signing

**SumatraPDF — 3.7.** The signing UI accepts a Windows-store certificate or PFX/P12, selects an existing signature field or places a new one, controls appearance and writes the signature during save. It distinguishes approval signatures, certifying signatures and document/field locks. The CLI signing tool has a narrower field-based workflow. [Sign a PDF](https://www.sumatrapdfreader.org/docs/Sign-a-PDF).

**Gravity implications.** Signing needs dedicated engine and operating-system work. Establish incremental-save behavior, certificate selection, private-key access, cancellation and preservation of earlier signatures. Do not reuse a generic full-document rewrite and call it signature-preserving. Verification and honest signed-document warnings should precede creating signatures.

### D28 Signature verification

**SumatraPDF — 3.7.** The UI has a document status bar, per-signature list and properties. Validation considers covered bytes, certificate trust, timestamps and revocation evidence, with some online checks. The documentation also describes EU trust-list handling. Its CLI verification output is not equivalent to the full Windows UI assessment. [Verify a PDF signature](https://www.sumatrapdfreader.org/docs/Verify-PDF-signature).

**Gravity implications.** Separate integrity, signer trust, timestamp trust, revocation availability and permitted subsequent changes. A result can be unknown without being invalid. Show network-dependent checks and offline limitations. Any future claim about legally qualified signatures requires a separately validated trust implementation and jurisdiction-specific requirements; a green icon alone is not a sufficient product specification.

### D29 Page assembly

**SumatraPDF — 3.7 for the merge window.** The thumbnail workflow adds PDFs, reorders and rotates pages, supports selection and clipboard operations, and can mark removed pages for restoration before saving. File-level merge recipes support ranges from each input. [Merge PDF window](https://www.sumatrapdfreader.org/docs/Merge-PDF-window), [Merge PDFs](https://www.sumatrapdfreader.org/docs/Merge-PDFs).

**Gravity implications.** This is already a core strength. Improve previewing insertion, range selection, selection counts, keyboard reordering and origin labels. Specify metadata and bookmark ownership when combining files. Form names from different PDFs can collide; page import is not automatically complete form merging. Provide a final page count and make cancellation leave every source untouched.

### D30 Split and extract

**SumatraPDF — 3.7.** Splitting is expressed as extracting ranges, producing one page per file or grouping fixed page counts. Some non-PDF formats can be extracted as text-preserving PDF, with explicitly excluded formats and layout-dependent ebook page numbering. [Split a PDF](https://www.sumatrapdfreader.org/docs/Split-a-PDF), [Extract pages](https://www.sumatrapdfreader.org/docs/Tool-x-extract-pages-from-pdf).

**Gravity implications.** Existing selected-page extraction is a foundation for a dedicated Split dialog. Support explicit ranges, every N pages and eventually bookmarks. Preview the output file count and names. Detect duplicates and collisions before writing, use one-based physical page indices consistently, and leave partially completed batch results identifiable if cancellation occurs.

### D31 Delete and reorder

**SumatraPDF — 3.7 tool workflows.** Page lists can select, omit, reverse or repeat pages. The reorder recipe specifically warns that one merge path can leave bookmarks at old page numbers for reversed ranges, while `clean` is recommended for reordering one file. [Delete pages](https://www.sumatrapdfreader.org/docs/Tool-x-delete-pages-from-pdf), [Reorder pages](https://www.sumatrapdfreader.org/docs/Reorder-PDF-pages).

**Gravity implications.** Never judge a page operation only by thumbnail order. Test links and outlines after movement, duplicate-page destinations, deletion of a target page, inherited page attributes and shared form widgets. Reject an empty document with a clear message. Preserve undo after save without retaining unbounded native resources.

### D32 Permanent rotation

**SumatraPDF — mixed.** View rotation is separate from modifying the PDF's page rotation. Permanent rotation is available through documented scripts and the merge-window workflow, including selected/odd/even page use cases in recipes. [Rotate pages](https://www.sumatrapdfreader.org/docs/Rotate-PDF-pages).

**Gravity implications.** Gravity already distinguishes view and saved page rotation. Make the distinction visible in command names, toolbar hints and undo labels. Test combined source rotation plus edit rotation, page-size changes, text selection, widgets and printed output. Rotation must not silently rasterize a text document.

### D33 Cropping and page boxes

**SumatraPDF — 3.7.** Crop recipes distinguish changing the visible CropBox from deleting content outside a region. MediaBox, CropBox and production boxes have different roles. Display trimming can be saved as a cropped copy, but hidden objects remain. [Crop margins](https://www.sumatrapdfreader.org/docs/Crop-PDF-margins).

**Gravity implications.** A crop editor should show handles, numeric margins, units, affected pages and a reset action. Store crop changes in undo history and preview mixed page sizes. State that cropping changes the visible area. Do not expose a crop command as a privacy-removal tool. Preserve text and vectors, and test rotated CropBoxes and nonzero page origins.

### D34 Redaction

**SumatraPDF — 3.7.** Redaction has two distinct stages: create marks, then apply them to remove underlying content and save. Marks alone do not remove information. The recipe includes checking the saved result. [Redact a PDF](https://www.sumatrapdfreader.org/docs/Redact-a-PDF).

**Gravity implications.** This should have a separate correctness gate. Implement genuine removal of affected text, images and graphics with appropriate handling of shared resources. Test independent text extraction, copy/paste, object inspection and saved revisions. A black rectangle or a crop is not redaction. Saving a redacted copy should avoid retaining earlier sensitive revisions; the preservation strategy for ordinary edits cannot be reused blindly.

### D35 Page numbers and watermarks

**SumatraPDF — 3.7.** Recipes add searchable page-number text with position, numbering start and skipped pages, or text/image watermarks with opacity, angle and over/under placement. The command reference also lists dedicated watermark and numbering commands, so scripts should not be described as the only documented route. [Page numbers](https://www.sumatrapdfreader.org/docs/Add-page-numbers-to-PDF), [Watermarks](https://www.sumatrapdfreader.org/docs/Add-a-watermark-to-PDF).

**Gravity implications.** Add these as export-copy workflows after crop and basic export. Preview on representative portrait, landscape and rotated pages. Include a numbering offset, selected ranges and a choice to skip the cover. Do not obscure existing content by default. Treat a watermark as visible labeling, not access control or prevention of copying.

### D36 Flattening

**SumatraPDF — 3.7.** Baking converts annotation and widget appearances into ordinary page content. The CLI can retain one category while flattening the other. Flattening removes those interactive objects; it does not make the PDF impossible to edit with another tool. [Flatten forms and annotations](https://www.sumatrapdfreader.org/docs/Flatten-PDF-forms-and-annotations), [Bake tool](https://www.sumatrapdfreader.org/docs/Tool-bake).

**Gravity implications.** Provide an explicit flattened export, preserving the editable source. Ensure missing or stale widget appearances are handled before flattening. Test checkboxes, radio buttons, multiline values, transparency and annotation visibility. Signing should normally follow the final content transformation; flattening a previously signed document changes its signed data.

### D37 Text extraction

**SumatraPDF — 3.7 tool workflows.** Text can be exported for all pages or a range. Structured text formats retain geometry and other details beyond plain strings; reconstruction depends on heuristics because PDF glyph placement does not necessarily encode reading order. [Extract text](https://www.sumatrapdfreader.org/docs/Tool-x-extract-text-from-pdf).

**Gravity implications.** Expose the text extraction already underlying search and selection. Start with UTF-8 text plus explicit page separators, then add a documented JSON representation with page, rectangle and text. Multi-column ordering, ligatures, bidirectional text, invisible OCR layers and malformed encodings need fixtures. A future Markdown export should preserve page citations and disclose inferred headings.

### D38 Embedded images and fonts

**SumatraPDF — 3.7 tool workflows.** Image extraction saves embedded assets rather than rendering whole pages. Object information can identify specific resources. Font extraction operates on embedded programs and may yield subsets rather than a full font; absent font data cannot be recovered by extraction. [Extract images](https://www.sumatrapdfreader.org/docs/Tool-x-extract-images-from-pdf), [Extract fonts](https://www.sumatrapdfreader.org/docs/Extract-fonts-from-PDF).

**Gravity implications.** Begin with image export and a useful asset list. Distinguish the original image from a transformed page-region rendering. Preserve masks and transparency, avoid duplicate exports of shared resources unless requested, and provide collision-safe filenames. Font inspection is more valuable to ordinary users than automatic font extraction, so the latter can remain an advanced tool.

### D39 Page regions and image tools

**SumatraPDF — 3.7 enhancements.** A rectangular selection can be copied or saved as an image at a chosen resolution. The docs also describe embedded-image saving/cropping/resizing and a screenshot utility, which are separate from whole-page rendering. [Save a page region](https://www.sumatrapdfreader.org/docs/Save-page-region-as-image).

**Gravity implications.** Region export is a useful near-term capability for tables, diagrams and figures. Let users choose pixel dimensions or DPI and include a preview. Base export on document coordinates, not screen capture, so results are independent of zoom and display scaling. A general desktop screenshot utility is outside the core roadmap unless it serves an established workflow.

### D40 Attachments

**SumatraPDF — mixed.** Document-level attachments appear in the outline area, and page attachment annotations can be opened or saved. CLI extraction can recover attachments, while scripts can preserve their names. Opening an attachment and merely saving it are different actions. [PDF attachments](https://www.sumatrapdfreader.org/docs/Save-PDF-attachments).

**Gravity implications.** Add an attachment list with name, size and type, then explicit Save As. Sanitize embedded filenames and reject paths escaping the chosen folder. Open supported PDFs as documents; do not automatically execute arbitrary embedded files. Preserve attachments during unrelated edits and make their presence visible before users believe a document has been fully sanitized.

### D41 Images and comics to PDF

**SumatraPDF — 3.7.** Images, image folders and comic archives can become PDFs with one page per image. The UI proposes a separate output and opens it after conversion. [Images to PDF](https://www.sumatrapdfreader.org/docs/Convert-images-to-PDF), [Comics to PDF](https://www.sumatrapdfreader.org/docs/Convert-comics-to-PDF).

**Gravity implications.** Prioritize images to PDF because it fits the current organizer. Include ordering, page size, orientation, margins, image DPI and fit behavior. Preserve JPEG data when suitable rather than recompressing unnecessarily. Handle EXIF rotation, transparency, huge dimensions and multipage TIFF explicitly. Archive conversion can wait until archive safety and ordering are designed.

### D42 Reflowable documents to PDF

**SumatraPDF — 3.7 tool workflows.** EPUB conversion can choose paper and font size while retaining searchable text. Text-file conversion is also documented. Conversion creates a fixed pagination that differs from a reflowable book's reading positions. [EPUB to PDF](https://www.sumatrapdfreader.org/docs/Convert-EPUB-to-PDF), [Text to PDF](https://www.sumatrapdfreader.org/docs/Tool-x-convert-text-to-pdf).

**Gravity implications.** This requires a layout engine rather than just a PDF writer. Defer until there is a clear demand for creating PDFs from text-based formats. If adopted, preview pagination, include missing-font reporting and test Unicode, long lines and styles. Do not claim Word/Office fidelity from a generic HTML or text conversion path.

### D43 PDF pages to images and color conversion

**SumatraPDF — 3.7.** Page rendering supports selected ranges, output templates, resolution and formats such as PNG/JPEG/BMP/GIF. Recoloring a PDF to grayscale is a distinct operation from grayscale display or monochrome printing; the recolor tool also supports RGB and CMYK. [Convert to images](https://www.sumatrapdfreader.org/docs/Convert-PDF-to-images), [Grayscale PDF](https://www.sumatrapdfreader.org/docs/Convert-PDF-to-grayscale).

**Gravity implications.** Page-image export can reuse rendering with a separate memory-aware job path. Let users specify whether forms and annotations are included and ensure that unsaved edits are represented consistently. Put destructive color conversion behind an output-copy operation. Color-critical workflows need profile-aware verification before advanced print-production promises.

### D44 Compression and decompression

**SumatraPDF — 3.7.** Compression recipes rewrite PDF structure and streams; already compressed files may shrink little. Decompression is aimed at structural inspection and can substantially increase size. [Compress](https://www.sumatrapdfreader.org/docs/Tool-x-compress-pdf), [Decompress](https://www.sumatrapdfreader.org/docs/Tool-x-decompress-pdf).

**Gravity implications.** First offer lossless optimization with an estimated or measured before/after size. Do not replace a source with a larger result automatically. Keep lossy image downsampling as a separately named option. Rewriting must report signature consequences and preserve attachments, forms, metadata and tagging according to an explicit contract. Engine support must be proven before promising an optimization percentage.

### D45 Encryption and decryption

**SumatraPDF — 3.7 tools.** The documented UI and CLI can write a password-encrypted copy or remove encryption using an available password. The examples use AES-256 for new encryption; the lower-level tool exposes additional legacy choices. [Encrypt](https://www.sumatrapdfreader.org/docs/Tool-x-encrypt-pdf-with-password), [Decrypt](https://www.sumatrapdfreader.org/docs/Tool-x-decrypt-pdf).

**Gravity implications.** Add explicit output encryption policy rather than inferring it from whether an input needed a password. Avoid storing passwords in settings or diagnostic output. Distinguish an open password from permission restrictions. Test save/reopen and interoperability with both backends. Keep obsolete encryption algorithms out of the default UI.

### D46 Repair and diagnostics

**SumatraPDF — 3.7 tool workflows.** Rewriting may repair damaged cross-reference structure, and surviving pages can be extracted. Repair cannot reconstruct bytes that were never present. The documentation recommends opening and inspecting the result rather than treating successful serialization as complete recovery. [Repair a PDF](https://www.sumatrapdfreader.org/docs/Repair-a-damaged-PDF).

**Gravity implications.** Provide an explicit recovery-copy workflow only after engine feasibility is established. Report recovered page counts and unresolved objects. Do not overwrite a damaged source during attempted repair. A read-only diagnostic report with engine warnings and format information can arrive earlier and help users distinguish unsupported, encrypted and genuinely malformed files.

### D47 Printing

**SumatraPDF — mixed.** Printing supports page ranges, current page and rectangular selections, with scaling, odd/even filtering, rotation, paper/tray choices, copies, collation, duplex and monochrome controls. The newer Windows 11 dialog differs from the classic dialog. SumatraPDF's documented print pipeline is raster-based; it does not establish a vector-printing advantage over Gravity. [Printing](https://www.sumatrapdfreader.org/docs/Printing), [Selected pages](https://www.sumatrapdfreader.org/docs/Print-selected-pages).

**Gravity implications.** Prioritize a predictable preview, actual-size output, fit/shrink distinctions, arbitrary ranges and selected pages. Test mixed dimensions, printer margins, large engineering pages, barcodes and current-page handling. Vector printing can be investigated, but is not required merely to match SumatraPDF's documented architecture. A submitted print job is not proof that physical printing completed.

### D48 Poster printing

**SumatraPDF — 3.7 with a documented defect.** Poster output divides a large page into tiles with optional overlap. The `poster` page reports a malformed-page-tree failure in the current prerelease and points to a script workaround. [Poster tool](https://www.sumatrapdfreader.org/docs/Tool-poster), [Poster recipe](https://www.sumatrapdfreader.org/docs/Print-PDF-as-poster).

**Gravity implications.** Defer until ordinary printing is accurate. A useful implementation needs scale, sheet count, overlap, tile order and assembly marks, with a full preview. Validate real dimensions rather than just checking that a multi-page PDF was produced. Do not treat the documented Sumatra command as a verified reference implementation for this behavior.

### D49 Temporary documents

**SumatraPDF — 3.7.** Files opened from temporary locations can be copied to a durable folder with duplicate detection and collision-safe names; reading history follows the retained copy. The documented feature can subsequently copy automatically once configured. [Temporary files](https://www.sumatrapdfreader.org/docs/Handling-temporary-files).

**Gravity implications.** Detect likely temporary attachments and offer Keep a Copy before the location disappears. Preserve unsaved document state and distinguish copying from moving the original. Retention should be an explicit preference with a clear destination. Never interpret a temporary-file warning as permission to silently upload the document to cloud storage.

### D50 Read aloud

**SumatraPDF — 3.7.** Speech can start from a selection, viewport or cursor, with voice/speed controls, pause/resume/stop and follow-along highlighting. Switching tabs has defined session behavior. It uses installed Windows voices rather than requiring a hosted text-to-speech service. [Read aloud](https://www.sumatrapdfreader.org/docs/Read-Aloud).

**Gravity implications.** Build on the accessible text model and platform speech APIs. Preserve selection scope, expose progress and stop promptly. Speech should not restart from the beginning after an incidental UI change. Test reading order, pronunciation, long documents and documents without text. Keep optional cloud voices separate from the default local feature.

### D51 AI document assistance

**SumatraPDF — 3.7.** The page documents PDF chat through installed agent CLIs and a custom compatible API. Conversations can be per tab, with cancellation and backend-specific controls. The custom API path can optionally include current-page text. This means local process execution is not proof that document data stays local; the selected provider and action matter. [AI chat](https://www.sumatrapdfreader.org/docs/AI-Chat-with-document).

**Gravity implications.** Keep AI optional and deferred until extraction, page citations and local document operations are reliable. Show exactly which pages or selection will be supplied, retain permission boundaries, and treat document text as untrusted content. The first useful scope is read-only questions with clickable page evidence. Do not couple basic reading to an account, subscription or provider installation.

## Commands and customization

### D52 Command discovery and keyboard control

**SumatraPDF — mixed.** The palette searches actions and has modes for tabs, history, thumbnails, outlines, favorites, annotations and settings. Shortcut customization can rebind or disable defaults, supply arguments and add named actions; newer releases include global bindings. [Command palette](https://www.sumatrapdfreader.org/docs/Command-Palette), [Customize shortcuts](https://www.sumatrapdfreader.org/docs/Customize-keyboard-shortcuts), [Keyboard shortcuts](https://www.sumatrapdfreader.org/docs/Keyboard-shortcuts).

**Gravity implications.** Introduce a command registry shared by menus, toolbars, shortcuts and the palette. Each command needs a stable identifier, label, enablement rule, help text and scope. Preserve platform conventions: a document command must not consume typing inside a form field. A shortcut editor should detect collisions and provide Reset. Global shortcuts are optional later functionality, not a prerequisite for local customization.

### D53 Toolbars and contextual controls

**SumatraPDF — mixed.** Toolbar layout can be customized, commands can receive text/SVG buttons, and newer modes include hidden or overlay toolbars. Selection handlers can appear beside a selection or in the main toolbar. Controls may be conditional on document capabilities. [Customize toolbar](https://www.sumatrapdfreader.org/docs/Customize-toolbar), [Selection handlers](https://www.sumatrapdfreader.org/docs/Customize-search-translation-services).

**Gravity implications.** Begin with a small configurable primary toolbar and clear reading/editing contexts. Do not move essential Save or cancellation controls into unpredictable hover-only UI. Use the same command enablement as menus and shortcuts. Keep advanced customization in preferences while ensuring the default layout supports the entire core workflow.

### D54 Themes and document colors

**SumatraPDF — mixed.** Application themes control chrome; document-color transformations are separate. The docs describe smart recoloring that can preserve images, legacy behavior and temporary inversion. Themes include control, tab, notification and hover-preview colors. [Themes](https://www.sumatrapdfreader.org/docs/Customize-theme-colors).

**Gravity implications.** Start with light, dark, system and high-contrast-compatible UI. Keep original document colors as the default for printing and export. Offer a clearly named reading-color mode with an easy reset. Test image-heavy pages, transparent artwork, annotation contrast, focus indicators and form-field text before shipping a dark mode that only looks correct on plain white pages.

### D55 Settings and persistence

**SumatraPDF — mixed.** Settings cover global defaults, per-document state and restored sessions. They also include structured sections for format behavior, annotations, printing, external handlers and themes. The docs warn that some changes require reopening or restarting and that malformed structure can affect later data. [Advanced settings](https://www.sumatrapdfreader.org/docs/Advanced-options-settings), [Settings storage](https://www.sumatrapdfreader.org/docs/How-we-store-settings).

**Gravity implications.** Keep a small versioned settings schema with validation and atomic persistence. Separate preferences, recent history and recovery state. Document precedence: defaults for new files, remembered overrides for reopened files and explicit session changes. Provide Clear History, disable history and reset-one-document actions. Store secrets outside ordinary preferences if optional integrations eventually require them.

### D56 External applications and selected-text services

**SumatraPDF — mixed.** External viewers can receive the current file, page and folder through configurable arguments. Selection handlers can launch web services, send requests or run a local program. These are explicit outbound actions, with different capabilities from in-document search. [External viewers](https://www.sumatrapdfreader.org/docs/Customize-external-viewers), [Search and translation](https://www.sumatrapdfreader.org/docs/Customize-search-translation-services).

**Gravity implications.** A simple Open With command is a useful compatibility escape hatch. Later templates should use structured executable/argument handling and correct escaping. Clearly identify when selected text leaves the application. Keep network requests, local program launches and document links behind one enforceable policy layer. Avoid copying examples that embed credentials in settings.

### D57 The wider command surface

The command reference contains features beyond the top-level how-tos: no-history opening, URL opening, in-app folder browsing, recent-file cleanup, measurement, word counts, caret selection, keyboard link hints, automatic scrolling, reading bars, page grids, transparency backgrounds, image editing, shell registration, diagnostics and web-service linking. It also includes internal/deprecated commands. The [complete identifier index](SUMATRAPDF_REFERENCE_INDEX.md#command-identifiers) retains these distinctions and makes the less visible surface searchable. [Commands](https://www.sumatrapdfreader.org/docs/Commands).

For Gravity, classify this tail by user purpose:

| Purpose | Candidate behavior | Roadmap treatment |
| --- | --- | --- |
| Document privacy | Open without history, clear history, remove missing entries | Early settings work |
| Reading comfort | Caret browsing, reading bar, auto-scroll, progress | Accessibility and reading phase |
| File navigation | Next/previous PDF in folder, open folder, reopen closed | Navigation phase |
| Technical inspection | Measurement, grids, page boxes, hex display | Properties first; specialist tools later |
| Content reuse | Word count, region capture, image replacement | Export and annotation phases |
| Service integration | Search/translate, URL download, web linking | Optional explicit integrations |
| Engineering diagnostics | Logs, cache/render counters and stress controls | Developer-facing support tools |

The reference inventory is complete for the parsed command tables; it does not turn every listed internal action into a public Gravity requirement. In particular, crash-trigger and memory-corruption diagnostics have no place in an ordinary user command palette.

## Automation and deployment

### D58 Viewer command line

**SumatraPDF — mixed.** Launch arguments control files/windows, page or destination, search, view/zoom/scroll, presentation, settings location, restrictions and printing. Additional options cover testing, logging, preview integration and compatibility syntax. Unknown options can be interpreted as filenames, so callers must know the grammar. [Command-line arguments](https://www.sumatrapdfreader.org/docs/Command-line-arguments), [Open at a location](https://www.sumatrapdfreader.org/docs/Open-PDF-at-page-from-command-line).

**Gravity implications.** Extend its current file, page and new-window arguments with a strict parser, `--help`, documented errors and explicit view/search options. Keep GUI launch distinct from a future console tool. Public automation should not expose test-only flags as supported stable interfaces. Use explicit error behavior rather than accepting misspelled switches as mysterious file failures.

### D59 DDE and application control

**SumatraPDF — established with extensions.** DDE can open files, navigate, search, set views and support forward search. The reference also includes state/open-file/mouse queries and named commands. This is a Windows integration surface. [DDE commands](https://www.sumatrapdfreader.org/docs/DDE-Commands).

**Gravity implications.** Do not adopt DDE merely to reproduce legacy breadth. Extend the existing per-user instance transport with a versioned, validated command protocol if needed. Add DDE only when a specific integration requires it. Separate read-only state requests from mutation commands, limit message sizes and require appropriate local-user access.

### D60 LaTeX and generated PDFs

**SumatraPDF — mixed.** SyncTeX provides source-to-PDF and PDF-to-source navigation, and file reload supports iterative compilation. Editor recipes cover multiple tools, while WSL integration addresses path translation. Typst live reload is described separately from SyncTeX support. [LaTeX integration](https://www.sumatrapdfreader.org/docs/LaTeX-integration), [WSL integration](https://www.sumatrapdfreader.org/docs/LaTeX-integration-wsl).

**Gravity implications.** Reliable reload and view preservation come first. Add SyncTeX as a focused authoring integration once reload is dependable on both platforms. Test atomic compiler replacement, partial writes, spaces/Unicode in paths and a document with unsaved annotations. Do not discard user edits because an external compiler wrote a new version.

### D61 Restricted use

**SumatraPDF — mixed.** A restriction file or launch mode can disable file/disk actions, links, printing, settings persistence, updates, clipboard access and other features. Unspecified policies in the restriction file default off; AI has an explicit policy. [Restricted use](https://www.sumatrapdfreader.org/docs/Configure-for-restricted-use).

**Gravity implications.** A capability-policy layer can serve kiosks, enterprise deployment and privacy preferences. Enforce policy at the operation layer as well as hiding UI, so a shortcut or CLI call cannot bypass it. Do not market a disabled menu as an operating-system sandbox. Document which behaviors are policy-controlled and which rely on OS process isolation.

### D62 Batch processing

**SumatraPDF — 3.7 tool workflows.** The batch page composes command-line utilities through shell loops; grep accepts multiple files directly. Batch processing therefore does not necessarily mean a graphical job manager. [Batch processing](https://www.sumatrapdfreader.org/docs/Batch-process-PDFs).

**Gravity implications.** Offer a console interface first for deterministic operations, then a small job queue if users need it. Require explicit inputs and destinations, stable exit codes, per-file results and collision handling. A batch run should never accidentally include outputs it has just created. Cancellation must not leave a destination that appears complete when it is only partially written.

### D63 A separate cross-platform CLI

**SumatraPDF — separately documented availability.** `SumatraPDF cli` lists reading commands for information, text, Markdown, search, outline, annotations, images, verification and rendering; editing commands cover page selection/order/rotation, merge/split, images to PDF, annotation, signing, compression, encryption, watermarking and numbering. JSON output, environment-based passwords and explicit output destinations are described. The page says macOS/Linux, with Windows forthcoming. [CLI reference](https://www.sumatrapdfreader.org/docs/Command-line-for-scripts-and-AI-agents).

**Gravity implications.** A future `gravity` console contract should expose document operations independently of GUI state. JSON output and schema versions are more useful than automating screen coordinates. Keep sensitive input out of command-line logs where possible. Reuse the same validation and serialization rules as the GUI, and publish platform capability differences rather than letting commands silently vary.

## Command line tools and scripting

The following is the separate MuPDF-based `sumatrapdf-tool` family, documented for 3.7 prerelease. It is not the same surface as `SumatraPDF cli`, and it does not automatically support every format the graphical reader opens. A GUI-subsystem executable and a console executable also differ when used in pipelines. [Tools overview](https://www.sumatrapdfreader.org/docs/Tools).

### D64 Tool inventory

| Tool | Documented function and useful controls | Important boundary | Gravity relevance |
| --- | --- | --- | --- |
| [draw](https://www.sumatrapdfreader.org/docs/Tool-draw) | Render/convert pages; choose format, range, DPI, size, rotation, page box, color handling and structured text; OCR modes | OCR needs language data; raster output and text-preserving output have different consequences | Page export, text export and later OCR |
| [convert](https://www.sumatrapdfreader.org/docs/Tool-convert) | Convert to raster, vector, print and text formats; format-specific writer options; reflow layout controls | Output extension alone is not a fidelity guarantee | A unified conversion job model |
| [audit](https://www.sumatrapdfreader.org/docs/Tool-audit) | HTML report on object space and graphical-operator usage | Approximate structural diagnostics, not a document-security certification | Advanced diagnostics only |
| [bake](https://www.sumatrapdfreader.org/docs/Tool-bake) | Flatten forms and annotations, optionally keeping either category interactive | Changes interactive structure and can affect signatures | Flattened export |
| [clean](https://www.sumatrapdfreader.org/docs/Tool-clean) | Rewrite, select pages, collect unused objects, deduplicate, compress, decompress and alter encryption | Broad low-level rewrite with preservation consequences | Separate explicit operations, not one vague Fix button |
| [extract](https://www.sumatrapdfreader.org/docs/Tool-extract) | Export embedded images, fonts and attachments, optionally by object number | Asset extraction differs from rendering a whole page | Asset/export workflow |
| [info](https://www.sumatrapdfreader.org/docs/Tool-info) | Inspect fonts, images, dimensions, patterns, shadings, XObjects and supported invoice information | Reports structure; does not prove visual correctness | Properties and diagnostics |
| [merge](https://www.sumatrapdfreader.org/docs/Tool-merge) | Assemble ranges from multiple PDFs with output writer options | Must validate destination, outline and form preservation | Existing organizer plus batch merge |
| [pages](https://www.sumatrapdfreader.org/docs/Tool-pages) | Emit page-box, rotation and unit information in XML | Metadata inspection, not page editing by itself | Properties and test diagnostics |
| [poster](https://www.sumatrapdfreader.org/docs/Tool-poster) | Divide pages into a grid of printable tiles with overlap | Documentation reports a current prerelease failure | Defer until an independently tested implementation exists |
| [recolor](https://www.sumatrapdfreader.org/docs/Tool-recolor) | Write grayscale, RGB or CMYK content; optionally remove output intents | Alters document colors, unlike view-only color modes | Optional conversion |
| [show](https://www.sumatrapdfreader.org/docs/Tool-show) | Inspect dictionaries, streams, xref, outlines, scripts and forms using object paths | Technical inspection may expose hidden content | Developer diagnostics |
| [sign](https://www.sumatrapdfreader.org/docs/Tool-sign) | List, verify, clear or sign existing signature fields using a certificate file | Does not add a new field; narrower validation than the GUI | Dedicated signature capability investigation |
| [trim](https://www.sumatrapdfreader.org/docs/Tool-trim) | Remove content inside/outside a page box with margins | Does not itself resize pages; docs disagree with usage text about the default box | Never conflate with simple cropping |
| [grep](https://www.sumatrapdfreader.org/docs/Tool-grep) | Search one or more files with case/diacritic options, regex, page/file reporting and text options | Search normalization and reading order affect matches | Search CLI after extraction foundations |
| [trace](https://www.sumatrapdfreader.org/docs/Tool-trace) | XML stream of drawing-device operations for selected pages | Output can be very large and is primarily diagnostic | Optional developer tooling |
| [run](https://www.sumatrapdfreader.org/docs/Tool-run) | Execute JavaScript with document/render/edit APIs, arguments, I/O and interactive modes | A programmable execution surface, not ordinary PDF viewing | Defer general scripting; first ship structured safe operations |
| [barcode](https://www.sumatrapdfreader.org/docs/Tool-barcode) | Create barcodes/QR codes and detect them in documents or images | Separate decoding/encoding dependency and fidelity requirements | Specialist optional feature |

### D65 Script dialog and examples

**SumatraPDF — 3.7.** The Run PDF Script dialog offers an editor, argument controls, examples and captured output. Example metadata supplies argument defaults; scripts operate on the saved input file, so unsaved annotations are not automatically included. The example library spans creation, annotations, extraction, rendering, page edits, outlines, forms and low-level operations. [Run PDF Script](https://www.sumatrapdfreader.org/docs/Run-PDF-script), [Examples](https://www.sumatrapdfreader.org/docs/Tool-run-javascript-examples).

**Gravity implications.** Most users benefit more from named operations with a preview than from an arbitrary script runner. If scripting is adopted, define whether scripts see the saved file or current edited state, expose an explicit output location and isolate execution. Never let code embedded in a PDF become an implicitly trusted application script. Keep the full example filenames in the reference index for engineering discovery without importing their code.

### D66 JavaScript API families

The documented API belongs to MuPDF's scripting model, not Gravity's PDFium bindings or PDFKit model. The reference index lists its 664 documented signature entries without incorporating implementation code. [JavaScript API](https://www.sumatrapdfreader.org/docs/Tool-run-javascript-reference).

| API family | Purpose | Architecture question for Gravity |
| --- | --- | --- |
| Document and Page | Open, inspect, paginate, search, navigate and render documents | Which operations can share a platform-neutral interface? |
| PDFDocument and PDFPage | PDF-specific creation, editing, serialization and page operations | Which changes require native-engine support or another component? |
| PDFObject and PDFGraftMap | Low-level object manipulation and copying across documents | How are object identity and shared resources preserved? |
| PDFAnnotation and PDFWidget | Markup, appearance, field and widget operations | Can the edit model preserve unsupported subtypes and shared fields? |
| StructuredText and walkers | Text with geometry and structured traversal | What stable extraction schema should Gravity own? |
| Pixmap, Image and ColorSpace | Pixel data, image handling and color operations | How are memory limits, alpha and profiles handled? |
| Device, DisplayList and writers | Replay drawing operations, render or create output | Can jobs be cancelled and isolated from interactive rendering? |
| Path, Text, Font and Shade | Drawing primitives and typography | Which advanced authoring features are worth supporting? |
| Matrix, Point, Rect and Quad | Geometry and transforms | Can all overlays and edits use one tested transform contract? |
| Story and DOM | Flowing HTML/CSS-based content | Is reflow a separate product capability? |
| Archive, Buffer and related types | Input resources, byte storage and archives | How are untrusted inputs bounded and validated? |
| Outline and link types | Navigation structure and destinations | How do page edits remap references? |

The lesson is the separation of rendering, document semantics and output operations. Gravity does not need to recreate this entire API to provide a useful command line. A small, stable set of operations with strong tests is a more realistic first surface.

## Support and privacy

### D67 Diagnostics and maintenance

SumatraPDF documents bug reproduction, crash reports, print-driver diagnosis, delayed update availability and missing/mismatched engine libraries. Its debugging reference describes logs and debugger-based investigation. These support workflows are part of a mature product, even though they are not reading tools. [Bug reports](https://www.sumatrapdfreader.org/docs/How-to-submit-bug-reports), [Crash reports](https://www.sumatrapdfreader.org/docs/Submit-crash-report), [Printing bugs](https://www.sumatrapdfreader.org/docs/Reporting-printing-bugs), [Updates](https://www.sumatrapdfreader.org/docs/Update-check-doesnt-work), [DLL failures](https://www.sumatrapdfreader.org/docs/Failed-to-load-libmupdf), [Corrupted installations](https://www.sumatrapdfreader.org/docs/Corrupted-installation), [Debugging](https://www.sumatrapdfreader.org/docs/Debugging-Sumatra).

**Gravity implications.** Add a copyable diagnostic summary containing application/engine versions and non-sensitive capability information. Keep document paths and content out by default. Distinguish rendering, save, permission and printer failures. Provide a user-controlled report preview and a fixture-based reproduction guide. Automatic engine updates need compatibility tests and rollback design before they are shipped.

### D68 Localization and help

The translation workflow addresses translated strings, menu accelerators and placeholders. The documentation site also provides short feature videos and an FAQ linking common problems to focused explanations. [Translation](https://www.sumatrapdfreader.org/docs/Contribute-translation), [Feature videos](https://www.sumatrapdfreader.org/docs/Features), [FAQ](https://www.sumatrapdfreader.org/docs/FAQ).

**Gravity implications.** Extract user-visible strings before the command surface grows much further. Preserve placeholders and pluralization, and test long labels and right-to-left UI independently from right-to-left document layout. Help should describe the controls actually shipped on that platform. Small task-based examples are more useful than a feature list alone.

### D69 Privacy behavior

The privacy policy describes local document processing but also update requests, automatic crash reports in official builds, and explicitly invoked external services. It states that crash dumps can contain document fragments. AI and selection services have their own data flows; Mac update requests include a machine-derived identifier. The policy's broad opening claim must be interpreted alongside these detailed exceptions. [Privacy policy](https://www.sumatrapdfreader.org/docs/Privacy-policy).

**Gravity implications.** Define local-only core behavior, clear history controls, explicit network actions and consented diagnostic export. A crash report should never be assumed content-free. Separate update checks, analytics, crash uploads, signature trust checks and optional AI in both implementation and settings. This is a concrete product design requirement, not a reason to block normal local reading or editing.

## Comparison conclusions

### What Gravity should retain

Gravity's page editing model, persistent undo on Windows, adjacent page panel, familiar save shortcuts and native Mac direction are worth preserving. They are not throwaway scaffolding for a larger clone. The product can cover common PDF work with fewer surfaces than SumatraPDF while still borrowing its best navigation and discoverability ideas.

The baseline also makes several proposed additions inexpensive in concept: region export reuses rendering; plain-text export reuses extraction; a command palette can expose existing actions; personal reading marks extend persistence. Each still needs an explicit UX, cancellation rules and tests to become a finished feature.

### What needs deeper engineering

Annotation editing, reliable form serialization, PDF preservation, signature workflows, secure redaction and consistent Windows/Mac behavior are not mainly menu work. They depend on native engine capabilities, object relationships and careful saving. A roadmap that places all of them in a single short feature sprint would be misleading.

The Windows bitmap cache limit does not establish a total-memory ceiling. Form snapshots and Mac serialization-based undo can make long editing sessions expensive. Performance work should therefore include document memory, undo retention and native-resource lifetimes, not only smooth scrolling.

### Important source caveats

| Topic | Interpretation to preserve |
| --- | --- |
| Stable versus prerelease | Stable Windows is 3.6.1; many pages describe 3.7 behavior |
| Multiple CLIs | `sumatrapdf-tool` and `SumatraPDF cli` have different scope and platform availability |
| Poster tool | The documentation reports a known failure and a workaround |
| Trim default | The explanatory text says CropBox while the embedded usage says MediaBox |
| Signature verification | GUI trust checks and CLI reports are not equivalent |
| Forms | Calculations in SumatraPDF do not establish XFA or arbitrary form compatibility |
| Office formats | One extraction page mentions Office, but the main format page does not establish comprehensive Office fidelity |
| Watermark and numbering | Recipes emphasize scripts; command tables also list direct actions |
| Printing | SumatraPDF itself documents raster printing |
| Privacy | Local viewing coexists with specific automatic and user-invoked network flows |
| Mac/Linux availability | Platform pages and CLI pages describe different stages and surfaces |

### Recommended opportunity order

1. **Protect documents and establish the platform baseline.** Fix the save fallback, test preservation, bound memory and validate the Mac build/runtime path.
2. **Make existing features easier to find and use.** Command registry/palette, search results, properties, favorites, recent/session controls and accessible navigation.
3. **Complete common PDF tasks.** Annotation editing, form polish, crop, split, images to PDF, text/image exports and flattening.
4. **Add repeatable operations.** Precise printing, structured CLI, batch jobs and authoring integrations.
5. **Add specialist capabilities behind evidence-based gates.** Redaction, verification/signing, OCR, layers and document optimization.
6. **Choose optional expansion deliberately.** Additional formats, engineering tools, general scripting and AI should have demonstrated demand and an explicit maintenance budget.

The [roadmap](GRAVITY_ROADMAP.md) supplies the dependencies, acceptance criteria, effort ranges and decisions for this sequence. The [reference index](SUMATRAPDF_REFERENCE_INDEX.md) supplies complete source coverage and the detailed identifiers that would otherwise overwhelm the product comparison.
