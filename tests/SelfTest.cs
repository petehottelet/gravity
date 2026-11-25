using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;

namespace Gravity.Tests
{
    /// <summary>
    /// Offline regression tests for the PDF engine and page-editing model. Generates its own
    /// input PDFs, so it needs nothing but pdfium.dll. Exit code 0 = all passed.
    /// </summary>
    internal static class SelfTest
    {
        private static int failures, passes;
        private static string dir;

        [STAThread]
        private static int Main(string[] args)
        {
            dir = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "gravity-tests-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(dir);
            Pdfium.EnsureInitialized();
            var tests = new List<KeyValuePair<string, Action>>
            {
                T("open and read text", OpenAndRead),
                T("reorder pages and save", Reorder),
                T("cut from one PDF, paste into another, rotate", CutPasteAcrossDocs),
                T("combine files, duplicate, blank page", CombineDuplicateBlank),
                T("duplicate a base page keeps both copies", DuplicateBasePage),
                T("undo and redo track modified state", UndoRedo),
                T("save over the open file", SaveInPlace),
                T("unchanged save-as copies the original bytes", PristineCopy),
                T("drag-move semantics (MovePages)", MoveSemantics),
                T("text search forward and backward", Search),
                T("bookmarks and internal links", OutlineAndLinks),
                T("rendering produces ink", Render),
                T("invalid files are rejected", NotAPdf),
                T("form filling, saving, and undo", () => FormTests.Run(dir)),
                T("distribution license notices", DistributionNotices),
                T("bundled vector icons", IconTests.Run),
                T("safe saves, search, exports, and image import", () => FeatureTests.Run(dir)),
            };
            foreach (var t in tests)
            {
                try { t.Value(); }
                catch (Exception ex) { Fail(t.Key + " threw " + ex.GetType().Name + ": " + ex.Message); }
            }
            if (!args.Contains("--no-ui"))
            {
                Console.WriteLine("-- user interface (real window, synthetic mouse and keyboard messages)");
                try { UiTest.Run(dir); FormTests.RunUI(dir); FeatureTests.RunUI(dir); DialogTests.Run(); }
                catch (Exception ex) { Fail("UI test threw " + ex); }
            }
            Console.WriteLine();
            Console.WriteLine(failures == 0 ? "ALL " + passes + " CHECKS PASSED" : failures + " FAILED, " + passes + " passed");
            return failures == 0 ? 0 : 1;
        }

        private static KeyValuePair<string, Action> T(string name, Action a)
        {
            return new KeyValuePair<string, Action>(name, delegate
            {
                Console.WriteLine("-- " + name);
                a();
            });
        }

        internal static void Check(bool ok, string what)
        {
            if (ok) { passes++; Console.WriteLine("   ok    " + what); }
            else Fail(what);
        }

        internal static void Fail(string what)
        {
            failures++;
            Console.WriteLine("   FAIL  " + what);
        }

        private static void Eq(string expected, string actual, string what)
        {
            Check(expected == actual, what + (expected == actual ? "" : " (expected [" + expected + "], got [" + actual + "])"));
        }

        // ---- fixtures -----------------------------------------------------------

        private static string MakePdf(string name, string prefix, int pages, double w, double h)
        {
            string path = Path.Combine(dir, name);
            lock (Pdfium.Lock)
            {
                IntPtr doc = Pdfium.FPDF_CreateNewDocument();
                IntPtr font = Pdfium.FPDFText_LoadStandardFont(doc, "Helvetica");
                for (int i = 0; i < pages; i++)
                {
                    IntPtr page = Pdfium.FPDFPage_New(doc, i, w, h);
                    IntPtr text = Pdfium.FPDFPageObj_CreateTextObj(doc, font, 36);
                    Pdfium.FPDFText_SetText(text, prefix + (i + 1));
                    Pdfium.FPDFPageObj_Transform(text, 1, 0, 0, 1, 72, h - 120);
                    Pdfium.FPDFPage_InsertObject(page, text);
                    Pdfium.FPDFPage_GenerateContent(page);
                    Pdfium.FPDF_ClosePage(page);
                }
                Pdfium.FPDFFont_Close(font);
                using (var fs = File.Create(path)) Pdfium.SaveDocument(doc, fs);
                Pdfium.FPDF_CloseDocument(doc);
            }
            return path;
        }

