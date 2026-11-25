using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Gravity.Tests
{
    internal static class IconColorTests
    {
        private static void Check(bool ok, string message) { SelfTest.Check(ok, message); }

        internal static void Run(string folder)
        {
            Color color;
            foreach (var value in new[] { "#e63946", "e63946", "  #E63946  ", "#000000", "#FFFFFF", "#abc", "F80" })
                Check(IconColor.TryParse(value, out color), "accepts RGB hex " + value.Trim());
            foreach (var value in new[] { null, "", "#", "red", "#1234", "#12345", "#1234567", "#GG0000", "#FF000080", "12 3456", "0x1234" })
                Check(!IconColor.TryParse(value, out color), "rejects invalid hex input");
            Check(IconColor.Normalize("abc") == "#AABBCC", "expands shorthand and normalizes case");
            Check(IconColor.Normalize("bad value") == IconColor.DefaultHex, "invalid stored colors fall back to blue");

            string settingsPath = Path.Combine(folder, "icon-settings.txt");
            var settings = new AppSettings { IconColorHex = "#e63946", SidebarWidth = 312, ShowPages = true };
            Check(settings.TrySave(settingsPath), "saves icon color immediately");
            var restored = AppSettings.Load(settingsPath);
            Check(restored.IconColorHex == "#E63946" && restored.SidebarWidth == 312 && restored.ShowPages,
                "restarts restore color and unrelated preferences");
            File.WriteAllText(settingsPath, "iconcolor=broken\nsidebar=299\n");
            restored = AppSettings.Load(settingsPath);
            Check(restored.IconColorHex == IconColor.DefaultHex && restored.SidebarWidth == 299,
                "damaged color does not prevent loading other settings");
            File.WriteAllText(settingsPath, "sidebar=301\n");
            Check(AppSettings.Load(settingsPath).IconColorHex == IconColor.DefaultHex, "older settings retain the original blue");
            using (var locked = new FileStream(settingsPath, FileMode.Open, FileAccess.Read, FileShare.None))
                Check(!settings.TrySave(settingsPath), "failed settings writes are reported");

            foreach (bool document in new[] { false, true })
            {
                var original = IconColor.IconBytes(document, IconColor.DefaultHex);
                using (var resource = typeof(Brand).Assembly.GetManifestResourceStream(document ? "GravityPdf.ico" : "Gravity.ico"))
                using (var memory = new MemoryStream())
                {
                    resource.CopyTo(memory);
                    Check(original.SequenceEqual(memory.ToArray()), "reset restores byte-identical original artwork");
                }
                foreach (var hex in new[] { "#E63946", "#000000", "#FFFFFF", "#00FF00" })
                {
                    var tinted = IconColor.IconBytes(document, hex);
                    Check(BitConverter.ToUInt16(tinted, 4) == 9, "retains all nine icon sizes");
                    for (int frame = 0; frame < 9; frame++)
                    {
                        using (var before = ReadFrame(original, frame))
                        using (var after = ReadFrame(tinted, frame))
                        {
                            bool alpha = true, neutral = true, changed = false, exact = false;
                            Color target = IconColor.Parse(hex);
                            for (int y = 0; y < before.Height; y++)
                                for (int x = 0; x < before.Width; x++)
                                {
                                    Color a = before.GetPixel(x, y), b = after.GetPixel(x, y);
                                    alpha &= a.A == b.A;
                                    if (a.A == 255 && (a.ToArgb() == Color.White.ToArgb() || a.ToArgb() == Color.FromArgb(230, 233, 237).ToArgb() ||
                                        a.ToArgb() == Color.FromArgb(201, 207, 214).ToArgb())) neutral &= a.ToArgb() == b.ToArgb();
                                    changed |= a.ToArgb() != b.ToArgb();
                                    exact |= b.A == 255 && b.ToArgb() == target.ToArgb();
                                }
                            Check(alpha && neutral && changed && exact, (document ? "PDF" : "app") + " icon at " + before.Width +
                                "px uses " + hex + " and preserves transparency, lettering and paper");
                        }
                    }
                }
            }
            using (var image = IconColor.Tint(Brand.Lockup, IconColor.Parse("#E63946")))
                Check(image.GetPixel(200, 40) == ((Bitmap)Brand.Lockup).GetPixel(200, 40), "wordmark lettering remains unchanged");

            // This disposable registry subtree never reaches the user's real PDF registration.
            string registryPath = @"Software\GravityIconTests-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var root = Registry.CurrentUser.CreateSubKey(registryPath))
                {
                    string icons = Path.Combine(folder, "icon-cache");
                    ShellIcons.Update("#E63946", icons, root, Path.Combine(folder, "missing.lnk"));
                    Check(!Directory.Exists(icons) && root.GetSubKeyNames().Length == 0, "portable colors do not create PDF associations");
                    string target = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Gravity.exe");
                    string command = "\"" + target + "\" \"%1\"";
                    string shortcut = Path.Combine(folder, "Gravity.lnk");
                    WithShortcut(shortcut, link => {
                        link.GetType().InvokeMember("TargetPath", BindingFlags.SetProperty, null, link, new object[] { target });
                        link.GetType().InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
                    });
                    using (var key = root.CreateSubKey(@"Software\Classes\Gravity.PDF\shell\open\command")) key.SetValue("", command);
                    using (var key = root.CreateSubKey(@"Software\Gravity\Capabilities")) key.SetValue("ApplicationName", "Gravity");
                    using (var key = root.CreateSubKey(@"Software\Classes\Applications\Gravity.exe")) key.SetValue("FriendlyAppName", "Gravity");
                    using (var key = root.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.pdf\UserChoice")) key.SetValue("ProgId", "Other.PDF");
                    ShellIcons.Update("#E63946", icons, root, shortcut);
                    string app = Path.Combine(icons, "Gravity-E63946.ico"), pdf = Path.Combine(icons, "GravityPdf-E63946.ico");
                    Check(File.ReadAllBytes(app).SequenceEqual(IconColor.IconBytes(false, "#E63946")) &&
                        File.ReadAllBytes(pdf).SequenceEqual(IconColor.IconBytes(true, "#E63946")), "writes matching app and PDF icons");
                    using (var key = root.OpenSubKey(@"Software\Classes\Gravity.PDF\DefaultIcon"))
                        Check((string)key.GetValue("") == "\"" + pdf + "\",0", "PDF registration points at the selected color");
                    using (var key = root.OpenSubKey(@"Software\Classes\Gravity.PDF\shell\open\command"))
                        Check((string)key.GetValue("") == command, "color updates preserve the PDF open command");
                    using (var key = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.pdf\UserChoice"))
                        Check((string)key.GetValue("ProgId") == "Other.PDF", "color updates preserve the chosen default PDF app");
                    using (var key = root.OpenSubKey(@"Software\Gravity\Capabilities"))
                        Check((string)key.GetValue("ApplicationIcon") == "\"" + app + "\",0", "Default apps uses the matching app icon");
                    using (var key = root.OpenSubKey(@"Software\Classes\Applications\Gravity.exe\DefaultIcon"))
                        Check((string)key.GetValue("") == "\"" + app + "\",0", "Open with uses the matching app icon");
                    WithShortcut(shortcut, link => {
                        Check((string)link.GetType().InvokeMember("IconLocation", BindingFlags.GetProperty, null, link, null) == app + ",0" &&
                            (string)link.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, link, null) == target,
                            "Start menu shortcut changes icon and preserves its target");
                    });
                    ShellIcons.Update(IconColor.DefaultHex, icons, root, Path.Combine(folder, "missing.lnk"));
                    Check(File.Exists(Path.Combine(icons, "GravityPdf-0B57D0.ico")), "reset and installer refresh restore the default document icon");
                    string blockedFolder = Path.Combine(folder, "blocked-icons");
                    File.WriteAllText(blockedFolder, "not a directory");
                    bool failed = false;
                    try { ShellIcons.Update("#FFFFFF", blockedFolder, root, shortcut); }
                    catch (IOException) { failed = true; }
                    using (var key = root.OpenSubKey(@"Software\Classes\Gravity.PDF\DefaultIcon"))
                        Check(failed && ((string)key.GetValue("")).Contains("GravityPdf-0B57D0.ico"), "failed icon writes leave the registered icon intact");
                }
            }
            finally { Registry.CurrentUser.DeleteSubKeyTree(registryPath, false); }
        }

        private static void WithShortcut(string path, Action<object> action)
        {
            object shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell", true)), link = null;
            try
            {
                link = shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });
                action(link);
            }
            finally
            {
                if (link != null) Marshal.FinalReleaseComObject(link);
                Marshal.FinalReleaseComObject(shell);
            }
        }

        private static Bitmap ReadFrame(byte[] bytes, int frame)
        {
            int entry = 6 + frame * 16, length = BitConverter.ToInt32(bytes, entry + 8), offset = BitConverter.ToInt32(bytes, entry + 12);
            if (bytes[offset] == 137)
            {
                using (var stream = new MemoryStream(bytes, offset, length))
                using (var image = new Bitmap(stream)) return new Bitmap(image);
            }
            int width = BitConverter.ToInt32(bytes, offset + 4), height = BitConverter.ToInt32(bytes, offset + 8) / 2;
            int pixels = offset + BitConverter.ToInt32(bytes, offset);
            var result = new Bitmap(width, height);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int i = pixels + ((height - 1 - y) * width + x) * 4;
                    result.SetPixel(x, y, Color.FromArgb(bytes[i + 3], bytes[i + 2], bytes[i + 1], bytes[i]));
                }
            return result;
        }

        internal static void RunUI()
        {
            using (var dialog = new IconColorDialog("#E63946"))
            {
                dialog.StartPosition = FormStartPosition.Manual; dialog.Location = new Point(-20000, -20000);
                dialog.Show(); Application.DoEvents();
                Check(dialog.SelectedHex == "#E63946" && dialog.ApplyButton.Enabled, "dialog starts with the saved color");
                dialog.HexBox.Text = "#oops";
                Check(!dialog.ApplyButton.Enabled, "invalid hex disables Apply");
                Capture(dialog, "icon-color-invalid");
                dialog.HexBox.Text = "f80";
                Check(dialog.ApplyButton.Enabled && dialog.SelectedHex == "#FF8800", "valid shorthand updates the preview and enables Apply");
                Capture(dialog, "icon-color-orange");
                dialog.ResetButton.PerformClick();
                Check(dialog.SelectedHex == IconColor.DefaultHex, "reset selects original blue before applying");
                dialog.DialogResult = DialogResult.Cancel; dialog.Close();
                Check(dialog.DialogResult == DialogResult.Cancel, "cancel dismisses the preview without applying it");
            }
            using (var form = new MainForm(new string[0], true))
            {
                form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-20000, -20000);
                form.Show(); Application.DoEvents(); form.ApplyIconColor("#E63946");
                var header = form.Controls.OfType<DocumentHeader>().First();
                Check(header.BrandAction.LogoColor.ToArgb() == IconColor.Parse("#E63946").ToArgb(), "applying color updates the header logo immediately");
                Capture(form, "icon-color-home");
                form.Close();
            }
        }

        private static void Capture(Form form, string name)
        {
            string folder = Environment.GetEnvironmentVariable("GRAVITY_TEST_SCREENSHOTS");
            if (string.IsNullOrEmpty(folder)) return;
            Directory.CreateDirectory(folder);
            using (var image = new Bitmap(form.Width, form.Height))
            { form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size)); image.Save(Path.Combine(folder, name + ".png"), ImageFormat.Png); }
        }
    }
}
