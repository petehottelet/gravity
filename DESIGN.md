---
name: Gravity for Windows
description: Document-first clarity for reading and organizing PDFs in a native Windows workspace.
colors:
  ink: "#222831"
  muted: "#414b57"
  accent: "#174b8b"
  accent-soft: "#e2ecf8"
  chrome: "#f7f8fa"
  canvas: "#e8ebef"
  paper: "#ffffff"
  rule: "#cdd3db"
  control-rule: "#7c8795"
  page-rule: "#c4c6ca"
  hover: "#e2e8f0"
  pressed: "#d4e1f1"
  switch-track: "#e5e8ed"
  status: "#f2f4f7"
  tab-hover: "#ecedef"
  modified: "#d96c00"
  text-selection: "#1a73e850"
  search-hit: "#ffb00078"
typography:
  brand:
    fontFamily: "Segoe UI Semibold"
    fontSize: "13pt"
    fontWeight: 600
  body:
    fontFamily: "Segoe UI"
    fontSize: "9pt"
    fontWeight: 400
  control:
    fontFamily: "Segoe UI"
    fontSize: "9.5pt"
    fontWeight: 400
  selection-label:
    fontFamily: "Segoe UI Semibold"
    fontSize: "9pt"
    fontWeight: 600
  caption:
    fontFamily: "Segoe UI"
    fontSize: "8.5pt"
    fontWeight: 400
  navigation-title:
    fontFamily: "Segoe UI Semibold"
    fontSize: "8.5pt"
    fontWeight: 600
  page-input:
    fontFamily: "Segoe UI"
    fontSize: "10pt"
    fontWeight: 400
rounded:
  control: "6px"
  tab: "7px"
spacing:
  item-margin: "2px"
  inset: "3px"
  tight: "4px"
  control: "8px"
  page: "10px"
  label: "12px"
  grid: "16px"
  sidebar: "18px"
components:
  button-ghost:
    backgroundColor: "transparent"
    textColor: "{colors.ink}"
    typography: "{typography.control}"
    rounded: "{rounded.control}"
    padding: "0 8px"
    height: "44px"
  button-save:
    backgroundColor: "transparent"
    textColor: "{colors.ink}"
    typography: "{typography.control}"
    rounded: "{rounded.control}"
    padding: "0 8px"
    height: "44px"
    width: "86px"
  button-hover:
    backgroundColor: "{colors.hover}"
    textColor: "{colors.ink}"
    rounded: "{rounded.control}"
  button-pressed:
    backgroundColor: "{colors.pressed}"
    textColor: "{colors.ink}"
    rounded: "{rounded.control}"
  mode-selected:
    backgroundColor: "{colors.accent-soft}"
    textColor: "{colors.accent}"
    typography: "{typography.control}"
    rounded: "{rounded.control}"
    padding: "0 8px"
    height: "38px"
    width: "110px"
  navigation-selected:
    backgroundColor: "{colors.accent-soft}"
    textColor: "{colors.accent}"
    typography: "{typography.control}"
    rounded: "{rounded.control}"
    height: "34px"
    width: "94px"
  outline-selected:
    backgroundColor: "{colors.accent-soft}"
    textColor: "{colors.accent}"
    typography: "{typography.control}"
    rounded: "{rounded.control}"
    height: "36px"
  page-field:
    typography: "{typography.page-input}"
    width: "44px"
  page-selected:
    backgroundColor: "{colors.accent-soft}"
    textColor: "{colors.accent}"
    typography: "{typography.body}"
    rounded: "{rounded.control}"
  tab-selected:
    backgroundColor: "{colors.paper}"
    textColor: "{colors.ink}"
    typography: "{typography.body}"
    rounded: "{rounded.tab}"
---

# Design System: Gravity for Windows

## Overview

**Creative North Star: "Document-first clarity"**

Quiet application chrome frames the document. Cool neutral surfaces, dark ink, restrained blue selection, and Lucide line icons make reading and organizing equally legible. The persistent Read/Organize switch changes the work area and its commands while preserving the document context.

This system describes the implemented Windows document workspace. Native window controls, editable text fields, menus, scrollbars, and document tabs retain Windows behavior. The document keeps its own typography and page proportions. The selected browser study in `docs/ui-draft/` supplies context; the C# implementation is the authority for native dimensions and states.

**Key Characteristics:**

- Quiet chrome around white document pages on a cool canvas.
- Equal Read and Organize modes with commands appropriate to the current task.
- One docked sidebar switches between Outline and Pages.
- Selection combines color, boundaries, labels, checks, and accessible state.
- Primary commands remain reachable as the window narrows.

## Colors

One restrained blue accent sits within a cool neutral palette; warm color marks document state rather than general decoration.

