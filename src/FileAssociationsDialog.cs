using System;
using System.Drawing;
using System.Windows.Forms;

namespace Gravity
{
    internal sealed class FileAssociationsDialog : GravityDialog
    {
        internal readonly ComboBox FileType;
        internal readonly Button ChooseButton;
        private readonly Label status;
        internal Action<string> ChooseDefault;
        internal Func<string, bool> CheckDefault;

        internal FileAssociationsDialog(string hex, bool ephemeral) : base("File Associations", 460, 300)
        {
            var explanation = new Label { Text = "Choose which files open with Gravity. Windows confirms your default app for each file type.",
                Left = 24, Top = 20, Width = 412, Height = 44 };
            var label = new Label { Text = "&File type", Left = 24, Top = 76, Width = 130, Height = 22, TabIndex = 0 };
            FileType = new ComboBox { Left = 24, Top = 102, Width = 412, DropDownStyle = ComboBoxStyle.DropDownList,
                AccessibleName = "File type", TabIndex = 1 };
            FileType.Items.AddRange(new object[] { "PDF document (.pdf)", "PNG image (.png)", "JPEG image (.jpg)", "JPEG image (.jpeg)", "Bitmap image (.bmp)" });
            status = new Label { Left = 24, Top = 150, Width = 412, Height = 64, ForeColor = Theme.SubtleText };
            ChooseButton = new DialogButton { Text = "Choose default app…", Width = 188, TabIndex = 2 };
            var close = new DialogButton { Text = "Close", DialogResult = DialogResult.Cancel, TabIndex = 3 };
            var footer = new DialogFooter(); footer.Controls.Add(close); footer.Controls.Add(ChooseButton);
            Content.Controls.AddRange(new Control[] { explanation, label, FileType, status, footer });
            AcceptButton = ChooseButton; CancelButton = close;
            CheckDefault = extension => !ephemeral && FileAssociations.IsDefault(extension);
            ChooseDefault = extension => {
                if (ephemeral) return;
                FileAssociations.Register(Application.ExecutablePath, hex);
                FileAssociations.OpenSettings(extension);
            };
            FileType.SelectedIndexChanged += delegate { RefreshStatus(); };
            Activated += delegate { RefreshStatus(); };
            ChooseButton.Click += delegate
            {
                try { ChooseDefault(FileAssociations.Extensions[FileType.SelectedIndex]); RefreshStatus(); }
                catch (Exception)
                {
                    MessageBox.Show(this, "Could not open Default Apps. Open Windows Settings > Apps > Default apps and choose Gravity for this file type.",
                        "File associations", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
            FileType.SelectedIndex = 0;
        }

        private void RefreshStatus()
        {
            if (FileType.SelectedIndex < 0) return;
            string extension = FileAssociations.Extensions[FileType.SelectedIndex];
            bool current = CheckDefault(extension);
            status.Text = (current ? "Gravity is the default for " : "In Windows Default Apps, choose Gravity for ") + extension + "." +
                (extension == ".pdf" ? "" : "\nImages become unsaved PDFs; original files stay unchanged.");
        }
    }
}
