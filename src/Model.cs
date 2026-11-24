using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Gravity
{
    internal sealed class PdfPasswordException : Exception
    {
        public readonly bool WasWrong;
        public PdfPasswordException(bool wasWrong)
            : base(wasWrong ? "The password is incorrect." : "This PDF is password protected.")
        {
            WasWrong = wasWrong;
        }
    }

    internal sealed class OutlineItem
    {
        public string Title;
        public int PageIndex = -1;     // index in the source document
        public bool HasY;
        public float Y;                // PDF user-space y of the destination
        public readonly List<OutlineItem> Children = new List<OutlineItem>();
    }

    /// <summary>Where a link or bookmark points.</summary>
    internal sealed class LinkTarget
    {
        public PdfSource Src;
        public int PageIndex = -1;
        public bool HasY;
        public float Y;
        public string Uri;
    }

    /// <summary>
    /// A PDF file loaded into memory. Sources are never modified: edits build a new
    /// page list that references pages in one or more sources (see <see cref="EditDoc"/>).
    /// All members that touch PDFium must be called with <see cref="Pdfium.Lock"/> held.
    /// </summary>
    internal sealed class PdfSource
    {
        private const int MaxPages = 48;
        private const int MaxTextPages = 16;

        private static readonly List<PdfSource> registry = new List<PdfSource>();
        private int pins;

        // Background searches and exports keep their immutable page sources alive.
        public static IDisposable Retain(IEnumerable<PageRef> pages) { return new SourceLease(pages); }

        private sealed class SourceLease : IDisposable
        {
            private List<PdfSource> sources;
            public SourceLease(IEnumerable<PageRef> pages)
            {
                sources = pages.Where(p => p.Src != null).Select(p => p.Src).Distinct().ToList();
                lock (registry) foreach (var source in sources) source.pins++;
            }
            public void Dispose()
            {
                lock (registry)
                {
                    if (sources == null) return;
                    foreach (var source in sources) source.pins--;
                    sources = null;
                }
            }
        }

        public IntPtr Doc { get; private set; }
        public string FilePath { get; private set; }
        public string Name { get; private set; }
        public int PageCount { get; private set; }
        public bool IsDisposed { get; private set; }
        public DateTime FileStamp { get; private set; }
        public long FileLength { get { return length; } }
        public string Password { get; private set; }

        private IntPtr buffer;
        private long length;
        private byte[] passwordBytes;
        private double[] widths, heights;
        private readonly Dictionary<int, IntPtr> pages = new Dictionary<int, IntPtr>();
        private readonly LinkedList<int> pageLru = new LinkedList<int>();
        private readonly Dictionary<int, IntPtr> textPages = new Dictionary<int, IntPtr>();
        private readonly LinkedList<int> textLru = new LinkedList<int>();
        private List<OutlineItem> outline;
        private readonly Dictionary<int, List<PdfFormField>> formFields = new Dictionary<int, List<PdfFormField>>();

        public List<PdfFormField> GetFormFields(int index)
        {
            List<PdfFormField> fields;
            if (!formFields.TryGetValue(index, out fields))
                using (var forms = new PdfForms(Doc)) formFields[index] = fields = forms.Read(index);
            return fields;
        }

        private PdfSource() { }

        public double PageWidth(int i) { return widths[i]; }
        public double PageHeight(int i) { return heights[i]; }

        /// <summary>Loads a file into memory (the file is not kept open or locked).</summary>
        public static PdfSource Open(string path, string password)
        {
            Pdfium.EnsureInitialized();
            path = System.IO.Path.GetFullPath(path);
            long len;
            DateTime stamp = File.GetLastWriteTimeUtc(path);
            IntPtr buf = ReadFile(path, out len);
            try
            {
                var src = FromBuffer(buf, len, password);
                src.FilePath = path;
                src.Name = System.IO.Path.GetFileName(path);
                src.FileStamp = stamp;
                return src;
            }
            catch
            {
                Marshal.FreeHGlobal(buf);
                throw;
            }
        }

        private static IntPtr ReadFile(string path, out long len)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.SequentialScan))
            {
                len = fs.Length;
                if (len == 0) throw new InvalidDataException("The file is empty.");
                IntPtr buf = Marshal.AllocHGlobal(new IntPtr(len));
                try
                {
                    var chunk = new byte[1 << 20];
                    long off = 0;
                    while (off < len)
                    {
                        int n = fs.Read(chunk, 0, (int)Math.Min(chunk.Length, len - off));
                        if (n <= 0) throw new EndOfStreamException("The file changed while it was being read.");
                        Marshal.Copy(chunk, 0, new IntPtr(buf.ToInt64() + off), n);
                        off += n;
                    }
                }
                catch
                {
                    Marshal.FreeHGlobal(buf);
                    throw;
                }
                return buf;
            }
        }

        private static PdfSource FromBuffer(IntPtr buf, long len, string password)
        {
            byte[] pw = password == null ? null : Encoding.UTF8.GetBytes(password + "\0");
            lock (Pdfium.Lock)
            {
                IntPtr doc = Pdfium.FPDF_LoadMemDocument64(buf, new UIntPtr((ulong)len), pw);
                if (doc == IntPtr.Zero)
                {
                    uint err = Pdfium.FPDF_GetLastError();
                    if (err == Pdfium.FPDF_ERR_PASSWORD) throw new PdfPasswordException(password != null);
                    throw new InvalidDataException(Pdfium.ErrorText(err));
                }
                var s = new PdfSource();
                s.Doc = doc;
                s.buffer = buf;
                s.length = len;
                s.passwordBytes = pw;
                s.Password = password;
                s.Name = "Untitled";
                s.PageCount = Math.Max(0, Pdfium.FPDF_GetPageCount(doc));
                s.widths = new double[s.PageCount];
                s.heights = new double[s.PageCount];
                for (int i = 0; i < s.PageCount; i++)
                {
                    double w, h;
                    if (Pdfium.FPDF_GetPageSizeByIndex(doc, i, out w, out h) == 0 || w < 1 || h < 1)
                    {
                        w = 612; h = 792;
                    }
                    s.widths[i] = w;
                    s.heights[i] = h;
                }
                lock (registry) registry.Add(s);
                return s;
            }
        }

        /// <summary>Loads a second, independent PDFium document over the same bytes.</summary>
        public IntPtr LoadCopy()
        {
            IntPtr d = Pdfium.FPDF_LoadMemDocument64(buffer, new UIntPtr((ulong)length), passwordBytes);
            if (d == IntPtr.Zero) throw new IOException(Pdfium.ErrorText(Pdfium.FPDF_GetLastError()));
            return d;
        }

        public static PdfSource FromBytes(byte[] bytes, string password)
        {
            Pdfium.EnsureInitialized();
            IntPtr buf = Marshal.AllocHGlobal(bytes.Length);
            try
            {
                Marshal.Copy(bytes, 0, buf, bytes.Length);
                return FromBuffer(buf, bytes.Length, password);
            }
            catch { Marshal.FreeHGlobal(buf); throw; }
        }

        public void WriteOriginalBytes(Stream s)
        {
            var chunk = new byte[1 << 20];
            long off = 0;
            while (off < length)
            {
                int n = (int)Math.Min(chunk.Length, length - off);
                Marshal.Copy(new IntPtr(buffer.ToInt64() + off), chunk, 0, n);
                s.Write(chunk, 0, n);
                off += n;
            }
        }

        public IntPtr GetPage(int index)
        {
            if (IsDisposed || index < 0 || index >= PageCount) return IntPtr.Zero;
            IntPtr p;
            if (pages.TryGetValue(index, out p))
            {
                pageLru.Remove(index);
                pageLru.AddLast(index);
                return p;
            }
            p = Pdfium.FPDF_LoadPage(Doc, index);
            if (p == IntPtr.Zero) return p;
            pages[index] = p;
            pageLru.AddLast(index);
            while (pages.Count > MaxPages)
            {
                int victim = pageLru.First.Value;
                pageLru.RemoveFirst();
                CloseTextPage(victim);
                Pdfium.FPDF_ClosePage(pages[victim]);
                pages.Remove(victim);
            }
            return p;
        }

        public IntPtr GetTextPage(int index)
        {
            IntPtr tp;
            if (textPages.TryGetValue(index, out tp))
            {
                textLru.Remove(index);
                textLru.AddLast(index);
                GetPage(index); // keep the owning page fresh in its LRU
                return tp;
            }
            IntPtr page = GetPage(index);
            if (page == IntPtr.Zero) return IntPtr.Zero;
            tp = Pdfium.FPDFText_LoadPage(page);
            if (tp == IntPtr.Zero) return tp;
            textPages[index] = tp;
            textLru.AddLast(index);
            while (textPages.Count > MaxTextPages)
            {
                int victim = textLru.First.Value;
                CloseTextPage(victim);
            }
            return tp;
        }

        private void CloseTextPage(int index)
        {
            IntPtr tp;
            if (textPages.TryGetValue(index, out tp))
            {
                Pdfium.FPDFText_ClosePage(tp);
                textPages.Remove(index);
                textLru.Remove(index);
            }
        }

        public List<OutlineItem> GetOutline()
        {
            if (outline != null) return outline;
            outline = new List<OutlineItem>();
            if (IsDisposed) return outline;
            var seen = new HashSet<long>();
            int budget = 20000;
            LoadOutline(IntPtr.Zero, outline, 0, seen, ref budget);
            return outline;
        }

        private void LoadOutline(IntPtr parent, List<OutlineItem> into, int depth, HashSet<long> seen, ref int budget)
        {
            for (IntPtr bm = Pdfium.FPDFBookmark_GetFirstChild(Doc, parent);
                 bm != IntPtr.Zero && budget > 0;
                 bm = Pdfium.FPDFBookmark_GetNextSibling(Doc, bm))
            {
                if (!seen.Add(bm.ToInt64())) break;
                budget--;
                IntPtr b = bm;
                var item = new OutlineItem();
                item.Title = Pdfium.GetUtf16(delegate (byte[] buf, uint len) { return Pdfium.FPDFBookmark_GetTitle(b, buf, len); }).Trim();
                IntPtr dest = Pdfium.FPDFBookmark_GetDest(Doc, bm);
                if (dest == IntPtr.Zero)
                {
                    IntPtr action = Pdfium.FPDFBookmark_GetAction(bm);
                    if (action != IntPtr.Zero && Pdfium.FPDFAction_GetType(action) == Pdfium.PDFACTION_GOTO)
                        dest = Pdfium.FPDFAction_GetDest(Doc, action);
                }
                if (dest != IntPtr.Zero) ReadDest(dest, out item.PageIndex, out item.HasY, out item.Y);
                into.Add(item);
                if (depth < 48) LoadOutline(bm, item.Children, depth + 1, seen, ref budget);
            }
        }

        public void ReadDest(IntPtr dest, out int pageIndex, out bool hasY, out float y)
        {
            pageIndex = Pdfium.FPDFDest_GetDestPageIndex(Doc, dest);
            int hx, hy, hz;
            float fx, fy, fz;
            hasY = false;
            y = 0;
            if (Pdfium.FPDFDest_GetLocationInPage(dest, out hx, out hy, out hz, out fx, out fy, out fz) != 0 && hy != 0)
            {
                hasY = true;
                y = fy;
            }
        }

        /// <summary>Resolves the link under a page-space point, or null.</summary>
        public LinkTarget LinkAt(int index, double px, double py)
        {
            IntPtr page = GetPage(index);
            if (page == IntPtr.Zero) return null;
            IntPtr link = Pdfium.FPDFLink_GetLinkAtPoint(page, px, py);
            if (link == IntPtr.Zero) return null;
            var t = new LinkTarget { Src = this };
            IntPtr dest = Pdfium.FPDFLink_GetDest(Doc, link);
            if (dest == IntPtr.Zero)
            {
                IntPtr action = Pdfium.FPDFLink_GetAction(link);
                if (action != IntPtr.Zero)
                {
                    uint type = Pdfium.FPDFAction_GetType(action);
                    if (type == Pdfium.PDFACTION_GOTO) dest = Pdfium.FPDFAction_GetDest(Doc, action);
                    else if (type == Pdfium.PDFACTION_URI)
                    {
                        uint len = Pdfium.FPDFAction_GetURIPath(Doc, action, null, 0);
                        if (len > 1 && len < 65536)
                        {
                            var buf = new byte[len];
                            Pdfium.FPDFAction_GetURIPath(Doc, action, buf, len);
                            t.Uri = Encoding.UTF8.GetString(buf, 0, (int)len - 1);
                        }
                    }
                }
            }
            if (dest != IntPtr.Zero) ReadDest(dest, out t.PageIndex, out t.HasY, out t.Y);
            if (t.Uri == null && t.PageIndex < 0) return null;
            return t;
        }

        public string GetTitle()
        {
            if (IsDisposed) return "";
            return Pdfium.GetMetaText(Doc, "Title").Trim();
        }

        public void Dispose()
        {
            lock (Pdfium.Lock)
            {
                if (IsDisposed) return;
                IsDisposed = true;
                foreach (var tp in textPages.Values) Pdfium.FPDFText_ClosePage(tp);
                textPages.Clear();
                foreach (var p in pages.Values) Pdfium.FPDF_ClosePage(p);
                pages.Clear();
                if (Doc != IntPtr.Zero) Pdfium.FPDF_CloseDocument(Doc);
                Doc = IntPtr.Zero;
                if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
                buffer = IntPtr.Zero;
            }
            lock (registry) registry.Remove(this);
        }

        /// <summary>Frees every source that is no longer referenced by any open document or the clipboard.</summary>
        public static List<PdfSource> Collect(HashSet<PdfSource> live)
        {
            List<PdfSource> dead;
            lock (registry) dead = registry.Where(s => s.pins == 0 && !live.Contains(s)).ToList();
            foreach (var s in dead) s.Dispose();
            return dead;
        }
    }

    /// <summary>One page in an edited document: a page of a source, plus extra rotation. Immutable.</summary>
    internal sealed class PageRef
    {
        public readonly PdfSource Src;   // null for an inserted blank page
        public readonly int Index;
        public readonly int Rotation;    // extra clockwise quarter turns, 0..3
        public readonly double BlankW, BlankH;

        public PageRef(PdfSource src, int index, int rotation)
        {
            Src = src;
            Index = index;
            Rotation = rotation & 3;
        }

        private PageRef(double w, double h, int rotation)
        {
            BlankW = w;
            BlankH = h;
            Rotation = rotation & 3;
        }

        public static PageRef Blank(double w, double h) { return new PageRef(w, h, 0); }

        public PageRef Rotated(int quarterTurns)
        {
            if (Src == null) return new PageRef(BlankW, BlankH, Rotation + quarterTurns);
            return new PageRef(Src, Index, Rotation + quarterTurns);
        }

        public PageRef Clone()
        {
            return Src == null ? new PageRef(BlankW, BlankH, Rotation) : new PageRef(Src, Index, Rotation);
        }

        /// <summary>Width in points as the page is defined (its own /Rotate applied, extra rotation not).</summary>
        public double BaseWidth { get { return Src != null ? Src.PageWidth(Index) : BlankW; } }
        public double BaseHeight { get { return Src != null ? Src.PageHeight(Index) : BlankH; } }

        public bool SameAs(PageRef o)
        {
            return o != null && o.Src == Src && o.Index == Index && o.Rotation == Rotation
                && o.BlankW == BlankW && o.BlankH == BlankH;
        }
    }

    internal sealed class RefComparer<T> : IEqualityComparer<T> where T : class
    {
        public static readonly RefComparer<T> Instance = new RefComparer<T>();
        public bool Equals(T a, T b) { return ReferenceEquals(a, b); }
        public int GetHashCode(T o) { return RuntimeHelpers.GetHashCode(o); }
    }

    /// <summary>
    /// A document open in a tab: an ordered list of page references with undo history.
    /// Saving materializes the list into a real PDF using PDFium's page import/move APIs.
    /// </summary>
    internal sealed class EditDoc
    {
        private const int MaxUndo = 300;

        public List<PageRef> Pages { get; private set; }
        public PdfSource Base { get; private set; }
        public string FilePath { get; private set; }
        public string UntitledName;
        public int Version { get; private set; }
        public event EventHandler Changed;

        private List<PageRef> saved;
        private readonly List<List<PageRef>> undo = new List<List<PageRef>>();
        private readonly List<List<PageRef>> redo = new List<List<PageRef>>();
        private bool modified;

        public EditDoc(PdfSource src)
        {
            Base = src;
            FilePath = src.FilePath;
            Pages = AllPages(src);
            saved = Pages;
        }

        public EditDoc(List<PageRef> pages, string untitledName)
        {
            Pages = pages;
            UntitledName = untitledName;
            saved = new List<PageRef>();
            modified = true;
        }

        public static List<PageRef> AllPages(PdfSource src)
        {
            var list = new List<PageRef>(src.PageCount);
            for (int i = 0; i < src.PageCount; i++) list.Add(new PageRef(src, i, 0));
            return list;
        }

        public string DisplayName
        {
            get { return FilePath != null ? Path.GetFileName(FilePath) : (UntitledName ?? "Untitled"); }
        }

        public bool IsModified { get { return modified; } }
        public bool CanUndo { get { return undo.Count > 0; } }
        public bool CanRedo { get { return redo.Count > 0; } }

        public void SetPages(List<PageRef> newPages)
        {
            undo.Add(Pages);
            if (undo.Count > MaxUndo) undo.RemoveAt(0);
            redo.Clear();
            Pages = newPages;
            OnChanged();
        }

        public bool Undo()
        {
            if (undo.Count == 0) return false;
            redo.Add(Pages);
            Pages = undo[undo.Count - 1];
            undo.RemoveAt(undo.Count - 1);
            OnChanged();
            return true;
        }

        public bool Redo()
        {
            if (redo.Count == 0) return false;
            undo.Add(Pages);
            Pages = redo[redo.Count - 1];
            redo.RemoveAt(redo.Count - 1);
            OnChanged();
            return true;
        }

        private void OnChanged()
        {
            Version++;
            modified = !Same(Pages, saved);
            var h = Changed;
            if (h != null) h(this, EventArgs.Empty);
        }

        private static bool Same(List<PageRef> a, List<PageRef> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (!a[i].SameAs(b[i])) return false;
            return true;
        }

        public IEnumerable<PdfSource> Sources()
        {
            if (Base != null) yield return Base;
            foreach (var p in Pages) if (p.Src != null) yield return p.Src;
            foreach (var l in undo) foreach (var p in l) if (p.Src != null) yield return p.Src;
            foreach (var l in redo) foreach (var p in l) if (p.Src != null) yield return p.Src;
        }

        /// <summary>Position of a source page in the current list, or -1 if it was removed.</summary>
        public int Find(PdfSource src, int index)
        {
            for (int i = 0; i < Pages.Count; i++)
                if (Pages[i].Src == src && Pages[i].Index == index) return i;
            return -1;
        }

        public bool IsPristineCopyOfBase()
        {
            return IsPristineCopy(Base);
        }

        private bool IsPristineCopy(PdfSource source)
        {
            if (source == null || Pages.Count != source.PageCount) return false;
            for (int i = 0; i < Pages.Count; i++)
            {
                var p = Pages[i];
                if (p.Src != source || p.Index != i || p.Rotation != 0) return false;
            }
            return true;
        }

        /// <summary>Writes the current page list as a PDF.</summary>
        public void WritePdf(Stream output)
        {
            if (Pages.Count == 0) throw new InvalidOperationException("A PDF needs at least one page.");
            lock (Pdfium.Lock)
            {
                // Form edits create immutable full-document snapshots. Use that
                // snapshot's catalog when no pages still refer to the saved base.
                var basis = Base != null && Pages.Any(p => p.Src == Base) ? Base : Pages.Select(p => p.Src).FirstOrDefault(s => s != null);
                if (IsPristineCopy(basis))
                {
                    basis.WriteOriginalBytes(output);
                    return;
                }
                foreach (var p in Pages)
                    if (p.Src != null && p.Src.IsDisposed) throw new InvalidOperationException("A source document was closed.");

                IntPtr doc = basis != null ? basis.LoadCopy() : Pdfium.FPDF_CreateNewDocument();
                if (doc == IntPtr.Zero) throw new IOException("Could not create a document.");
                try
                {
                    int m = Pages.Count;
                    var pos = new int[m];       // where target page k currently is in 'doc'
                    var placed = new bool[m];
                    int count = 0;

                    // 1. Keep the base document's own pages in place (this preserves its
                    //    outline, metadata and forms), deleting the ones no longer used.
                    if (basis != null)
                    {
                        int n = basis.PageCount;
                        var keeper = new int[n];
                        for (int i = 0; i < n; i++) keeper[i] = -1;
                        for (int k = 0; k < m; k++)
                        {
                            var p = Pages[k];
                            if (p.Src == basis && keeper[p.Index] < 0) keeper[p.Index] = k;
                        }
                        for (int i = n - 1; i >= 0; i--)
                            if (keeper[i] < 0) Pdfium.FPDFPage_Delete(doc, i);
                        for (int i = 0; i < n; i++)
                        {
                            if (keeper[i] < 0) continue;
                            pos[keeper[i]] = count++;
                            placed[keeper[i]] = true;
                        }
                    }

                    // 2. Append everything else (other files, duplicates, blank pages) in runs.
                    int k2 = 0;
                    while (k2 < m)
                    {
                        if (placed[k2]) { k2++; continue; }
                        var p = Pages[k2];
                        if (p.Src == null)
                        {
                            IntPtr np = Pdfium.FPDFPage_New(doc, count, p.BlankW, p.BlankH);
                            if (np == IntPtr.Zero) throw new IOException("Could not insert a blank page.");
                            Pdfium.FPDFPage_GenerateContent(np);
                            Pdfium.FPDF_ClosePage(np);
                            pos[k2++] = count++;
                            continue;
                        }
                        int start = k2;
                        var run = new List<int>();
                        while (k2 < m && !placed[k2] && Pages[k2].Src == p.Src)
                        {
                            run.Add(Pages[k2].Index);
                            k2++;
                        }
                        if (Pdfium.FPDF_ImportPagesByIndex(doc, p.Src.Doc, run.ToArray(), (uint)run.Count, count) == 0)
                            throw new IOException("Could not copy pages from " + p.Src.Name + ".");
                        for (int j = 0; j < run.Count; j++) pos[start + j] = count++;
                    }
                    if (Pdfium.FPDF_GetPageCount(doc) != m)
                        throw new IOException("Page count mismatch while saving.");

                    // 3. Put pages in the final order.
                    bool identity = true;
                    for (int k = 0; k < m; k++) if (pos[k] != k) { identity = false; break; }
                    if (!identity && Pdfium.FPDF_MovePages(doc, pos, (uint)m, 0) == 0)
                        throw new IOException("Could not reorder pages.");

                    // 4. Apply rotations.
                    for (int k = 0; k < m; k++)
                    {
                        if (Pages[k].Rotation == 0) continue;
                        IntPtr pg = Pdfium.FPDF_LoadPage(doc, k);
                        if (pg == IntPtr.Zero) throw new IOException("Could not rotate page " + (k + 1) + ".");
                        int r = Pdfium.FPDFPage_GetRotation(pg);
                        Pdfium.FPDFPage_SetRotation(pg, (r + Pages[k].Rotation) & 3);
                        Pdfium.FPDF_ClosePage(pg);
                    }

                    Pdfium.SaveDocument(doc, output);
                }
                finally
                {
                    Pdfium.FPDF_CloseDocument(doc);
                }
            }
        }

        /// <summary>Applies form values to an independent snapshot, as one undoable edit.</summary>
        public void FillForm(int pageIndex, IList<PdfFormEdit> edits)
        {
            if (edits.Count == 0) return;
            if (pageIndex < 0 || pageIndex >= Pages.Count) throw new ArgumentOutOfRangeException("pageIndex");
            lock (Pdfium.Lock)
            {
                string password = Base != null ? Base.Password : null;
                PdfSource working;
                using (var bytes = new MemoryStream())
                {
                    WritePdf(bytes);
                    working = PdfSource.FromBytes(bytes.ToArray(), password);
                }
                try
                {
                    using (var forms = new PdfForms(working.Doc))
                    {
                        // Load every page so shared fields get refreshed appearances.
                        for (int i = 0; i < working.PageCount; i++) forms.Page(i);
                        foreach (var edit in edits) forms.Apply(pageIndex, edit);
                    }
                    using (var bytes = new MemoryStream())
                    {
                        Pdfium.SaveDocument(working.Doc, bytes);
                        var fresh = PdfSource.FromBytes(bytes.ToArray(), password);
                        SetPages(AllPages(fresh));
                    }
                }
                finally { working.Dispose(); }
            }
        }

        /// <summary>Saves to a file (atomically via a temp file) and re-bases the document on it.</summary>
        public void SaveAs(string path)
        {
            path = Path.GetFullPath(path);
            WriteFile(path);
            var fresh = PdfSource.Open(path, Base != null ? Base.Password : null);
            Base = fresh;
            FilePath = path;
            UntitledName = null;
            var list = AllPages(fresh);
            saved = list;
            Pages = list;
            OnChanged();
        }

        /// <summary>Writes the current pages to a file via a temp file, without changing this document.</summary>
        public void WriteFile(string path)
        {
            AtomicFile.Write(path, WritePdf);
        }

        /// <summary>Replaces the document with a fresh load of its file (used when it changes on disk).</summary>
        public void Reload(PdfSource fresh)
        {
            Base = fresh;
            FilePath = fresh.FilePath;
            undo.Clear();
            redo.Clear();
            Pages = AllPages(fresh);
            saved = Pages;
            OnChanged();
        }
    }
}
