using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Gravity
{
    internal sealed class RecentFile
    {
        public string Path;
        public ViewState State;
    }

    /// <summary>Settings and recent files, stored as a small text file in %APPDATA%\Gravity.</summary>
    internal sealed class AppSettings
    {
        private const int MaxRecent = 20;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public List<RecentFile> Recent = new List<RecentFile>();
        public bool ShowBookmarks = true;
        public int SidebarWidth = 236;
        public bool ShowPages;
        public int PagePanelWidth = 240;
        public Rectangle WindowBounds = Rectangle.Empty;
        public bool Maximized;
        public int ThumbSize = 160;
        public bool MatchCase;
        public ZoomMode DefaultZoom = ZoomMode.FitPage;

        public static string FolderPath
        {
            get { return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Gravity"); }
        }

        private static string FilePath { get { return System.IO.Path.Combine(FolderPath, "settings.txt"); } }

        public static AppSettings Load()
        {
            var s = new AppSettings();
            try
            {
                if (!File.Exists(FilePath)) return s;
                foreach (var raw in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    int eq = raw.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = raw.Substring(0, eq).Trim(), val = raw.Substring(eq + 1);
                    switch (key)
                    {
                        case "bookmarks": s.ShowBookmarks = val == "1"; break;
                        case "sidebar": int.TryParse(val, out s.SidebarWidth); break;
                        case "pages": s.ShowPages = val == "1"; break;
                        case "pagepanel": int.TryParse(val, out s.PagePanelWidth); break;
                        case "thumb": int.TryParse(val, out s.ThumbSize); break;
                        case "matchcase": s.MatchCase = val == "1"; break;
                        case "maximized": s.Maximized = val == "1"; break;
                        case "zoom": ZoomMode z; if (Enum.TryParse(val, out z)) s.DefaultZoom = z; break;
                        case "bounds":
                            var b = val.Split(',');
                            int x, y, w, h;
                            if (b.Length == 4 && int.TryParse(b[0], out x) && int.TryParse(b[1], out y) && int.TryParse(b[2], out w) && int.TryParse(b[3], out h))
                                s.WindowBounds = new Rectangle(x, y, w, h);
                            break;
                        case "recent":
                            var r = ParseRecent(val);
                            if (r != null) s.Recent.Add(r);
                            break;
                    }
                }
            }
            catch (Exception)
            {
                // A damaged settings file should never stop the app from starting.
            }
            return s;
        }

        private static RecentFile ParseRecent(string val)
        {
            var f = val.Split('|');
            if (f.Length != 9) return null;
            var st = new ViewState();
            ZoomMode mode;
            PageLayout layout;
            if (!int.TryParse(f[0], NumberStyles.Integer, Inv, out st.Page)) return null;
            double.TryParse(f[1], NumberStyles.Float, Inv, out st.PageV);
            if (Enum.TryParse(f[2], out mode)) st.Mode = mode;
            double.TryParse(f[3], NumberStyles.Float, Inv, out st.Zoom);
            int.TryParse(f[4], out st.Rotation);
            if (Enum.TryParse(f[5], out layout)) st.Layout = layout;
            st.Continuous = f[6] != "0";
            int.TryParse(f[7], out st.ScrollX);
            if (st.Zoom <= 0) st.Zoom = 1;
            return new RecentFile { Path = f[8], State = st };
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(FolderPath);
                var sb = new StringBuilder();
                sb.AppendLine("bookmarks=" + (ShowBookmarks ? "1" : "0"));
                sb.AppendLine("sidebar=" + SidebarWidth);
                sb.AppendLine("pages=" + (ShowPages ? "1" : "0"));
                sb.AppendLine("pagepanel=" + PagePanelWidth);
                sb.AppendLine("thumb=" + ThumbSize);
                sb.AppendLine("matchcase=" + (MatchCase ? "1" : "0"));
                sb.AppendLine("maximized=" + (Maximized ? "1" : "0"));
                sb.AppendLine("zoom=" + DefaultZoom);
                if (!WindowBounds.IsEmpty)
                    sb.AppendLine(string.Format(Inv, "bounds={0},{1},{2},{3}", WindowBounds.X, WindowBounds.Y, WindowBounds.Width, WindowBounds.Height));
                foreach (var r in Recent.Take(MaxRecent))
                {
                    var st = r.State ?? new ViewState();
                    sb.AppendLine(string.Format(Inv, "recent={0}|{1:0.####}|{2}|{3:0.####}|{4}|{5}|{6}|{7}|{8}",
                        st.Page, st.PageV, st.Mode, st.Zoom, st.Rotation, st.Layout, st.Continuous ? 1 : 0, st.ScrollX, r.Path));
                }
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, sb.ToString(), Encoding.UTF8);
                if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
                else File.Move(tmp, FilePath);
            }
            catch (Exception)
            {
                // Losing settings is acceptable; crashing on exit is not.
            }
        }

        public ViewState StateFor(string path)
        {
            var r = Recent.FirstOrDefault(x => string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase));
            if (r != null && r.State != null)
            {
                var s = r.State;
                return new ViewState { Mode = s.Mode, Zoom = s.Zoom, Rotation = s.Rotation, Layout = s.Layout, Continuous = s.Continuous, Page = s.Page, PageV = s.PageV, ScrollX = s.ScrollX };
            }
            return new ViewState { Mode = DefaultZoom };
        }

        public void Remember(string path, ViewState state)
        {
            if (string.IsNullOrEmpty(path)) return;
            Recent.RemoveAll(x => string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase));
            Recent.Insert(0, new RecentFile { Path = path, State = state });
            if (Recent.Count > MaxRecent) Recent.RemoveRange(MaxRecent, Recent.Count - MaxRecent);
        }

        public void Forget(string path)
        {
            Recent.RemoveAll(x => string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase));
        }
    }
}
