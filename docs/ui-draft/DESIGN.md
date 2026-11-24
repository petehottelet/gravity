---
name: Gravity UI draft
description: Document-first clarity for reading and organizing PDFs.
colors:
  ink: "#222831"
  muted: "#414b57"
  accent: "#174b8b"
  accent-soft: "#e2ecf8"
  chrome: "#f7f8fa"
  canvas: "#e8ebef"
  rule: "#cdd3db"
  control-rule: "#7c8795"
  paper: "#ffffff"
  hover: "#e2e8f0"
  active: "#d4e1f1"
  switch-track: "#e5e8ed"
  navigation-selected: "#e2eaf5"
  chapter-selected-ink: "#153e72"
  tile-selected: "#dce8f7"
typography:
  body:
    fontFamily: '-apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif'
    fontSize: "14px"
    fontWeight: 400
  title:
    fontFamily: '-apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif'
    fontSize: "13px"
    fontWeight: 600
    lineHeight: 1.4
  button:
    fontFamily: '-apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif'
    fontSize: "13px"
    fontWeight: 500
  label:
    fontFamily: '-apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif'
    fontSize: "12px"
    fontWeight: 600
  caption:
    fontFamily: '-apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif'
    fontSize: "11px"
    fontWeight: 400
rounded:
  field: "5px"
  control: "6px"
  group: "8px"
spacing:
  tight: "4px"
  control: "8px"
  label: "12px"
  group: "16px"
  panel: "20px"
  workspace: "24px"
components:
  button-ghost:
    backgroundColor: "transparent"
    textColor: "{colors.ink}"
    typography: "{typography.button}"
    rounded: "{rounded.control}"
    padding: "0 12px"
  button-save:
    backgroundColor: "transparent"
    textColor: "{colors.ink}"
    typography: "{typography.button}"
    rounded: "{rounded.control}"
    padding: "0 15px"
  mode-selected:
    backgroundColor: "{colors.paper}"
    textColor: "{colors.accent}"
    rounded: "{rounded.control}"
    padding: "0 16px"
  chapter-selected:
    backgroundColor: "{colors.accent-soft}"
    textColor: "{colors.chapter-selected-ink}"
    rounded: "{rounded.control}"
    padding: "6px 10px"
  search-field:
    textColor: "{colors.ink}"
    rounded: "{rounded.field}"
    padding: "8px 10px"
    width: "270px"
  page-selection:
    backgroundColor: "{colors.tile-selected}"
    rounded: "{rounded.control}"
    padding: "9px"
    width: "150px"
    height: "190px"
---

# Design System: Gravity UI draft

## Overview

**Creative North Star: "Document-first clarity"**

The selected first redesign combines Apple-inspired clarity, Lucide line icons, and Tufte's restraint. Cool neutral chrome frames the document; blue identifies selection and action. Reading and organizing have equal prominence in a persistent mode switch.

This system describes `draft.html`, `draft.css`, and `draft.js`. The comparison and styling-only alternate are studies, not token authorities. This is a browser prototype; native integration and full accessibility conformance remain outside its demonstrated scope.

**Key Characteristics:**

- Quiet chrome around a white document surface.
- Compact labels and explicit Read/Organize modes.
- Outline and page thumbnails share one navigation sidebar.
- Color, borders, checks, and semantic states reinforce selection.

## Colors

One restrained blue accent sits within a cool neutral palette.

### Primary

- **Navigation blue (`accent`):** active modes, selection borders, focus outlines, links, and check backgrounds.
- **Selection wash (`accent-soft`):** current outline row; related navigation and thumbnail washes distinguish their selected surfaces.
- **Chapter blue (`chapter-selected-ink`):** current outline text and page number.

### Neutral

- **Ink / Muted:** primary interface text / supporting labels and metadata.
- **Chrome / Canvas / Paper:** tool surfaces / document workspace / page and selected mode surfaces.
- **Rule / Control rule:** quiet structural dividers / stronger field and outlined-button boundaries.
- **Hover / Active / Switch track:** interaction feedback and segmented-control grouping.

Measured primary text pairs are ink on chrome (13.96:1), muted on canvas (7.41:1), blue on selection wash (7.28:1), and blue on white (8.69:1). These measurements are not a complete accessibility certification.

**The State Color Rule.** Use blue to communicate action, focus, and selection; keep the surrounding workspace neutral.

## Typography

The platform system sans stack serves interface text. There is no separate display family or proportional type scale. Document imagery retains its own typography; Georgia appears only in the sample-page fallback and does not define application chrome.

