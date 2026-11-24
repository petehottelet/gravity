using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Gravity
{
    internal static class PageRanges
    {
        // Preserve the requested order, but export each page only once.
        public static List<int> Parse(string value, int pageCount)
        {
            var pages = new List<int>();
            var seen = new HashSet<int>();
            foreach (string part in (value ?? "").Split(','))
            {
                var ends = part.Trim().Split('-');
                int first, last;
                if (ends.Length > 2 || !int.TryParse(ends[0].Trim(), out first))
                    throw new ArgumentException("Enter pages such as 1, 3-5, 8.");
                last = first;
                if (ends.Length == 2 && !int.TryParse(ends[1].Trim(), out last))
                    throw new ArgumentException("Enter a page number on each side of the dash.");
                if (first < 1 || last < 1 || first > pageCount || last > pageCount)
                    throw new ArgumentException("Page numbers must be between 1 and " + pageCount + ".");
                int step = first <= last ? 1 : -1;
                for (int i = first; ; i += step)
                {
                    if (seen.Add(i - 1)) pages.Add(i - 1);
                    if (i == last) break;
                }
            }
            return pages;
        }
    }

    internal static class DocumentExport
    {
        internal const long MaxPixels = 40000000;

        public static Size PixelSize(PageRef page, int dpi)
        {
            if (dpi < 72 || dpi > 600) throw new ArgumentException("Resolution must be between 72 and 600 dpi.");
            double w = page.BaseWidth, h = page.BaseHeight;
            if ((page.Rotation & 1) != 0) { double swap = w; w = h; h = swap; }
            w = Math.Ceiling(w * dpi / 72); h = Math.Ceiling(h * dpi / 72);
            if (double.IsNaN(w) || double.IsNaN(h) || w < 1 || h < 1 || w > 32767 || h > 32767 || w * h > MaxPixels)
                throw new ArgumentException("This page is too large at that resolution. Choose a lower dpi (maximum 40 million pixels per image).");
            return new Size((int)w, (int)h);
        }

        public static string SafeStem(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            string stem = new string((name ?? "").Select(c => invalid.Contains(c) || char.IsControl(c) ? '_' : c).ToArray()).Trim().TrimEnd('.', ' ');
            if (stem.Length > 80) stem = stem.Substring(0, 80).TrimEnd('.', ' ');
            if (stem.Length == 0) stem = "Document";
            string head = stem.Split('.')[0].ToUpperInvariant();
            if (new[] { "CON", "PRN", "AUX", "NUL" }.Contains(head) ||
                (head.Length == 4 && (head.StartsWith("COM") || head.StartsWith("LPT")) && head[3] >= '1' && head[3] <= '9')) stem = "_" + stem;
            return stem;
        }

        public static int Images(IList<PageRef> pages, IList<int> indices, string folder, string name, int dpi, bool jpeg,
            CancellationToken cancel, Action<int, string> progress)
        {
            if (!Directory.Exists(folder)) throw new DirectoryNotFoundException("Choose an existing export folder.");
            foreach (int i in indices) PixelSize(pages[i], dpi); // validate before writing anything
            string stem = SafeStem(name);
            int written = 0;
            foreach (int index in indices)
            {
                cancel.ThrowIfCancellationRequested();
                Size size = PixelSize(pages[index], dpi);
                using (var bitmap = RenderService.RenderPage(pages[index], size.Width, size.Height, Pdfium.FPDF_ANNOT))
                {
                    bitmap.SetResolution(dpi, dpi);
                    string baseName = stem + "-page-" + (index + 1).ToString("D4");
                    string ext = jpeg ? ".jpg" : ".png";
                    int suffix = 1;
                    while (true)
                    {
                        cancel.ThrowIfCancellationRequested();
                        string path = Path.Combine(folder, baseName + (suffix == 1 ? "" : " (" + suffix + ")") + ext);
                        if (File.Exists(path) || Directory.Exists(path)) { suffix++; continue; }
                        try
                        {
                            AtomicFile.Write(path, stream =>
                            {
                                bitmap.Save(stream, jpeg ? ImageFormat.Jpeg : ImageFormat.Png);
                                cancel.ThrowIfCancellationRequested();
                            }, false);
                            break;
                        }
                        catch (IOException) { if (!File.Exists(path)) throw; suffix++; }
                    }
                }
                written++;
                if (progress != null) progress(written * 100 / indices.Count, "Exported " + written + " of " + indices.Count + " pages");
            }
            return written;
        }

        public static void Text(IList<PageRef> pages, IList<int> indices, string path,
            CancellationToken cancel, Action<int, string> progress)
        {
            AtomicFile.Write(path, stream =>
            {
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, true))
                {
                    for (int at = 0; at < indices.Count; at++)
                    {
                        cancel.ThrowIfCancellationRequested();
                        var p = pages[indices[at]];
                        if (at > 0) writer.Write("\r\n\f\r\n");
                        if (p.Src != null)
                        {
                            lock (Pdfium.Lock)
                            {
                                if (p.Src.IsDisposed) throw new ObjectDisposedException("PDF");
                                IntPtr text = p.Src.GetTextPage(p.Index);
                                if (text == IntPtr.Zero) throw new IOException("Could not read text on page " + (indices[at] + 1) + ".");
                                int count = Pdfium.FPDFText_CountChars(text);
                                // Bound allocations for unusually text-heavy pages.
                                for (int start = 0; start < count; start += 8192)
                                {
                                    cancel.ThrowIfCancellationRequested();
                                    writer.Write(Pdfium.GetText(text, start, Math.Min(8192, count - start)));
                                }
                            }
                        }
                        if (progress != null) progress((at + 1) * 100 / indices.Count, "Read " + (at + 1) + " of " + indices.Count + " pages");
                    }
                    writer.Flush();
                    cancel.ThrowIfCancellationRequested();
                }
            });
        }
    }
}
