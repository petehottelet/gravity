using System;
using System.Drawing;
using System.Windows.Forms;

namespace Gravity
{
    internal sealed class IconColorDialog : Form
    {
        internal readonly TextBox HexBox;
        internal readonly Button ApplyButton, ResetButton;
        private readonly PictureBox appPreview, pdfPreview;
        private readonly Label validation;
        public string SelectedHex { get { return IconColor.Normalize(HexBox.Text); } }

        public IconColorDialog(string initial)
        {
            Text = "Icon Color";
            Font = new Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = MaximizeBox = ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            ClientSize = new Size(420, 332);
            BackColor = Theme.Chrome;

            var explanation = new Label { Text = "Choose a color for the Gravity logo and PDF file icon.",
                Left = 20, Top = 18, Width = 380, Height = 36 };
            var label = new Label { Text = "&Hex color", Left = 20, Top = 62, Width = 110, Height = 20, TabIndex = 0 };
            HexBox = new TextBox { Left = 20, Top = 86, Width = 160, Text = IconColor.Normalize(initial),
                AccessibleName = "Hex color", TabIndex = 1 };
            ResetButton = new Button { Text = "&Reset to blue", Left = 196, Top = 83, Width = 132, Height = 28, TabIndex = 2 };
            validation = new Label { Left = 20, Top = 118, Width = 380, Height = 32 };
            appPreview = new PictureBox { Left = 112, Top = 160, Width = 64, Height = 64,
                SizeMode = PictureBoxSizeMode.Zoom, AccessibleName = "Application icon preview" };
            pdfPreview = new PictureBox { Left = 244, Top = 160, Width = 64, Height = 64,
                SizeMode = PictureBoxSizeMode.Zoom, AccessibleName = "PDF file icon preview" };
            var appLabel = new Label { Text = "Application", Left = 92, Top = 232, Width = 104, Height = 20, TextAlign = ContentAlignment.MiddleCenter };
            var pdfLabel = new Label { Text = "PDF files", Left = 224, Top = 232, Width = 104, Height = 20, TextAlign = ContentAlignment.MiddleCenter };
            ApplyButton = new Button { Text = "&Apply", Left = 224, Top = 284, Width = 84, Height = 28,
                DialogResult = DialogResult.OK, TabIndex = 3 };
            var cancel = new Button { Text = "Cancel", Left = 316, Top = 284, Width = 84, Height = 28,
                DialogResult = DialogResult.Cancel, TabIndex = 4 };
            Controls.AddRange(new Control[] { explanation, label, HexBox, ResetButton, validation,
                appPreview, pdfPreview, appLabel, pdfLabel, ApplyButton, cancel });
            AcceptButton = ApplyButton; CancelButton = cancel;
            ResetButton.Click += delegate { HexBox.Text = IconColor.DefaultHex; HexBox.Focus(); HexBox.SelectAll(); };
            HexBox.TextChanged += delegate { UpdatePreview(); };
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                Color color;
                if (DialogResult == DialogResult.OK && !IconColor.TryParse(HexBox.Text, out color))
                { e.Cancel = true; HexBox.Focus(); }
            };
            Shown += delegate { HexBox.Focus(); HexBox.SelectAll(); };
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            Color color;
            bool valid = IconColor.TryParse(HexBox.Text, out color);
            ApplyButton.Enabled = valid;
            validation.Text = valid ? "Use 3 or 6 hex digits, with or without #." : "Enter a hex color, such as #0B57D0 or #F80.";
            validation.ForeColor = valid ? Theme.SubtleText : Color.FromArgb(160, 32, 32);
            if (!valid) return;
            SetPreview(appPreview, false, SelectedHex);
            SetPreview(pdfPreview, true, SelectedHex);
        }

        private static void SetPreview(PictureBox box, bool document, string hex)
        {
            using (var allSizes = IconColor.CreateIcon(document, hex))
            using (var icon = new Icon(allSizes, new Size(64, 64)))
            {
                var old = box.Image;
                box.Image = icon.ToBitmap();
                if (old != null) old.Dispose();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (appPreview.Image != null) { appPreview.Image.Dispose(); appPreview.Image = null; }
                if (pdfPreview.Image != null) { pdfPreview.Image.Dispose(); pdfPreview.Image = null; }
            }
            base.Dispose(disposing);
        }
    }
}
