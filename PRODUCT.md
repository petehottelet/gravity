# Gravity

## Platform

Native Windows desktop application in C# and Windows Forms, with a separate macOS application using AppKit and PDFKit. The current Lucide interface applies to Windows.

## Purpose and users

People reading PDFs, filling existing forms, searching text, and organizing pages. Reading and organizing have equal priority. Documents remain the visual focus.

## Capabilities

Multiple document tabs, outline and page navigation, tiled rendering, text selection, search results, printing, form filling, page rearrangement, rotation, duplication, insertion, extraction, image and text export, undo and redo, and explicit saving. The native app operates offline. Existing menus, keyboard shortcuts, and the command palette retain access to advanced commands.

## Selected interface

Use the selected Lucide direction demonstrated in `docs/ui-draft/draft.html`: a Read/Organize switch, one Outline/Pages sidebar, task-specific controls above the canvas, and document status below it. Keep the native window and multiple-document tabs. Use cool neutral surfaces, strong text contrast, restrained blue selection, and rounded controls. The alternative styling-only prototype is not the selected direction.

## Accessibility and constraints

Target strong text contrast, labeled icon actions, visible keyboard focus, generous primary targets, DPI scaling, and narrow-window operation. These are design targets, not a claim of full accessibility conformance. Preserve PDF content and the existing save/undo model. Vendor icons locally and retain their licenses; do not introduce a web runtime or network dependency for the interface.
