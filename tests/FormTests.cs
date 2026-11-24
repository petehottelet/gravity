using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace Gravity.Tests
{
    internal static class FormTests
    {
        private static IEnumerable<Control> Descendants(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                yield return child;
                foreach (var nested in Descendants(child)) yield return nested;
            }
        }

        internal static void RunUI(string directory)
        {
            string input = Path.GetFullPath(Path.Combine(directory, "form-ui.pdf"));
            WriteFixture(input);
            using (var form = new MainForm(new[] { input }, true))
            {
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-2500, -2500);
                form.Show();
                Application.DoEvents();
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var view = (DocView)typeof(MainForm).GetField("view", flags).GetValue(form);
                Exception failure = null;
                using (var timer = new Timer { Interval = 100 })
                {
                    timer.Tick += delegate
                    {
                        var editor = Application.OpenForms.OfType<FormEditor>().FirstOrDefault();
                        if (editor == null) return;
                        timer.Stop();
                        try
                        {
                            SelfTest.Check(Descendants(editor).Any(c => c.AccessibleName == "Locked (read only)" && !c.Enabled), "form editor disables read-only fields");
                            var text = Descendants(editor).OfType<TextBox>().First(c => c.AccessibleName == "Full name");
                            text.Text = "Edited from UI";
                            using (var image = new Bitmap(editor.Width, editor.Height))
                            {
                                editor.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
                                image.Save(Path.Combine(directory, "form-editor.png"));
                            }
                            editor.Size = editor.MinimumSize;
                            editor.PerformLayout();
                            using (var image = new Bitmap(editor.Width, editor.Height))
                            {
                                editor.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
                                image.Save(Path.Combine(directory, "form-editor-minimum.png"));
                            }
                            editor.AcceptButton.PerformClick();
                        }
                        catch (Exception ex) { failure = ex; editor.DialogResult = DialogResult.Cancel; }
                    };
                    timer.Start();
                    typeof(MainForm).GetMethod("HandleKey", flags).Invoke(form, new object[] { Keys.Control | Keys.Shift | Keys.F });
                }
                if (failure != null) throw failure;
                SelfTest.Check(view.Document.IsModified, "form shortcut opens editor and applies values");
                lock (Pdfium.Lock) SelfTest.Check(view.Document.Pages[0].Src.GetFormFields(0)[0].Value == "Edited from UI", "form editor updates the document model");
                view.Document.Undo();
                SelfTest.Check(!view.Document.IsModified, "form UI edit participates in undo");

                bool clicked = false;
                using (var timer = new Timer { Interval = 100 })
                {
                    timer.Tick += delegate
                    {
                        var editor = Application.OpenForms.OfType<FormEditor>().FirstOrDefault();
                        if (editor == null) return;
                        timer.Stop(); clicked = true;
                        editor.CancelButton.PerformClick();
                    };
                    timer.Start();
                    var client = (Point)typeof(UiTest).GetMethod("PdfToClient", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { view, 0, 100.0, 710.0 });
                    typeof(DocView).GetMethod("OnMouseDown", flags).Invoke(view, new object[] { new MouseEventArgs(MouseButtons.Left, 1, client.X, client.Y, 0) });
                }
                SelfTest.Check(clicked && !view.Document.IsModified, "clicking a widget opens the editor and cancel leaves it unchanged");
                form.Close();
            }
        }

        internal static void WriteFixture(string path, int maxLength = 0)
        {
            Func<string, string> stream = text => "<< /Type /XObject /Subtype /Form /BBox [0 0 20 20] /Length " + text.Length + " >>\nstream\n" + text + "\nendstream";
            var objects = new[] {
                "<< /Type /Catalog /Pages 2 0 R /AcroForm 4 0 R >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /Helv 5 0 R >> >> /Annots [6 0 R 7 0 R 8 0 R 9 0 R 10 0 R 12 0 R 13 0 R 16 0 R] >>",
                "<< /Fields [6 0 R 7 0 R 8 0 R 9 0 R 10 0 R 11 0 R 16 0 R] /DA (/Helv 12 Tf 0 g) /DR << /Font << /Helv 5 0 R >> >> >>",
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
                "<< /Type /Annot /Subtype /Widget /FT /Tx /T (Name) /TU (Full name) /Rect [50 700 300 725] /P 3 0 R /F 4 /V (Initial) /DA (/Helv 12 Tf 0 g) " + (maxLength > 0 ? "/MaxLen " + maxLength : "") + " >>",
                "<< /Type /Annot /Subtype /Widget /FT /Tx /T (Notes) /Rect [50 610 300 685] /P 3 0 R /F 4 /Ff 4096 /DA (/Helv 12 Tf 0 g) >>",
                "<< /Type /Annot /Subtype /Widget /FT /Btn /T (Agree) /Rect [50 570 70 590] /P 3 0 R /F 4 /V /Off /AS /Off /AP << /N << /Off 14 0 R /Yes 15 0 R >> >> >>",
                "<< /Type /Annot /Subtype /Widget /FT /Ch /T (Country) /Rect [50 520 300 545] /P 3 0 R /F 4 /Ff 131072 /Opt [[(CA) (Canada)] [(US) (United States)]] /V (CA) /I [0] /DA (/Helv 12 Tf 0 g) >>",
                "<< /Type /Annot /Subtype /Widget /FT /Ch /T (Colors) /Rect [50 420 300 500] /P 3 0 R /F 4 /Ff 2097152 /Opt [(Red) (Green) (Blue)] /V [(Red)] /I [0] /DA (/Helv 12 Tf 0 g) >>",
                "<< /FT /Btn /T (Shipping) /Ff 32768 /Kids [12 0 R 13 0 R] /V /Standard >>",
                "<< /Type /Annot /Subtype /Widget /Parent 11 0 R /Rect [50 370 70 390] /P 3 0 R /F 4 /AS /Standard /AP << /N << /Off 14 0 R /Standard 15 0 R >> >> >>",
                "<< /Type /Annot /Subtype /Widget /Parent 11 0 R /Rect [100 370 120 390] /P 3 0 R /F 4 /AS /Off /AP << /N << /Off 14 0 R /Express 15 0 R >> >> >>",
                stream("0 G 0 0 20 20 re S"),
                stream("0 G 0 0 20 20 re S 2 2 m 18 18 l 2 18 m 18 2 l S"),
                "<< /Type /Annot /Subtype /Widget /FT /Tx /T (Locked) /Rect [50 320 300 345] /P 3 0 R /F 4 /Ff 1 /V (Keep) /DA (/Helv 12 Tf 0 g) >>"
            };
            var pdf = new StringBuilder("%PDF-1.7\n");
            var offsets = new List<int>();
            for (int i = 0; i < objects.Length; i++) { offsets.Add(pdf.Length); pdf.Append(i + 1).Append(" 0 obj\n").Append(objects[i]).Append("\nendobj\n"); }
            int xref = pdf.Length;
            pdf.Append("xref\n0 ").Append(objects.Length + 1).Append("\n0000000000 65535 f \n");
            foreach (int offset in offsets) pdf.Append(offset.ToString("D10")).Append(" 00000 n \n");
            pdf.Append("trailer\n<< /Root 1 0 R /Size ").Append(objects.Length + 1).Append(" >>\nstartxref\n").Append(xref).Append("\n%%EOF\n");
            File.WriteAllBytes(path, Encoding.ASCII.GetBytes(pdf.ToString()));
        }

        internal static void Run(string directory)
        {
            string input = Path.Combine(directory, "form.pdf"), output = Path.Combine(directory, "filled-form.pdf");
            WriteFixture(input);
            lock (Pdfium.Lock)
            {
                var source = PdfSource.Open(input, null);
                var document = new EditDoc(source);
                var fields = source.GetFormFields(0);
                SelfTest.Check(fields.Count == 8 && fields[0].Label == "Full name", "enumerate form fields and accessible names");
                SelfTest.Check(fields[3].Options.SequenceEqual(new[] { "Canada", "United States" }), "choice labels differ from export values");
                document.FillForm(0, new[] {
                    new PdfFormEdit { Index = 0, Text = "Renée Example" },
                    new PdfFormEdit { Index = 1, Text = "First line\r\nSecond line" },
                    new PdfFormEdit { Index = 2, Checked = true },
                    new PdfFormEdit { Index = 3, Selected = new[] { 1 } },
                    new PdfFormEdit { Index = 4, Selected = new[] { 1, 2 } },
                    new PdfFormEdit { Index = 6, Checked = true }
                });
                SelfTest.Check(document.IsModified && source.GetFormFields(0)[0].Value == "Initial", "form edits are isolated from the original source");
                document.Undo();
                SelfTest.Check(!document.IsModified && document.Pages[0].Src == source, "undo restores the untouched form");
                document.Redo();
                document.SaveAs(output);
                SelfTest.Check(!document.IsModified, "saving form clears modified state");
                var saved = PdfSource.Open(output, null);
                fields = saved.GetFormFields(0);
                SelfTest.Check(fields[0].Value == "Renée Example", "Unicode text survives save and reopen");
                SelfTest.Check(fields[1].Value.Contains("First line") && fields[1].Value.Contains("Second line"), "multiline text survives save");
                SelfTest.Check(fields[2].Checked, "checkbox survives save");
                SelfTest.Check(fields[3].Value == "US" && fields[3].Selected.SequenceEqual(new[] { 1 }), "choice export value survives save");
                SelfTest.Check(fields[4].Selected.SequenceEqual(new[] { 1, 2 }), "multiple selected choices survive save");
                SelfTest.Check(!fields[5].Checked && fields[6].Checked, "radio group selects exactly one value");
                bool rejected = false;
                try { document.FillForm(0, new[] { new PdfFormEdit { Index = 7, Text = "Changed" } }); }
                catch (InvalidOperationException) { rejected = true; }
                SelfTest.Check(rejected && !document.IsModified, "read-only fields reject edits without changing the document");
                using (var tile = RenderService.RenderTile(new TileKey(saved, 0, 0, 612, 792, 0, 0)))
                {
                    int ink = 0;
                    for (int y = 67; y < 92; y++) for (int x = 50; x < 300; x++) if (tile.GetPixel(x, y).R < 180) ink++;
                    SelfTest.Check(ink > 20, "saved text field has a visible appearance");
                }
                document.SetPages(new List<PageRef> { PageRef.Blank(612, 792), document.Pages[0].Rotated(1) });
                document.SaveAs(output);
                var rotated = PdfSource.Open(output, null);
                SelfTest.Check(rotated.GetFormFields(1)[0].Value == "Renée Example", "form values survive page insertion and rotation");
                document.Undo(); document.Undo();
                document.SaveAs(output);
                var undone = PdfSource.Open(output, null);
                SelfTest.Check(undone.GetFormFields(0)[0].Value == "Initial", "undo after saving restores form values on disk");
                string limitedPath = Path.Combine(directory, "limited-form.pdf");
                WriteFixture(limitedPath, 4);
                var limited = new EditDoc(PdfSource.Open(limitedPath, null));
                rejected = false;
                try { limited.FillForm(0, new[] { new PdfFormEdit { Index = 0, Text = "Too long" } }); }
                catch (InvalidOperationException) { rejected = true; }
                SelfTest.Check(rejected && !limited.IsModified, "text exceeding a field limit is rejected without silently truncating");
                limited.Base.Dispose();
                source.Dispose(); saved.Dispose(); rotated.Dispose(); undone.Dispose();
            }
        }
    }
}
