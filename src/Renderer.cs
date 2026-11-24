using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using System.Windows.Forms;

namespace Gravity
{
    internal delegate bool TileKeyMap(TileKey from, out TileKey to);

    /// <summary>A rendered piece of a page: page identity, full rendered size, rotation, and tile coordinates.</summary>
    internal struct TileKey : IEquatable<TileKey>
    {
        public readonly PdfSource Src;
        public readonly int Index, Rot, W, H, TX, TY;

        public TileKey(PdfSource src, int index, int rot, int w, int h, int tx, int ty)
        {
            Src = src; Index = index; Rot = rot; W = w; H = h; TX = tx; TY = ty;
        }

        public bool Equals(TileKey o)
        {
            return ReferenceEquals(Src, o.Src) && Index == o.Index && Rot == o.Rot && W == o.W && H == o.H && TX == o.TX && TY == o.TY;
        }

        public override bool Equals(object obj) { return obj is TileKey && Equals((TileKey)obj); }

        public override int GetHashCode()
        {
            unchecked
            {
                int h = Src == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Src);
                h = h * 31 + Index;
                h = h * 31 + Rot;
                h = h * 31 + W;
                h = h * 31 + H;
                h = h * 31 + TX;
                h = h * 31 + TY;
                return h;
            }
        }