### Primary

- **Navigation blue (`accent`):** checked controls, selected outline text, selected page boundaries and labels, keyboard focus, and drag insertion markers.
- **Selection wash (`accent-soft`):** selected mode, sidebar tab, outline row, and page tile.
- **Text selection (`text-selection`):** translucent overlay on selected PDF text, preserving the content beneath it.

### Neutral

- **Ink / Muted:** primary control labels and selected document tabs / supporting text, inactive tab titles, and status.
- **Chrome / Canvas / Paper:** surrounding controls and sidebar / document workspace / PDF page backing and active document tab.
- **Rule / Control rule / Page rule:** structural separators / Save outline / page edges.
- **Hover / Pressed / Switch track:** immediate button feedback and the persistent mode group's background.
- **Status / Tab hover:** quiet status strip and inactive-tab pointer feedback.

### Document State

- **Modified:** the small unsaved-change dot in a document tab, accompanied by textual status.
- **Search hit:** translucent amber over matched PDF text; matches also receive a separate amber boundary.

**The State Color Rule.** Use blue to communicate action, focus, and selection; keep the surrounding workspace neutral.

Strong text contrast, including a 7:1 target for ordinary interface text, guides the palette. That target is not a claim of full AAA or other accessibility conformance. Disabled control text follows the Windows system color rather than a fixed palette token.

## Typography

Segoe UI is the Windows interface family; Segoe UI Semibold supplies restrained emphasis. There is no application display type scale. Font sizes in the frontmatter are points, as used by WinForms; geometric sizes are logical pixels at 96 DPI unless stated otherwise. Font metrics and native text rendering determine line height.

### Hierarchy

- **Brand:** the compact Gravity label in the document header.
- **Body:** document tab titles, page captions, menus, and the inherited form font.
- **Control:** primary and contextual commands, Outline/Pages labels, outline rows, and the zoom field.
- **Selection label:** the organizer selection summary.
- **Caption:** sidebar page count and document status.
- **Navigation title:** the document name above the outline.
- **Page input:** the editable page-number field; toast text uses the same point size.

Long tab titles and outline rows use ellipsis. Supporting text is smaller, not low-opacity body text. The document name remains available in tab tooltips.

**The Document Type Rule.** Preserve the PDF's typography; sample-page fonts never define application chrome.

## Layout

The workspace uses docked native controls. A document header (48px) contains the Gravity label and the tab strip. A command bar (62px) holds opening controls on the left, equal mode controls in the center, and document actions on the right. A contextual command row (56px) sits above the document canvas; the status strip (31px) sits below it. The native window frame is outside these measurements.

The sidebar defaults to 236px. Its persisted requested width is clamped to 196–400px, then constrained to leave 240px for the document host, with a 160px lower bound in that calculation. It hides when the content width falls below 420px or when explicitly toggled off. It stays docked when visible; it does not become the browser study's overlay. Outline/Pages occupies a 52px row; the document-name row is 36px and the bottom page-count row is 34px.

The reader preserves page proportions, fits the available viewport according to the selected zoom mode, and uses 10px outer padding and 10px page gaps outside presentation mode. The organizer centers a grid with 16px outer margins. Its default thumbnail box is 160px, with 10px internal padding and a 24px caption allocation, giving a 180 × 204px cell. Thumbnail size can vary from 72 to 480px. The number of columns follows available width; the Pages sidebar uses one column and constrains the thumbnail box to its width.

### Narrow Windows

- Below 1000px client width, the command bar becomes 110px tall and the mode group occupies a centered second row. The group remains 234 × 46px with 110 × 38px buttons.
- Below 700px client width, Print leaves the primary command row; below 620px, Redo also leaves it. The right command strip narrows from 358px to 310px and then 262px. Application menus retain these commands.
- Contextual actions depend on the document host's command-row width, not the whole window. Reading shows previous/next from 330px and zoom controls from 500px.
- Organizing shows Select all and Rotate from 480px, Duplicate from 600px, Delete from 700px, and move earlier/later from 800px.
- The labeled More menu remains present and exposes the task's page and view commands, including those omitted from the row.

The window minimum is 520 × 380px. Reader, outline, and page grid scroll independently. Most layout values pass through the implementation's DPI scaling helpers; native borders and several custom drawing details retain device-pixel measurements.

**The Reachable Commands Rule.** When space removes an action from the row, preserve access through More or the application menus.

## Elevation & Depth

Tonal separation and thin rules organize the workspace. Buttons, mode controls, sidebar rows, and organizer thumbnails have no decorative drop shadow. White paper remains distinct through its page boundary. The reader adds a small unblurred edge shadow outside presentation mode: black at alpha 28/255, painted as 2-device-pixel strips along the bottom and right, beginning one device pixel beyond the page. This is a page edge treatment, not a general card shadow.

