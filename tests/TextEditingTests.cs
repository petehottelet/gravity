using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Gravity.Tests
{
    internal static class TextEditingTests
    {
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [StructLayout(LayoutKind.Sequential)]
        private struct Matrix { public float A, B, C, D, E, F; }
        [DllImport("pdfium.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int FPDFPageObj_GetMatrix(IntPtr obj, out Matrix matrix);
        [DllImport("pdfium.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int FPDFPageObj_GetFillColor(IntPtr obj, out uint r, out uint g, out uint b, out uint a);

        internal static void Fixture(string path)
        {
            string content = "BT /F1 24 Tf 0.1 0.3 0.7 rg 1 0 0 1 54 720 Tm (Project update) Tj ET\n" +
                "BT /F1 12 Tf 0 g 1 0 0 1 54 680 Tm (Draft for discussion) Tj ET\n" +
                "BT /F1 12 Tf 0 g 1 0 0 1 54 640 Tm (Meeting on Friday) Tj ET\n" +
                "q 1 0 0 1 54 580 cm /Group Do Q\n";
            string group = "BT /F1 12 Tf 0 g 1 0 0 1 0 0 Tm (Grouped text stays unchanged) Tj ET\n";
            Func<string, string> stream = value => "<< /Length " + value.Length + " >>\nstream\n" + value + "endstream";
            var objects = new[] {
                "<< /Type /Catalog /Pages 2 0 R /Outlines 7 0 R >>",
                "<< /Type /Pages /Kids [3 0 R 6 0 R] /Count 2 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> /XObject << /Group 10 0 R >> >> /Contents 5 0 R >>",
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
                stream(content),
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> /XObject << /Group 10 0 R >> >> /Contents 5 0 R >>",
                "<< /Type /Outlines /First 8 0 R /Last 8 0 R /Count 1 >>",
                "<< /Title (Project update) /Parent 7 0 R /Dest [3 0 R /Fit] >>",
                "<< /Title (Text editing sample) >>",
                "<< /Type /XObject /Subtype /Form /BBox [0 -20 400 30] /Resources << /Font << /F1 4 0 R >> >> /Length " + group.Length + " >>\nstream\n" + group + "endstream"
            };
            var pdf = new StringBuilder("%PDF-1.7\n");
            var offsets = new List<int>();
            for (int i = 0; i < objects.Length; i++) { offsets.Add(pdf.Length); pdf.Append(i + 1).Append(" 0 obj\n").Append(objects[i]).Append("\nendobj\n"); }
            int xref = pdf.Length;
            pdf.Append("xref\n0 ").Append(objects.Length + 1).Append("\n0000000000 65535 f \n");
            foreach (int offset in offsets) pdf.Append(offset.ToString("D10")).Append(" 00000 n \n");
            pdf.Append("trailer\n<< /Root 1 0 R /Info 9 0 R /Size ").Append(objects.Length + 1).Append(" >>\nstartxref\n").Append(xref).Append("\n%%EOF\n");
            File.WriteAllBytes(path, Encoding.ASCII.GetBytes(pdf.ToString()));
        }

        private static PdfTextEdit Change(PdfTextRun run, string text)
        { return new PdfTextEdit { Index = run.Index, Original = run.Text, Text = text }; }

        private static string PageText(PageRef page)
        {
            var textPage = page.Src.GetTextPage(page.Index);
            return Pdfium.GetText(textPage, 0, Pdfium.FPDFText_CountChars(textPage));
        }

        internal static void Run(string directory)
        {
            string input = Path.Combine(directory, "editable-text.pdf"), output = Path.Combine(directory, "edited-text.pdf");
            Fixture(input);
            byte[] originalBytes = File.ReadAllBytes(input);
            lock (Pdfium.Lock)
            {
                var source = PdfSource.Open(input, null);
                var doc = new EditDoc(source);
                var original = PdfTextEditing.Read(doc.Pages[0]);
                SelfTest.Check(original.Count == 3 && original[0].Text == "Project update", "enumerates editable sections without grouped artwork");
                doc.EditText(0, new[] { Change(original[0], original[0].Text) });
                SelfTest.Check(!doc.IsModified && !doc.CanUndo, "unchanged replacement creates no undo entry");
                var obj = Pdfium.FPDFPage_GetObject(source.GetPage(0), original[0].Index);
                Matrix before, after; uint r, g, b, a, rr, gg, bb, aa;
                FPDFPageObj_GetMatrix(obj, out before); FPDFPageObj_GetFillColor(obj, out r, out g, out b, out a);
                doc.EditText(0, new[] { Change(original[0], "Project approved"), Change(original[2], "Meeting on Monday") });
                var runs = PdfTextEditing.Read(doc.Pages[0]);
                SelfTest.Check(doc.IsModified && runs[0].Text == "Project approved" && runs[2].Text == "Meeting on Monday", "multiple text replacements apply together");
                obj = Pdfium.FPDFPage_GetObject(doc.Pages[0].Src.GetPage(0), runs[0].Index);
                FPDFPageObj_GetMatrix(obj, out after); FPDFPageObj_GetFillColor(obj, out rr, out gg, out bb, out aa);
                SelfTest.Check(before.Equals(after) && original[0].FontSize == runs[0].FontSize && r == rr && g == gg && b == bb && a == aa,
                    "text replacement retains font size, transformation and color");
                SelfTest.Check(PageText(doc.Pages[1]).Contains("Project update") && PageText(doc.Pages[0]).Contains("Grouped text stays unchanged") &&
                    PdfTextEditing.Read(new PageRef(source, 0, 0))[0].Text == "Project update", "other pages, grouped text and shared sources remain unchanged");
                SelfTest.Check(File.ReadAllBytes(input).SequenceEqual(originalBytes), "text edits do not write the source file");
                doc.Undo(); SelfTest.Check(!doc.IsModified && !doc.CanUndo, "one undo reverses all replacements");
                doc.Redo(); doc.SaveAs(output);
                SelfTest.Check(!doc.IsModified && PageText(doc.Pages[0]).Contains("Project approved"), "replacement survives save and reopening");
                SelfTest.Check(doc.Base.GetTitle() == "Text editing sample" && doc.Base.GetOutline()[0].Title == "Project update", "text edits preserve metadata and bookmarks on disk");
                using (var bitmap = RenderService.RenderPage(doc.Pages[0], 612, 792, 0))
                {
                    int ink = 0;
                    for (int y = 48; y < 74; y++) for (int x = 54; x < 280; x++) if (bitmap.GetPixel(x, y).R < 180) ink++;
                    SelfTest.Check(ink > 150, "saved replacement renders visible text");
                }
                doc.Undo(); SelfTest.Check(PageText(doc.Pages[0]).Contains("Project update"), "undo after save restores original text");
                doc.Redo();
                var stable = doc.Pages;
                runs = PdfTextEditing.Read(doc.Pages[0]);
                foreach (var changes in new[] {
                    new[] { Change(runs[0], "Valid change"), Change(runs[1], "Unsupported \u4e2d\u6587") },
                    new[] { Change(runs[0], "Line one\nLine two") },
                    new[] { Change(runs[0], "Zero\0tail") },
                    new[] { Change(runs[0], new string('a', PdfTextEditing.MaxTextLength + 1)) },
                    new[] { new PdfTextEdit { Index = runs[0].Index, Original = "Stale text", Text = "Wrong replacement" } },
                    new[] { Change(runs[0], "One"), Change(runs[0], "Two") }
                })
                {
                    bool failed = false;
                    try { doc.EditText(0, changes); } catch (InvalidOperationException) { failed = true; }
                    SelfTest.Check(failed && ReferenceEquals(stable, doc.Pages), "unsupported or invalid replacement leaves the entire document unchanged");
                }
                doc.EditText(0, new[] { Change(runs[0], "") });
                SelfTest.Check(!PageText(doc.Pages[0]).Contains("Project approved") && PdfTextEditing.Read(doc.Pages[0]).Count == 2, "clearing text removes the object and its searchable content");
                doc.SaveAs(output);
                SelfTest.Check(!PageText(doc.Pages[0]).Contains("Project approved"), "removed text stays absent after save");
                doc.Undo();
                SelfTest.Check(PageText(doc.Pages[0]).Contains("Project approved"), "undo restores deleted text");
                doc.SetPages(new List<PageRef> { doc.Pages[1].Rotated(1), doc.Pages[0], doc.Pages[0].Clone(), PageRef.Blank(612, 792) });
                runs = PdfTextEditing.Read(doc.Pages[0]);
                doc.EditText(0, new[] { Change(runs[0], "Rotated page") });
                SelfTest.Check(doc.Pages.Count == 4 && doc.Pages[0].BaseWidth == 792 && PageText(doc.Pages[0]).Contains("Rotated page") &&
                    PageText(doc.Pages[1]).Contains("Project approved"), "editing preserves page reordering, rotations and blank insertion");
                runs = PdfTextEditing.Read(doc.Pages[1]);
                doc.EditText(1, new[] { Change(runs[0], "Only this copy") });
                SelfTest.Check(PageText(doc.Pages[1]).Contains("Only this copy") && PageText(doc.Pages[2]).Contains("Project approved"), "editing a duplicated page affects only the chosen copy");
                runs = PdfTextEditing.Read(doc.Pages[1]);
                doc.EditText(1, new[] { Change(runs[1], "R\u00e9sum\u00e9 approved") });
                SelfTest.Check(PageText(doc.Pages[1]).Contains("R\u00e9sum\u00e9 approved"), "supported accented characters survive replacement and serialization");
                SelfTest.Check(PdfTextEditing.Read(doc.Pages[3]).Count == 0, "blank pages have no editable sections");
                foreach (var item in doc.Sources().Distinct().ToList()) item.Dispose();
                source.Dispose();
            }
        }

        private static IEnumerable<Control> Descendants(Control parent)
        {
            foreach (Control child in parent.Controls) { yield return child; foreach (var nested in Descendants(child)) yield return nested; }
        }

        private static void Capture(Form editor, string name)
        {
            string folder = Environment.GetEnvironmentVariable("GRAVITY_TEST_SCREENSHOTS");
            if (string.IsNullOrEmpty(folder)) return;
            Directory.CreateDirectory(folder);
            using (var image = new Bitmap(editor.Width, editor.Height))
            { editor.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save(Path.Combine(folder, name + ".png")); }
        }

        internal static void RunUI(string directory)
        {
            string input = Path.GetFullPath(Path.Combine(directory, "text-ui.pdf")); Fixture(input);
            using (var form = new MainForm(new[] { input }, true))
            {
                form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-2500, -2500); form.Show(); Application.DoEvents();
                var view = (DocView)typeof(MainForm).GetField("view", Private).GetValue(form);
                Action<Keys> key = keys => typeof(MainForm).GetMethod("HandleKey", Private).Invoke(form, new object[] { keys });
                Exception failure = null;
                bool opened = false;
                using (var timer = new Timer { Interval = 100 })
                {
                    timer.Tick += delegate
                    {
                        var editor = Application.OpenForms.OfType<TextEditor>().FirstOrDefault();
                        if (editor == null) return;
                        timer.Stop(); opened = true;
                        try
                        {
                            var text = Descendants(editor).OfType<TextBox>().Single();
                            var sections = Descendants(editor).OfType<ListBox>().Single();
                            SelfTest.Check(!((Button)editor.AcceptButton).Enabled, "Apply starts disabled until text changes");
                            text.Text = "Project approved"; sections.SelectedIndex = 1; text.Text = "Ready to share"; sections.SelectedIndex = 0;
                            SelfTest.Check(text.Text == "Project approved", "switching sections preserves pending replacements");
                            Capture(editor, "text-editor"); editor.Size = editor.MinimumSize; Application.DoEvents(); Capture(editor, "text-editor-minimum");
                            SelfTest.Check(text.Width > 200 && text.Height > 40 && Descendants(editor).OfType<Button>().All(button => editor.ClientRectangle.Contains(editor.RectangleToClient(button.RectangleToScreen(button.ClientRectangle)))),
                                "editor and action buttons fit the minimum window size");
                            text.Text = "Line one\nLine two"; editor.AcceptButton.PerformClick();
                            SelfTest.Check(editor.Visible && !view.Document.IsModified && Descendants(editor).OfType<Label>().Any(label => label.Text.Contains("without tabs or line breaks")),
                                "invalid edits show an error without closing or changing the document");
                            text.Text = "Project approved"; editor.AcceptButton.PerformClick();
                        }
                        catch (Exception ex) { failure = ex; editor.DialogResult = DialogResult.Cancel; }
                    };
                    timer.Start(); key(Keys.Control | Keys.Shift | Keys.T);
                }
                if (failure != null) throw failure;
                SelfTest.Check(opened && view.Document.IsModified, "text editing shortcut opens the editor and applies replacements");
                lock (Pdfium.Lock) SelfTest.Check(PageText(view.Document.Pages[0]).Contains("Ready to share"), "all UI replacements reach the PDF model");
                var outline = (TreeView)typeof(MainForm).GetField("toc", Private).GetValue(form);
                view.GoToPage(1, false);
                view.Navigate((OutlineItem)outline.Nodes[0].Tag);
                SelfTest.Check(view.CurrentPage == 0, "bookmarks still navigate after a text edit creates a document snapshot");
                key(Keys.Control | Keys.Z); SelfTest.Check(!view.Document.IsModified, "UI text changes share document undo");
                view.GoToPage(1, false); view.Navigate((OutlineItem)outline.Nodes[0].Tag);
                SelfTest.Check(view.CurrentPage == 0, "bookmarks follow the restored source after undo");
                key(Keys.Control | Keys.Y); SelfTest.Check(view.Document.IsModified, "UI text changes share document redo"); key(Keys.Control | Keys.Z);
                using (var timer = new Timer { Interval = 100 })
                {
                    timer.Tick += delegate
                    {
                        var editor = Application.OpenForms.OfType<TextEditor>().FirstOrDefault(); if (editor == null) return;
                        timer.Stop(); Descendants(editor).OfType<TextBox>().Single().Text = "Cancelled edit"; editor.CancelButton.PerformClick();
                    };
                    timer.Start(); key(Keys.Control | Keys.Shift | Keys.T);
                }
                SelfTest.Check(!view.Document.IsModified, "Cancel discards pending text edits");
                var menu = (ToolStripMenuItem)typeof(MainForm).GetField("miEditText", Private).GetValue(form);
                SelfTest.Check(menu.Enabled, "Edit Text is enabled in the reader");
                key(Keys.Control | Keys.E); SelfTest.Check(!menu.Enabled, "Edit Text is disabled in the organizer");
                key(Keys.Control | Keys.W); SelfTest.Check(!menu.Enabled, "Edit Text is disabled without a document");
                form.Close();
            }
            using (var empty = new TextEditor(PageRef.Blank(612, 792), 0, -1, edits => { throw new Exception("Empty editor must not apply"); }))
            {
                empty.Show(); Application.DoEvents(); Capture(empty, "text-editor-empty");
                SelfTest.Check(!((Button)empty.AcceptButton).Enabled && !Descendants(empty).OfType<TextBox>().Single().Enabled &&
                    Descendants(empty).OfType<Label>().Any(label => label.Text.StartsWith("No editable text")), "blank pages explain the empty state and disable editing");
                empty.Close();
            }
        }
    }
}
