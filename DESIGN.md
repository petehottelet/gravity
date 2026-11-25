---
name: Gravity for Windows
description: The approved document-first Lucide interface, implemented for native Windows PDF reading and organization.
colors:
  ink: '#222831'
  muted: '#414b57'
  accent: '#174b8b'
  accent-soft: '#e2ecf8'
  chrome: '#f7f8fa'
  canvas: '#e8ebef'
  paper: '#ffffff'
  rule: '#cdd3db'
  control-rule: '#7c8795'
  hover: '#e2e8f0'
  pressed: '#d4e1f1'
  switch-track: '#e5e8ed'
  status: '#f2f4f7'
  tab-hover: '#ecedef'
  modified: '#d96c00'
  text-selection: '#1a73e850'
  search-hit: '#ffb00078'
  navigation-selected: '#e2eaf5'
  chapter-selected-ink: '#153e72'
  tile-selected: '#dce8f7'
  window-close-hover: '#c42b1c'
typography:
  brand:
    fontFamily: Segoe UI
    fontSize: 11.25pt
    fontWeight: 700
  title:
    fontFamily: Segoe UI Semibold
    fontSize: 9.75pt
    fontWeight: 600
  body:
    fontFamily: Segoe UI
    fontSize: 9pt
    fontWeight: 400
  control:
    fontFamily: Segoe UI
    fontSize: 9.5pt
    fontWeight: 400
  mode:
    fontFamily: Segoe UI Semibold
    fontSize: 9.75pt
    fontWeight: 600
  label:
    fontFamily: Segoe UI Semibold
    fontSize: 9pt
    fontWeight: 600
  caption:
    fontFamily: Segoe UI
    fontSize: 8.25pt
    fontWeight: 400
  navigation-title:
    fontFamily: Segoe UI Semibold
    fontSize: 8.25pt
    fontWeight: 600
  status:
    fontFamily: Segoe UI
    fontSize: 8.5pt
    fontWeight: 400
rounded:
  field: 5px
  control: 6px
  group: 8px
  tab: 7px
spacing:
  item-margin: 2px
  group-inset: 3px
  tight: 4px
  control: 8px
  label: 12px
  navigation: 16px
  panel: 20px
  workspace: 24px
  grid-row: 26px
components:
  button-ghost:
    backgroundColor: transparent
    textColor: '{colors.ink}'
    typography: '{typography.control}'
    rounded: '{rounded.control}'
    padding: 0 8px
    height: 44px
  button-save:
    backgroundColor: transparent
    textColor: '{colors.ink}'
    typography: '{typography.control}'
    rounded: '{rounded.control}'
    padding: 0 8px
    height: 44px
    width: 86px
  button-hover:
    backgroundColor: '{colors.hover}'
    textColor: '{colors.ink}'
    rounded: '{rounded.control}'
  button-pressed:
    backgroundColor: '{colors.pressed}'
    textColor: '{colors.ink}'
    rounded: '{rounded.control}'
  mode-selected:
    backgroundColor: '{colors.paper}'
    textColor: '{colors.accent}'
    typography: '{typography.mode}'
    rounded: '{rounded.control}'
    padding: 0 8px
    height: 38px
    width: 116px
  navigation-selected:
    backgroundColor: '{colors.navigation-selected}'
    textColor: '{colors.accent}'
    typography: '{typography.label}'
    rounded: '{rounded.field}'
    height: 36px
    width: 96px
  outline-selected:
    backgroundColor: '{colors.accent-soft}'
    textColor: '{colors.chapter-selected-ink}'
    typography: '{typography.label}'
    rounded: '{rounded.control}'
    height: 36px
  page-field:
    backgroundColor: '{colors.paper}'
    textColor: '{colors.ink}'
    typography: '{typography.body}'
    rounded: '{rounded.field}'
    height: 32px
    width: 44px
  page-selected:
    backgroundColor: '{colors.tile-selected}'
    textColor: '{colors.ink}'
    typography: '{typography.body}'
    rounded: '{rounded.control}'
    width: 150px
    height: 190px
  tab-selected:
    backgroundColor: '{colors.paper}'
    textColor: '{colors.ink}'
    typography: '{typography.body}'
    rounded: '{rounded.tab}'
  zoom-choice:
    backgroundColor: transparent
    textColor: '{colors.ink}'
    rounded: '{rounded.control}'
    height: 44px
    width: 104px
  window-button:
    backgroundColor: '{colors.chrome}'
    textColor: '{colors.muted}'
    height: 34px
    width: 46px
  window-button-hover:
    backgroundColor: '{colors.hover}'
    textColor: '{colors.muted}'
  window-close-hover:
    backgroundColor: '{colors.window-close-hover}'
    textColor: '{colors.paper}'
