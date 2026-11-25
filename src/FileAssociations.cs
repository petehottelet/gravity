using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace Gravity
{
    internal static class FileAssociations
    {
        internal static readonly string[] Extensions = { ".pdf", ".png", ".jpg", ".jpeg", ".bmp" };
        internal static string ProgId(string extension) { return extension == ".pdf" ? "Gravity.PDF" : "Gravity.Image"; }

        public static void Register(string executable, string hex)
        {
            Register(Registry.CurrentUser, executable, Path.Combine(AppSettings.FolderPath, "icons"), hex);
            ShellIcons.Refresh(hex);
        }

        internal static void Register(RegistryKey root, string executable, string iconsFolder, string hex)
        {
            string appIcon, pdfIcon;
            ShellIcons.WriteIcons(hex, iconsFolder, out appIcon, out pdfIcon);
            string command = "\"" + Path.GetFullPath(executable) + "\" \"%1\"";
            Set(root, @"Software\Classes\Gravity.PDF", "", "PDF Document");
            Set(root, @"Software\Classes\Gravity.Image", "", "Image for Gravity");
            foreach (string extension in Extensions)
            {
                string progId = ProgId(extension);
                Set(root, @"Software\Classes\" + progId + @"\shell\open\command", "", command);
                Set(root, @"Software\Classes\" + progId + @"\DefaultIcon", "", Quote(extension == ".pdf" ? pdfIcon : appIcon));
                Set(root, @"Software\Classes\" + extension + @"\OpenWithProgids", progId, "");
                Set(root, @"Software\Classes\Applications\Gravity.exe\SupportedTypes", extension, "");
                Set(root, @"Software\Gravity\Capabilities\FileAssociations", extension, progId);
            }
            Set(root, @"Software\Classes\Applications\Gravity.exe", "FriendlyAppName", "Gravity");
            Set(root, @"Software\Classes\Applications\Gravity.exe\shell\open\command", "", command);
            Set(root, @"Software\Classes\Applications\Gravity.exe\DefaultIcon", "", Quote(appIcon));
            Set(root, @"Software\Gravity\Capabilities", "ApplicationName", "Gravity");
            Set(root, @"Software\Gravity\Capabilities", "ApplicationDescription", "Read PDFs and import images as PDF pages");
            Set(root, @"Software\Gravity\Capabilities", "ApplicationIcon", Quote(appIcon));
            Set(root, @"Software\RegisteredApplications", "Gravity", @"Software\Gravity\Capabilities");
        }

        private static string Quote(string path) { return "\"" + path + "\",0"; }

        private static void Set(RegistryKey root, string path, string name, string value)
        {
            using (var key = root.CreateSubKey(path)) key.SetValue(name, value, RegistryValueKind.String);
        }

        internal static string DefaultProgId(RegistryKey root, string extension)
        {
            using (var choice = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\" + extension + @"\UserChoice"))
                if (choice != null) return choice.GetValue("ProgId") as string;
            using (var key = root.OpenSubKey(@"Software\Classes\" + extension))
                return key == null ? null : key.GetValue("") as string;
        }

        public static bool IsDefault(string extension)
        {
            string current = DefaultProgId(Registry.CurrentUser, extension);
            if (current == null)
                using (var key = Registry.ClassesRoot.OpenSubKey(extension))
                    if (key != null) current = key.GetValue("") as string;
            return string.Equals(current, ProgId(extension), StringComparison.OrdinalIgnoreCase);
        }

        public static void OpenSettings(string extension)
        {
            if (Array.IndexOf(Extensions, extension) < 0) throw new ArgumentException("Unsupported file type.");
            Process.Start(new ProcessStartInfo("ms-settings:defaultapps?registeredAppUser=Gravity") { UseShellExecute = true });
        }
    }
}