        internal static string FileTexts(string path) { return Texts(path); }

        internal static string PageText(PdfSource s, int i)
        {
            lock (Pdfium.Lock)
            {
                IntPtr tp = s.GetTextPage(i);
                if (tp == IntPtr.Zero) return "";
                return Pdfium.GetText(tp, 0, Pdfium.FPDFText_CountChars(tp)).Trim();
            }
        }

        private static string Texts(string path)
        {
            var s = PdfSource.Open(path, null);
            try { return string.Join(" ", Enumerable.Range(0, s.PageCount).Select(i => PageText(s, i))); }
            finally { s.Dispose(); }
        }

        private static string Texts(List<PageRef> pages)
        {
            return string.Join(" ", pages.Select(p => p.Src == null ? "_" : PageText(p.Src, p.Index)));
        }

        private static int Rotation(string path, int index)
        {
            var s = PdfSource.Open(path, null);
            try
            {
                lock (Pdfium.Lock) return Pdfium.FPDFPage_GetRotation(s.GetPage(index));
            }
            finally { s.Dispose(); }
        }

        private static List<PageRef> Move(List<PageRef> pages, List<PageRef> moving, int at)
        {
            // Same algorithm as MainForm.MovePages.
            var set = new HashSet<PageRef>(moving, RefComparer<PageRef>.Instance);
            return pages.Take(at).Where(p => !set.Contains(p)).Concat(moving).Concat(pages.Skip(at).Where(p => !set.Contains(p))).ToList();
        }

        private static string A { get { return Path.Combine(dir, "A.pdf"); } }
        private static string B { get { return Path.Combine(dir, "B.pdf"); } }

        // ---- tests ------------------------------------------------------------------

        private static void OpenAndRead()
        {
            MakePdf("A.pdf", "A", 5, 612, 792);
            MakePdf("B.pdf", "B", 3, 842, 595);
            Eq("A1 A2 A3 A4 A5", Texts(A), "A.pdf text");
            Eq("B1 B2 B3", Texts(B), "B.pdf text");
            var s = PdfSource.Open(B, null);
            Check(s.PageCount == 3 && Math.Abs(s.PageWidth(0) - 842) < 0.5 && Math.Abs(s.PageHeight(0) - 595) < 0.5, "page count and size");
            s.Dispose();
            Check(s.IsDisposed, "dispose");
        }

        private static void Reorder()
        {
            var doc = new EditDoc(PdfSource.Open(A, null));
            var p = doc.Pages;
            doc.SetPages(Move(p, new List<PageRef> { p[2], p[0] }, p.Count));
            Eq("A2 A4 A5 A3 A1", Texts(doc.Pages), "in-memory order");
            Check(doc.IsModified, "modified after move");
            string out1 = Path.Combine(dir, "reordered.pdf");
            doc.SaveAs(out1);
            Eq("A2 A4 A5 A3 A1", Texts(out1), "saved order");
            Check(!doc.IsModified && doc.FilePath == Path.GetFullPath(out1), "clean after save");
            Check(doc.CanUndo, "undo history survives save");
            doc.Undo();
            Eq("A1 A2 A3 A4 A5", Texts(doc.Pages), "undo after save restores old order");
        }