### Hierarchy

- **Body:** base interface text.
- **Title:** document name, with smaller supporting save state.
- **Button:** text actions; mode labels use a slightly stronger weight (550).
- **Label:** navigation tabs, page labels, and selection count; chapter rows use regular weight, increasing to 650 when current.
- **Caption:** metadata, status, and secondary page labels.

Tabular numerals keep page counts and numbers aligned. The compact caption size is an observed prototype choice, not proof of legibility at every scaling setting.

## Layout

A viewport-height flex shell contains a title bar (55px), toolbar, flexible body, and status strip. The desktop sidebar is 236px wide. Reading centers a white page with a 510:662 aspect ratio inside a scrollable canvas. Organizing uses an auto-fill grid with a 148px minimum column, gaps of 26px by 22px, and 26px/34px/32px outer padding. Spacing is compact near controls and wider around document content; it is not a strict single-step grid.

- At 1000px and below, the sidebar becomes 210px, optional print control hides, and organizer spacing tightens.
- At 760px and below, the sidebar becomes 190px, labels tighten, and organization actions wrap beneath the selection summary.
- At 600px and below, the mode switch occupies a full toolbar row with 44px controls. The 215px sidebar becomes an overlay and initially closes. Choosing a page closes it. The reader page fills available width, search expands to a full row, and the organizer uses two columns.

Reader, outline, page rail, and organizer manage their own scrolling. The shell has a 440px minimum height. The mobile sidebar is an overlay, not a modal dialog.

## Elevation & Depth

Tonal separation and thin rules organize the application. Shadows establish paper and selected-control depth; they do not decorate every container.

### Shadow Vocabulary

- **Selected mode:** `0 1px 3px #202b3c22`.
- **Reading paper:** `0 3px 12px #26384f24`.
- **Page thumbnail:** `0 2px 5px #23304427`.
- **Mobile sidebar:** `5px 0 14px #2233481a`.

## Shapes

Small rounded rectangles distinguish fields, controls, and groups using the frontmatter radius scale. Document pages remain square. Structural and field borders are 1px; selected thumbnail borders are 2px. A circular check marks selected thumbnails. Lucide SVGs generally use a 20px box and 1.75 stroke width, with smaller contextual variants.

## Components

### Buttons

Quiet, labeled actions use transparent backgrounds; Save adds a control-rule border. Primary toolbar controls are at least 44px high, icon buttons are 44px square, and desktop mode buttons are 38px high. Hover and pressed fills provide immediate feedback. Disabled buttons use reduced opacity (0.38) and disable activation.

All keyboard-focusable elements receive a 3px blue outline with a 2px offset. Icon-only actions have accessible names. The skip link becomes visible on focus.

### Inputs / Fields

The page-number field is centered and uses tabular numerals; search uses left-aligned text, a strong neutral border, and a blue caret. Search filters chapter titles and numbers, not PDF contents. Invalid page numbers restore the current value and display a status message.

### Navigation

Read/Organize is a persistent pressed-state button group. Its selected segment is white with blue text and a small shadow. Outline/Pages is a separate sidebar group with a pale selected fill. Current chapters combine a selection wash, stronger text, and `aria-current`.

### Page Selection

Thumbnail buttons combine a blue border, wash, circular check, and `aria-pressed`. Click selects one page; Ctrl/Command-click toggles selection; Shift-click extends it; Space toggles the focused thumbnail. Double-click returns to reading. Rotation, duplication, deletion, single-page movement, and undo affect in-memory sample state.

### Notices and Motion

Dismissible notices and search results use status semantics. Thumbnail rotation transitions over 160ms with ease-out. Reduced-motion preference removes transitions and smooth scrolling. Forced-colors mode adds system-highlight selection boundaries.

Open, Save, and Print display explanatory notices. No PDF is opened, written, or printed; sample state resets on reload. Sample fallback pages and status copy do not establish native application behavior.

## Do's and Don'ts

### Do:

- **Do** preserve equal prominence for Read and Organize.
- **Do** pair visible selection styling with semantic state and keyboard focus.
- **Do** keep document surfaces visually distinct from quiet application chrome.
- **Do** retain reduced-motion and forced-colors treatments when extending controls.

### Don't:

- **Don't** substitute the styling-only alternate's tokens for the selected draft.
- **Don't** treat sample-document typography as application chrome typography.
- **Don't** infer native file operations or full accessibility conformance from this prototype.
