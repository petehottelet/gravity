using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Gravity.Tests
{
    internal static class AppearanceTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Check(bool value, string message) { SelfTest.Check(value, message); }

        internal static void Run(string folder)
        {
            try
            {
                bool readable = true, visible = true;
                foreach (int r in new[] { 0, 85, 170, 255 })
                    foreach (int g in new[] { 0, 85, 170, 255 })
                        foreach (int b in new[] { 0, 85, 170, 255 })
                        {
                            Theme.SetAccent(IconColor.ToHex(Color.FromArgb(r, g, b)));
                            foreach (var surface in new[] { Color.White, Theme.Chrome, Theme.ViewBack, Theme.Pressed, Theme.Hover, Theme.SidebarSelection, Theme.ThumbnailSelection })
                                readable &= Theme.Contrast(Theme.Accent, surface) >= 4.5;
                            readable &= Theme.Contrast(Color.White, Theme.AccentHot) >= 4.5;
                            visible &= Theme.Hover != Color.White && Theme.Hover != Theme.Pressed && Theme.SidebarSelection != Theme.AccentSoft;
                        }
                Check(readable && visible, "64 accent colors including white, black and yellow retain readable text and distinct state washes");
                Theme.SetAccent("#E63946");
                Check(Theme.Accent.R > Theme.Accent.B && Theme.Hover.R > Theme.Hover.B && Theme.Selection.R > Theme.Selection.B,
                    "buttons, hovers and text selections follow the chosen hue");
                var colors = new FlatColors();
                var renderer = new RoundedRenderer { NavigationSelection = true };
                Theme.SetAccent("#00AA44");
                Check(colors.MenuItemSelected == Theme.MenuHover && renderer.SelectionColor == Theme.SidebarSelection,
                    "existing menu and sidebar renderers follow a new color without reconstruction");
            }
            finally { Theme.SetAccent(IconColor.DefaultHex); }
            Check(Theme.Accent == Color.FromArgb(0x17, 0x4b, 0x8b) && Theme.SidebarSelection == Color.FromArgb(0xe2, 0xea, 0xf5),
                "reset restores original blue accent and navigation colors");

            string registryPath = @"Software\GravityAssociationTests-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var root = Registry.CurrentUser.CreateSubKey(registryPath))
                {
                    const string choicePath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.pdf\UserChoice";
                    using (var key = root.CreateSubKey(choicePath)) key.SetValue("ProgId", "Other.PDF");
                    string executable = Path.Combine(folder, "Application folder", "Gravity.exe");
                    string icons = Path.Combine(folder, "association-icons");
                    FileAssociations.Register(root, executable, icons, "#FF8800");
                    Check(FileAssociations.DefaultProgId(root, ".pdf") == "Other.PDF", "registration preserves Windows default-app consent");
                    foreach (string extension in FileAssociations.Extensions)
                    {
                        string progId = FileAssociations.ProgId(extension);
                        using (var key = root.OpenSubKey(@"Software\Gravity\Capabilities\FileAssociations"))
                            Check((string)key.GetValue(extension) == progId, extension + " is offered in Windows Default Apps");
                        using (var key = root.OpenSubKey(@"Software\Classes\" + progId + @"\shell\open\command"))
                            Check((string)key.GetValue("") == "\"" + executable + "\" \"%1\"", extension + " safely quotes application and document paths");
                        using (var key = root.OpenSubKey(@"Software\Classes\" + extension + @"\OpenWithProgids"))
                            Check(key.GetValue(progId) != null, extension + " is offered by Open with");
                    }
                    Check(FileAssociations.Extensions.Skip(1).All(ImageImport.Supports), "only supported image formats are registered");
                    using (var key = root.OpenSubKey(@"Software\Classes\Gravity.Image\DefaultIcon"))
                        Check(((string)key.GetValue("")).Contains("Gravity-FF8800.ico"), "image associations use an app icon rather than a PDF label");
                    ShellIcons.Update("#2266AA", icons, root, Path.Combine(folder, "absent.lnk"));
                    using (var key = root.OpenSubKey(@"Software\Classes\Gravity.Image\DefaultIcon"))
                        Check(((string)key.GetValue("")).Contains("Gravity-2266AA.ico"), "changing colors refreshes image association icons too");
                    string pdfIcon = Path.Combine(icons, "GravityPdf-2266AA.ico");
                    using (var locked = new FileStream(pdfIcon, FileMode.Open, FileAccess.Read, FileShare.Read))
                        ShellIcons.Update("#2266AA", icons, root, Path.Combine(folder, "absent.lnk"));
                    Check(File.Exists(pdfIcon), "reapplying unchanged icons works while Explorer holds a read handle");
                }
            }
            finally { Registry.CurrentUser.DeleteSubKeyTree(registryPath, false); }
        }

        internal static void RunUI(string folder)
        {
            try
            {
                using (var dialog = new IconColorDialog("#E63946"))
                {
                    ShowOffscreen(dialog);
                    dialog.ShowColorPicker = picker => {
                        Check(picker.FullOpen && picker.Color.ToArgb() == Color.FromArgb(230, 57, 70).ToArgb(), "native picker opens expanded at the current hex color");
                        picker.Color = Color.FromArgb(21, 132, 85); return DialogResult.OK;
                    };
                    dialog.PickerButton.PerformClick();
                    Check(dialog.HexBox.Text == "#158455" && dialog.ApplyButton.Enabled && dialog.PdfIconButton.Enabled,
                        "picking a color updates hex, previews, and both apply actions");
                    dialog.ShowColorPicker = picker => { picker.Color = Color.Blue; return DialogResult.Cancel; };
                    dialog.PickerButton.PerformClick();
                    Check(dialog.HexBox.Text == "#158455", "cancelling the native picker preserves the current color");
                    dialog.HexBox.Text = "broken";
                    Check(!dialog.PdfIconButton.Enabled && dialog.PickerButton.Enabled, "invalid hex blocks applying the PDF icon but allows choosing a replacement");
                    dialog.ShowColorPicker = picker => {
                        Check(picker.Color.ToArgb() == Color.FromArgb(21, 132, 85).ToArgb(), "picker recovers from invalid input using the last valid preview");
                        picker.Color = Color.FromArgb(21, 132, 85); return DialogResult.OK;
                    };
                    dialog.PickerButton.PerformClick();
                    Capture(dialog, "color-picker-controls");
                    dialog.Scale(new SizeF(1.5f, 1.5f));
                    Check(!dialog.HexBox.Bounds.IntersectsWith(dialog.PickerButton.Bounds) && !dialog.PickerButton.Bounds.IntersectsWith(dialog.ResetButton.Bounds) &&
                        dialog.ClientRectangle.Contains(dialog.ApplyButton.Bounds), "color controls remain separated and visible at 150 percent scale");
                    Capture(dialog, "color-picker-controls-150");
                    dialog.PdfIconButton.PerformClick();
                    Check(dialog.DialogResult == DialogResult.Yes, "Apply PDF icon requests registration with the selected color");
                }
                using (var dialog = new FileAssociationsDialog(IconColor.DefaultHex, true))
                {
                    ShowOffscreen(dialog);
                    string requested = null;
                    dialog.ChooseDefault = extension => requested = extension;
                    dialog.FileType.SelectedIndex = 3; dialog.ChooseButton.PerformClick();
                    Check(requested == ".jpeg", "association picker sends the chosen file type to Windows default-app handling");
                    Check(dialog.Content.ClientRectangle.Contains(dialog.FileType.Parent.Bounds), "association field stays inside the dialog");
                    Capture(dialog, "file-associations");
                    dialog.Close();
                }
                using (var form = new MainForm(new string[0], true))
                {
                    ShowOffscreen(form);
                    var file = (ToolStripMenuItem)form.MainMenuStrip.Items[0];
                    var view = (ToolStripMenuItem)form.MainMenuStrip.Items[2];
                    Check(file.DropDownItems.Cast<ToolStripItem>().Any(item => item.Text.Replace("&", "") == "Icon Color…") &&
                        !view.DropDownItems.Cast<ToolStripItem>().Any(item => item.Text.Replace("&", "") == "Icon Color…") &&
                        file.DropDownItems.Cast<ToolStripItem>().Any(item => item.Text.Replace("&", "") == "File Associations…"),
                        "File menu exposes color and association settings and View has no duplicate");
                    var home = (HomeView)typeof(MainForm).GetField("home", Private).GetValue(form);
                    using (var blue = Render(home))
                    {
                        form.ApplyIconColor("#E63946");
                        using (var red = Render(home))
                            Check(CountPixels(blue, Color.FromArgb(0x17, 0x4b, 0x8b)) > 100 && CountPixels(red, Theme.Accent) > 100 &&
                                CountPixels(red, Color.FromArgb(0x17, 0x4b, 0x8b)) == 0, "open-PDF button repaints with the selected accent immediately");
                    }
                    Capture(form, "red-accent-home");
                    var recent = new RecentFile { Path = Path.Combine(folder, "A.pdf") };
                    home.SetRecent(new System.Collections.Generic.List<RecentFile> { recent });
                    using (var prime = Render(home)) { }
                    var hits = (System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<Rectangle, object>>)
                        typeof(HomeView).GetField("hits", Private).GetValue(home);
                    var row = hits.Single(hit => ReferenceEquals(hit.Value, recent)).Key;
                    typeof(HomeView).GetMethod("OnMouseMove", Private).Invoke(home,
                        new object[] { new MouseEventArgs(MouseButtons.None, 0, row.X + 10, row.Y + 10, 0) });
                    using (var hovered = Render(home))
                    using (var icon = hovered.Clone(new Rectangle(row.X + 14, row.Y + (row.Height - 20) / 2, 20, 20), hovered.PixelFormat))
                        Check(CountPixels(hovered, Theme.RecentHover) > 1000 && CountPixels(icon, Theme.Accent) > 10,
                            "recent-file mouseover and document icon use the chosen color");
                    var sample = Path.Combine(folder, "association.png");
                    using (var image = new Bitmap(80, 40)) { using (var g = Graphics.FromImage(image)) g.Clear(Color.Green); image.Save(sample); }
                    var original = File.ReadAllBytes(sample);
                    form.OpenFiles(new[] { sample }, -1);
                    var tab = (DocTab)typeof(MainForm).GetField("active", Private).GetValue(form);
                    Check(tab.Doc.Pages.Count == 1 && tab.Doc.FilePath == null && tab.Doc.SaveState == DocumentSaveState.Unsaved &&
                        Math.Abs(tab.Doc.Pages[0].BaseWidth - 60) < .1 && File.ReadAllBytes(sample).SequenceEqual(original),
                        "associated images open as unsaved image-size PDFs without modifying the source");
                    typeof(MainForm).GetMethod("SetApplicationMenus", Private).Invoke(form, new object[] { true, false });
                    form.MainMenuStrip.Items[4].Select();
                    Capture(form, "red-accent-menus");
                    using (var menuImage = Render(form.MainMenuStrip))
                        Check(CountPixels(menuImage, Theme.Hover) > 100, "menu mouseover uses the chosen accent wash");
                    tab.Doc.SaveAs(Path.Combine(folder, "association.pdf"));
                    form.Close();
                }
            }
            finally { Theme.SetAccent(IconColor.DefaultHex); }
        }

        private static int CountPixels(Bitmap image, Color color)
        {
            int count = 0;
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++) if (image.GetPixel(x, y).ToArgb() == color.ToArgb()) count++;
            return count;
        }
        private static Bitmap Render(Control control)
        {
            var image = new Bitmap(control.Width, control.Height);
            control.DrawToBitmap(image, new Rectangle(Point.Empty, control.Size)); return image;
        }
        private static void ShowOffscreen(Form form)
        {
            form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-20000, -20000);
            form.Show(); Application.DoEvents();
        }
        private static void Capture(Form form, string name)
        {
            string folder = Environment.GetEnvironmentVariable("GRAVITY_TEST_SCREENSHOTS");
            if (string.IsNullOrEmpty(folder)) return;
            Directory.CreateDirectory(folder);
            using (var image = Render(form)) image.Save(Path.Combine(folder, name + ".png"));
        }
    }
}