        private static void CutPasteAcrossDocs()
        {
            var a = new EditDoc(PdfSource.Open(A, null));
            var b = new EditDoc(PdfSource.Open(B, null));
            var clip = new List<PageRef> { a.Pages[1] };
            a.SetPages(a.Pages.Where((x, i) => i != 1).ToList());
            var list = new List<PageRef>(b.Pages);
            list.InsertRange(1, clip.Select(x => x.Clone()));
            list[2] = list[2].Rotated(1);
            b.SetPages(list);
            Eq("B1 A2 B2 B3", Texts(b.Pages), "pasted order");

            string outB = Path.Combine(dir, "b-pasted.pdf"), outA = Path.Combine(dir, "a-cut.pdf");
            b.SaveAs(outB);
            a.SaveAs(outA);
            Eq("B1 A2 B2 B3", Texts(outB), "saved paste target");
            Eq("A1 A3 A4 A5", Texts(outA), "saved cut source");
            Check(Rotation(outB, 2) == 1, "rotated page has /Rotate 90");
            Check(Rotation(outB, 1) == 0, "other pages unrotated");
            var s = PdfSource.Open(outB, null);
            Check(Math.Abs(s.PageWidth(2) - 595) < 0.5 && Math.Abs(s.PageHeight(2) - 842) < 0.5, "rotated page reports swapped size");
            Check(Math.Abs(s.PageWidth(1) - 612) < 0.5, "pasted page keeps its own size");
            s.Dispose();

            // Rotating an already rotated page adds up.
            var c = new EditDoc(PdfSource.Open(outB, null));
            var l2 = new List<PageRef>(c.Pages);
            l2[2] = l2[2].Rotated(3);
            c.SetPages(l2);
            string outC = Path.Combine(dir, "rot-back.pdf");
            c.SaveAs(outC);
            Check(Rotation(outC, 2) == 0, "rotate right then left returns to 0");
        }

        private static void CombineDuplicateBlank()
        {
            var sa = PdfSource.Open(A, null);
            var sb = PdfSource.Open(B, null);
            var pages = EditDoc.AllPages(sa).Concat(EditDoc.AllPages(sb)).ToList();
            pages.Insert(1, pages[0].Clone());
            pages.Add(PageRef.Blank(300, 400));
            var doc = new EditDoc(pages, "Combined.pdf");
            Check(doc.IsModified && doc.FilePath == null, "combined doc is untitled and unsaved");
            string path = Path.Combine(dir, "combined.pdf");
            doc.SaveAs(path);
            Eq("A1 A1 A2 A3 A4 A5 B1 B2 B3", Texts(path).Trim(), "combined text order");
            var s = PdfSource.Open(path, null);
            Check(s.PageCount == 10, "combined page count");
            Check(Math.Abs(s.PageWidth(9) - 300) < 0.5 && Math.Abs(s.PageHeight(9) - 400) < 0.5, "blank page size");
            s.Dispose();
        }

        private static void DuplicateBasePage()
        {
            var doc = new EditDoc(PdfSource.Open(A, null));
            var p = doc.Pages;
            doc.SetPages(new List<PageRef> { p[3], p[0], p[3].Clone(), p[1] });
            string path = Path.Combine(dir, "dups.pdf");
            doc.SaveAs(path);
            Eq("A4 A1 A4 A2", Texts(path), "base page used twice");
        }

        private static void UndoRedo()
        {
            var doc = new EditDoc(PdfSource.Open(A, null));
            int changes = 0;
            doc.Changed += delegate { changes++; };
            Check(!doc.IsModified && !doc.CanUndo, "fresh doc is clean");
            doc.SetPages(doc.Pages.Skip(1).ToList());
            Check(doc.Pages.Count == 4 && doc.IsModified, "delete marks modified");
            doc.Undo();
            Check(doc.Pages.Count == 5 && !doc.IsModified, "undo restores clean state");
            doc.Redo();
            Check(doc.Pages.Count == 4 && doc.IsModified && !doc.CanRedo, "redo");
            doc.Undo();
            doc.SetPages(doc.Pages.Take(2).ToList());
            Check(!doc.CanRedo, "new edit clears redo");
            Check(changes == 5, "Changed raised for every edit (" + changes + ")");
        }

