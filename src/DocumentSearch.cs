using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

namespace Gravity
{
    internal sealed class SearchMatch
    {
        public int Page, Character, Length;
        public string Snippet;
        public override string ToString() { return "Page " + (Page + 1) + "    " + Snippet; }
    }

    internal sealed class SearchResults
    {
        public const int DisplayLimit = 10000;
        public readonly List<SearchMatch> Matches = new List<SearchMatch>();
        public long Total;
        public int PagesScanned;
    }

    internal static class DocumentSearch
    {
        public static SearchResults Find(IList<PageRef> pages, string query, bool matchCase,
            CancellationToken cancel, Action<int, long> progress)
        {
            var result = new SearchResults();
            if (string.IsNullOrEmpty(query)) return result;
            for (int i = 0; i < pages.Count; i++)
            {
                cancel.ThrowIfCancellationRequested();
                var p = pages[i];
                if (p.Src != null)
                {
                    lock (Pdfium.Lock)
                    {
                        cancel.ThrowIfCancellationRequested();
                        if (p.Src.IsDisposed) throw new ObjectDisposedException("PDF");
                        IntPtr text = p.Src.GetTextPage(p.Index);
                        if (text == IntPtr.Zero) throw new InvalidOperationException("Could not read text on page " + (i + 1) + ".");
                        IntPtr search = Pdfium.FPDFText_FindStart(text, query, matchCase ? Pdfium.FPDF_MATCHCASE : 0, 0);
                        if (search == IntPtr.Zero) throw new InvalidOperationException("Could not start the search.");
                        try
                        {
                            int chars = Pdfium.FPDFText_CountChars(text);
                            while (Pdfium.FPDFText_FindNext(search) != 0)
                            {
                                cancel.ThrowIfCancellationRequested();
                                result.Total++;
                                if (result.Matches.Count >= SearchResults.DisplayLimit) continue;
                                int start = Pdfium.FPDFText_GetSchResultIndex(search);
                                int length = Pdfium.FPDFText_GetSchCount(search);
                                int before = Math.Max(0, start - 45);
                                int end = Math.Min(chars, start + Math.Min(length, 120) + 65);
                                string snippet = Pdfium.GetText(text, before, Math.Max(0, end - before));
                                snippet = Regex.Replace(snippet, @"\s+", " ").Trim();
                                result.Matches.Add(new SearchMatch { Page = i, Character = start, Length = length,
                                    Snippet = (before > 0 ? "…" : "") + snippet + (end < chars ? "…" : "") });
                            }
                        }
                        finally { Pdfium.FPDFText_FindClose(search); }
                    }
                }
                result.PagesScanned = i + 1;
                if (progress != null) progress(i + 1, result.Total);
            }
            cancel.ThrowIfCancellationRequested();
            return result;
        }
    }
}
