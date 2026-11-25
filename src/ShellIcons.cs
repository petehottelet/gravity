using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Gravity
{
    internal static class ShellIcons
    {
        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);

        public static void Refresh(string hex)
        {
            string folder = Path.Combine(AppSettings.FolderPath, "icons");
            string shortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Gravity.lnk");
            Update(hex, folder, Registry.CurrentUser, shortcut);
            SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
        }

        // The root and paths are injectable so offline tests never touch real associations or shortcuts.
        internal static void Update(string hex, string folder, RegistryKey root, string shortcut)
        {
            using (var handler = root.OpenSubKey(@"Software\Classes\Gravity.PDF\shell\open\command"))
            {
                if (handler == null) return; // Portable use must not register or take over PDF files.
                string command = handler.GetValue("") as string;
                if (string.IsNullOrEmpty(command)) return;
                string appIcon, pdfIcon;
                WriteIcons(hex, folder, out appIcon, out pdfIcon);
                using (var key = root.CreateSubKey(@"Software\Classes\Gravity.PDF\DefaultIcon")) key.SetValue("", Quote(pdfIcon));
                using (var key = root.OpenSubKey(@"Software\Gravity\Capabilities", true))
                    if (key != null) key.SetValue("ApplicationIcon", Quote(appIcon));
                using (var key = root.OpenSubKey(@"Software\Classes\Applications\Gravity.exe", true))
                    if (key != null)
                        using (var iconKey = key.CreateSubKey("DefaultIcon")) iconKey.SetValue("", Quote(appIcon));
                UpdateShortcut(shortcut, appIcon, command);
            }
        }

        internal static void WriteIcons(string hex, string folder, out string appIcon, out string pdfIcon)
        {
            Directory.CreateDirectory(folder);
            string suffix = IconColor.Normalize(hex).Substring(1);
            appIcon = Path.Combine(folder, "Gravity-" + suffix + ".ico");
            pdfIcon = Path.Combine(folder, "GravityPdf-" + suffix + ".ico");
            // A distinct path for each color invalidates Explorer's icon cache without restarting it.
            WriteIcon(appIcon, false, hex);
            WriteIcon(pdfIcon, true, hex);
        }

        private static void WriteIcon(string path, bool document, string hex)
        {
            var bytes = IconColor.IconBytes(document, hex);
            AtomicFile.Write(path, stream => stream.Write(bytes, 0, bytes.Length));
        }

        private static string Quote(string path) { return "\"" + path + "\",0"; }

        private static void UpdateShortcut(string path, string icon, string command)
        {
            if (!File.Exists(path)) return;
            object shell = null, link = null;
            try
            {
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell", true));
                link = shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });
                string target = link.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, link, null) as string;
                if (string.IsNullOrEmpty(target) || !command.StartsWith("\"" + target + "\"", StringComparison.OrdinalIgnoreCase)) return;
                link.GetType().InvokeMember("IconLocation", BindingFlags.SetProperty, null, link, new object[] { icon + ",0" });
                link.GetType().InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
            }
            finally
            {
                if (link != null && Marshal.IsComObject(link)) Marshal.FinalReleaseComObject(link);
                if (shell != null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell);
            }
        }
    }
}