        private static void SaveInPlace()
        {
            string path = Path.Combine(dir, "inplace.pdf");
            File.Copy(A, path, true);
            var doc = new EditDoc(PdfSource.Open(path, null));
            doc.SetPages(Enumerable.Reverse(doc.Pages).ToList());
            doc.SaveAs(path);
            Eq("A5 A4 A3 A2 A1", Texts(path), "overwritten file");
            Eq("A5 A4 A3 A2 A1", Texts(doc.Pages), "document re-based on the new file");
            Check(Directory.GetFiles(dir, "~inplace*").Length == 0, "no temp files left behind");
            doc.SetPages(doc.Pages.Take(2).ToList());
            doc.SaveAs(path);
            Eq("A5 A4", Texts(path), "second in-place save");
        }

        private static void PristineCopy()
        {
            var doc = new EditDoc(PdfSource.Open(A, null));
            string path = Path.Combine(dir, "copy.pdf");
            doc.SaveAs(path);
            Check(File.ReadAllBytes(A).SequenceEqual(File.ReadAllBytes(path)), "byte-identical copy");
        }

        private static void MoveSemantics()
        {
            var s = PdfSource.Open(A, null);
            var p = EditDoc.AllPages(s);
            Func<List<PageRef>, string> names = l => string.Join("", l.Select(x => (x.Index + 1).ToString()));
            Eq("21345", names(Move(p, new List<PageRef> { p[0] }, 2)), "move first page after second");
            Eq("13452", names(Move(p, new List<PageRef> { p[1] }, 5)), "move to end");
            Eq("51234", names(Move(p, new List<PageRef> { p[4] }, 0)), "move to start");
            Eq("12534", names(Move(p, new List<PageRef> { p[4] }, 2)), "move into middle");
            Eq("24135", names(Move(p, new List<PageRef> { p[1], p[3] }, 0)), "multi-select move keeps order");
            Eq("12345", names(Move(p, new List<PageRef> { p[2] }, 3)), "drop in place is a no-op");
            s.Dispose();
        }

        private static void Search()
        {
            string path = Path.Combine(dir, "search.pdf");
            lock (Pdfium.Lock)
            {
                IntPtr doc = Pdfium.FPDF_CreateNewDocument();
                IntPtr font = Pdfium.FPDFText_LoadStandardFont(doc, "Helvetica");
                IntPtr page = Pdfium.FPDFPage_New(doc, 0, 612, 792);
                IntPtr text = Pdfium.FPDFPageObj_CreateTextObj(doc, font, 18);
                Pdfium.FPDFText_SetText(text, "alpha beta Alpha gamma alpha");
                Pdfium.FPDFPageObj_Transform(text, 1, 0, 0, 1, 72, 600);
                Pdfium.FPDFPage_InsertObject(page, text);
                Pdfium.FPDFPage_GenerateContent(page);
                Pdfium.FPDF_ClosePage(page);
                Pdfium.FPDFFont_Close(font);
                using (var fs = File.Create(path)) Pdfium.SaveDocument(doc, fs);
                Pdfium.FPDF_CloseDocument(doc);
            }
            var s = PdfSource.Open(path, null);
            lock (Pdfium.Lock)
            {
                IntPtr tp = s.GetTextPage(0);
                Func<uint, int, bool, int> find = (flags, start, forward) =>
                {
                    IntPtr h = Pdfium.FPDFText_FindStart(tp, "alpha", flags, start);
                    try
                    {
                        bool ok = forward ? Pdfium.FPDFText_FindNext(h) != 0 : Pdfium.FPDFText_FindPrev(h) != 0;
                        return ok ? Pdfium.FPDFText_GetSchResultIndex(h) : -1;
                    }
                    finally { Pdfium.FPDFText_FindClose(h); }
                };
                Check(find(0, 0, true) == 0, "first match at 0");
                Check(find(0, 1, true) == 11, "next (case-insensitive) match at 11");
                Check(find(Pdfium.FPDF_MATCHCASE, 1, true) == 23, "match-case skips 'Alpha'");
                Check(find(0, -1, false) == 23, "backward from end finds last match");
                int prev = find(0, 23, false);
                Check(prev == 11, "backward from a match finds the previous one (" + prev + ")");
                Check(find(0, 24, true) == -1, "no match after the last");
            }
            s.Dispose();
        }

