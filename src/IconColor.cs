using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace Gravity
{
    /// <summary>Recolors the original artwork, retaining every icon size and its transparency.</summary>
    internal static class IconColor
    {
        public const string DefaultHex = "#0B57D0";

        public static bool TryParse(string text, out Color color)
        {
            color = Color.Empty;
            if (text == null) return false;
            text = text.Trim();
            if (text.StartsWith("#", StringComparison.Ordinal)) text = text.Substring(1);
            if (text.Length == 3) text = new string(new[] { text[0], text[0], text[1], text[1], text[2], text[2] });
            int rgb;
            if (text.Length != 6 || !int.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out rgb)) return false;
            color = Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
            return true;
        }

        public static string Normalize(string text)
        {
            Color color;
            return TryParse(text, out color) ? ToHex(color) : DefaultHex;
        }

        public static string ToHex(Color color)
        {
            return string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B);
        }

        public static Color Parse(string text)
        {
            Color color;
            TryParse(Normalize(text), out color);
            return color;
        }

        private static byte Clamp(double value) { return (byte)Math.Max(0, Math.Min(255, Math.Round(value))); }

        private static void TintPixel(byte[] pixels, int i, Color color)
        {
            if (pixels[i] - pixels[i + 2] <= 16 || pixels[i] - pixels[i + 1] <= 8) return;
            // Blue coverage also captures antialiased edges mixed with white or neutral paper.
            // Neutral lettering, the page and fold, and alpha are left intact.
            double coverage = Math.Min(1, Math.Max(0, Math.Min((pixels[i] - pixels[i + 2]) / 197.0,
                (pixels[i] - pixels[i + 1]) / 121.0)));
            pixels[i] = Clamp(pixels[i] + coverage * (color.B - 208));
            pixels[i + 1] = Clamp(pixels[i + 1] + coverage * (color.G - 87));
            pixels[i + 2] = Clamp(pixels[i + 2] + coverage * (color.R - 11));
        }

        public static Bitmap Tint(Image source, Color color)
        {
            var result = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(result)) graphics.DrawImageUnscaled(source, 0, 0);
            var data = result.LockBits(new Rectangle(Point.Empty, result.Size), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                var row = new byte[result.Width * 4];
                for (int y = 0; y < result.Height; y++)
                {
                    var address = IntPtr.Add(data.Scan0, y * data.Stride);
                    Marshal.Copy(address, row, 0, row.Length);
                    for (int x = 0; x < row.Length; x += 4) TintPixel(row, x, color);
                    Marshal.Copy(row, 0, address, row.Length);
                }
            }
            finally { result.UnlockBits(data); }
            return result;
        }

        public static byte[] IconBytes(bool document, string hex)
        {
            using (var source = typeof(Brand).Assembly.GetManifestResourceStream(document ? "GravityPdf.ico" : "Gravity.ico"))
            using (var input = new MemoryStream())
            {
                if (source == null) throw new InvalidOperationException("The icon artwork is missing. Rebuild Gravity.");
                source.CopyTo(input);
                var bytes = input.ToArray();
                if (Normalize(hex) == DefaultHex) return bytes;
                var color = Parse(hex);
                int count = BitConverter.ToUInt16(bytes, 4);
                var frames = new byte[count][];
                for (int frame = 0; frame < count; frame++)
                {
                    int entry = 6 + frame * 16;
                    int length = BitConverter.ToInt32(bytes, entry + 8), offset = BitConverter.ToInt32(bytes, entry + 12);
                    var blob = new byte[length];
                    Buffer.BlockCopy(bytes, offset, blob, 0, length);
                    if (blob[0] == 137) // Large frames are PNG; smaller frames use classic 32-bit DIBs.
                    {
                        using (var stream = new MemoryStream(blob))
                        using (var image = new Bitmap(stream))
                        using (var tinted = Tint(image, color))
                        using (var output = new MemoryStream())
                        {
                            tinted.Save(output, ImageFormat.Png);
                            blob = output.ToArray();
                        }
                    }
                    else
                    {
                        int header = BitConverter.ToInt32(blob, 0), width = BitConverter.ToInt32(blob, 4);
                        int height = Math.Abs(BitConverter.ToInt32(blob, 8)) / 2;
                        if (BitConverter.ToInt16(blob, 14) != 32) throw new InvalidDataException("Expected 32-bit icon artwork.");
                        for (int i = header; i < header + width * height * 4; i += 4) TintPixel(blob, i, color);
                    }
                    frames[frame] = blob;
                }
                using (var output = new MemoryStream())
                using (var writer = new BinaryWriter(output))
                {
                    writer.Write(bytes, 0, 6);
                    int offset = 6 + count * 16;
                    for (int frame = 0; frame < count; frame++)
                    {
                        writer.Write(bytes, 6 + frame * 16, 8);
                        writer.Write(frames[frame].Length); writer.Write(offset);
                        offset += frames[frame].Length;
                    }
                    foreach (var frame in frames) writer.Write(frame);
                    return output.ToArray();
                }
            }
        }

        public static Icon CreateIcon(bool document, string hex)
        {
            using (var stream = new MemoryStream(IconBytes(document, hex)))
            using (var icon = new Icon(stream)) return (Icon)icon.Clone();
        }
    }
}