---

# Design System: Gravity for Windows

## Overview

**Creative North Star: "Document-first clarity"**

The approved interface uses quiet chrome, a centered document identity, and white paper on a cool canvas. The real G artwork anchors the header, with flat window controls integrated at the right. Equal Read and Organize controls keep the task visible; hierarchy, spacing, restrained blue selection, and Lucide line icons make the document the focus.

The visual authority is the selected `docs/ui-draft/draft.html` and `docs/ui-draft/draft.css`, together with the approved reference image. Windows implementation choices preserve that composition and its visual distinctions while retaining real PDF operations, native keyboard and text-editing behavior, document tabs, menus, and DPI scaling. An implementation difference is not a new visual rule. Sample filenames, chapter titles, and document typography are never copied into real document metadata.

**Key Characteristics:**

- Real G artwork, a centered live filename and save state, and integrated flat window controls.
- A white selected mode on a softly rounded gray track.
- Numbered outline navigation with document metadata and a position footer.
- Softly lifted paper and external thumbnail captions.
- Primary organization actions remain visible when controls wrap.

## Colors

Restrained blue communicates action and selection against cool neutrals. Keep the reference's distinct selection surfaces; one pale blue does not replace every selected state. Sidecar tonal ramps are preview references, not additional implementation tokens.

### Primary

- **Navigation blue (`accent`):** mode text and icon, actions, focus, page selection boundaries, and check badges.
- **Outline selection wash (`accent-soft`):** selected outline row.
- **Navigation selection (`navigation-selected`):** selected Outline/Pages control.
- **Chapter blue (`chapter-selected-ink`):** selected outline title and its page number.
- **Tile selection (`tile-selected`):** the selected thumbnail frame, excluding its external caption.
- **Text selection:** translucent blue over selected PDF text.

### Tertiary

- **Modified:** unsaved-document indicator in the optional document tab row.
- **Window close hover:** red feedback only under the Close window control, paired with a white icon.
- **Search hit:** translucent amber over matched PDF text.

### Neutral

- **Ink / Muted:** primary interface labels / metadata and supporting text.
- **Chrome / Canvas / Paper:** application controls and sidebar / reading and organizing workspace / document pages and the selected mode.
- **Rule / Control rule:** full-width structural dividers / Save and page-field boundaries.
- **Hover / Pressed / Switch track:** control feedback and the rounded mode track.
- **Status / Tab hover:** the bottom status surface and optional inactive-tab feedback.

**The State Color Rule.** Preserve the separate mode, navigation, outline, and thumbnail treatments instead of applying one selected fill everywhere.

Strong text contrast, including a 7:1 target for ordinary interface text, guides the palette. This is not a claim of full AAA or other accessibility conformance. Native disabled text follows the Windows system color.

## Typography

Segoe UI implements the reference's system sans typography on Windows, with Semibold for document identity, navigation, selection count, and page numbers. Point sizes are recorded in the frontmatter; geometric dimensions are logical pixels at 96 DPI. Native text metrics determine line height.

- **Brand:** compact bold Gravity wordmark beside the actual G artwork.
- **Title:** centered real filename; the smaller caption beneath it reports page count and Saved or Unsaved changes.
- **Body:** outline titles, page field, menus, and optional document tabs.
- **Control / Mode:** task actions / stronger Read and Organize labels.
- **Label:** sidebar tabs, selected outline rows, page numbers, and selection summary.
- **Caption:** page titles, document metadata, header state, and sidebar footer.
- **Status:** bottom position and task guidance.

Long filenames and outline titles use ellipsis. Organizer captions pair a stronger page number with a smaller muted title from the real PDF bookmarks; pages without a bookmark use a page-number fallback.

**The Document Type Rule.** Preserve the PDF's typography and real metadata; sample-page fonts and sample chapter names never define the application interface.

## Layout

The header is 55px high. Its left identity uses the actual G artwork at 23px, inset 20px, followed by Gravity. The live filename and page-count/save-state stack are centered independently of the left identity. Flat minimize, maximize/restore, and close controls occupy the top-right of this same header; the application-menu action sits immediately before them. Each window-control target is 46 × 34px, with a 10px line icon. The centered title reserves 202px at either side. A separate 34px document-tab row appears only when more than one document is open.

The main command bar is 60px high, with opening controls left, the 238 × 44px mode track centered, and Find, Undo, Print, and Save right. Its bottom rule spans the entire bar. The task-control row is 57px high with a full-width bottom rule; document status is 31px high. The application header also owns the window controls; there is no separate legacy title strip above it.

