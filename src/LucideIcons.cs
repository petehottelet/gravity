using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;

namespace Gravity
{
    /// <summary>Renders the bundled Lucide SVG geometry at the current display scale.</summary>
    internal static class LucideIcons
    {
        private static readonly Dictionary<string, GraphicsPath> paths = new Dictionary<string, GraphicsPath>();
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly Regex tokens = new Regex(@"[a-zA-Z]|[-+]?(?:\d*\.\d+|\d+\.?\d*)(?:[eE][-+]?\d+)?", RegexOptions.Compiled);

        public static Bitmap Create(string name, int size, Color color)
        {
            var bitmap = new Bitmap(size, size);
            using (var graphics = Graphics.FromImage(bitmap)) Draw(graphics, name, new RectangleF(0, 0, size, size), color);
            return bitmap;
        }

        public static void Draw(Graphics graphics, string name, RectangleF bounds, Color color)
        {
            GraphicsPath path;
            lock (paths)
            {
                if (!paths.TryGetValue(name, out path))
                {
                    path = Load(name);
                    paths.Add(name, path);
                }
            }
            var state = graphics.Save();
            try
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.TranslateTransform(bounds.X, bounds.Y);
                graphics.ScaleTransform(bounds.Width / 24f, bounds.Height / 24f);
                using (var pen = new Pen(color, 1.75f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                    graphics.DrawPath(pen, path);
            }
            finally { graphics.Restore(state); }
        }

        private static float Number(XmlElement element, string name, float fallback = 0)
        {
            return element.HasAttribute(name) ? float.Parse(element.GetAttribute(name), Inv) : fallback;
        }

        private static GraphicsPath Load(string name)
        {
            var result = new GraphicsPath();
            using (var stream = typeof(LucideIcons).Assembly.GetManifestResourceStream("Lucide." + name + ".svg"))
            {
                if (stream == null) throw new InvalidOperationException("Missing Lucide icon: " + name);
                var xml = new XmlDocument { XmlResolver = null };
                using (var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                    xml.Load(reader);
                foreach (XmlNode child in xml.DocumentElement.ChildNodes)
                {
                    var element = child as XmlElement;
                    if (element == null) continue;
                    result.StartFigure();
                    switch (element.LocalName)
                    {
                        case "path":
                            using (var path = ParsePath(element.GetAttribute("d"))) result.AddPath(path, false);
                            break;
                        case "line": result.AddLine(Number(element, "x1"), Number(element, "y1"), Number(element, "x2"), Number(element, "y2")); break;
                        case "circle":
                            float radius = Number(element, "r");
                            result.AddEllipse(Number(element, "cx") - radius, Number(element, "cy") - radius, 2 * radius, 2 * radius);
                            break;
                        case "rect":
                            var rect = new RectangleF(Number(element, "x"), Number(element, "y"), Number(element, "width"), Number(element, "height"));
                            using (var rounded = Theme.RoundRect(rect, Number(element, "rx"))) result.AddPath(rounded, false);
                            break;
                        case "polyline":
                        case "polygon":
                            var values = tokens.Matches(element.GetAttribute("points"));
                            var points = new PointF[values.Count / 2];
                            for (int i = 0; i < points.Length; i++) points[i] = new PointF(float.Parse(values[2 * i].Value, Inv), float.Parse(values[2 * i + 1].Value, Inv));
                            result.AddLines(points);
                            if (element.LocalName == "polygon") result.CloseFigure();
                            break;
                        default: throw new InvalidDataException("Unsupported Lucide shape: " + element.LocalName);
                    }
                }
            }
            return result;
        }

        private sealed class PathReader
        {
            private readonly List<string> input = new List<string>();
            public int At;
            public PathReader(string data) { foreach (Match token in tokens.Matches(data)) input.Add(token.Value); }
            public bool End { get { return At >= input.Count; } }
            public bool Command { get { return !End && char.IsLetter(input[At][0]); } }
            public char ReadCommand() { return input[At++][0]; }
            public float Read() { if (End || Command) throw new InvalidDataException("Incomplete Lucide path."); return float.Parse(input[At++], Inv); }
            public bool Flag()
            {
                if (End || (input[At][0] != '0' && input[At][0] != '1')) throw new InvalidDataException("Invalid Lucide arc flag.");
                bool value = input[At][0] == '1';
                // SVG permits adjacent single-digit flags and coordinates, such as 0022.
                if (input[At].Length == 1) At++; else input[At] = input[At].Substring(1);
                return value;
            }
            public PointF Point(PointF origin, bool relative) { return new PointF(Read() + (relative ? origin.X : 0), Read() + (relative ? origin.Y : 0)); }
        }

        private static GraphicsPath ParsePath(string data)
        {
            var path = new GraphicsPath();
            var reader = new PathReader(data);
            PointF current = PointF.Empty, start = PointF.Empty, control = PointF.Empty;
            char command = ' ', previous = ' ';
            while (!reader.End)
            {
                if (reader.Command) command = reader.ReadCommand();
                bool relative = char.IsLower(command);
                char op = char.ToUpperInvariant(command);
                PointF end;
                switch (op)
                {
                    case 'M':
                        current = reader.Point(current, relative); start = current; path.StartFigure();
                        command = relative ? 'l' : 'L'; break;
                    case 'L': end = reader.Point(current, relative); path.AddLine(current, end); current = end; break;
                    case 'H': end = new PointF(reader.Read() + (relative ? current.X : 0), current.Y); path.AddLine(current, end); current = end; break;
                    case 'V': end = new PointF(current.X, reader.Read() + (relative ? current.Y : 0)); path.AddLine(current, end); current = end; break;
                    case 'C':
                        var a = reader.Point(current, relative); var b = reader.Point(current, relative); end = reader.Point(current, relative);
                        path.AddBezier(current, a, b, end); control = b; current = end; break;
                    case 'S':
                        var first = previous == 'C' || previous == 'S' ? Reflect(control, current) : current;
                        var second = reader.Point(current, relative); end = reader.Point(current, relative);
                        path.AddBezier(current, first, second, end); control = second; current = end; break;
                    case 'Q':
                    case 'T':
                        var quadratic = op == 'Q' ? reader.Point(current, relative) : previous == 'Q' || previous == 'T' ? Reflect(control, current) : current;
                        end = reader.Point(current, relative);
                        path.AddBezier(current, Blend(current, quadratic, 2f / 3), Blend(end, quadratic, 2f / 3), end);
                        control = quadratic; current = end; break;
                    case 'A':
                        float rx = reader.Read(), ry = reader.Read(), angle = reader.Read();
                        bool large = reader.Flag(), sweep = reader.Flag(); end = reader.Point(current, relative);
                        Arc(path, current, end, rx, ry, angle, large, sweep); current = end; break;
                    case 'Z': path.CloseFigure(); current = start; command = ' '; break;
                    default: throw new InvalidDataException("Unsupported Lucide path command.");
                }
                previous = op;
            }
            return path;
        }

        private static PointF Reflect(PointF point, PointF around) { return new PointF(2 * around.X - point.X, 2 * around.Y - point.Y); }
        private static PointF Blend(PointF from, PointF to, float amount) { return new PointF(from.X + (to.X - from.X) * amount, from.Y + (to.Y - from.Y) * amount); }

        // SVG endpoint-to-center conversion; short line segments retain the specified ellipse at every DPI.
        private static void Arc(GraphicsPath path, PointF from, PointF to, double rx, double ry, double rotation, bool large, bool sweep)
        {
            rx = Math.Abs(rx); ry = Math.Abs(ry);
            if (rx == 0 || ry == 0) { path.AddLine(from, to); return; }
            if (from == to) return;
            double phi = rotation * Math.PI / 180, cos = Math.Cos(phi), sin = Math.Sin(phi);
            double dx = (from.X - to.X) / 2.0, dy = (from.Y - to.Y) / 2.0;
            double x = cos * dx + sin * dy, y = -sin * dx + cos * dy;
            double scale = x * x / (rx * rx) + y * y / (ry * ry);
            if (scale > 1) { scale = Math.Sqrt(scale); rx *= scale; ry *= scale; }
            double denominator = rx * rx * y * y + ry * ry * x * x;
            double factor = (large == sweep ? -1 : 1) * Math.Sqrt(Math.Max(0, (rx * rx * ry * ry - denominator) / denominator));
            double cx = factor * rx * y / ry, cy = -factor * ry * x / rx;
            double centerX = cos * cx - sin * cy + (from.X + to.X) / 2.0;
            double centerY = sin * cx + cos * cy + (from.Y + to.Y) / 2.0;
            double start = Math.Atan2((y - cy) / ry, (x - cx) / rx);
            double finish = Math.Atan2((-y - cy) / ry, (-x - cx) / rx), delta = finish - start;
            if (!sweep && delta > 0) delta -= 2 * Math.PI;
            if (sweep && delta < 0) delta += 2 * Math.PI;
            int steps = Math.Max(1, (int)Math.Ceiling(Math.Abs(delta) / (Math.PI / 32)));
            var points = new PointF[steps + 1]; points[0] = from;
            for (int i = 1; i < steps; i++)
            {
                double t = start + delta * i / steps, ex = rx * Math.Cos(t), ey = ry * Math.Sin(t);
                points[i] = new PointF((float)(cos * ex - sin * ey + centerX), (float)(sin * ex + cos * ey + centerY));
            }
            points[steps] = to; path.AddLines(points);
        }

        public static string FromGlyph(string glyph, bool mirror)
        {
            if (glyph.Length != 1 || glyph[0] < 0xE000) return glyph;
            switch ((int)glyph[0])
            {
                case 0xE8E5: return "folder-open";
                case 0xE74E: return "save";
                case 0xE749: return "printer";
                case 0xE7A7: return "undo-2";
                case 0xE7A6: return "redo-2";
                case 0xE8FD: return "panel-left";
                case 0xECA5: return "grid-2x2";
                case 0xE70E: return "chevron-up";
                case 0xE70D: return "chevron-down";
                case 0xE71F: return "zoom-out";
                case 0xE8A3: return "zoom-in";
                case 0xE7AD: return mirror ? "rotate-ccw" : "rotate-cw";
                case 0xE8C6: return "scissors";
                case 0xE8C8: return "copy";
                case 0xE77F: return "clipboard";
                case 0xE74D: return "trash-2";
                case 0xE710: return "plus";
                case 0xE8A7: return "file-output";
                case 0xE76C: return "chevron-left";
                case 0xE8A5: return "panel-left";
                case 0xE8BB: return "x";
                default: throw new InvalidOperationException("No Lucide mapping for this icon.");
            }
        }
    }
}
