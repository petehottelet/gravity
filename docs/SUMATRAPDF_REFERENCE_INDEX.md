# SumatraPDF reference index

Reference date: November 24, 2026.

Use the [feature dossier](SUMATRAPDF_DOSSIER.md) for behavior and Gravity comparisons, and the [Gravity roadmap](GRAVITY_ROADMAP.md) for priorities and acceptance criteria. This index preserves technical identifiers so less visible commands, settings and scripting functions remain discoverable.

The inventory covers **117 documentation pages**, **392 command rows**, **375 distinct setting paths**, **664 JavaScript API entries**, and **56 example scripts**. The 114 pages in the official documentation bundle are supplemented by the barcode, debugging and WSL references. Counts describe documentation entries, not independently tested features. API entries include constructors, overloads, methods and documented properties.

The [machine readable catalog](sumatrapdf-reference-catalog.json) contains the same identifiers. Defaults, full argument semantics and compatibility details belong to the linked official references. A listed command is not a promise that it exists in stable 3.6.1, works on every platform, or is intended for end users.

## Contents

- [Documentation coverage](#documentation-coverage)
- [Command identifiers](#command-identifiers)
- [Setting paths](#setting-paths)
- [JavaScript API signatures](#javascript-api-signatures)
- [Example scripts](#example-scripts)

## Documentation coverage

### Formats and specialist reading

- [Supported document formats](https://www.sumatrapdfreader.org/docs/Supported-document-formats)
- [Comics and manga](https://www.sumatrapdfreader.org/docs/Comics-and-manga)
- [CAD / Engineering Drawings](https://www.sumatrapdfreader.org/docs/CAD-Engineering-Drawings)
- [OFD documents](https://www.sumatrapdfreader.org/docs/OFD-documents)

### Product and installation

- [Installation](https://www.sumatrapdfreader.org/docs/Installation)
- [Features](https://www.sumatrapdfreader.org/docs/Features)
- [Portable vs installer](https://www.sumatrapdfreader.org/docs/SumatraPDF-portable)
- [Set as default PDF viewer](https://www.sumatrapdfreader.org/docs/Set-as-default-pdf-viewer)
- [Frequently asked questions](https://www.sumatrapdfreader.org/docs/FAQ)
- [Version history](https://www.sumatrapdfreader.org/docs/Version-history)
- [Uninstalling SumatraPDF](https://www.sumatrapdfreader.org/docs/Uninstalling-SumatraPDF)

### Commands and customization

- [Command Palette](https://www.sumatrapdfreader.org/docs/Command-Palette)
- [Keyboard shortcuts](https://www.sumatrapdfreader.org/docs/Keyboard-shortcuts)
- [Customize keyboard shortcuts](https://www.sumatrapdfreader.org/docs/Customize-keyboard-shortcuts)
- [Commands](https://www.sumatrapdfreader.org/docs/Commands)
- [Customize toolbar](https://www.sumatrapdfreader.org/docs/Customize-toolbar)
- [Customize theme (colors)](https://www.sumatrapdfreader.org/docs/Customize-theme-colors)
- [Customize eBook UI](https://www.sumatrapdfreader.org/docs/Customize-eBook-UI)
- [Customize external viewers](https://www.sumatrapdfreader.org/docs/Customize-external-viewers)
- [Customize search / translation](https://www.sumatrapdfreader.org/docs/Customize-search-translation-services)
- [Advanced settings](https://www.sumatrapdfreader.org/docs/Advanced-options-settings)
- [How we store settings](https://www.sumatrapdfreader.org/docs/How-we-store-settings)

### Reading and navigation

- [Tabs and windows](https://www.sumatrapdfreader.org/docs/Tabs-and-windows)
- [Viewing modes](https://www.sumatrapdfreader.org/docs/Viewing-modes)
- [Scrolling, zooming and navigating](https://www.sumatrapdfreader.org/docs/Scrolling-and-zooming)
- [Thumbnails sidebar](https://www.sumatrapdfreader.org/docs/Thumbnails-sidebar)
- [PDF layers](https://www.sumatrapdfreader.org/docs/PDF-layers)
- [Page info](https://www.sumatrapdfreader.org/docs/Page-info)
- [Finding text in documents](https://www.sumatrapdfreader.org/docs/Finding-text)
- [Hyperlinks in documents](https://www.sumatrapdfreader.org/docs/Hyperlinks)
- [Citation hover preview](https://www.sumatrapdfreader.org/docs/Citation-hover-preview)
- [Managing favorites](https://www.sumatrapdfreader.org/docs/Managing-favorites)
- [Accessibility](https://www.sumatrapdfreader.org/docs/Accessibility)
- [Present a PDF as slides](https://www.sumatrapdfreader.org/docs/Present-a-PDF)
- [Open a PDF at a page or search term](https://www.sumatrapdfreader.org/docs/Open-PDF-at-page-from-command-line)

### Document operations

- [Editing annotations](https://www.sumatrapdfreader.org/docs/Editing-annotations)
- [Fill a PDF form](https://www.sumatrapdfreader.org/docs/Fill-PDF-forms)
- [Sign a PDF](https://www.sumatrapdfreader.org/docs/Sign-a-PDF)
- [Verify a PDF signature](https://www.sumatrapdfreader.org/docs/Verify-PDF-signature)
- [Printing](https://www.sumatrapdfreader.org/docs/Printing)
- [Handling temporary files](https://www.sumatrapdfreader.org/docs/Handling-temporary-files)
- [Read Aloud (TTS)](https://www.sumatrapdfreader.org/docs/Read-Aloud)
- [AI Chat with document](https://www.sumatrapdfreader.org/docs/AI-Chat-with-document)
- [Run PDF Script](https://www.sumatrapdfreader.org/docs/Run-PDF-script)
- [Merge PDFs](https://www.sumatrapdfreader.org/docs/Merge-PDFs)
- [Merge PDF window](https://www.sumatrapdfreader.org/docs/Merge-PDF-window)
- [Split a PDF](https://www.sumatrapdfreader.org/docs/Split-a-PDF)
- [Reorder pages in a PDF](https://www.sumatrapdfreader.org/docs/Reorder-PDF-pages)
- [Delete pages from PDF](https://www.sumatrapdfreader.org/docs/Tool-x-delete-pages-from-pdf)
- [Extract pages from PDF](https://www.sumatrapdfreader.org/docs/Tool-x-extract-pages-from-pdf)
- [Rotate PDF pages permanently](https://www.sumatrapdfreader.org/docs/Rotate-PDF-pages)
- [Crop PDF page margins](https://www.sumatrapdfreader.org/docs/Crop-PDF-margins)
- [Print a large page as a poster](https://www.sumatrapdfreader.org/docs/Print-PDF-as-poster)
- [Convert images to PDF](https://www.sumatrapdfreader.org/docs/Convert-images-to-PDF)
- [Convert comics to PDF](https://www.sumatrapdfreader.org/docs/Convert-comics-to-PDF)
- [Convert EPUB to PDF](https://www.sumatrapdfreader.org/docs/Convert-EPUB-to-PDF)
- [Convert text file to a PDF](https://www.sumatrapdfreader.org/docs/Tool-x-convert-text-to-pdf)
- [Convert to Images](https://www.sumatrapdfreader.org/docs/Convert-PDF-to-images)
- [Convert a PDF to grayscale](https://www.sumatrapdfreader.org/docs/Convert-PDF-to-grayscale)
- [Redact a PDF](https://www.sumatrapdfreader.org/docs/Redact-a-PDF)
- [Add an image to a PDF](https://www.sumatrapdfreader.org/docs/Add-an-image-to-a-PDF)
- [Add page numbers to a PDF](https://www.sumatrapdfreader.org/docs/Add-page-numbers-to-PDF)
- [Add a watermark to a PDF](https://www.sumatrapdfreader.org/docs/Add-a-watermark-to-PDF)
- [Add a table of contents to a PDF](https://www.sumatrapdfreader.org/docs/Add-table-of-contents-to-PDF)
- [Flatten PDF forms and annotations](https://www.sumatrapdfreader.org/docs/Flatten-PDF-forms-and-annotations)
- [Extract text from PDF using SumatraPDF](https://www.sumatrapdfreader.org/docs/Tool-x-extract-text-from-pdf)
- [Extract images from a PDF](https://www.sumatrapdfreader.org/docs/Tool-x-extract-images-from-pdf)
- [Extract fonts from a PDF](https://www.sumatrapdfreader.org/docs/Extract-fonts-from-PDF)
- [Save part of a page as an image](https://www.sumatrapdfreader.org/docs/Save-page-region-as-image)
- [Save files attached to a PDF](https://www.sumatrapdfreader.org/docs/Save-PDF-attachments)
- [Search text in PDF from command line](https://www.sumatrapdfreader.org/docs/Tool-x-search-pdf)
- [Compress a PDF](https://www.sumatrapdfreader.org/docs/Tool-x-compress-pdf)
- [Decompress a PDF](https://www.sumatrapdfreader.org/docs/Tool-x-decompress-pdf)
- [Encrypt a PDF with password](https://www.sumatrapdfreader.org/docs/Tool-x-encrypt-pdf-with-password)
- [Decrypt a PDF](https://www.sumatrapdfreader.org/docs/Tool-x-decrypt-pdf)
- [Repair a damaged PDF](https://www.sumatrapdfreader.org/docs/Repair-a-damaged-PDF)
- [See PDF info: pages, page size, fonts, security](https://www.sumatrapdfreader.org/docs/Show-PDF-info)
- [Process many PDFs at once](https://www.sumatrapdfreader.org/docs/Batch-process-PDFs)
- [Print only some pages](https://www.sumatrapdfreader.org/docs/Print-selected-pages)

### Automation and deployment

- [Command-line arguments](https://www.sumatrapdfreader.org/docs/Command-line-arguments)
- [DDE Commands](https://www.sumatrapdfreader.org/docs/DDE-Commands)
- [LaTeX integration](https://www.sumatrapdfreader.org/docs/LaTeX-integration)
- [Configure for restricted use](https://www.sumatrapdfreader.org/docs/Configure-for-restricted-use)
- [Installer cmd-line arguments](https://www.sumatrapdfreader.org/docs/Installer-cmd-line-arguments)
- [Command line for scripts and AI agents](https://www.sumatrapdfreader.org/docs/Command-line-for-scripts-and-AI-agents)
- [LaTeX Integration with WSL](https://www.sumatrapdfreader.org/docs/LaTeX-integration-wsl)

### Command line tools and scripting

- [SumatraPDF command-line tools](https://www.sumatrapdfreader.org/docs/Tools)
- [sumatrapdf-tool draw](https://www.sumatrapdfreader.org/docs/Tool-draw)
- [sumatrapdf-tool convert](https://www.sumatrapdfreader.org/docs/Tool-convert)
- [sumatrapdf-tool audit](https://www.sumatrapdfreader.org/docs/Tool-audit)
- [sumatrapdf-tool bake](https://www.sumatrapdfreader.org/docs/Tool-bake)
- [sumatrapdf-tool clean](https://www.sumatrapdfreader.org/docs/Tool-clean)
- [sumatrapdf-tool extract](https://www.sumatrapdfreader.org/docs/Tool-extract)
- [sumatrapdf-tool info](https://www.sumatrapdfreader.org/docs/Tool-info)
- [sumatrapdf-tool merge](https://www.sumatrapdfreader.org/docs/Tool-merge)
- [sumatrapdf-tool pages](https://www.sumatrapdfreader.org/docs/Tool-pages)
- [sumatrapdf-tool poster](https://www.sumatrapdfreader.org/docs/Tool-poster)
- [sumatrapdf-tool recolor](https://www.sumatrapdfreader.org/docs/Tool-recolor)
- [sumatrapdf-tool show](https://www.sumatrapdfreader.org/docs/Tool-show)
- [sumatrapdf-tool sign](https://www.sumatrapdfreader.org/docs/Tool-sign)
- [sumatrapdf-tool trim](https://www.sumatrapdfreader.org/docs/Tool-trim)
- [sumatrapdf-tool grep](https://www.sumatrapdfreader.org/docs/Tool-grep)
- [sumatrapdf-tool trace](https://www.sumatrapdfreader.org/docs/Tool-trace)
- [sumatrapdf-tool run](https://www.sumatrapdfreader.org/docs/Tool-run)
- [JavaScript examples for sumatrapdf-tool run](https://www.sumatrapdfreader.org/docs/Tool-run-javascript-examples)
- [JavaScript API reference for sumatrapdf-tool run](https://www.sumatrapdfreader.org/docs/Tool-run-javascript-reference)
- [sumatrapdf-tool barcode](https://www.sumatrapdfreader.org/docs/Tool-barcode)

### Support and privacy

- [Contribute translation](https://www.sumatrapdfreader.org/docs/Contribute-translation)
- [How to submit bug reports](https://www.sumatrapdfreader.org/docs/How-to-submit-bug-reports)
- [Submit crash report](https://www.sumatrapdfreader.org/docs/Submit-crash-report)
- [Reporting printing bugs](https://www.sumatrapdfreader.org/docs/Reporting-printing-bugs)
- [Update check doesn't work?](https://www.sumatrapdfreader.org/docs/Update-check-doesnt-work)
- [Failed to load libsumatrapdf.dll](https://www.sumatrapdfreader.org/docs/Failed-to-load-libmupdf)
- [Corrupted installation](https://www.sumatrapdfreader.org/docs/Corrupted-installation)
- [SumatraPDF on macOS and other systems](https://www.sumatrapdfreader.org/docs/Why-only-Windows)
- [Privacy policy](https://www.sumatrapdfreader.org/docs/Privacy-policy)
- [Debugging Sumatra](https://www.sumatrapdfreader.org/docs/Debugging-Sumatra)

## Command identifiers

The grouping follows the command reference. Debug and internal entries are included for completeness; they are not recommendations for Gravity features. Source: [Commands](https://www.sumatrapdfreader.org/docs/Commands).

### File

- `CmdClose` — `Ctrl + W, Ctrl + F4`
- `CmdCloseCurrentDocument` — `q`
- `CmdCommandPalette` — `Ctrl + K, >`
- `CmdNavigateTabs` — `@`
- `CmdNavigateHistory` — `#`
- `CmdNavigateFavorites` — `$`
- `CmdNavigateTableOfContents` — `Shift + F12, %`
- `CmdQuickEditSettings` — `=`
- `CmdDuplicateInNewWindow` — `Shift + Ctrl + N`
- `CmdDuplicateInNewTab`
- `CmdViewAsHex`
- `CmdExit` — `Ctrl + Q`
- `CmdMoveFrameFocus` — `F6`
- `CmdNewWindow` — `Ctrl + N`
- `CmdMinimizeWindow`
- `CmdOpenFile` — `Ctrl + O`
- `CmdOpenFileNoHistory`
- `CmdOpenFileWithOSFilePicker`
- `CmdOpenFileWithSumatraFilePicker`
- `CmdOpenFromURL`
- `CmdToggleFilePicker`
- `CmdToggleBoolSetting`
- `CmdFixDefaultApp`
- `CmdFileHistory`
- `CmdFavorite`
- `CmdOpenNextFileInFolder` — `Shift + Ctrl + Right`
- `CmdNavigateFilesInFolder` — `Shift + Ctrl + Up`
- `CmdOpenPrevFileInFolder` — `Shift + Ctrl + Left`
- `CmdOpenSelectedDocument`
- `CmdPinSelectedDocument`
- `CmdPrint` — `Ctrl + P`
- `CmdSumatraPDFWeb`
- `CmdProperties` — `Ctrl + D`
- `CmdReloadDocument` — `r`
- `CmdRenameFile` — `F2`
- `CmdCopyTemporaryFileToNonTemporary`
- `CmdReopenLastClosedFile` — `Shift + Ctrl + T`
- `CmdSaveAs` — `Ctrl + S`
- `CmdToggleCursorPosition` — `m`
- `CmdMeasure`
- `CmdShowInFolder`
- `CmdToggleBookmarks` — `F12`
- `CmdToggleThumbnails`
- `CmdToggleTableOfContents`
- `CmdCollapseAll`
- `CmdExpandAll`
- `CmdExpandToCurrentPage`
- `CmdAutoGenerateTOC`
- `CmdTocExpandToLevel1`
- `CmdTocExpandToLevel2`
- `CmdTocExpandToLevel3`
- `CmdTocCollapseSameLevel`
- `CmdTocExpandChapter`
- `CmdTocCollapseChapter`
- `CmdOpenEmbeddedPDF`
- `CmdSaveEmbeddedFile`
- `CmdCreateShortcutToFile`
- `CmdSelectAll` — `Ctrl + A`
- `CmdSelectCurrentPage`
- `CmdExtendSelectionCharLeft`
- `CmdExtendSelectionCharRight`
- `CmdExtendSelectionWordLeft`
- `CmdExtendSelectionWordRight`
- `CmdCaretUp`
- `CmdCaretDown`
- `CmdCaretLeft`
- `CmdCaretRight`
- `CmdCaretWordLeft`
- `CmdCaretWordRight`
- `CmdCaretLineStart`
- `CmdCaretLineEnd`
- `CmdCaretPageUp`
- `CmdCaretPageDown`
- `CmdCaretDocStart`
- `CmdCaretDocEnd`
- `CmdToggleTextSelecting`
- `CmdCopyComment`
- `CmdShowAnnotationText`
- `CmdCopyImage`
- `CmdCopyLinkTarget`
- `CmdCopySelection` — `Ctrl + C, Ctrl + Insert`
- `CmdCopySelectionAsImage`
- `CmdShowSelectedText`
- `CmdCountWords`
- `CmdSaveSelectionAsImage`
- `CmdPrintSelection`
- `CmdSearchGoogleLens`
- `CmdSearchGoogleLensPage`
- `CmdSearchGoogleLensImage`
- `CmdCopyFilePath`
- `CmdCopyLocationToClipboard`
- `CmdDeleteFile`
- `CmdDeleteFileAndOpenNext`
- `CmdShowGeneratedHTML`

### Search

- `CmdFindFirst` — `Ctrl + F`
- `CmdFindHistory` — `Alt + Down`
- `CmdFindToggleMatchCase`
- `CmdFindToggleMatchWholeWord`
- `CmdFindNext` — `F3`
- `CmdFindNextSel` — `Ctrl + F3`
- `CmdFindPrev` — `Shift + F3`
- `CmdFindPrevSel` — `Shift + Ctrl + F3`

### Viewing

- `CmdBookView` — `Ctrl + 8, Ctrl + Numpad 8`
- `CmdFacingView` — `Ctrl + 7, Ctrl + Numpad 7`
- `CmdInvertColors` — `Shift + I`
- `CmdToggleGrayscale`
- `CmdAdjustBrightness`
- `CmdAdjustContrast`
- `CmdRotateLeft` — `[, Shift + Ctrl + Subtract`
- `CmdRotateRight` — `], Shift + Ctrl + Add`
- `CmdToggleMirror`
- `CmdSinglePageView` — `Ctrl + 6, Ctrl + Numpad 6`
- `CmdToggleContinuousView` — `c`
- `CmdSelectTextViaKeyboard` — `F7`
- `CmdToggleKeyboardLinkFollowing` — `Shift + F`
- `CmdToggleFullscreen` — `f, Shift + Ctrl + L, F11`
- `CmdToggleMangaMode`
- `CmdToggleAutoRotateWidePages`
- `CmdToggleUniformPageWidth`
- `CmdToggleTrimEmptyMargins`
- `CmdTrimPagesToSelection`
- `CmdShowWholePages`
- `CmdSaveTrimmedPagesAs`
- `CmdToggleReadColumns`
- `CmdToggleColumnsWholePage`
- `CmdToggleFreePan`
- `CmdToggleMenuBar` — `F9`
- `CmdTogglePageInfo` — `i`
- `CmdTogglePageBoxes`
- `CmdChangeScrollbar`
- `CmdChangeBackgroundColor`
- `CmdChangeEbookSettings`
- `CmdToggleToolbar` — `F8`
- `CmdToggleCaption` — `Shift + F8`
- `CmdToggleSidebarAutoHide`
- `CmdAIChatWithClaudeCode`
- `CmdAIChatWithGrokBuild`
- `CmdAIChatWithOpenAICodex`
- `CmdAIChatWithAntiGravity`
- `CmdAIChatWithCustomAPI`
- `CmdTranslateSelectionWithAntiGravity`
- `CmdChangeTheme`
- `CmdNavigateThumbnail` — `&`
- `CmdToggleLightDarkTheme`
- `CmdToggleEngineeringDrawingEnhance`
- `CmdToggleLineWeights`
- `CmdSetDocumentColorsFollowTheme`
- `CmdTogglePreservePdfImages`
- `CmdToggleLinks`
- `CmdToggleHighlightFormFields`
- `CmdToggleTransparencyGrid`
- `CmdTogglePageGrid`
- `CmdConfigurePageGrid`
- `CmdToggleDisableLinks`
- `CmdToggleHoverPreview`

### Tabs

- `CmdCloseAllTabs`
- `CmdCloseMissingTabs`
- `CmdCloseTabsToTheLeft`
- `CmdCloseTabsToTheRight`
- `CmdGoToHomePage`
- `CmdNewTab` — `Ctrl + T`
- `CmdCloseOtherTabs`
- `CmdNextTab` — `Ctrl + PageUp`
- `CmdPrevTab` — `Ctrl + PageDown`
- `CmdMoveTabRight` — `Ctrl + Shift + PageUp`
- `CmdMoveTabLeft` — `Ctrl + Shift + PageDown`
- `CmdNextTabSmart` — `Ctrl + Tab`
- `CmdPrevTabSmart` — `Ctrl + Shift + Tab`
- `CmdTabGroupSave`
- `CmdTabGroupRestore`
- `CmdStripGroupNew`
- `CmdStripGroupAddTab`
- `CmdStripGroupRemoveTab`
- `CmdStripGroupToggle`
- `CmdStripGroupRename`
- `CmdStripGroupColor`
- `CmdStripGroupUngroup`
- `CmdStripGroupClose`

### Navigation

- `CmdScrollUp` — `k, Up`
- `CmdScrollDown` — `j, Down`
- `CmdScrollLeft` — `h, Left`
- `CmdScrollRight` — `l, Right`
- `CmdScrollUpHalfPage` — `Shift + Up`
- `CmdScrollDownHalfPage` — `Shift + Down`
- `CmdStartAutoScroll`
- `CmdToggleAutomaticallyScroll` — `Ctrl + Shift + H`
- `CmdAutomaticallyScrollFaster`
- `CmdAutomaticallyScrollSlower`
- `CmdToggleReadingBar`
- `CmdToggleReadingBarInvert`
- `CmdScrollUpPage` — `Ctrl + Up, PageUp, Shift + Return, Shift + Space`
- `CmdScrollDownPage` — `Ctrl + Down, PageDown, Return, Space`
- `CmdScrollLeftPage` — `Shift + Left`
- `CmdScrollRightPage` — `Shift + Right`
- `CmdGoToFirstPage` — `Ctrl + Home, Home`
- `CmdGoToLastPage` — `Ctrl + End, End`
- `CmdGoToPrevPage` — `p`
- `CmdGoToNextPage` — `n`
- `CmdGoToPrevChapter`
- `CmdGoToNextChapter`
- `CmdGoToPage` — `g, Ctrl + G`
- `CmdNavigateBack` — `Alt + Left, Backspace`
- `CmdNavigateForward` — `Alt + Right, Shift + Backspace`

### Favorites

- `CmdFavoriteAdd` — `Ctrl + B`
- `CmdFavoriteDel`
- `CmdFavoriteToggle`
- `CmdFavoriteShowInTab`
- `CmdToggleFavoritesSort`
- `CmdEditFavorites`
- `CmdGoToNextFavorite`
- `CmdGoToPrevFavorite`

### Presentation

- `CmdTogglePresentationMode` — `Ctrl + L, Shift + F11, F5`
- `CmdPresentationBlackBackground` — `.`
- `CmdPresentationWhiteBackground` — `w`
- `CmdToggleLaserPointer`

### Annotations

- `CmdInsertImage`
- `CmdSignWithImage`
- `CmdInsertTextSnippet`
- `CmdCreateAnnotCaret`
- `CmdCreateAnnotCircle`
- `CmdFindAnnotation`
- `CmdCreateAnnotFileAttachment`
- `CmdCreateAnnotFreeText`
- `CmdCreateAnnotHighlight` — `a, A`
- `CmdAnnotationHighlightBrush`
- `CmdCreateAnnotInk`
- `CmdCreateAnnotLine`
- `CmdCreateAnnotLink`
- `CmdCreateAnnotPolygon`
- `CmdCreateAnnotPolyLine`
- `CmdCreateAnnotPopup`
- `CmdCreateAnnotRedact`
- `CmdApplyRedactions`
- `CmdCreateAnnotSquare`
- `CmdCreateAnnotSquiggly`
- `CmdCreateAnnotStamp`
- `CmdCreateAnnotImageFromClipboard`
- `CmdCreateAnnotStrikeOut`
- `CmdCreateAnnotText`
- `CmdCreateAnnotUnderline` — `u, U`
- `CmdUndo` — `Ctrl + Z`
- `CmdRedo` — `Shift + Ctrl + Z`
- `CmdCutAnnotation` — `Ctrl + X`
- `CmdCopyAnnotation` — `Ctrl + C`
- `CmdPasteAnnotation` — `Ctrl + V`
- `CmdDeleteAnnotation` — `Delete`
- `CmdEditAnnotationText`
- `CmdSaveAnnotations` — `Shift + Ctrl + S`
- `CmdSaveAnnotationsNewFile`
- `CmdSignDocument`
- `CmdDiscardChanges`
- `CmdShowAnnotations`
- `CmdHideAnnotations`
- `CmdToggleShowAnnotations`
- `CmdToggleEditPDF`

### Zoom

- `CmdToggleZoom` — `z`
- `CmdZoomActualSize` — `Ctrl + 1, Ctrl + Numpad 1`
- `CmdZoomCustom` — `Ctrl + Y`
- `CmdZoomFitContent` — `Ctrl + 3, Ctrl + Numpad 3`
- `CmdZoomFitVisible`
- `CmdZoomToSelection` — `Ctrl + 4, Ctrl + Numpad 4`
- `CmdZoomShrinkToFit`
- `CmdZoomFitPage` — `Ctrl + 0, Ctrl + Numpad 0`
- `CmdZoomFitPageAndSinglePage`
- `CmdZoomFitWidth` — `Ctrl + 2, Ctrl + Numpad 2`
- `CmdZoomFitHeight`
- `CmdZoomFitByOrientation`
- `CmdZoomFitWidthAndContinuous`
- `CmdZoomIn` — `Ctrl + Add`
- `CmdZoomOut` — `Ctrl + Subtract`
- `CmdZoom100`
- `CmdZoom12_5`
- `CmdZoom125`
- `CmdZoom150`
- `CmdZoom1600`
- `CmdZoom200`
- `CmdZoom25`
- `CmdZoom3200`
- `CmdZoom400`
- `CmdZoom50`
- `CmdZoom6400`
- `CmdZoom8_33`
- `CmdZoom800`

### File information and conversion

- `CmdPdfShowInfo`
- `CmdShowSignatures`
- `CmdToggleLayers`
- `CmdToggleFacingStepOne`
- `CmdDocumentShowOutline`
- `CmdPdfBake`
- `CmdPdfWatermark`
- `CmdPdfAddPageNumbers`
- `CmdDocumentExtractText`

### External app

- `CmdOpenWithExplorer`
- `CmdOpenWithDirectoryOpus`
- `CmdOpenWithTotalCommander`
- `CmdOpenWithDoubleCommander`
- `CmdOpenWithAcrobat`
- `CmdOpenWithFoxit`
- `CmdOpenWithFoxitPhantom`
- `CmdOpenWithHtmlHelp`
- `CmdOpenWithPdfDjvuBookmarker`
- `CmdOpenWithPdfXchange`
- `CmdOpenWithXpsViewer`
- `CmdTranslateSelection`
- `CmdTranslateSelectionWithDeepL`
- `CmdTranslateSelectionWithGoogle`
- `CmdTranslateSelectionWithGrokBuild`
- `CmdTranslateSelectionWithClaudeCode`
- `CmdTranslateSelectionWithOpenAICodex`
- `CmdSearchSelectionWithBing`
- `CmdSearchSelectionWithGoogle`
- `CmdSearchSelectionWithWikipedia`
- `CmdSearchSelectionWithGoogleScholar`
- `CmdSendByEmail`
- `CmdInvokeInverseSearch`

### System

- `CmdOpenSettingsFile`
- `CmdAdvancedSettings`
- `CmdConfigureRestrictedUse`
- `CmdRunPDFScript`
- `CmdChangeLanguage`
- `CmdCheckUpdate`
- `CmdClearHistory`
- `CmdRemoveDeletedFilesFromHistory`
- `CmdDeleteCachedFiles`
- `CmdContributeTranslation`
- `CmdForgetSelectedDocument`
- `CmdListPrinters`
- `CmdOptions`
- `CmdSetInverseSearch`
- `CmdScreenshot`
- `CmdCropImage`
- `CmdResizeImage`
- `CmdReplaceImage`
- `CmdEditImageExternally`
- `CmdSaveImage`
- `CmdConvertImageToPdf`
- `CmdConvertToPDF`
- `CmdConvertPdfToImages`
- `CmdConvertEpubToPdf`
- `CmdConvertCajToPDF`
- `CmdPasteClipboardImage` — `Ctrl + V`
- `CmdShowErrors`
- `CmdShowLog`

### Help

- `CmdHelpOpenManual` — `F1`
- `CmdHelpOpenKeyboardShortcuts`
- `CmdToggleKeyboardHelp` — `?`
- `CmdHelpAbout`
- `CmdHelpOpenManualOnWebsite`
- `CmdHelpVisitWebsite`

### Misc

- `CmdToggleInverseSearch`
- `CmdToggleWindowsPreviewer`
- `CmdToggleWindowsSearchFilter`
- `CmdSetTabColor`
- `CmdPdfCompress`
- `CmdPdfDecompress`
- `CmdPdfDeletePages`
- `CmdMergePDF`
- `CmdPdfExtractPages`
- `CmdPdfEncrypt`
- `CmdPdfDecrypt`
- `CmdSetScreenshotHotkey`
- `CmdToggleReadAloud`
- `CmdPauseReadAloud`
- `CmdContinueReadAloud`
- `CmdStopReadAloud`
- `CmdReadAloudFromTopPage`
- `CmdReadAloudSelection`
- `CmdReadAloudFromCursorPosition`
- `CmdToggleToolbarShowReadAloud`

### Debug

- `CmdDebugCrashMe`
- `CmdDebugShowNotif`
- `CmdDebugStartStressTest`
- `CmdDebugTogglePredictiveRender`
- `CmdDebugToggleRenderInfo`
- `CmdDebugToggleCacheInfo`
- `CmdDebugToggleRtl`
- `CmdToggleImages`
- `CmdDebugShowCrashMiniDumps`
- `CmdDebugShowFitContentArea`
- `CmdDebugSaveSettings`
- `CmdNone`

### Deprecated or internal

- `CmdInstallPrereleaseUpdate`
- `CmdTogglePdfPreviewLogging`
- `CmdDebugCorruptMemory`
- `CmdSelectionHandler`
- `CmdSetTheme`
- `CmdViewWithExternalViewer`
- `CmdSaveAttachment`
- `CmdOpenAttachment`
- `CmdExec`
- `CmdDebugToggleDpiOverride`
- `CmdDebugShowDownloadedFonts`

## Setting paths

Names are qualified by their containing section. Array indices are omitted. Stored state, credentials and user preferences are different categories even when they share one settings file. This inventory includes names only, never values. Source: [Advanced settings](https://www.sumatrapdfreader.org/docs/Advanced-options-settings).

### Global

- `DefaultDisplayMode`
- `DefaultZoom`
- `DisableJavaScript`
- `AllowExternalImages`
- `EnableTeXEnhancements`
- `EscToExit`
- `FullPathInTitle`
- `InverseSearchCmdLine`
- `LazyLoading`
- `MainWindowBackground`
- `NoHomeTab`
- `NoCaj2pdfConverterDownload`
- `HomePageSortByFrequentlyRead`
- `HomePageViewMode`
- `ThumbnailsType`
- `FilePicker`
- `PrinterUI`
- `ReloadModifiedDocuments`
- `ShowSignatureStatus`
- `OfdTrustedCertificates`
- `TemporaryFileShowNotification`
- `TemporaryFileDestDir`
- `DownloadDirectory`
- `TemporaryFileAutoMove`
- `RememberOpenedFiles`
- `RememberStatePerDocument`
- `RestoreSession`
- `KeepMissingSessionTabs`
- `ReuseInstance`
- `OpenOnOtherDesktop`
- `ShowMenubar`
- `ShowMenubarWithTabs`
- `ShowPageNumberInTabs`
- `ShowHomePageReadingProgress`
- `ShowChaptersInEbooks`
- `ShowTips`
- `CustomColors`
- `ShowToolbar`
- `Toolbar`
- `ToolbarPosition`
- `PageInfoLocation`
- `ProgressBarDy`
- `SearchUIFloating`
- `ShowFavorites`
- `SidebarBottomView`
- `ShowSidebarMiddle`
- `SortFavoritesByName`
- `FavoritesManualOrder`
- `ShowToc`
- `AlwaysShowSidebar`
- `ShowThumbnailsIfNoToc`
- `SidebarOnRight`
- `SidebarAutoHide`
- `SidebarWindowSize`
- `ShowLinks`
- `ShowDocumentErrors`
- `HighlightFormFields`
- `ClickEdgeToTurnPage`
- `DisableLinks`
- `DisableLeftDragPan`
- `ExplorerQuickLook`
- `RememberViewOffsetOnPageTurn`
- `MouseWheelTurnsPage`
- `InvertMouseWheel`
- `ScrollEdgeTurnsPage`
- `ShowDocumentFocusIndicator`
- `ShowAnnotationNotification`
- `ShowFileNavigateHint`
- `ShowAnnotationAuthorInTooltip`
- `ShowTocPageNumbers`
- `TocWrapText`
- `AutoGenerateTOC`
- `ShowStartPage`
- `SidebarDx`
- `Scrollbars`
- `ScrollbarInSinglePage`
- `SmoothScroll`
- `ScrollLineAmount`
- `SaveMemory`
- `PaddingAfterLastPage`
- `ContinuousScrollFollowsRotation`
- `IgnoreDestinationZoom`
- `HighlightLinkDestination`
- `FlashFindMatch`
- `CitationHoverDelay`
- `ReadAloudVoiceId`
- `ReadAloudSpeed`
- `ReadAloudSentenceColor`
- `ReadAloudWordColor`
- `ReadingAutoScrollSpeed`
- `ReadingAutoScrollMinutes`
- `FastScrollOverScrollbar`
- `PreventSleepInFullscreen`
- `TabWidth`
- `TabsEqualWidth`
- `Theme`
- `HelpTheme`
- `LastLightTheme`
- `LastDarkTheme`
- `DocumentColorsFollowTheme`
- `TocDy`
- `SidebarMiddleDy`
- `ToolbarCustomLayout`
- `ToolbarShowReadAloud`
- `ToolbarSize`
- `TreeFontName`
- `TreeFontSize`
- `UIFontSize`
- `DisableAntiAlias`
- `EngineeringDrawingEnhance`
- `ShowLineWeights`
- `DisableAutoLinks`
- `UseSysColors`
- `UseTabs`
- `ShowCaption`
- `SelectionToolbar`
- `SelectionToolbarLayout`
- `TabsMru`
- `CtrlTabSimple`
- `ZoomLevels`
- `ZoomIncrement`
- `FitWidthPercent`
- `AIChatSidebarDx`
- `TranslateToLang`
- `TranslateFromLang`
- `TranslateEngine`
- `CustomScreenDPI`
- `DefaultPasswords`
- `UiLanguage`
- `VersionToSkip`
- `WindowState`
- `WindowPos`
- `SearchUIWindowPos`
- `AnnotationsWindowShowNote`
- `HelpWindowPos`
- `ReopenOnce`
- `TimeOfLastUpdateCheck`
- `OpenCountWeek`
- `PropWinPos`
- `CheckForUpdates`

### ReadingBar

- `ReadingBar.Background`
- `ReadingBar.Invert`
- `ReadingBar.Height`

### FixedPageUI

- `FixedPageUI.Grayscale`
- `FixedPageUI.TextColor`
- `FixedPageUI.BackgroundColor`
- `FixedPageUI.SelectionColor`
- `FixedPageUI.WindowMargin`
- `FixedPageUI.PageSpacing`
- `FixedPageUI.GradientColors`
- `FixedPageUI.WindowBgCol`
- `FixedPageUI.PageGrid.Width`
- `FixedPageUI.PageGrid.Height`
- `FixedPageUI.PageGrid.Subdivisions`
- `FixedPageUI.PageGrid.OffsetX`
- `FixedPageUI.PageGrid.OffsetY`
- `FixedPageUI.PageGrid.Slant`
- `FixedPageUI.PageGrid.Color`
- `FixedPageUI.PageGrid.Style`
- `FixedPageUI.PageGrid.Units`

### EBookUI

- `EBookUI.FontName`
- `EBookUI.FontSize`
- `EBookUI.Margin`
- `EBookUI.LineSpacing`
- `EBookUI.LayoutDx`
- `EBookUI.LayoutDy`
- `EBookUI.IgnoreDocumentCSS`
- `EBookUI.CustomCSS`
- `EBookUI.WindowBgCol`
- `EBookUI.DefaultDisplayMode`

### ComicBookUI

- `ComicBookUI.WindowMargin`
- `ComicBookUI.PageSpacing`
- `ComicBookUI.CbxMangaMode`
- `ComicBookUI.WindowBgCol`
- `ComicBookUI.LimitToWindowWidth`
- `ComicBookUI.LimitToWindowHeight`
- `ComicBookUI.AutoRotateWidePages`
- `ComicBookUI.DefaultDisplayMode`
- `ComicBookUI.DefaultZoom`
- `ComicBookUI.LandscapeAsSpread`

### ImageUI

- `ImageUI.WindowBgCol`
- `ImageUI.DefaultZoom`
- `ImageUI.LimitToWindowWidth`
- `ImageUI.LimitToWindowHeight`
- `ImageUI.LandscapeAsSpread`

### ChmUI

- `ChmUI.UseFixedPageUI`
- `ChmUI.FontName`

### MarkdownUI

- `MarkdownUI.UseFixedPageUI`
- `MarkdownUI.PreserveLineBreaks`

### HtmlUI

- `HtmlUI.UseFixedPageUI`

### ClaudeCode

- `ClaudeCode.Model`
- `ClaudeCode.Models`
- `ClaudeCode.Effort`
- `ClaudeCode.SkipPermissions`
- `ClaudeCode.BgColor`

### GrokBuild

- `GrokBuild.Model`
- `GrokBuild.Models`
- `GrokBuild.Effort`
- `GrokBuild.AlwaysApprove`
- `GrokBuild.BgColor`

### CodexBuild

- `CodexBuild.Model`
- `CodexBuild.Models`
- `CodexBuild.Sandbox`
- `CodexBuild.SkipSandbox`
- `CodexBuild.BgColor`

### AntiGravity

- `AntiGravity.Model`
- `AntiGravity.Models`
- `AntiGravity.Effort`
- `AntiGravity.AutoApprove`
- `AntiGravity.BgColor`

### CustomAI

- `CustomAI.Name`
- `CustomAI.BaseURL`
- `CustomAI.ApiKeyEnv`
- `CustomAI.ApiKey`
- `CustomAI.Model`
- `CustomAI.Models`
- `CustomAI.IncludeCurrentPage`
- `CustomAI.BgColor`

### AIChat

- `AIChat.FontName`
- `AIChat.FontSize`
- `AIChat.LineSpacing`
- `AIChat.MessagePadding`
- `AIChat.Bubbles`

### Annotations

- `Annotations.HighlightColor`
- `Annotations.UnderlineColor`
- `Annotations.SquigglyColor`
- `Annotations.StrikeOutColor`
- `Annotations.FreeTextColor`
- `Annotations.FreeTextBackgroundColor`
- `Annotations.FreeTextOpacity`
- `Annotations.FreeTextSize`
- `Annotations.FreeTextBorderWidth`
- `Annotations.FreeTextAlignment`
- `Annotations.PresetColors`
- `Annotations.TextIconColor`
- `Annotations.LineColor`
- `Annotations.PolyLineColor`
- `Annotations.SquareColor`
- `Annotations.CircleColor`
- `Annotations.PolygonColor`
- `Annotations.InkColor`
- `Annotations.InkColors`
- `Annotations.InkBorderWidth`
- `Annotations.StampColor`
- `Annotations.CaretColor`
- `Annotations.FileAttachmentColor`
- `Annotations.TextIconType`
- `Annotations.DefaultAuthor`
- `Annotations.SignatureImage`
- `Annotations.PopupFontSize`

### ExternalViewers

- `ExternalViewers.CommandLine`
- `ExternalViewers.Name`
- `ExternalViewers.Filter`
- `ExternalViewers.Key`
- `ExternalViewers.ToolbarText`
- `ExternalViewers.ToolbarSvgIcon`

### ForwardSearch

- `ForwardSearch.HighlightOffset`
- `ForwardSearch.HighlightWidth`
- `ForwardSearch.HighlightColor`
- `ForwardSearch.HighlightPermanent`

### PrinterDefaults

- `PrinterDefaults.PrintScale`
- `PrinterDefaults.Collate`
- `PrinterDefaults.PrintDpi`
- `PrinterDefaults.AutoRotate`

### Fullscreen

- `Fullscreen.ShowToolbar`
- `Fullscreen.Toolbar`
- `Fullscreen.ShowMenubar`
- `Fullscreen.DisplayMode`

### SelectionHandlers

- `SelectionHandlers.URL`
- `SelectionHandlers.Name`
- `SelectionHandlers.Key`
- `SelectionHandlers.Exe`
- `SelectionHandlers.Method`
- `SelectionHandlers.Body`
- `SelectionHandlers.ContentType`
- `SelectionHandlers.Headers`
- `SelectionHandlers.SelectToolbarNameOrSvg`
- `SelectionHandlers.ToolbarText`
- `SelectionHandlers.ToolbarSvgIcon`

### TextSnippets

- `TextSnippets.Name`
- `TextSnippets.Text`
- `TextSnippets.Key`

### Shortcuts

- `Shortcuts.Cmd`
- `Shortcuts.Key`
- `Shortcuts.Name`
- `Shortcuts.ToolbarText`
- `Shortcuts.ToolbarSvgIcon`

### Themes

- `Themes.Name`
- `Themes.TextColor`
- `Themes.BackgroundColor`
- `Themes.ControlBackgroundColor`
- `Themes.ActiveTabBackgroundColor`
- `Themes.InactiveTabBackgroundColor`
- `Themes.LinkColor`
- `Themes.DisabledTextColor`
- `Themes.DarkerTextColor`
- `Themes.HotBackgroundColor`
- `Themes.EdgeColor`
- `Themes.HotEdgeColor`
- `Themes.DisabledEdgeColor`
- `Themes.ErrorBackgroundColor`
- `Themes.NotificationBackgroundColor`
- `Themes.NotificationHighlightColor`
- `Themes.NotificationHighlightTextColor`
- `Themes.HoverPreviewBackgroundColor`
- `Themes.HoverPreviewEdgeColor`
- `Themes.ColorizeControls`

### TabGroups

- `TabGroups.Name`
- `TabGroups.TabFiles.Path`
- `TabGroups.AutoSync`

### FileStates

- `FileStates.Favorites.Name`
- `FileStates.Favorites.PageNo`
- `FileStates.Favorites.PageLabel`
- `FileStates.Favorites.ScrollPos`
- `FileStates.Favorites.IsTemporary`
- `FileStates.EBookUI.FontName`
- `FileStates.EBookUI.FontSize`
- `FileStates.EBookUI.Margin`
- `FileStates.EBookUI.LineSpacing`
- `FileStates.EBookUI.LayoutDx`
- `FileStates.EBookUI.LayoutDy`
- `FileStates.EBookUI.IgnoreDocumentCSS`
- `FileStates.EBookUI.CustomCSS`
- `FileStates.TocState`
- `FileStates.FilePath`
- `FileStates.DecryptionKey`
- `FileStates.SidebarView`
- `FileStates.DisplayMode`
- `FileStates.Zoom`
- `FileStates.BgCol`
- `FileStates.TabCol`
- `FileStates.LayerStates`
- `FileStates.MeasurementUnit`
- `FileStates.MeasurementScale`
- `FileStates.OpenCount`
- `FileStates.PageNo`
- `FileStates.PageCount`
- `FileStates.Rotation`
- `FileStates.Brightness`
- `FileStates.Contrast`
- `FileStates.WindowState`
- `FileStates.SidebarDx`
- `FileStates.ScrollPos`
- `FileStates.WindowPos`
- `FileStates.IsPinned`
- `FileStates.IsMissing`
- `FileStates.UseDefaultState`
- `FileStates.ShowToc`
- `FileStates.DisplayR2L`
- `FileStates.UniformPageWidth`
- `FileStates.TrimEmptyMargins`
- `FileStates.UserCrop`
- `FileStates.FreePan`
- `FileStates.FacingStepOne`
- `FileStates.Mirror`
- `FileStates.ReadingAutoScrollSpeed`
- `FileStates.ReadColumns`

### SessionData

- `SessionData.TabStates.FilePath`
- `SessionData.TabStates.DisplayMode`
- `SessionData.TabStates.PageNo`
- `SessionData.TabStates.Zoom`
- `SessionData.TabStates.Rotation`
- `SessionData.TabStates.ScrollPos`
- `SessionData.TabStates.ShowToc`
- `SessionData.TabStates.SidebarView`
- `SessionData.TabStates.TocState`
- `SessionData.TabStates.Group`
- `SessionData.TabStates.GroupName`
- `SessionData.TabStates.GroupColor`
- `SessionData.TabStates.GroupCollapsed`
- `SessionData.TabIndex`
- `SessionData.WindowState`
- `SessionData.WindowPos`
- `SessionData.SidebarDx`
- `SessionData.VirtualDesktopId`

## JavaScript API signatures

These signatures describe the documented MuPDF based scripting interface. They are not PDFium or PDFKit APIs and cannot be called directly from Gravity. Source: [JavaScript API reference](https://www.sumatrapdfreader.org/docs/Tool-run-javascript-reference).

### Global functions

- `mupdf.installLoadFontFunction(callback)`
- `mupdf.enableICC()`
- `mupdf.disableICC()`
- `mupdf.emptyStore()`
- `mupdf.shrinkStore(percent)`
- `mupdf.setUserCSS(stylesheet, useDocumentStyles)`

### Archive

- `Archive(path)`
- `Archive(buffer)`
- `Archive.prototype.getFormat()`
- `Archive.prototype.countEntries()`
- `Archive.prototype.listEntry(idx)`
- `Archive.prototype.hasEntry(name)`
- `Archive.prototype.readEntry(name)`

### Buffer

- `Buffer(data)`
- `Buffer.prototype.length`
- `Buffer.prototype.[n]`
- `Buffer.prototype.asString()`
- `Buffer.prototype.getLength()`
- `Buffer.prototype.readByte(at)`
- `Buffer.prototype.slice(start, end)`
- `Buffer.prototype.write(str)`
- `Buffer.prototype.writeBuffer(data)`
- `Buffer.prototype.writeByte(byte)`
- `Buffer.prototype.writeLine(str)`
- `Buffer.prototype.writeRune(c)`
- `Buffer.prototype.save(filename)`

### ColorSpace

- `ColorSpace(from, name)`
- `ColorSpace.DeviceGray`
- `ColorSpace.DeviceRGB`
- `ColorSpace.DeviceBGR`
- `ColorSpace.DeviceCMYK`
- `ColorSpace.Lab`
- `ColorSpace.prototype.getName()`
- `ColorSpace.prototype.getNumberOfComponents()`
- `ColorSpace.prototype.getType()`
- `ColorSpace.prototype.isCMYK()`
- `ColorSpace.prototype.isDeviceN()`
- `ColorSpace.prototype.isGray()`
- `ColorSpace.prototype.isIndexed()`
- `ColorSpace.prototype.isLab()`
- `ColorSpace.prototype.isRGB()`
- `ColorSpace.prototype.isSubtractive()`
- `ColorSpace.prototype.toString()`

### DOM

- `DOM.prototype.body()`
- `DOM.prototype.documentElement()`
- `DOM.prototype.createElement(tag)`
- `DOM.prototype.createTextNode(text)`
- `DOM.prototype.find(tag, attribute, value)`
- `DOM.prototype.findNext(tag, attribute, value)`
- `DOM.prototype.appendChild(dom, childDom)`
- `DOM.prototype.insertBefore(dom, elementDom)`
- `DOM.prototype.insertAfter(dom, elementDom)`
- `DOM.prototype.remove()`
- `DOM.prototype.clone()`
- `DOM.prototype.firstChild()`
- `DOM.prototype.parent()`
- `DOM.prototype.next()`
- `DOM.prototype.previous()`
- `DOM.prototype.addAttribute(attribute, value)`
- `DOM.prototype.removeAttribute(attribute)`
- `DOM.prototype.getAttribute(attribute)`
- `DOM.prototype.getAttributes()`
- `DOM.prototype.getText()`
- `DOM.prototype.getTag()`

### DefaultColorSpaces

- `DefaultColorSpaces()`
- `DefaultColorSpaces.prototype.getDefaultGray()`
- `DefaultColorSpaces.prototype.getDefaultRGB()`
- `DefaultColorSpaces.prototype.getDefaultCMYK()`
- `DefaultColorSpaces.prototype.getOutputIntent()`
- `DefaultColorSpaces.prototype.setDefaultGray(colorspace)`
- `DefaultColorSpaces.prototype.setDefaultRGB(colorspace)`
- `DefaultColorSpaces.prototype.setDefaultCMYK(colorspace)`
- `DefaultColorSpaces.prototype.setOutputIntent(colorspace)`

### Device

- `Device(callbacks)`
- `Device.BLEND_NORMAL`
- `Device.BLEND_MULTIPLY`
- `Device.BLEND_SCREEN`
- `Device.BLEND_OVERLAY`
- `Device.BLEND_DARKEN`
- `Device.BLEND_LIGHTEN`
- `Device.BLEND_COLOR_DODGE`
- `Device.BLEND_COLOR_BURN`
- `Device.BLEND_HARD_LIGHT`
- `Device.BLEND_SOFT_LIGHT`
- `Device.BLEND_DIFFERENCE`
- `Device.BLEND_EXCLUSION`
- `Device.BLEND_HUE`
- `Device.BLEND_SATURATION`
- `Device.BLEND_COLOR`
- `Device.BLEND_LUMINOSITY`
- `Device.prototype.close()`
- `Device.prototype.fillPath(path, evenOdd, ctm, colorspace, color, alpha)`
- `Device.prototype.strokePath(path, stroke, ctm, colorspace, color, alpha)`
- `Device.prototype.clipPath(path, evenOdd, ctm)`
- `Device.prototype.clipStrokePath(path, stroke, ctm)`
- `Device.prototype.fillText(text, ctm, colorspace, color, alpha)`
- `Device.prototype.strokeText(text, stroke, ctm, colorspace, color, alpha)`
- `Device.prototype.clipText(text, ctm)`
- `Device.prototype.clipStrokeText(text, stroke, ctm)`
- `Device.prototype.ignoreText(text, ctm)`
- `Device.prototype.fillShade(shade, ctm, alpha)`
- `Device.prototype.fillImage(image, ctm, alpha)`
- `Device.prototype.fillImageMask(image, ctm, colorspace, color, alpha)`
- `Device.prototype.clipImageMask(image, ctm)`
- `Device.prototype.popClip()`
- `Device.prototype.beginMask(area, luminosity, colorspace, color)`
- `Device.prototype.endMask()`
- `Device.prototype.beginGroup(area, colorspace, isolated, knockout, blendmode, alpha)`
- `Device.prototype.endGroup()`
- `Device.prototype.beginTile(area, view, xstep, ystep, ctm, id, doc_id)`
- `Device.prototype.endTile()`
- `Device.prototype.renderFlags(set, clear)`
- `Device.prototype.setDefaultColorSpaces(defaultCS)`
- `Device.prototype.beginLayer(name)`
- `Device.prototype.endLayer()`
- `Device.prototype.beginStructure(structure, raw, index)`
- `Device.prototype.endStructure()`
- `Device.prototype.beginMetatext(meta, text)`
- `Device.prototype.endMetatext()`

### DisplayList

- `DisplayList(mediabox)`
- `DisplayList.prototype.run(device, matrix)`
- `DisplayList.prototype.getBounds()`
- `DisplayList.prototype.toPixmap(matrix, colorspace, alpha)`
- `DisplayList.prototype.toStructuredText(options)`
- `DisplayList.prototype.search(needle, max_hits)`
- `DisplayList.protoype.decodeBarcode(subarea, rotate)`

### DisplayListDevice

- `DisplayListDevice(displayList)`

### Document

- `Document.PERMISSION_PRINT`
- `Document.PERMISSION_EDIT`
- `Document.PERMISSION_COPY`
- `Document.PERMISSION_ANNOTATE`
- `Document.PERMISSION_FORM`
- `Document.PERMISSION_ACCESSIBILITY`
- `Document.PERMISSION_ASSEMBLE`
- `Document.PERMISSION_PRINT_HQ`
- `Document.prototype.needsPassword()`
- `Document.prototype.authenticatePassword(password)`
- `Document.prototype.hasPermission(permission)`
- `Document.prototype.getMetaData(key)`
- `Document.prototype.setMetaData(key, value)`
- `Document.prototype.isReflowable()`
- `Document.prototype.layout(pageWidth, pageHeight, fontSize)`
- `Document.prototype.countPages()`
- `Document.prototype.loadPage(number)`
- `Document.prototype.loadOutline()`
- `Document.prototype.outlineIterator()`
- `Document.prototype.resolveLink(link)`
- `Document.prototype.resolveLinkDestination(link)`
- `Document.prototype.isPDF()`
- `Document.prototype.asPDF()`
- `Document.prototype.formatLinkURI(linkDestination)`

### DocumentWriter

- `DocumentWriter.prototype.beginPage(mediabox)`
- `DocumentWriter.prototype.endPage()`
- `DocumentWriter.prototype.close()`

### DrawDevice

- `DrawDevice(matrix, pixmap)`

### Font

- `Font.SIMPLE_ENCODING_LATIN`
- `Font.SIMPLE_ENCODING_GREEK`
- `Font.SIMPLE_ENCODING_CYRILLIC`
- `Font.prototype.getName()`
- `Font.prototype.encodeCharacter(unicode)`
- `Font.prototype.advanceGlyph(glyph , wmode)`
- `Font.prototype.isBold()`
- `Font.prototype.isItalic()`
- `Font.prototype.isMono()`
- `Font.prototype.isSerif()`

### Image

- `Image.prototype.getWidth()`
- `Image.prototype.getHeight()`
- `Image.prototype.getXResolution()`
- `Image.prototype.getYResolution()`
- `Image.prototype.getColorSpace()`
- `Image.prototype.getNumberOfComponents()`
- `Image.prototype.getBitsPerComponent()`
- `Image.prototype.getImageMask()`
- `Image.prototype.getMask()`
- `Image.prototype.getInterpolate()`
- `Image.prototype.getColorKey()`
- `Image.prototype.getDecode()`
- `Image.prototype.getOrientation()`
- `Image.prototype.setOrientation(orientation)`
- `Image.prototype.toPixmap(scaledWidth, scaledHeight)`

### Link

- `Link.prototype.getBounds()`
- `Link.prototype.setBounds(rect)`
- `Link.prototype.getURI()`
- `Link.prototype.setURI(uri)`
- `Link.prototype.isExternal()`

### LinkDestination

- `LinkDestination(chapter, page, type, x, y, width, height)`
- `LinkDestination.FIT`
- `LinkDestination.FIT_H`
- `LinkDestination.FIT_V`
- `LinkDestination.FIT_B`
- `LinkDestination.FIT_BH`
- `LinkDestination.FIT_BV`
- `LinkDestination.FIT_R`
- `LinkDestination.XYZ`
- `LinkDestination.prototype.chapter`
- `LinkDestination.prototype.page`
- `LinkDestination.prototype.type`
- `LinkDestination.prototype.x`
- `LinkDestination.prototype.y`
- `LinkDestination.prototype.width`
- `LinkDestination.prototype.height`
- `LinkDestination.prototype.zoom`

### Matrix

- `Matrix.identity`
- `Matrix.scale(sx, sy)`
- `Matrix.translate(tx, ty)`
- `Matrix.rotate(theta)`
- `Matrix.concat(a, b)`
- `Matrix.invert(matrix)`

### MultiArchive

- `MultiArchive()`
- `MultiArchive.prototype.mountArchive(subArchive, path)`

### OutlineIterator

- `OutlineIterator.ITERATOR_DID_NOT_MOVE`
- `OutlineIterator.ITERATOR_AT_ITEM`
- `OutlineIterator.ITERATOR_AT_EMPTY`
- `OutlineIterator.FLAG_BOLD`
- `OutlineIterator.FLAG_ITALIC`
- `OutlineIterator.prototype.item()`
- `OutlineIterator.prototype.next()`
- `OutlineIterator.prototype.prev()`
- `OutlineIterator.prototype.up()`
- `OutlineIterator.prototype.down()`
- `OutlineIterator.prototype.insert(item)`
- `OutlineIterator.prototype.delete()`
- `OutlineIterator.prototype.update(item)`

### Page

- `Page.MEDIA_BOX`
- `Page.CROP_BOX`
- `Page.BLEED_BOX`
- `Page.TRIM_BOX`
- `Page.ART_BOX`
- `Page.prototype.getBounds(box)`
- `Page.prototype.run(device, transform)`
- `Page.prototype.runPageContents(device, transform)`
- `Page.prototype.runPageAnnots(device, transform)`
- `Page.prototype.runPageWidgets(device, transform)`
- `Page.prototype.toPixmap(matrix, colorspace, alpha, showExtras)`
- `Page.prototype.toDisplayList(showExtras)`
- `Page.prototype.toStructuredText(options)`
- `Page.prototype.search(needle, maxHits)`
- `Page.prototype.getLinks()`
- `Page.prototype.createLink(rect, uri)`
- `Page.prototype.deleteLink(link)`
- `Page.prototype.getLabel()`
- `Page.prototype.isPDF()`
- `Page.prototype.decodeBarcode(subarea, rotate)`

### Path

- `Path()`
- `Path.prototype.closePath()`
- `Path.prototype.curveTo(x1, y1, x2, y2, x3, y3)`
- `Path.prototype.curveToV(cx, cy, ex, ey)`
- `Path.prototype.curveToY(cx, cy, ex, ey)`
- `Path.prototype.getBounds(strokeState, transform)`
- `Path.prototype.lineTo(x, y)`
- `Path.prototype.moveTo(x, y)`
- `Path.prototype.rect(x1, y1, x2, y2)`
- `Path.prototype.transform(matrix)`
- `Path.prototype.walk(walker)`

### PathWalker

- `closePath()`
- `curveTo(x1, y1, x2, y2, x3, y3)`
- `lineTo(x, y)`
- `moveTo(x, y)`

### Pixmap

- `Pixmap.prototype.clear(value)`
- `Pixmap.prototype.getBounds()`
- `Pixmap.prototype.getWidth()`
- `Pixmap.prototype.getHeight()`
- `Pixmap.prototype.getNumberOfComponents()`
- `Pixmap.prototype.getAlpha()`
- `Pixmap.prototype.getStride()`
- `Pixmap.prototype.getColorSpace()`
- `Pixmap.prototype.setResolution(x, y)`
- `Pixmap.prototype.getX()`
- `Pixmap.prototype.getY()`
- `Pixmap.prototype.getXResolution()`
- `Pixmap.prototype.getYResolution()`
- `Pixmap.prototype.invert()`
- `Pixmap.prototype.invertLuminance()`
- `Pixmap.prototype.gamma(p)`
- `Pixmap.prototype.tint(black, white)`
- `Pixmap.prototype.warp(points, width, height)`
- `Pixmap.prototype.autowarp(points)`
- `Pixmap.prototype.convertToColorSpace(colorspace, proof, defaultColorSpaces, colorParams, keepAlpha)`
- `Pixmap.prototype.getPixels()`
- `Pixmap.prototype.asPNG()`
- `Pixmap.prototype.asPSD()`
- `Pixmap.prototype.asPAM()`
- `Pixmap.prototype.asJPEG(quality, invert_cmyk)`
- `Pixmap.prototype.decodeBarcode(rotate)`
- `Pixmap.prototype.encodeBarcode(barcodeType, contents, size, errorCorrectionLevel, quietZones, humanReadableText)`
- `Pixmap.prototype.getSample(x, y, index)`
- `Pixmap.prototype.saveAsPNG(filename)`
- `Pixmap.prototype.saveAsJPEG(filename, quality)`
- `Pixmap.prototype.saveAsPAM(filename)`
- `Pixmap.prototype.saveAsPNM(filename)`
- `Pixmap.prototype.saveAsPBM(filename)`
- `Pixmap.prototype.saveAsPKM(filename)`
- `Pixmap.prototype.saveAsJPX(filename, quality)`
- `Pixmap.prototype.detectDocument(points)`
- `Pixmap.prototype.detectSkew()`
- `Pixmap.prototype.deskew(angle, border)`
- `Pixmap.prototype.computeMD5()`

### Point

- `Point.transform(point, matrix)`

### Quad

- `Quad.empty`
- `Quad.invalid`
- `Quad.infinite`
- `Quad.isEmpty(quad)`
- `Quad.isValid(quad)`
- `Quad.isInfinite(quad)`
- `Quad.transform(quad, matrix)`
- `Quad.isPointInside(quad, point)`
- `Quad.quadFromRect(rect)`

### Rect

- `Rect.empty`
- `Rect.invalid`
- `Rect.infinite`
- `Rect.isEmpty(rect)`
- `Rect.isValid(rect)`
- `Rect.isInfinite(rect)`
- `Rect.transform(rect, matrix)`
- `Rect.isPointInside(rect, point)`
- `Rect.rectFromQuad(quad)`

### Shade

- `Shade.prototype.getBounds()`

### Story

- `Story(contents, userCSS, em, archive)`
- `Story.prototype.document()`
- `Story.prototype.place(rect, flags)`
- `Story.prototype.draw(device, transform)`

### StrokeState

- `StrokeState(template)`
- `StrokeState.prototype.getLineCap()`
- `StrokeState.prototype.getLineJoin()`
- `StrokeState.prototype.getLineWidth()`
- `StrokeState.prototype.getMiterLimit()`
- `StrokeState.prototype.getDashPhase()`
- `StrokeState.prototype.getDashPattern()`

### StructuredText

- `StructuredText.SEARCH_EXACT`
- `StructuredText.SEARCH_IGNORE_CASE`
- `StructuredText.SEARCH_IGNORE_DIACRITICS`
- `StructuredText.SEARCH_REGEXP`
- `StructuredText.SEARCH_KEEP_LINES`
- `StructuredText.SEARCH_KEEP_PARAGRAPHS`
- `StructuredText.SEARCH_KEEP_HYPHENS`
- `StructuredText.prototype.search(needle, maxHits)`
- `StructuredText.prototype.highlight(p, q, maxHits)`
- `StructuredText.prototype.copy(p, q)`
- `StructuredText.prototype.walk(walker)`
- `StructuredText.prototype.asText()`
- `StructuredText.prototype.asHTML(id)`
- `StructuredText.prototype.asJSON(scale)`

### StructuredTextWalker

- `beginTextBlock(bbox)`
- `endTextBlock()`
- `beginLine(bbox, wmode, direction)`
- `endLine()`
- `beginStruct(standard, raw, index)`
- `endStruct()`
- `onChar(c, origin, font, size, quad, color, flags)`
- `onImageBlock(bbox, transform, image)`
- `onVector(bbox, flags, rgb)`

### Text

- `Text()`
- `Text.prototype.getBounds(strokeState, transform)`
- `Text.prototype.showGlyph(font, trm, gid, uni, wmode)`
- `Text.prototype.showString(font, trm, str, wmode)`
- `Text.prototype.walk(walker)`

### TextWalker

- `beginSpan(font, trm, wmode, bidiLevel, markupDirection, language)`
- `endSpan()`
- `showGlyph(font, trm, gid, ucs, wmode, bidiLevel)`

### TreeArchive

- `TreeArchive()`
- `TreeArchive.prototype.add(name, buffer)`

### PDFAnnotation

- `PDFAnnotation.prototype.getBounds()`
- `PDFAnnotation.prototype.run(device, matrix)`
- `PDFAnnotation.prototype.toPixmap(matrix, colorspace, alpha)`
- `PDFAnnotation.prototype.toDisplayList()`
- `PDFAnnotation.prototype.getObject()`
- `PDFAnnotation.prototype.setAppearance(appearance, state, transform, bbox, resources, contents)`
- `PDFAnnotation.prototype.update()`
- `PDFAnnotation.prototype.setAppearanceFromDisplayList(appearance, state, transform, list)`
- `PDFAnnotation.prototype.getHiddenForEditing()`
- `PDFAnnotation.prototype.setHiddenForEditing(hidden)`
- `PDFAnnotation.prototype.getHot()`
- `PDFAnnotation.prototype.setHot(hot)`
- `PDFAnnotation.prototype.requestSynthesis()`
- `PDFAnnotation.prototype.requestResynthesis()`
- `PDFAnnotation.prototype.process(processor)`
- `PDFAnnotation.prototype.applyRedaction(blackBoxes, imageMethod, lineArtMethod, textMethod)`
- `PDFAnnotation.prototype.getType()`
- `PDFAnnotation.prototype.getFlags()`
- `PDFAnnotation.prototype.setFlags(flags)`
- `PDFAnnotation.prototype.getContents()`
- `PDFAnnotation.prototype.setContents(text)`
- `PDFAnnotation.prototype.getCreationDate()`
- `PDFAnnotation.prototype.setCreationDate(date)`
- `PDFAnnotation.prototype.getModificationDate()`
- `PDFAnnotation.prototype.setModificationDate(date)`
- `PDFAnnotation.prototype.getLanguage()`
- `PDFAnnotation.prototype.setLanguage(language)`
- `PDFAnnotation.prototype.hasRect()`
- `PDFAnnotation.prototype.getRect()`
- `PDFAnnotation.prototype.setRect(rect)`
- `PDFAnnotation.prototype.hasRichContents()`
- `PDFAnnotation.prototype.getRichContents()`
- `PDFAnnotation.prototype.setRichContents(plainText, richText)`
- `PDFAnnotation.prototype.getRichDefaults()`
- `PDFAnnotation.prototype.setRichDefaults(style)`
- `PDFAnnotation.prototype.getColor()`
- `PDFAnnotation.prototype.setColor(color)`
- `PDFAnnotation.prototype.getOpacity()`
- `PDFAnnotation.prototype.setOpacity(opacity)`
- `PDFAnnotation.prototype.hasQuadding()`
- `PDFAnnotation.prototype.getQuadding()`
- `PDFAnnotation.prototype.setQuadding(value)`
- `PDFAnnotation.prototype.hasAuthor()`
- `PDFAnnotation.prototype.getAuthor()`
- `PDFAnnotation.prototype.setAuthor(author)`
- `PDFAnnotation.prototype.hasBorder()`
- `PDFAnnotation.prototype.getBorderStyle()`
- `PDFAnnotation.prototype.setBorderStyle(style)`
- `PDFAnnotation.prototype.getBorderWidth()`
- `PDFAnnotation.prototype.setBorderWidth(width)`
- `PDFAnnotation.prototype.getBorderDashCount()`
- `PDFAnnotation.prototype.getBorderDashItem(idx)`
- `PDFAnnotation.prototype.setBorderDashPattern(list)`
- `PDFAnnotation.prototype.clearBorderDash()`
- `PDFAnnotation.prototype.addBorderDashItem(length)`
- `PDFAnnotation.prototype.hasBorderEffect()`
- `PDFAnnotation.prototype.getBorderEffect()`
- `PDFAnnotation.prototype.setBorderEffect(effect)`
- `PDFAnnotation.prototype.getBorderEffectIntensity()`
- `PDFAnnotation.prototype.setBorderEffectIntensity(intensity)`
- `PDFAnnotation.prototype.hasCallout()`
- `PDFAnnotation.prototype.setCalloutLine(line)`
- `PDFAnnotation.prototype.getCalloutLine()`
- `PDFAnnotation.prototype.setCalloutPoint(p)`
- `PDFAnnotation.prototype.getCalloutPoint()`
- `PDFAnnotation.prototype.setCalloutStyle(style)`
- `PDFAnnotation.prototype.getCalloutStyle()`
- `PDFAnnotation.prototype.hasDefaultAppearance()`
- `PDFAnnotation.prototype.getDefaultAppearance()`
- `PDFAnnotation.prototype.setDefaultAppearance(font, size, color)`
- `PDFAnnotation.prototype.hasFilespec()`
- `PDFAnnotation.prototype.getFilespec()`
- `PDFAnnotation.prototype.setFilespec(fs)`
- `PDFAnnotation.prototype.hasIcon()`
- `PDFAnnotation.prototype.getIcon()`
- `PDFAnnotation.prototype.setIcon(name)`
- `PDFAnnotation.prototype.hasInkList()`
- `PDFAnnotation.prototype.getInkList()`
- `PDFAnnotation.prototype.setInkList(inkList)`
- `PDFAnnotation.prototype.clearInkList()`
- `PDFAnnotation.prototype.addInkListStroke()`
- `PDFAnnotation.prototype.addInkListStrokeVertex(v)`
- `PDFAnnotation.prototype.hasInteriorColor()`
- `PDFAnnotation.prototype.getInteriorColor()`
- `PDFAnnotation.prototype.setInteriorColor(color)`
- `PDFAnnotation.prototype.hasLine()`
- `PDFAnnotation.prototype.getLine()`
- `PDFAnnotation.prototype.setLine(a, b)`
- `PDFAnnotation.prototype.hasLineEndingStyles()`
- `PDFAnnotation.prototype.getLineEndingStyles()`
- `PDFAnnotation.prototype.setLineEndingStyles(start, end)`
- `PDFAnnotation.prototype.setLineLeader(v)`
- `PDFAnnotation.prototype.getLineLeader()`
- `PDFAnnotation.prototype.setLineLeaderExtension(v)`
- `PDFAnnotation.prototype.getLineLeaderExtension()`
- `PDFAnnotation.prototype.setLineLeaderOffset(v)`
- `PDFAnnotation.prototype.getLineLeaderOffset()`
- `PDFAnnotation.prototype.setLineCaption(on)`
- `PDFAnnotation.prototype.getLineCaption()`
- `PDFAnnotation.prototype.setLineCaptionOffset(point)`
- `PDFAnnotation.prototype.getLineCaptionOffset()`
- `PDFAnnotation.prototype.hasOpen()`
- `PDFAnnotation.prototype.getIsOpen()`
- `PDFAnnotation.prototype.setIsOpen(state)`
- `PDFAnnotation.prototype.hasPopup()`
- `PDFAnnotation.prototype.getPopup()`
- `PDFAnnotation.prototype.setPopup(rect)`
- `PDFAnnotation.prototype.hasQuadPoints()`
- `PDFAnnotation.prototype.getQuadPoints()`
- `PDFAnnotation.prototype.setQuadPoints(quadList)`
- `PDFAnnotation.prototype.clearQuadPoints()`
- `PDFAnnotation.prototype.addQuadPoint(quad)`
- `PDFAnnotation.prototype.hasVertices()`
- `PDFAnnotation.prototype.getVertices()`
- `PDFAnnotation.prototype.setVertices(vertices)`
- `PDFAnnotation.prototype.clearVertices()`
- `PDFAnnotation.prototype.addVertex(vertex)`
- `PDFAnnotation.prototype.getStampImageObject()`
- `PDFAnnotation.prototype.setStampImageObject(imgobj)`
- `PDFAnnotation.prototype.setStampImage(img)`
- `PDFAnnotation.prototype.hasIntent()`
- `PDFAnnotation.prototype.getIntent()`
- `PDFAnnotation.prototype.setIntent(intent)`
- `PDFAnnotation.prototype.eventEnter()`
- `PDFAnnotation.prototype.eventExit()`
- `PDFAnnotation.prototype.eventDown()`
- `PDFAnnotation.prototype.eventUp()`
- `PDFAnnotation.prototype.eventFocus()`
- `PDFAnnotation.prototype.eventBlur()`

### PDFDocument

- `PDFDocument()`
- `PDFDocument.formatURIFromPathAndDest(path, destination)`
- `PDFDocument.appendDestToURI(uri, destination)`
- `PDFDocument.prototype.needsPassword()`
- `PDFDocument.prototype.authenticatePassword(password)`
- `PDFDocument.prototype.hasPermission(permission)`
- `PDFDocument.prototype.getVersion()`
- `PDFDocument.prototype.setLanguage(lang)`
- `PDFDocument.prototype.getLanguage()`
- `PDFDocument.prototype.wasPureXFA()`
- `PDFDocument.prototype.wasRepaired()`
- `PDFDocument.prototype.loadNameTree(treeName)`
- `PDFDocument.prototype.newNull()`
- `PDFDocument.prototype.newBoolean(v)`
- `PDFDocument.prototype.newInteger(v)`
- `PDFDocument.prototype.newReal(v)`
- `PDFDocument.prototype.newString(v)`
- `PDFDocument.prototype.newByteString(v)`
- `PDFDocument.prototype.newName(v)`
- `PDFDocument.prototype.newIndirect(objectNumber, generation)`
- `PDFDocument.prototype.newArray()`
- `PDFDocument.prototype.newDictionary()`
- `PDFDocument.prototype.getTrailer()`
- `PDFDocument.prototype.countObjects()`
- `PDFDocument.prototype.createObject()`
- `PDFDocument.prototype.deleteObject(num)`
- `PDFDocument.prototype.addObject(obj)`
- `PDFDocument.prototype.addStream(buf, obj)`
- `PDFDocument.prototype.addRawStream(buf, obj)`
- `PDFDocument.prototype.setPageTreeCache(enabled)`
- `PDFDocument.protoype.findPage(number)`
- `PDFDocument.prototype.findPageNumber(page)`
- `PDFDocument.prototype.lookupDest(obj)`
- `PDFDocument.prototype.rearrangePages(pages)`
- `PDFDocument.prototype.insertPage(at, page)`
- `PDFDocument.prototype.deletePage(index)`
- `PDFDocument.prototype.addPage(mediabox, rotate, resources, contents)`
- `PDFDocument.prototype.addSimpleFont(font, encoding)`
- `PDFDocument.prototype.addCJKFont(font, language, wmode, style)`
- `PDFDocument.prototype.addFont(font)`
- `PDFDocument.prototype.addImage(image)`
- `PDFDocument.prototype.loadImage(obj)`
- `PDFDocument.protoype.addEmbeddedFile(filename, mimetype, contents, creationDate, modificationDate, addChecksum)`
- `PDFDocument.prototype.getEmbeddedFiles()`
- `PDFDocument.prototype.deleteEmbeddedFile(filename)`
- `PDFDocument.prototype.insertEmbeddedFile(filename, fileSpecObject)`
- `PDFDocument.prototype.getFilespecParams(fileSpecObject)`
- `PDFDocument.prototype.getEmbeddedFileContents(fileSpecObject)`
- `PDFDocument.prototype.verifyEmbeddedFileChecksum(fileSpecObject)`
- `PDFDocument.prototype.isFilespec(object)`
- `PDFDocument.prototype.isEmbeddedFile(object)`
- `PDFDocument.prototype.countAssociatedFiles()`
- `PDFDocument.prototype.associatedFile(n)`
- `PDFDocument.prototype.newGraftMap()`
- `PDFDocument.prototype.graftObject(obj)`
- `PDFDocument.prototype.graftPage(to, srcDoc, srcPage)`
- `PDFDocument.prototype.enableJournal()`
- `PDFDocument.prototype.getJournal()`
- `PDFDocument.prototype.beginOperation(op)`
- `PDFDocument.prototype.beginImplicitOperation()`
- `PDFDocument.prototype.endOperation()`
- `PDFDocument.prototype.abandonOperation()`
- `PDFDocument.prototype.canUndo()`
- `PDFDocument.prototype.canRedo()`
- `PDFDocument.prototype.undo()`
- `PDFDocument.prototype.redo()`
- `PDFDocument.prototype.saveJournal(filename)`
- `PDFDocument.prototype.countLayerConfigs()`
- `PDFDocument.prototype.getLayerConfigName(n)`
- `PDFDocument.prototype.getLayerConfigInfo(n)`
- `PDFDocument.prototype.selectLayerConfig(n)`
- `PDFDocument.prototype.countLayerConfigUIs()`
- `PDFDocument.prototype.getLayerConfigUIInfo(n)`
- `PDFDocument.prototype.countLayers()`
- `PDFDocument.prototype.isLayerVisible(n)`
- `PDFDocument.prototype.setLayerVisible(n, visible)`
- `getLayerName(n)`
- `PDFDocument.prototype.setPageLabels(index, style, prefix, start)`
- `PDFDocument.prototype.deletePageLabels(index)`
- `PDFDocument.prototype.check()`
- `PDFDocument.prototype.canBeSavedIncrementally()`
- `PDFDocument.prototype.saveToBuffer(options)`
- `PDFDocument.prototype.save(filename, options)`
- `PDFDocument.prototype.countVersions()`
- `PDFDocument.prototype.hasUnsavedChanges()`
- `PDFDocument.prototype.countUnsavedVersions()`
- `PDFDocument.prototype.validateChangeHistory()`
- `PDFDocument.prototype.subsetFonts()`
- `PDFDocument.prototype.bake(bakeAnnots, bakeWidgets)`
- `PDFDocument.prototype.enableJS()`
- `PDFDocument.prototype.disableJS()`
- `PDFDocument.prototype.isJSSupported()`
- `PDFDocument.prototype.setJSEventListener(listener)`
- `PDFDocument.prototype.zugferdProfile()`
- `PDFDocument.prototype.zugferdVersion()`
- `PDFDocument.prototype.zugferdXML()`

### PDFGraftMap

- `PDFGraftMap.prototype.graftObject(obj)`
- `PDFGraftMap.prototype.graftPage(to, srcDoc, srcPage)`

### PDFObject

- `PDFObject.prototype.length`
- `PDFObject.prototype.[n]`
- `PDFObject.prototype.[name]`
- `PDFObject.prototype.get(...path)`
- `PDFObject.prototype.getInheritable(key)`
- `PDFObject.prototype.put(key, value)`
- `PDFObject.prototype.delete(key)`
- `PDFObject.prototype.resolve()`
- `PDFObject.prototype.isArray()`
- `PDFObject.prototype.isDictionary()`
- `PDFObject.prototype.forEach(callback)`
- `PDFObject.prototype.push(item)`
- `PDFObject.prototype.toString(tight, ascii)`
- `PDFObject.prototype.valueOf()`
- `PDFObject.prototype.compare(other_obj)`
- `PDFObject.prototype.isStream()`
- `PDFObject.prototype.readStream()`
- `PDFObject.prototype.readRawStream()`
- `PDFObject.prototype.writeObject(obj)`
- `PDFObject.prototype.writeStream(buf)`
- `PDFObject.prototype.writeRawStream(buf)`
- `PDFObject.prototype.isNull()`
- `PDFObject.prototype.isBoolean()`
- `PDFObject.prototype.asBoolean()`
- `PDFObject.prototype.isInteger()`
- `PDFObject.prototype.isReal()`
- `PDFObject.prototype.isNumber()`
- `PDFObject.prototype.asNumber()`
- `PDFObject.prototype.isName()`
- `PDFObject.prototype.asName()`
- `PDFObject.prototype.isString()`
- `PDFObject.prototype.asString()`
- `PDFObject.prototype.asByteString()`
- `PDFObject.prototype.isIndirect()`
- `PDFObject.prototype.asIndirect()`

### PDFPage

- `PDFPage.prototype.getObject()`
- `PDFPage.prototype.getAnnotations()`
- `PDFPage.prototype.getWidgets()`
- `PDFPage.prototype.setPageBox(box, rect)`
- `PDFPage.prototype.createAnnotation(type)`
- `PDFPage.prototype.deleteAnnotation(annot)`
- `PDFPage.prototype.update()`
- `PDFPage.prototype.toPixmap(matrix, colorspace, alpha, showExtras, usage, box)`
- `PDFPage.prototype.applyRedactions(blackBoxes, imageMethod, lineArtMethod, textMethod)`
- `PDFPage.prototype.process(processor)`
- `PDFPage.prototype.getTransform()`
- `PDFPage.prototype.createSignature(name)`
- `PDFPage.prototype.countAssociatedFiles()`
- `PDFPage.prototype.associatedFile(n)`
- `PDFPage.prototype.clip(rect)`

### PDFProcessor

- `PDFProcessor(callbacks)`
- `XObjects (Images and Forms)`

### PDFWidget

- `PDFWidget.prototype.getFieldType()`
- `PDFWidget.prototype.getFieldFlags()`
- `PDFWidget.prototype.getName()`
- `PDFWidget.prototype.getMaxLen()`
- `PDFWidget.prototype.getOptions()`
- `PDFWidget.prototype.getLabel()`
- `PDFWidget.prototype.getValue()`
- `PDFWidget.prototype.setTextValue(value)`
- `PDFWidget.prototype.setChoiceValue(value)`
- `PDFWidget.prototype.toggle()`
- `PDFWidget.prototype.getEditingState()`
- `PDFWidget.prototype.setEditingState(state)`
- `PDFWidget.prototype.layoutTextWidget()`
- `PDFWidget.prototype.isReadOnly()`
- `PDFWidget.prototype.isRadioButton()`
- `PDFWidget.prototype.isSigned()`
- `PDFWidget.prototype.validateSignature()`
- `PDFWidget.prototype.checkCertificate()`
- `PDFWidget.prototype.checkDigest()`
- `PDFWidget.prototype.getSignatory()`
- `PDFWidget.prototype.previewSignature(signer, signatureConfig, image, reason, location)`
- `PDFWidget.prototype.sign(signer, signatureConfig, image, reason, location)`
- `PDFWidget.prototype.clearSignature()`
- `PDFWidget.prototype.incrementalChangesSinceSigning()`

## Example scripts

The links identify the examples; no example code is incorporated into Gravity.

- [load-a-pdf.js](https://www.sumatrapdfreader.org/docs/scripts/load-a-pdf.js)
- [create-a-pdf.js](https://www.sumatrapdfreader.org/docs/scripts/create-a-pdf.js)
- [add-an-annotation.js](https://www.sumatrapdfreader.org/docs/scripts/add-an-annotation.js)
- [convert-pdf-to-plain-text.js](https://www.sumatrapdfreader.org/docs/scripts/convert-pdf-to-plain-text.js)
- [convert-pdf-to-images.js](https://www.sumatrapdfreader.org/docs/scripts/convert-pdf-to-images.js)
- [create-pdf.js](https://www.sumatrapdfreader.org/docs/scripts/create-pdf.js)
- [create-pdf-low-level.js](https://www.sumatrapdfreader.org/docs/scripts/create-pdf-low-level.js)
- [merge-pdfs.js](https://www.sumatrapdfreader.org/docs/scripts/merge-pdfs.js)
- [create-pdf-with-thumbnails.js](https://www.sumatrapdfreader.org/docs/scripts/create-pdf-with-thumbnails.js)
- [draw-graphics-with-device.js](https://www.sumatrapdfreader.org/docs/scripts/draw-graphics-with-device.js)
- [trace-device.js](https://www.sumatrapdfreader.org/docs/scripts/trace-device.js)
- [copy-outline.js](https://www.sumatrapdfreader.org/docs/scripts/copy-outline.js)
- [pdf-bake.js](https://www.sumatrapdfreader.org/docs/scripts/pdf-bake.js)
- [pdf-dejpx.js](https://www.sumatrapdfreader.org/docs/scripts/pdf-dejpx.js)
- [rotate-pages.js](https://www.sumatrapdfreader.org/docs/scripts/rotate-pages.js)
- [add-watermark.js](https://www.sumatrapdfreader.org/docs/scripts/add-watermark.js)
- [stamp-image.js](https://www.sumatrapdfreader.org/docs/scripts/stamp-image.js)
- [toc-from-headings.js](https://www.sumatrapdfreader.org/docs/scripts/toc-from-headings.js)
- [crop.js](https://www.sumatrapdfreader.org/docs/scripts/crop.js)
- [discussion-2429-remove-annotation-authors.js](https://www.sumatrapdfreader.org/docs/scripts/discussion-2429-remove-annotation-authors.js)
- [discussion-2472-auto-crop-margins.js](https://www.sumatrapdfreader.org/docs/scripts/discussion-2472-auto-crop-margins.js)
- [discussion-2480-edit-bookmarks.js](https://www.sumatrapdfreader.org/docs/scripts/discussion-2480-edit-bookmarks.js)
- [discussion-2938-highlight-occurrences.js](https://www.sumatrapdfreader.org/docs/scripts/discussion-2938-highlight-occurrences.js)
- [discussion-3034-extract-attachments.js](https://www.sumatrapdfreader.org/docs/scripts/discussion-3034-extract-attachments.js)
- [discussion-3174-join-or-split.js](https://www.sumatrapdfreader.org/docs/scripts/discussion-3174-join-or-split.js)
- [discussion-3311-edit-pages.js](https://www.sumatrapdfreader.org/docs/scripts/discussion-3311-edit-pages.js)
- [discussion-3422-annotations-only.js](https://www.sumatrapdfreader.org/docs/scripts/discussion-3422-annotations-only.js)
- [discussion-3574-extract-attachments.js](https://www.sumatrapdfreader.org/docs/scripts/discussion-3574-extract-attachments.js)
- [discussion-3823-extract-text.js](https://www.sumatrapdfreader.org/docs/scripts/discussion-3823-extract-text.js)
- [discussion-3920-export-inverted-pages.js](https://www.sumatrapdfreader.org/docs/scripts/discussion-3920-export-inverted-pages.js)
- [discussion-3962-rectangle-highlight.js](https://www.sumatrapdfreader.org/docs/scripts/discussion-3962-rectangle-highlight.js)
- [discussion-4021-export-annotations.js](https://www.sumatrapdfreader.org/docs/scripts/discussion-4021-export-annotations.js)
- [discussion-4090-delete-annotations.js](https://www.sumatrapdfreader.org/docs/scripts/discussion-4090-delete-annotations.js)
- [discussion-4230-report-character-fonts.js](https://www.sumatrapdfreader.org/docs/scripts/discussion-4230-report-character-fonts.js)
- [discussion-4327-extract-vector-pages.js](https://www.sumatrapdfreader.org/docs/scripts/discussion-4327-extract-vector-pages.js)
- [discussion-4359-populate-highlight-contents.js](https://www.sumatrapdfreader.org/docs/scripts/discussion-4359-populate-highlight-contents.js)
- [discussion-4386-square-highlight-appearances.js](https://www.sumatrapdfreader.org/docs/scripts/discussion-4386-square-highlight-appearances.js)
- [discussion-4544-backfill-annotation-ids.js](https://www.sumatrapdfreader.org/docs/scripts/discussion-4544-backfill-annotation-ids.js)
- [discussion-4616-render-images.js](https://www.sumatrapdfreader.org/docs/scripts/discussion-4616-render-images.js)
- [discussion-4633-edit-pdf-metadata.js](https://www.sumatrapdfreader.org/docs/scripts/discussion-4633-edit-pdf-metadata.js)
- [discussion-4674-export-bookmarks.js](https://www.sumatrapdfreader.org/docs/scripts/discussion-4674-export-bookmarks.js)
- [discussion-4853-extract-vector-pages.js](https://www.sumatrapdfreader.org/docs/scripts/discussion-4853-extract-vector-pages.js)
- [discussion-4971-set-crop-box.js](https://www.sumatrapdfreader.org/docs/scripts/discussion-4971-set-crop-box.js)
- [discussion-5019-table-to-tsv.js](https://www.sumatrapdfreader.org/docs/scripts/discussion-5019-table-to-tsv.js)
- [discussion-5209-highlight-opacity.js](https://www.sumatrapdfreader.org/docs/scripts/discussion-5209-highlight-opacity.js)
- [discussion-5403-bulk-annotation-style.js](https://www.sumatrapdfreader.org/docs/scripts/discussion-5403-bulk-annotation-style.js)
- [discussion-5549-extract-favorite-pages.js](https://www.sumatrapdfreader.org/docs/scripts/discussion-5549-extract-favorite-pages.js)
- [discussion-5841-add-free-text.js](https://www.sumatrapdfreader.org/docs/scripts/discussion-5841-add-free-text.js)
- [discussion-6115-extract-matching-pages.js](https://www.sumatrapdfreader.org/docs/scripts/discussion-6115-extract-matching-pages.js)
- [discussion-6122-render-page-region.js](https://www.sumatrapdfreader.org/docs/scripts/discussion-6122-render-page-region.js)
- [discussion-6283-render-images.js](https://www.sumatrapdfreader.org/docs/scripts/discussion-6283-render-images.js)
- [discussion-6294-named-page-export.js](https://www.sumatrapdfreader.org/docs/scripts/discussion-6294-named-page-export.js)
- [issue-4375-manual-duplex.js](https://www.sumatrapdfreader.org/docs/scripts/issue-4375-manual-duplex.js)
- [issue-6051-add-header-footer.js](https://www.sumatrapdfreader.org/docs/scripts/issue-6051-add-header-footer.js)
- [issue-6177-poster-tiles.js](https://www.sumatrapdfreader.org/docs/scripts/issue-6177-poster-tiles.js)
- [issue-6221-mirror-pages.js](https://www.sumatrapdfreader.org/docs/scripts/issue-6221-mirror-pages.js)