        private static void OutlineAndLinks()
        {
            string path = Path.Combine(dir, "outline.pdf");
            WriteOutlinePdf(path);
            var s = PdfSource.Open(path, null);
            List<OutlineItem> items;
            lock (Pdfium.Lock) items = s.GetOutline();
            Check(items.Count == 2, "two top-level bookmarks");
            if (items.Count == 2)
            {
                Eq("Chapter One", items[0].Title, "bookmark title");
                Check(items[0].PageIndex == 0 && items[1].PageIndex == 1, "bookmark pages");
                Check(items[1].HasY && Math.Abs(items[1].Y - 400) < 0.5, "bookmark y position");
                Check(items[0].Children.Count == 1 && items[0].Children[0].Title == "Section 1.1", "nested bookmark");
            }
            LinkTarget link;
            lock (Pdfium.Lock) link = s.LinkAt(0, 100, 710);
            Check(link != null && link.PageIndex == 1 && link.Uri == null, "internal link resolves to page 2");
            LinkTarget web;
            lock (Pdfium.Lock) web = s.LinkAt(0, 100, 610);
            Check(web != null && web.Uri == "https://example.com/", "URI link");
            LinkTarget none;
            lock (Pdfium.Lock) none = s.LinkAt(0, 500, 100);
            Check(none == null, "no link elsewhere");

            // Bookmarks map to new positions after reordering.
            var doc = new EditDoc(s);
            doc.SetPages(Enumerable.Reverse(doc.Pages).ToList());
            Check(doc.Find(s, items[0].PageIndex) == 1 && doc.Find(s, items[1].PageIndex) == 0, "bookmarks follow moved pages");
            doc.SetPages(doc.Pages.Take(1).ToList());
            Check(doc.Find(s, 0) == -1, "bookmark to a deleted page is detected");
        }

        private static void WriteOutlinePdf(string path)
        {
            var objs = new List<string>
            {
                "<< /Type /Catalog /Pages 2 0 R /Outlines 5 0 R >>",
                "<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 8 0 R /Resources << /Font << /F1 9 0 R >> >> /Annots [10 0 R 13 0 R] >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 11 0 R /Resources << /Font << /F1 9 0 R >> >> >>",
                "<< /Type /Outlines /First 6 0 R /Last 7 0 R /Count 3 >>",
                "<< /Title (Chapter One) /Parent 5 0 R /Next 7 0 R /First 12 0 R /Last 12 0 R /Count 1 /Dest [3 0 R /XYZ 0 792 0] >>",
                "<< /Title (Chapter Two) /Parent 5 0 R /Prev 6 0 R /Dest [4 0 R /XYZ 0 400 0] >>",
                null, // 8: content stream
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
                "<< /Type /Annot /Subtype /Link /Rect [72 690 320 730] /Border [0 0 0] /Dest [4 0 R /XYZ 0 400 0] >>",
                null, // 11: content stream
                "<< /Title (Section 1.1) /Parent 6 0 R /Dest [3 0 R /XYZ 0 500 0] >>",
                "<< /Type /Annot /Subtype /Link /Rect [72 590 320 630] /Border [0 0 0] /A << /S /URI /URI (https://example.com/) >> >>",
            };
            Func<string, string> stream = c => "<< /Length " + c.Length + " >>\nstream\n" + c + "\nendstream";
            objs[7] = stream("BT /F1 24 Tf 72 700 Td (Go to page two) Tj ET BT /F1 24 Tf 72 600 Td (Visit example.com) Tj ET");
            objs[10] = stream("BT /F1 24 Tf 72 400 Td (Page two target) Tj ET");
            var sb = new StringBuilder("%PDF-1.4\n");
            var offsets = new List<int>();
            for (int i = 0; i < objs.Count; i++)
            {
                offsets.Add(sb.Length);
                sb.Append(i + 1).Append(" 0 obj\n").Append(objs[i]).Append("\nendobj\n");
            }
            int xref = sb.Length;
            sb.Append("xref\n0 ").Append(objs.Count + 1).Append("\n0000000000 65535 f \n");
            foreach (int o in offsets) sb.Append(o.ToString("D10")).Append(" 00000 n \n");
            sb.Append("trailer\n<< /Size ").Append(objs.Count + 1).Append(" /Root 1 0 R >>\nstartxref\n").Append(xref).Append("\n%%EOF\n");
            File.WriteAllBytes(path, Encoding.ASCII.GetBytes(sb.ToString()));
        }

