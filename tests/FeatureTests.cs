using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace Gravity.Tests
{
    internal static class FeatureTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private static void Check(bool value, string label) { SelfTest.Check(value, label); }
        private static T Field<T>(object obj, string name) { return (T)obj.GetType().GetField(name, Flags).GetValue(obj); }
        private static object Call(object obj, string name, params object[] args) { return obj.GetType().GetMethod(name, Flags).Invoke(obj, args); }
        private static void Throws<T>(Action action, string label) where T : Exception
        {
            try { action(); Check(false, label); } catch (T) { Check(true, label); }
        }

        internal static byte[] Pdf(params string[] text)
        {
            lock (Pdfium.Lock)
            {
                IntPtr doc = Pdfium.FPDF_CreateNewDocument();
                IntPtr font = Pdfium.FPDFText_LoadStandardFont(doc, "Helvetica");
                try
                {
                    for (int i = 0; i < text.Length; i++)
                    {
                        IntPtr page = Pdfium.FPDFPage_New(doc, i, 288, 432);
                        IntPtr obj = Pdfium.FPDFPageObj_CreateTextObj(doc, font, 16);
                        Pdfium.FPDFText_SetText(obj, text[i]);
                        Pdfium.FPDFPageObj_Transform(obj, 1, 0, 0, 1, 20, 350);
                        Pdfium.FPDFPage_InsertObject(page, obj); Pdfium.FPDFPage_GenerateContent(page); Pdfium.FPDF_ClosePage(page);
                    }
                    using (var stream = new MemoryStream()) { Pdfium.SaveDocument(doc, stream); return stream.ToArray(); }
                }
                finally { Pdfium.FPDFFont_Close(font); Pdfium.FPDF_CloseDocument(doc); }
            }
        }

        public static void Run(string root)
        {
            string dir = Path.Combine(root, "features"); Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "safe.pdf"); byte[] original = Pdf("Original", "Second"); File.WriteAllBytes(path, original);
            var source = PdfSource.Open(path, null); var doc = new EditDoc(source);
            doc.SetPages(new List<PageRef> { doc.Pages[1], doc.Pages[0] });
            var edited = doc.Pages;
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
                Throws<IOException>(() => doc.SaveAs(path), "replacement failure is reported instead of copying over the destination");
            Check(File.ReadAllBytes(path).SequenceEqual(original), "failed replacement preserves every original byte");
            Check(ReferenceEquals(doc.Pages, edited) && doc.IsModified && doc.CanUndo, "failed save preserves edits, dirty state and undo");
            Check(Directory.GetFiles(dir, "~gravity-*.tmp").Length == 0, "failed save removes its temporary file");
            Throws<IOException>(() => AtomicFile.Write(path, output => { output.WriteByte(123); throw new IOException("Simulated write failure"); }), "partial serialization reports failure");
            Check(File.ReadAllBytes(path).SequenceEqual(original), "partial serialization cannot damage the destination");
            string sentinel = Path.Combine(dir, "unrelated.tmp"); File.WriteAllText(sentinel, "keep");
            Throws<IOException>(() => AtomicFile.Write(path, output => output.WriteByte(0), false), "create-only export rejects an existing file");
            Check(File.ReadAllText(sentinel) == "keep" && Directory.GetFiles(dir, "~gravity-*.tmp").Length == 0, "cleanup removes only this operation's temporary file");
            doc.SaveAs(path);
            Check(!doc.IsModified && SelfTest.PageText(doc.Base, 0).Contains("Second"), "successful retry saves the edited page order");

            var textSource = PdfSource.FromBytes(Pdf("Alpha alpha ALPHA", "alpha on page two"), null);
            var pages = new List<PageRef> { new PageRef(textSource, 1, 0), PageRef.Blank(288, 432), new PageRef(textSource, 0, 1), new PageRef(textSource, 1, 0) };
            var result = DocumentSearch.Find(pages, "alpha", false, CancellationToken.None, null);
            Check(result.Total == 5 && result.PagesScanned == 4, "search counts matches across reordered, duplicated and blank pages");
            Check(result.Matches.Select(m => m.Page).SequenceEqual(new[] { 0, 2, 2, 2, 3 }), "search results use edited document page numbers");
            Check(result.Matches[0].Snippet.Contains("page two"), "search results include readable snippets");
            Check(DocumentSearch.Find(pages, "alpha", true, CancellationToken.None, null).Total == 3, "match-case search is honored");
            var manyPages = Enumerable.Repeat(new PageRef(textSource, 0, 0), 3500).ToList();
            var manyMatches = DocumentSearch.Find(manyPages, "alpha", false, CancellationToken.None, null);
            Check(manyMatches.Total == 10500 && manyMatches.Matches.Count == SearchResults.DisplayLimit,
                "large searches count every match while bounding the displayed result list");
            using (var stop = new CancellationTokenSource())
            {
                Throws<OperationCanceledException>(() => DocumentSearch.Find(pages, "alpha", false, stop.Token, (p, n) => stop.Cancel()), "search stops between pages when cancelled");
            }
            Check(PageRanges.Parse("4, 2-3, 2, 1", 4).SequenceEqual(new[] { 3, 1, 2, 0 }), "ranges preserve requested order and remove duplicates");
            Check(PageRanges.Parse("3-1", 4).SequenceEqual(new[] { 2, 1, 0 }), "descending page ranges are supported");
            Throws<ArgumentException>(() => PageRanges.Parse("0-2", 4), "out-of-range pages are rejected");
            Throws<ArgumentException>(() => PageRanges.Parse("1,,2", 4), "malformed page ranges are rejected");
            Check(DocumentExport.SafeStem("CON") == "_CON" && !DocumentExport.SafeStem("a:b?").Contains(":"), "export names handle reserved names and invalid characters");
            Throws<ArgumentException>(() => DocumentExport.PixelSize(PageRef.Blank(20000, 20000), 600), "oversized exports fail before bitmap allocation");
            var exportIndices = new List<int> { 2, 1 };
            Check(DocumentExport.Images(pages, exportIndices, dir, "pages", 72, false, CancellationToken.None, null) == 2, "PNG export writes the requested pages");
            using (var bitmap = new Bitmap(Path.Combine(dir, "pages-page-0003.png")))
                Check(bitmap.Width == 432 && bitmap.Height == 288 && Math.Abs(bitmap.HorizontalResolution - 72) < 1, "image dimensions include page rotation and dpi");
            byte[] png = File.ReadAllBytes(Path.Combine(dir, "pages-page-0003.png"));
            DocumentExport.Images(pages, exportIndices, dir, "pages", 72, false, CancellationToken.None, null);
            Check(File.ReadAllBytes(Path.Combine(dir, "pages-page-0003.png")).SequenceEqual(png) && File.Exists(Path.Combine(dir, "pages-page-0003 (2).png")), "repeat image exports preserve existing files");
            using (var stop = new CancellationTokenSource())
            {
                Throws<OperationCanceledException>(() => DocumentExport.Images(pages, exportIndices, dir, "cancelled", 72, false, stop.Token, (p, s) => stop.Cancel()), "image export stops before starting the next page");
                Check(File.Exists(Path.Combine(dir, "cancelled-page-0003.png")) && !File.Exists(Path.Combine(dir, "cancelled-page-0002.png")), "cancelled image export keeps only completed pages");
            }
            DocumentExport.Images(pages, new[] { 0 }, dir, "jpeg", 150, true, CancellationToken.None, null);
            using (var bitmap = new Bitmap(Path.Combine(dir, "jpeg-page-0001.jpg")))
                Check(bitmap.RawFormat.Guid == ImageFormat.Jpeg.Guid && bitmap.Width == 600, "JPEG export is a decodable image at the chosen resolution");
            string txt = Path.Combine(dir, "pages.txt");
            DocumentExport.Text(pages, new[] { 0, 1, 2 }, txt, CancellationToken.None, null);
            string exported = File.ReadAllText(txt);
            Check(exported.IndexOf("page two", StringComparison.Ordinal) < exported.IndexOf("ALPHA", StringComparison.Ordinal) && exported.Count(c => c == '\f') == 2, "text export keeps page order and blank-page separators");
            using (var stop = new CancellationTokenSource())
            {
                Throws<OperationCanceledException>(() => DocumentExport.Text(pages, new[] { 0, 2 }, txt, stop.Token, (p, s) => stop.Cancel()), "text export can be cancelled");
                Check(File.ReadAllText(txt) == exported, "cancelled text export preserves the previous destination");
            }
            string image = Path.Combine(dir, "image.png");
            using (var bitmap = new Bitmap(80, 40))
            {
                using (var g = Graphics.FromImage(bitmap)) { g.Clear(Color.White); g.FillRectangle(Brushes.Red, 0, 0, 80, 20); g.FillRectangle(Brushes.Blue, 0, 20, 80, 20); }
                bitmap.Save(image, ImageFormat.Png);
            }
            var imported = PdfSource.FromBytes(ImageImport.Create(new[] { image, image }, new ImageImportOptions(), CancellationToken.None, null), null);
            Check(imported.PageCount == 2 && imported.PageWidth(0) == 612 && imported.PageHeight(0) == 792, "images become separate Letter pages");
            using (var bitmap = RenderService.RenderPage(new PageRef(imported, 0, 0), 612, 792, Pdfium.FPDF_ANNOT))
                Check(bitmap.GetPixel(306, 330).R > 220 && bitmap.GetPixel(306, 460).B > 220 && bitmap.GetPixel(10, 396).R > 240, "embedded image retains upright colors and white margins");
            var size = ImageImport.PageSize(80, 40, new ImageImportOptions { Size = ImagePageSize.Image, Margin = 0 });
            Check(size.Width == 60 && size.Height == 30, "image-size pages use the documented 96 dpi scale");
            size = ImageImport.PageSize(80, 40, new ImageImportOptions { Size = ImagePageSize.A4, Landscape = true, Margin = 18 });
            Check(size.Width > size.Height && Math.Abs(size.Height - 595.276) < 0.01, "A4 landscape dimensions are correct");
            using (var stop = new CancellationTokenSource())
                Throws<OperationCanceledException>(() => ImageImport.Create(new[] { image, image }, new ImageImportOptions(), stop.Token, (p, s) => stop.Cancel()), "image conversion cancels before publishing a partial document");
            string corrupt = Path.Combine(dir, "broken.png"); File.WriteAllText(corrupt, "not an image");
            Throws<InvalidDataException>(() => ImageImport.Create(new[] { corrupt }, new ImageImportOptions(), CancellationToken.None, null), "unreadable image import reports the invalid image without creating a document");
            var imageDoc = new EditDoc(new List<PageRef> { new PageRef(imported, 1, 1), new PageRef(imported, 0, 0) }, "Images.pdf");
            string imagePdf = Path.Combine(dir, "images.pdf"); imageDoc.SaveAs(imagePdf);
            Check(imageDoc.Base.PageCount == 2 && imageDoc.Base.PageWidth(0) == 792, "image pages can be reordered, rotated and saved as a PDF");
            var disposed = PdfSource.FromBytes(Pdf("disposed"), null); var deadPage = new PageRef(disposed, 0, 0); disposed.Dispose();
            Throws<InvalidOperationException>(() => { using (var ignored = RenderService.RenderPage(deadPage, 100, 100, 0)) { } }, "export refuses an unavailable page instead of silently writing blank pixels");
            using (PdfSource.Retain(pages))
            {
                PdfSource.Collect(new HashSet<PdfSource>());
                Check(!textSource.IsDisposed && DocumentSearch.Find(pages, "alpha", false, CancellationToken.None, null).Total == 5,
                    "background snapshots retain their sources when tabs are collected");
            }
            PdfSource.Collect(new HashSet<PdfSource>());
            Check(textSource.IsDisposed, "sources become collectable when a background snapshot finishes");
        }

        private static bool Until(Func<bool> condition, int milliseconds = 5000)
        {
            var clock = Stopwatch.StartNew();
            do { Application.DoEvents(); if (condition()) return true; Thread.Sleep(10); } while (clock.ElapsedMilliseconds < milliseconds);
            return condition();
        }

        public static void RunUI(string root)
        {
            string path = Path.Combine(root, "feature-ui.pdf"); File.WriteAllBytes(path, Pdf("Alpha alpha", "alpha second page"));
            using (var form = new MainForm(new[] { path }, true))
            {
                form.StartPosition = FormStartPosition.Manual; form.Bounds = new Rectangle(-4000, -4000, 1100, 800); form.ShowInTaskbar = false; form.Show();
                Until(() => Field<DocTab>(form, "active") != null);
                var view = Field<DocView>(form, "view"); var panel = Field<SearchPanel>(form, "searchPanel");
                var find = Field<ToolStripTextBox>(form, "findBox");
                Call(form, "FocusFind"); find.Text = "alpha";
                Check(Until(() => panel.Complete), "typing Find starts a background search");
                Check(panel.Visible && panel.MatchCount == 3 && view.CurrentPage == 0, "Find shows all results and selects the first match");
                Call(form, "FindNext", true); Call(form, "FindNext", true);
                Check(view.CurrentPage == 1, "Find Next navigates to a result on another page");
                Call(form, "FindNext", true); Check(view.CurrentPage == 0, "Find Next wraps to the first result");
                find.Text = "second"; find.Text = "missing";
                Check(Until(() => panel.Complete) && panel.MatchCount == 0, "rapid query changes discard stale results");
                find.Text = "alpha"; Call(form, "StartFind", true); panel.Cancel();
                Check(!panel.Searching && !panel.Complete, "cancelled searches cannot install results");
                Call(form, "StartFind", true); Check(Until(() => panel.Complete), "search restarts after cancellation");
                form.Size = form.MinimumSize; Call(form, "FocusFind"); Application.DoEvents();
                Check(panel.QueryFocused && Field<TextBox>(panel, "query").Width >= 100 && panel.Width > 400,
                    "Find query stays visible and editable at the minimum window size");
                form.Size = new Size(1100, 800);
                Call(form, "HandleKey", Keys.Escape); Check(!panel.Visible, "Escape closes Find results");
                find.Focus(); find.Text = "Al";
                Check(panel.QueryFocused && Field<TextBox>(panel, "query").SelectionStart == 2 && Field<TextBox>(panel, "query").SelectionLength == 0,
                    "typing in the toolbar transfers the caret without selecting or losing the query");
                Check(Until(() => panel.Complete) && panel.MatchCount == 3, "toolbar queries populate the results panel");
                var editedDoc = Field<DocTab>(form, "active").Doc;
                editedDoc.SetPages(editedDoc.Pages.AsEnumerable().Reverse().ToList());
                Check(Until(() => panel.Complete) && panel.MatchCount == 3 && Field<int>(view, "hitPage") >= 0,
                    "page edits discard old hits and rebuild Find results");
                editedDoc.Undo(); Until(() => panel.Complete); Call(form, "CloseFind");
                Call(form, "UpdateUI");
                var commands = PaletteCommand.FromMenu(Field<MenuStrip>(form, "menu").Items);
                Check(commands.Any(c => c.Matches("export pages")) && commands.Any(c => c.Matches("images")), "command palette discovers new feature commands");
                var fit = commands.First(c => c.Matches("fit width"));
                using (var palette = new CommandPalette(commands))
                {
                    palette.StartPosition = FormStartPosition.Manual; palette.Location = new Point(-4000, -4000); palette.Show(form);
                    Field<TextBox>(palette, "query").Text = "fit width"; Application.DoEvents();
                    Call(palette, "RunSelected");
                    Check(palette.SelectedCommand == fit, "palette filtering and Enter select the matching action");
                    palette.SelectedCommand.Item.PerformClick();
                    Check(view.Mode == ZoomMode.FitWidth, "palette runs the existing menu action");
                }
                using (var palette = new CommandPalette(commands))
                {
                    palette.Show(form); Field<TextBox>(palette, "query").Text = "undo"; Application.DoEvents(); Call(palette, "RunSelected");
                    Check(palette.SelectedCommand == null, "palette cannot run disabled commands"); palette.Close();
                }
                // Exercise the real import dialog and background conversion, then undo the insertion.
                string image = Path.Combine(root, "features", "image.png");
                using (var timer = new System.Windows.Forms.Timer { Interval = 30 })
                {
                    timer.Tick += delegate
                    {
                        var dialog = Application.OpenForms.OfType<ImagesDialog>().FirstOrDefault();
                        if (dialog == null) return;
                        timer.Stop(); ((Button)dialog.AcceptButton).PerformClick();
                    };
                    timer.Start(); Call(form, "ImportImages", new[] { image }, true, 1);
                }
                var doc = Field<DocTab>(form, "active").Doc;
                Check(doc.Pages.Count == 3 && doc.IsModified && doc.CanUndo, "image insertion reaches the document through its normal undo model");
                doc.Undo(); Check(doc.Pages.Count == 2 && !doc.IsModified, "undo removes an image insertion in one step");
                using (var export = new ExportDialog(2, 0, new[] { 1 }))
                {
                    Field<ComboBox>(export, "scope").SelectedIndex = 3;
                    Field<TextBox>(export, "range").Text = "0"; Call(export, "Accept");
                    Check(export.DialogResult != DialogResult.OK, "export dialog keeps invalid ranges open");
                    Field<TextBox>(export, "range").Text = "2,1"; Call(export, "Accept");
                    Check(export.Indices.SequenceEqual(new[] { 1, 0 }), "export dialog passes the requested page order");
                }
                form.Close();
            }
        }
    }
}