The sidebar defaults to 236px. Outline/Pages occupies a 58px row; a 42px metadata row pairs the filename without its extension with the page count. Outline rows are 36px high, with page numbers in a 16px column and 14px indentation per tree level. A 44px footer, separated by a top rule, combines a book icon with real section or page position. Pages uses this same sidebar without extra editing toolbars.

Reading preserves page proportions, with 24px outer padding and 40px page gaps outside presentation mode. Paper is centered in the available canvas and separated by a soft shadow. The organizer starts 26px below its command row, with 34px horizontal margins at comfortable widths. Default thumbnail frames are 150 × 190px; page imagery fits inside with an 11px inset. Captions begin 8px below the frame. Horizontal cells have at least 22px extra space, distributed across available width; each row allocates 32px for the caption and 26px additional separation. Sidebar frames are 125 × 150px with a 12px top inset and 18px separation after the caption.

### Narrow Windows

- Below 800px client width, the main bar becomes 106px high and the centered mode switch moves to a second row. Print leaves the primary row below 700px; its menu command remains available.
- The docked sidebar narrows through 210px below 1000px content width, 190px below 760px, and 160px below 600px, subject to the saved width and document-space constraint. The sidebar toggle remains available.
- Reading retains the rounded page field and visible minus, zoom choice, and plus controls. Previous/next buttons leave the row below 410px host width; the field and keyboard commands retain page navigation. Zoom controls tighten at that threshold.
- Organization controls wrap below 700px host width into a 98px row. The selection summary and Select all occupy the first line; Rotate, Duplicate, and Delete remain on the action line. Below 390px host width, Duplicate and Delete become labeled-accessible icon actions while Rotate retains its text.
- Earlier/later controls appear from 650px host width. More appears only in Organize below 800px host width, supplementing the visible primary actions. Reading and wide organizing do not carry a permanent More button.
- Organizer margins reduce to 12px below 650px host width; its grid recalculates columns while preserving page proportions and captions.

The window minimum is 520 × 380px. The sidebar can be hidden; it also hides below 420px content width or in fullscreen. The reader, outline, and page rail scroll independently. Most geometry uses DPI scaling; preserve native input, scrolling, focus, and keyboard behavior when matching the reference.

**The Visible Actions Rule.** Wrap the organizer's primary actions with the selection summary before reducing actions to overflow; More supplements narrow layouts.

## Elevation & Depth

Depth belongs to paper and the selected mode. Other chrome stays flat, with continuous thin dividers. The reference shadow vocabulary is selected mode (`0 1px 3px #202b3c22`), reading paper (`0 3px 12px #26384f24`), and page thumbnail (`0 2px 5px #23304427`). Use soft ambient separation, not hard page-edge strips or heavy card borders.

The native paper renderer builds a soft falloff from overlapping low-alpha shapes, using a 10px extent and 3px downward offset for reading paper, or 5px and 2px for thumbnails. The selected mode has a small downward shadow under its white fill. These rendering details serve the reference's depth, rather than defining a different visual language.

## Shapes

The page-number field and sidebar tabs use gently rounded corners from the field radius. Buttons, selected outline rows, and thumbnail frames use the control radius; the mode track uses the group radius. Paper stays square. Save and page-field boundaries are thin neutral strokes; a selected thumbnail frame has a 2px blue boundary and a 20px circular check at its top-right corner.

Lucide icons use a 24-unit view box with round caps and joins and a 1.75-unit stroke, generally displayed at 20px. Preserve the actual G artwork in `assets/g-mark.png` and the bundled icon assets and license in `third_party/lucide/`. The brand mark is not a substitute text glyph.

## Components

### Header, Window Controls, and Document Tabs

The header's real filename and page count/save state remain centered. Saved and Unsaved changes come from the open document, including documents not yet saved. The brand artwork is tinted to interface ink. The optional multi-document row retains native tab activation, close actions, tooltips, modification indicators, and accessible roles without occupying the single-document header.

Window controls are flat and borderless at rest. Minimize is a horizontal line; maximize is a square; restore uses overlapping corners; close is a diagonal cross. The icons use a one-pixel stroke scaled with DPI. Neutral hover fills the target, while Close uses the dedicated red hover fill with a white icon. Keyboard focus remains visible and each control exposes a named native button role. The maximize name and icon switch to Restore when maximized.