        private static void Render()
        {
            var s = PdfSource.Open(A, null);
            var bmp = RenderService.RenderTile(new TileKey(s, 0, 0, 306, 396, 0, 0));
            Check(bmp != null && bmp.Width == 306 && bmp.Height == 396, "tile size");
            int dark = 0;
            for (int y = 0; y < bmp.Height; y++)
                for (int x = 0; x < bmp.Width; x++)
                    if (bmp.GetPixel(x, y).GetBrightness() < 0.5f) dark++;
            Check(dark > 20, "page text is drawn (" + dark + " dark samples)");
            Check(bmp.GetPixel(300, 390).ToArgb() == Color.White.ToArgb(), "background is white");
            bmp.Dispose();

            // A tile from the middle of a large render is offset correctly.
            var big = RenderService.RenderTile(new TileKey(s, 0, 0, 2448, 3168, 1, 2));
            Check(big != null && big.Width == 1024 && big.Height == 1024, "interior tile size");
            var edge = RenderService.RenderTile(new TileKey(s, 0, 0, 2448, 3168, 2, 3));
            Check(edge != null && edge.Width == 2448 - 2048 && edge.Height == 3168 - 3072, "edge tile is clipped");
            if (big != null) big.Dispose();
            if (edge != null) edge.Dispose();

            var rotated = RenderService.RenderPage(new PageRef(s, 0, 1), 396, 306, Pdfium.FPDF_ANNOT);
            Check(rotated != null && rotated.Width == 396, "rotated full-page render");
            if (rotated != null) rotated.Dispose();
            s.Dispose();
            Check(RenderService.RenderTile(new TileKey(s, 0, 0, 100, 100, 0, 0)) == null, "disposed source renders nothing");
        }

        private static void DistributionNotices()
        {
            string licenses = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "licenses");
            var required = new[] {
                "Gravity.txt", "Lucide.txt", "pdfium-binaries.txt", "pdfium.txt", "abseil.txt", "agg23.txt",
                "dragonbox-Boost.txt", "dragonbox-Apache2-LLVM.txt", "fast_float.txt", "freetype.txt",
                "harfbuzz.txt", "highway.txt", "icu.txt", "lcms.txt", "libjpeg_turbo.ijg",
                "libjpeg_turbo.md", "libopenjpeg.txt", "libpng.txt", "llvm-libc.txt", "simdutf.txt", "zlib.txt"
            };
            Check(required.All(name => File.Exists(Path.Combine(licenses, name))), "built application includes every upstream license and Gravity's license");
            string notices = File.ReadAllText(Path.Combine(licenses, "THIRD_PARTY_NOTICES.md"));
            Check(notices.Contains("This software is based in part on the work of the FreeType Team.") &&
                notices.Contains("This software is based in part on the work of the Independent JPEG Group."),
                "distribution documentation includes both required acknowledgments");
        }

        private static void NotAPdf()
        {
            string path = Path.Combine(dir, "not-a.pdf");
            File.WriteAllText(path, "hello, this is not a PDF");
            try
            {
                PdfSource.Open(path, null);
                Fail("garbage file should not open");
            }
            catch (InvalidDataException) { Check(true, "garbage file is rejected with a message"); }
            string empty = Path.Combine(dir, "empty.pdf");
            File.WriteAllBytes(empty, new byte[0]);
            try
            {
                PdfSource.Open(empty, null);
                Fail("empty file should not open");
            }
            catch (InvalidDataException) { Check(true, "empty file is rejected"); }
        }
    }
}