        public Rectangle TileRect
        {
            get
            {
                int t = RenderService.TileSize;
                return new Rectangle(TX * t, TY * t, Math.Min(t, W - TX * t), Math.Min(t, H - TY * t));
            }
        }
    }

    /// <summary>
    /// Renders page tiles on one background thread and caches the bitmaps (LRU, memory-bounded).
    /// The cache is only touched on the UI thread; the worker hands results back via BeginInvoke.
    /// </summary>
    internal sealed class RenderService : IDisposable
    {
        public const int TileSize = 1024;
        public const int ClientView = 0, ClientThumbs = 1;

        private readonly Control ui;
        private readonly Thread worker;
        private readonly AutoResetEvent signal = new AutoResetEvent(false);
        private volatile bool stopping;

        private readonly object qLock = new object();
        private readonly List<TileKey>[] wanted = { new List<TileKey>(), new List<TileKey>() };
        private readonly HashSet<TileKey> inFlight = new HashSet<TileKey>();

        private sealed class Entry
        {
            public Bitmap Bmp;
            public LinkedListNode<TileKey> Node;
            public long Bytes;
        }
        private readonly Dictionary<TileKey, Entry> cache = new Dictionary<TileKey, Entry>();
        private readonly LinkedList<TileKey> lru = new LinkedList<TileKey>();
        private long bytes;
        private readonly long budget;

        /// <summary>Raised on the UI thread when a tile arrives.</summary>
        public event Action<TileKey> TileReady;

        public RenderService(Control ui)
        {
            this.ui = ui;
            long mb = Environment.Is64BitProcess ? 320 : 160;
            budget = mb * 1024 * 1024;
            worker = new Thread(Run) { IsBackground = true, Name = "Gravity renderer", Priority = ThreadPriority.BelowNormal };
            worker.Start();
        }

        public bool IsIdle
        {
            get
            {
                lock (qLock) return inFlight.Count == 0 && wanted[0].Count == 0 && wanted[1].Count == 0;
            }
        }

        // ---- UI-thread API ------------------------------------------------

        public Bitmap Get(TileKey k)
        {
            Entry e;
            if (!cache.TryGetValue(k, out e)) return null;
            lru.Remove(e.Node);
            lru.AddLast(e.Node);
            return e.Bmp;
        }

        /// <summary>Cached tiles of the same page and rotation at any size (for scaled placeholders).</summary>
        public void CollectAlternates(PdfSource src, int index, int rot, List<KeyValuePair<TileKey, Bitmap>> into)
        {
            foreach (var kv in cache)
            {
                var k = kv.Key;
                if (ReferenceEquals(k.Src, src) && k.Index == index && k.Rot == rot) into.Add(new KeyValuePair<TileKey, Bitmap>(k, kv.Value.Bmp));
            }
        }

        /// <summary>Replaces a client's wish list. Order is priority; cached keys are skipped.</summary>
        public void Request(int client, List<TileKey> keys)
        {
            lock (qLock)
            {
                var list = wanted[client];
                list.Clear();
                foreach (var k in keys)
                    if (!cache.ContainsKey(k) && !inFlight.Contains(k)) list.Add(k);
            }
            signal.Set();
        }

        /// <summary>Moves cached tiles to new keys (used after a save re-bases a document on the written file).</summary>
        public void Rekey(TileKeyMap map)
        {
            var moves = new List<KeyValuePair<TileKey, TileKey>>();
            foreach (var k in cache.Keys)
            {
                TileKey to;
                if (map(k, out to) && !to.Equals(k)) moves.Add(new KeyValuePair<TileKey, TileKey>(k, to));
            }
            foreach (var m in moves)
            {
                Entry e;
                if (!cache.TryGetValue(m.Key, out e)) continue;
                cache.Remove(m.Key);
                lru.Remove(e.Node);
                if (cache.ContainsKey(m.Value))
                {
                    bytes -= e.Bytes;
                    e.Bmp.Dispose();
                    continue;
                }
                e.Node = lru.AddLast(m.Value);
                cache[m.Value] = e;
            }
        }

        public void Purge(PdfSource src)
        {
            var dead = new List<TileKey>();
            foreach (var k in cache.Keys) if (ReferenceEquals(k.Src, src)) dead.Add(k);
            foreach (var k in dead) Remove(k);
        }

        private void Remove(TileKey k)
        {
            Entry e;
            if (!cache.TryGetValue(k, out e)) return;
            cache.Remove(k);
            lru.Remove(e.Node);
            bytes -= e.Bytes;
            e.Bmp.Dispose();
        }

        private void Deliver(TileKey k, Bitmap bmp)
        {
            lock (qLock) inFlight.Remove(k);
            if (bmp == null) return;
            if (k.Src == null || k.Src.IsDisposed || cache.ContainsKey(k))
            {
                bmp.Dispose();
                return;
            }
            var e = new Entry { Bmp = bmp, Node = lru.AddLast(k), Bytes = (long)bmp.Width * bmp.Height * 4 };
            cache[k] = e;
            bytes += e.Bytes;
            while (bytes > budget && lru.Count > 1) Remove(lru.First.Value);
            var h = TileReady;
            if (h != null) h(k);
        }

        // ---- worker -------------------------------------------------------

        private void Run()
        {
            while (!stopping)
            {
                signal.WaitOne();
                while (!stopping)
                {
                    TileKey k = default(TileKey);
                    bool found = false;
                    lock (qLock)
                    {
                        for (int c = 0; c < wanted.Length && !found; c++)
                        {
                            var list = wanted[c];
                            while (list.Count > 0)
                            {
                                k = list[0];
                                list.RemoveAt(0);
                                if (inFlight.Add(k)) { found = true; break; }
                            }
                        }
                    }
                    if (!found) break;
                    Bitmap bmp = null;
                    try { bmp = RenderTile(k); }
                    catch (Exception) { bmp = null; }
                    var key = k;
                    var result = bmp;
                    try
                    {
                        ui.BeginInvoke((Action)delegate { Deliver(key, result); });
                    }
                    catch (Exception)
                    {
                        if (result != null) result.Dispose();
                        return; // UI is gone
                    }
                }
            }
        }

        public static Bitmap RenderTile(TileKey k)
        {
            Rectangle tr = k.TileRect;
            if (tr.Width <= 0 || tr.Height <= 0) return null;
            var bmp = new Bitmap(tr.Width, tr.Height, PixelFormat.Format32bppPArgb);
            lock (Pdfium.Lock)
            {
                if (k.Src.IsDisposed)
                {
                    bmp.Dispose();
                    return null;
                }
                IntPtr page = k.Src.GetPage(k.Index);
                var data = bmp.LockBits(new Rectangle(0, 0, tr.Width, tr.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
                try
                {
                    IntPtr fb = Pdfium.FPDFBitmap_CreateEx(tr.Width, tr.Height, Pdfium.FPDFBitmap_BGRA, data.Scan0, data.Stride);
                    Pdfium.FPDFBitmap_FillRect(fb, 0, 0, tr.Width, tr.Height, 0xFFFFFFFF);
                    if (page != IntPtr.Zero)
                        Pdfium.FPDF_RenderPageBitmap(fb, page, -tr.X, -tr.Y, k.W, k.H, k.Rot, Pdfium.FPDF_ANNOT);
                    Pdfium.FPDFBitmap_Destroy(fb);
                }
                finally
                {
                    bmp.UnlockBits(data);
                }
            }
            return bmp;
        }

        /// <summary>Renders a whole page (with its extra rotation) into a new bitmap; used for printing.</summary>
        public static Bitmap RenderPage(PageRef p, int w, int h, int flags)
        {
            if (w < 1 || h < 1) return null;
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            lock (Pdfium.Lock)
            {
                var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
                try
                {
                    IntPtr fb = Pdfium.FPDFBitmap_CreateEx(w, h, Pdfium.FPDFBitmap_BGRA, data.Scan0, data.Stride);
                    Pdfium.FPDFBitmap_FillRect(fb, 0, 0, w, h, 0xFFFFFFFF);
                    IntPtr page = p.Src != null && !p.Src.IsDisposed ? p.Src.GetPage(p.Index) : IntPtr.Zero;
                    if (page != IntPtr.Zero) Pdfium.FPDF_RenderPageBitmap(fb, page, 0, 0, w, h, p.Rotation, flags);
                    Pdfium.FPDFBitmap_Destroy(fb);
                }
                finally
                {
                    bmp.UnlockBits(data);
                }
            }
            return bmp;
        }

        public void Dispose()
        {
            stopping = true;
            signal.Set();
            foreach (var e in cache.Values) e.Bmp.Dispose();
            cache.Clear();
            lru.Clear();
        }
    }
}
