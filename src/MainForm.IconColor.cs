using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Gravity
{
    internal sealed partial class MainForm
    {
        private void ChooseIconColor()
        {
            using (var dialog = new IconColorDialog(settings.IconColorHex))
            {
                dialog.PerformAutoScale();
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                string hex = dialog.SelectedHex;
                if (!Ephemeral)
                {
                    // Persist immediately, without replacing another window's reading preferences.
                    var saved = AppSettings.Load();
                    saved.IconColorHex = hex;
                    if (!saved.TrySave())
                    {
                        MessageBox.Show(this, "The color could not be saved. Check that your settings folder is writable and try again.",
                            "Could not save icon color", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
                foreach (var window in Application.OpenForms.OfType<MainForm>().Where(f => f.Ephemeral == Ephemeral).ToArray())
                    window.ApplyIconColor(hex);
                if (!Ephemeral)
                {
                    try { ShellIcons.Refresh(hex); }
                    catch (Exception)
                    {
                        MessageBox.Show(this, "The app color was saved, but Windows could not update the file or shortcut icons. " +
                            "Run the Gravity installer again to retry. Some pinned shortcuts may need to be unpinned and pinned again.",
                            "Could not update Windows icons", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
        }

        internal void ApplyIconColor(string hex)
        {
            settings.IconColorHex = IconColor.Normalize(hex);
            var previous = Icon;
            Icon = IconColor.CreateIcon(false, settings.IconColorHex);
            if (previous != null) previous.Dispose();
            home.SetIconColor(settings.IconColorHex);
            shellHeader.BrandAction.LogoColor = IconColor.Parse(settings.IconColorHex);
            shellHeader.BrandAction.Invalidate();
        }
    }
}