## Shapes

Controls, selected outline rows, and selected page tiles use the control radius. Document tabs use the slightly larger tab radius. PDF pages remain square. Ordinary page boundaries use one device pixel; selected page tiles use a 2px scaled blue outline. The selected organizer badge is an 18px blue circle containing a white Lucide check; it is omitted in the Pages sidebar.

Bundled Lucide geometry uses a 24-unit view box, round caps and joins, and a 1.75-unit stroke. Main command icons render in a 20px box. Smaller icons fit tab controls and selection badges. Preserve the assets and license in `third_party/lucide/`.

## Components

### Buttons

Quiet, labeled actions use transparent backgrounds. The main icon targets are 44px square; Open and Save are 86 × 44px. Save adds a one-device-pixel control-rule outline. Items have 2px margins and 8px horizontal padding. Most contextual icon actions are 38 × 40px; the 44px main target size is not a claim about every control.

Hover uses the hover fill, pressed uses the pressed fill, and checked controls use the selection wash with blue text. A keyboard-selected item in a focused tool strip receives a blue boundary at least two device pixels wide, scaled with DPI. Disabled items cannot activate; native rendering supplies disabled icon and text treatment. State changes are immediate, with no animated transition.

### Inputs / Fields

The page-number field and zoom selector remain native WinForms text and combo-box controls. The page field is 44px wide; the zoom field is 110px wide. Their borders, text selection, caret, focus, and disabled states follow native control rendering. Do not apply the browser study's rounded field or CSS focus ring to these native controls.

### Navigation

Read and Organize have equal-sized labeled icon buttons. Their selected segment uses the selection wash, not a raised white card. Outline and Pages form a separate pair of 94 × 34px controls. The outline uses 36px rows, blue selected text, a rounded selection wash, and a native focus rectangle when focused. The current outline entry follows the document position.

Choosing a page in the sidebar navigates the reader; choosing Pages while organizing returns to reading with page navigation visible. Outline entries retain tree navigation and Enter activation. The toolbar menu button reveals the application's native menu strip, while More is the task-specific command menu.

### Document Tabs

Tabs are custom-drawn native controls within the header. Each tab ranges from 80 to 230px wide according to available width and document count. The active tab is white with a thin structural boundary; inactive tabs use muted text and gain the tab-hover fill under the pointer. A dotted focus rectangle identifies the active tab when the strip is focused.

Close is shown on the active or hovered tab. The current close hit area is 18px square and the add-document hit area is 24px square; these smaller existing controls do not establish a new primary-target standard. A modified document shows a 7px dot and exposes unsaved status in its accessible name. Tabs, close actions, and add-document action expose native accessibility roles and default actions.

### Page Selection

The organizer's selected tile combines a blue boundary, selection wash, blue page caption, and circular check. A dotted inset rectangle distinguishes keyboard focus. Pages expose list/list-item roles, names, selection, focus, and offscreen state. Ctrl and Shift extend selection; arrow keys and Home/End move through pages; Space toggles selection; Enter or double-click opens the page in reading mode. Dragging shows an insertion marker and a page-count label.

The same page grid supplies sidebar thumbnails, with one column and no check badge. Empty documents show a concise message explaining paste or PDF drop. Imported-source dots are document provenance cues, not additional application accent colors.

### Status and Notices

The status strip pairs page position or selection count on the left with explicit save status on the right. Unsaved changes and a new document that has not been saved have distinct text. These states are independent of the document tab's color dot.

Canvas notices use white 10pt text on a dark translucent capsule, centered near the lower edge. The capsule grows to fit wrapped text, has 12px horizontal inset, and normally dismisses after 2200ms. This timed notice is not a substitute for the persistent save status.

## Do's and Don'ts

### Do:

- **Do** preserve equal prominence for Read and Organize.
- **Do** keep native fields, menus, scrolling, and keyboard behavior intact.
- **Do** pair visible selection styling with accessible state and distinct keyboard focus.
- **Do** keep document pages visually distinct from quiet application chrome.
- **Do** preserve access to commands through More or application menus at narrow widths.
- **Do** derive native measurements from the C# implementation and scale geometry with DPI.

### Don't:

- **Don't** substitute the browser study's measurements or animation rules for native behavior.
- **Don't** treat sample-document typography as application chrome typography.
- **Don't** use color alone to convey selection or unsaved changes.
- **Don't** generalize smaller tab controls into the primary command target standard.
- **Don't** infer full accessibility conformance from the palette, target sizes, or accessibility roles alone.