The integrated caption keeps native window dragging, double-click maximize/restore, system-menu behavior, and Snap hit testing. A 6px edge region preserves resize hit testing in the normal state, and maximized bounds respect the monitor work area. These behaviors belong to the window chrome, not to decorative controls.

### Buttons and Mode Switch

Main commands use 44px targets, with Open and Save at 86 × 44px. Save has a thin outline. Equal 116 × 38px Read and Organize buttons sit inside a track with 3px inset. The selected segment is white, with blue text and icon and a slight shadow; it is not the sidebar's pale-blue selected treatment.

Hover and pressed fills provide immediate feedback. Keyboard focus remains visible. Disabled commands do not activate. The 44px target applies to primary actions; existing smaller document-tab controls are not a new primary-target standard.

### Page and Zoom Controls

The page field is 44 × 32px, white, centered, and rounded, with a neutral border that changes to blue on focus. Its embedded native text editor preserves selection, caret, typing, Enter submission, and validation behavior.

Zoom uses separate unboxed minus and plus icons around an unboxed Fit page or percentage drop-down. The menu retains fit modes, presets, and a custom percentage text field. Do not restore the boxed combo-box appearance or magnifying-glass zoom icons in the main row.

### Outline and Pages

Outline/Pages is a separate sidebar choice. Current outline rows combine a pale wash, stronger dark-blue title, page number, and visible keyboard focus. Numbers map to actual document pages; indentation and disclosure preserve the PDF bookmark hierarchy. The metadata row and position footer complete the navigation context.

The Pages rail is a clean, single-column thumbnail navigator on chrome. It omits the organizer's editing bars and selection check badge. Selecting a page navigates reading; selecting Pages from Organize returns to reading with the rail visible.

### Page Selection and Captions

Selection surrounds only the thumbnail frame. The page number and real bookmark title remain outside it, below the image. A blue boundary, pale tile wash, and circular white check reinforce the selected state; a dotted inset distinguishes keyboard focus. The caption does not become part of a large selected card.

Real bookmark titles follow their page source through organization. A page without a bookmark uses Page plus its current number. Ctrl and Shift extend selection; arrows and Home/End move through pages; Space toggles; Enter or double-click reads the page. Dragging keeps its insertion marker and page-count feedback. Native list roles expose page names, selection, focus, and offscreen state.

### Organizer Actions and Status

The selection summary stacks the count over an underlined Select all action. Rotate, Duplicate, and Delete form the primary action group; a subtle vertical divider separates movement controls when shown. Narrow windows retain these actions by wrapping, with More reserved for additional narrow-organizer commands.

The lower status strip reports page position or selection count on the left. Reading reminds the user that changes require saving; organizing gives selection and reading guidance. Actual save state is reported in the centered header, with modification state also available in document tabs. Existing document operations, search, forms, undo/redo, file dialogs, and explicit saving retain their behavior.

### App Dialogs

Form filling, text and password prompts, combining files, export, image import, keyboard shortcuts, command search, and job progress share an integrated 56px heading with the main window's flat Close control. Chrome and action footers use the cool neutral surface; content stays white. Titles use Segoe UI Semibold at 12pt, with 9.5pt body text. Actions use 36px rounded buttons, a blue primary action, and a neutral outlined secondary action. A 72px footer keeps actions separate from scrollable content.

Text, choice, numeric, and list fields retain their native Windows editors inside rounded frames, with a blue focus boundary and a muted disabled surface. The form editor wraps long labels and scrolls the focused field into view when resized. Preserve Enter/Escape, multiline input, password masking, selection, read-only fields, native dialog dragging, and explicit Apply followed by Save.

## Do's and Don'ts

### Do:

- **Do** match the approved reference's composition, spacing, surfaces, and distinct selection states.
- **Do** use the real G artwork and live document metadata.
- **Do** integrate flat window controls into the application header while preserving native window behavior.
- **Do** preserve equal prominence for Read and Organize.
- **Do** keep primary organizer actions visible when the row wraps.
- **Do** place real page numbers and bookmark captions outside thumbnail selection frames.
- **Do** preserve Windows editing, keyboard, scrolling, accessibility roles, and PDF features.

### Don't:

- **Don't** treat implementation deviations as permission to replace the approved visual design.
- **Don't** use a blue selected mode, square page field, boxed zoom selector, or permanent More button.
- **Don't** add a separate legacy title strip or raised caption-button frames above the integrated header.
- **Don't** replace soft paper shadows with hard edge strips or heavy thumbnail borders.
- **Don't** add duplicate editing controls to the Pages rail.
- **Don't** copy sample filenames or chapter titles into actual document metadata.
- **Don't** infer full accessibility conformance from contrast targets or control styling.
