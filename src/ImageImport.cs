using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;

namespace Gravity
{
    internal enum ImagePageSize { Image, Letter, A4 }

    internal sealed class ImageImportOptions
    {
        public ImagePageSize Size = ImagePageSize.Letter;
        public double Margin = 18; // points
        public bool Landscape;
    }

    internal static class ImageImport
    {
        public const string Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp";
        public static bool Supports(string path)
        {
            return new[] { ".png", ".jpg", ".jpeg", ".bmp" }.Contains(Path.GetExtension(path).ToLowerInvariant());
        }

        public static SizeF PageSize(int width, int height, ImageImportOptions options)
        {
            if (options.Margin < 0 || options.Margin > 144 || double.IsNaN(options.Margin))
                throw new ArgumentException("Margins must be between 0 and 2 inches.");
            double w = options.Size == ImagePageSize.A4 ? 595.276 : 612;
            double h = options.Size == ImagePageSize.A4 ? 841.89 : 792;
            if (options.Size == ImagePageSize.Image)
            {
                w = width * 72.0 / 96 + options.Margin * 2;
                h = height * 72.0 / 96 + options.Margin * 2;
            }
            else if (options.Landscape) { double swap = w; w = h; h = swap; }
            return new SizeF((float)w, (float)h);
        }

        public static byte[] Create(IList<string> files, ImageImportOptions options, CancellationToken cancel, Action<int, string> progress)
        {
            if (files.Count == 0) throw new ArgumentException("Add at least one image.");
            Pdfium.EnsureInitialized();
            IntPtr doc;
            lock (Pdfium.Lock) doc = Pdfium.FPDF_CreateNewDocument();
            if (doc == IntPtr.Zero) throw new IOException("Could not create the PDF.");
            try
            {
                for (int i = 0; i < files.Count; i++)
                {
                    cancel.ThrowIfCancellationRequested();
                    if (!Supports(files[i])) throw new ArgumentException("Choose PNG, JPEG, or BMP images.");
                    using (var source = OpenImage(files[i]))
                    {
                        if ((long)source.Width * source.Height > DocumentExport.MaxPixels)
                            throw new ArgumentException("“" + Path.GetFileName(files[i]) + "” exceeds 40 million pixels. Resize it before importing.");
                        // Camera orientation is part of the image, not a page rotation.
                        if (source.PropertyIdList.Contains(0x112))
                        {
                            byte[] value = source.GetPropertyItem(0x112).Value;
                            int orientation = value.Length > 0 ? value[0] : 1;
                            var rotations = new[] { RotateFlipType.RotateNoneFlipNone, RotateFlipType.RotateNoneFlipNone,
                                RotateFlipType.RotateNoneFlipX, RotateFlipType.Rotate180FlipNone, RotateFlipType.Rotate180FlipX,
                                RotateFlipType.Rotate90FlipX, RotateFlipType.Rotate90FlipNone, RotateFlipType.Rotate270FlipX, RotateFlipType.Rotate270FlipNone };
                            if (orientation >= 1 && orientation <= 8) source.RotateFlip(rotations[orientation]);
                        }
                        using (var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb))
                        {
                            using (var graphics = Graphics.FromImage(bitmap))
                            {
                                graphics.Clear(Color.White);
                                graphics.DrawImage(source, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                            }
                            cancel.ThrowIfCancellationRequested();
                            AddPage(doc, i, bitmap, options);
                        }
                    }
                    if (progress != null) progress((i + 1) * 100 / files.Count, "Imported " + (i + 1) + " of " + files.Count + " images");
                }
                cancel.ThrowIfCancellationRequested();
                using (var bytes = new MemoryStream())
                {
                    lock (Pdfium.Lock) Pdfium.SaveDocument(doc, bytes);
                    cancel.ThrowIfCancellationRequested();
                    return bytes.ToArray();
                }
            }
            finally { lock (Pdfium.Lock) Pdfium.FPDF_CloseDocument(doc); }
        }

        private static Image OpenImage(string path)
        {
            try { return Image.FromFile(path); }
            catch (OutOfMemoryException ex)
            {
                // GDI+ uses this exception for malformed images as well as allocation failures.
                throw new InvalidDataException("Could not read “" + Path.GetFileName(path) + "”. Check that it is a valid PNG, JPEG, or BMP image and that enough memory is available.", ex);
            }
            catch (ArgumentException ex)
            {
                throw new InvalidDataException("Could not read “" + Path.GetFileName(path) + "”. Choose a valid PNG, JPEG, or BMP image.", ex);
            }
        }

        private static void AddPage(IntPtr doc, int index, Bitmap bitmap, ImageImportOptions options)
        {
            SizeF size = PageSize(bitmap.Width, bitmap.Height, options);
            double scale = Math.Min((size.Width - 2 * options.Margin) / bitmap.Width, (size.Height - 2 * options.Margin) / bitmap.Height);
            double w = bitmap.Width * scale, h = bitmap.Height * scale;
            lock (Pdfium.Lock)
            {
                IntPtr page = Pdfium.FPDFPage_New(doc, index, size.Width, size.Height);
                if (page == IntPtr.Zero) throw new IOException("Could not create an image page.");
                IntPtr obj = IntPtr.Zero;
                try
                {
                    obj = Pdfium.FPDFPageObj_NewImageObj(doc);
                    if (obj == IntPtr.Zero) throw new IOException("Could not create the PDF image.");
                    var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                    try
                    {
                        IntPtr native = Pdfium.FPDFBitmap_CreateEx(bitmap.Width, bitmap.Height, Pdfium.FPDFBitmap_BGRA, data.Scan0, data.Stride);
                        if (native == IntPtr.Zero) throw new IOException("Could not load the image pixels.");
                        try
                        {
                            if (Pdfium.FPDFImageObj_SetBitmap(IntPtr.Zero, 0, obj, native) == 0) throw new IOException("Could not embed the image.");
                        }
                        finally { Pdfium.FPDFBitmap_Destroy(native); }
                    }
                    finally { bitmap.UnlockBits(data); }
                    Pdfium.FPDFPageObj_Transform(obj, w, 0, 0, h, (size.Width - w) / 2, (size.Height - h) / 2);
                    Pdfium.FPDFPage_InsertObject(page, obj);
                    obj = IntPtr.Zero; // the page now owns it
                    if (Pdfium.FPDFPage_GenerateContent(page) == 0) throw new IOException("Could not write the image page.");
                }
                finally
                {
                    if (obj != IntPtr.Zero) Pdfium.FPDFPageObj_Destroy(obj);
                    Pdfium.FPDF_ClosePage(page);
                }
            }
        }
    }
}
