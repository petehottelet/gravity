using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace Gravity
{
    internal sealed class PdfTextRun
    {
        public int Index;
        public string Text;
        public float FontSize;
        // PDF user space, with Y increasing upwards.
        public RectangleF Bounds;
        public override string ToString() { return Text; }
    }

    internal sealed class PdfTextEdit
    {
        public int Index;
        public string Original, Text;
    }

    /// <summary>Edits top-level text objects. All native calls require Pdfium.Lock.</summary>
    internal static class PdfTextEditing
    {
        internal const int MaxTextLength = 16384;

        public static bool CanEdit(PageRef page)
        {
            return page.Src != null && !page.Src.IsDisposed && (Pdfium.FPDF_GetDocPermissions(page.Src.Doc) & 8) != 0;
        }

        public static List<PdfTextRun> Read(PageRef page)
        {
            if (page.Src == null) return new List<PdfTextRun>();
            return Read(page.Src.GetPage(page.Index));
        }

        private static string ReadText(IntPtr obj, IntPtr textPage)
        {
            return Pdfium.GetUtf16((buffer, size) => Pdfium.FPDFTextObj_GetText(obj, textPage, buffer, size));
        }

        private static List<PdfTextRun> Read(IntPtr page)
        {
            if (page == IntPtr.Zero) throw new IOException("Could not read this page's text.");
            var runs = new List<PdfTextRun>();
            IntPtr textPage = Pdfium.FPDFText_LoadPage(page);
            if (textPage == IntPtr.Zero) throw new IOException("Could not read this page's text.");
            try
            {
                int count = Pdfium.FPDFPage_CountObjects(page);
                for (int i = 0; i < count; i++)
                {
                    IntPtr obj = Pdfium.FPDFPage_GetObject(page, i);
                    if (Pdfium.FPDFPageObj_GetType(obj) != 1) continue;
                    string text = ReadText(obj, textPage);
                    float left, bottom, right, top, size;
                    if (string.IsNullOrEmpty(text) || Pdfium.FPDFPageObj_GetBounds(obj, out left, out bottom, out right, out top) == 0) continue;
                    Pdfium.FPDFTextObj_GetFontSize(obj, out size);
                    runs.Add(new PdfTextRun { Index = i, Text = text, FontSize = size,
                        Bounds = RectangleF.FromLTRB(left, bottom, right, top) });
                }
            }
            finally { Pdfium.FPDFText_ClosePage(textPage); }
            return runs;
        }

        public static void Apply(IntPtr page, IList<PdfTextEdit> edits)
        {
            // Resolve and validate all targets before changing any object. In particular,
            // never guess at an index if serialization changed a page's object structure.
            var originals = Read(page).ToDictionary(run => run.Index);
            var targets = new List<IntPtr>();
            var indices = new HashSet<int>();
            foreach (var edit in edits)
            {
                PdfTextRun original;
                if (edit == null || !indices.Add(edit.Index) || !originals.TryGetValue(edit.Index, out original) || original.Text != edit.Original)
                    throw new InvalidOperationException("The selected text has changed. Close the editor and select it again.");
                if (edit.Text == null || edit.Text.Length > MaxTextLength || edit.Text.Any(c => char.IsControl(c) || c == '\u2028' || c == '\u2029'))
                    throw new InvalidOperationException("Use one line of text per section, without tabs or line breaks (up to 16,384 characters).");
                targets.Add(Pdfium.FPDFPage_GetObject(page, edit.Index));
            }
            for (int i = 0; i < edits.Count; i++)
            {
                if (edits[i].Text.Length == 0)
                {
                    if (Pdfium.FPDFPage_RemoveObject(page, targets[i]) == 0) throw new IOException("Could not remove this text section.");
                    Pdfium.FPDFPageObj_Destroy(targets[i]);
                }
                else if (Pdfium.FPDFText_SetText(targets[i], edits[i].Text) == 0)
                    throw new InvalidOperationException("This font cannot represent the replacement text. Try different characters.");
            }
            // Subset fonts can accept SetText while silently losing unsupported characters.
            // Read back from a new text page before accepting the edit.
            IntPtr check = Pdfium.FPDFText_LoadPage(page);
            if (check == IntPtr.Zero) throw new IOException("Could not verify the replacement text.");
            try
            {
                for (int i = 0; i < edits.Count; i++)
                    if (edits[i].Text.Length > 0 && ReadText(targets[i], check) != edits[i].Text)
                        throw new InvalidOperationException("This font cannot represent all replacement characters. Try different characters; no changes were applied.");
            }
            finally { Pdfium.FPDFText_ClosePage(check); }
            if (Pdfium.FPDFPage_GenerateContent(page) == 0) throw new IOException("Could not update the page's text.");
        }
    }
}
