# Third-party notices

Gravity's original source code is licensed under the MIT License in `LICENSE`. The third-party components below retain their own copyright notices and licenses. Their inclusion does not change the license of Gravity's original code.

## Windows PDF engine

The Windows application includes the unmodified `pdfium.dll` from [PDFium binaries 160.0.8179.0, release chromium/8179](https://github.com/bblanchon/pdfium-binaries/releases/tag/chromium/8179), specifically `pdfium-win-x64.tgz`. This build disables V8 and XFA.

- Archive SHA-256: `836a77c9053f624839c66010cddea7b675628a36b8831d2330178bb169d241b6`
- DLL SHA-256: `b9324fe77556753e24443b8ff1d03b39a3aa01ccd6e56e7debb82922438e8965`

The DLL matches that upstream archive byte for byte. All 19 upstream license files are retained in `third_party/pdfium/`; the build copies them into the application's `licenses` directory. The upstream packaging license is renamed to `pdfium-binaries.txt` there, and Gravity's MIT license is included as `Gravity.txt`.

| Component | License terms supplied upstream | File in the application's `licenses` directory |
|---|---|---|
| PDFium binary packaging | MIT | `pdfium-binaries.txt` |
| PDFium | BSD-3-Clause and Apache-2.0 notices | `pdfium.txt` |
| Abseil | Apache-2.0 | `abseil.txt` |
| Anti-Grain Geometry 2.3 | Permissive AGG 2.3 license | `agg23.txt` |
| Dragonbox | Boost Software License 1.0 or Apache-2.0 with LLVM exceptions | `dragonbox-Boost.txt`, `dragonbox-Apache2-LLVM.txt` |
| fast_float | MIT | `fast_float.txt` |
| FreeType | FreeType Project License | `freetype.txt` |
| HarfBuzz | MIT-style license | `harfbuzz.txt` |
| Highway | Apache-2.0 | `highway.txt` |
| ICU and its included data | Unicode License V3 and the accompanying component notices | `icu.txt` |
| Little CMS | MIT | `lcms.txt` |
| libjpeg-turbo and Independent JPEG Group code | IJG, BSD-3-Clause, and associated zlib terms | `libjpeg_turbo.ijg`, `libjpeg_turbo.md` |
| OpenJPEG | BSD-2-Clause | `libopenjpeg.txt` |
| libpng | PNG Reference Library License | `libpng.txt` |
| LLVM libc | Apache-2.0 with LLVM exceptions and accompanying legacy notices | `llvm-libc.txt` |
| simdutf | MIT | `simdutf.txt` |
| zlib | zlib license | `zlib.txt` |

These components permit commercial use and redistribution under their respective terms. Preserve the full license texts and required acknowledgments with binary distributions. Notices about modification, patents, trademarks, endorsement, and warranty remain applicable where specified by each license; Gravity's MIT license does not replace them.

### Required acknowledgments

This software is based in part on the work of the FreeType Team.

This software is based in part on the work of the Independent JPEG Group.

The upstream ICU notice also contains terms for build-support scripts and an Autoconf exception. Those notices are retained unchanged; the build-support scripts are not distributed as part of Gravity's application.

## Lucide icons

The Windows application embeds 32 Lucide SVG definitions from `lucide-static` 1.79.0 and renders them locally with GDI+. The original assets, version information, ISC license, and applicable Feather MIT notices are retained in `third_party/lucide/`. The Windows build includes the complete notices as `licenses/Lucide.txt`.

The browser prototype in `docs/ui-draft/` uses the same icon set, with its license retained in [docs/ui-draft/vendor/LICENSE](docs/ui-draft/vendor/LICENSE). The macOS application bundles the Lucide Save icon, with the complete notices in `Contents/Resources/licenses/Lucide.txt`; its other interface icons use system SF Symbols.

## System libraries and development tools

Windows supplies .NET Framework and the Windows APIs. macOS supplies AppKit, PDFKit, and the other system frameworks used by the native application. Gravity uses these installed system libraries; it does not bundle or relicense them. The macOS application does not include the Windows PDFium binary.

Python, Pillow, Ruff, compilers, and CI actions are development dependencies, not bundled application components. Their own licenses apply when obtaining or redistributing those tools. The application distributions contain no font files.
