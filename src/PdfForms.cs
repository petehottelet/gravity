using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Gravity
{
    internal sealed class PdfFormField
    {
        public int Index, Type, Flags;
        public string Name, Label, Value, ExportValue;
        public bool Checked, ReadOnly;
        public Pdfium.PdfRect Bounds;
        public readonly List<string> Options = new List<string>();
        public readonly List<int> Selected = new List<int>();
        public bool Supported { get { return Type >= 2 && Type <= 6; } }
        public bool Multiline { get { return (Flags & (1 << 12)) != 0; } }
        public bool Password { get { return (Flags & (1 << 13)) != 0; } }
        public bool EditableChoice { get { return Type == 4 && (Flags & (1 << 18)) != 0; } }
        public bool Multiple { get { return Type == 5 && (Flags & (1 << 21)) != 0; } }
    }

    internal sealed class PdfFormEdit
    {
        public int Index;
        public string Text;
        public bool Checked;
        public int[] Selected = new int[0];
    }

    /// <summary>Short-lived, script-free form environment. Caller holds Pdfium.Lock.</summary>
    internal sealed class PdfForms : IDisposable
    {
        private readonly IntPtr document;
        private IntPtr info, handle;
        private readonly Pdfium.FormGetPage getPage;
        private readonly Dictionary<int, IntPtr> pages = new Dictionary<int, IntPtr>();

        public PdfForms(IntPtr document)
        {
            this.document = document;
            getPage = delegate(IntPtr self, IntPtr doc, int index)
            {
                IntPtr page;
                return pages.TryGetValue(index, out page) ? page : IntPtr.Zero;
            };
            var callbacks = new Pdfium.FormFillInfo { Version = 1, GetPage = Marshal.GetFunctionPointerForDelegate(getPage) };
            info = Marshal.AllocHGlobal(Marshal.SizeOf(callbacks));
            Marshal.StructureToPtr(callbacks, info, false);
            handle = Pdfium.FPDFDOC_InitFormFillEnvironment(document, info);
            if (handle == IntPtr.Zero) { Dispose(); throw new IOException("Could not open the PDF form."); }
        }

        public IntPtr Page(int index)
        {
            IntPtr page;
            if (pages.TryGetValue(index, out page)) return page;
            page = Pdfium.FPDF_LoadPage(document, index);
            if (page == IntPtr.Zero) throw new IOException("Could not read the form page.");
            pages.Add(index, page);
            Pdfium.FORM_OnAfterLoadPage(page, handle);
            return page;
        }

        public List<PdfFormField> Read(int pageIndex)
        {
            var result = new List<PdfFormField>();
            IntPtr page = Page(pageIndex);
            int count = Pdfium.FPDFPage_GetAnnotCount(page);
            for (int i = 0; i < count; i++)
            {
                IntPtr annot = Pdfium.FPDFPage_GetAnnot(page, i);
                if (annot == IntPtr.Zero) continue;
                try
                {
                    if (Pdfium.FPDFAnnot_GetSubtype(annot) != 20) continue;
                    // Hidden, invisible, and non-viewable widgets aren't interactive.
                    int annotationFlags = Pdfium.FPDFAnnot_GetFlags(annot);
                    if ((annotationFlags & 35) != 0) continue;
                    var field = new PdfFormField { Index = i };
                    field.Type = Pdfium.FPDFAnnot_GetFormFieldType(handle, annot);
                    field.Flags = Pdfium.FPDFAnnot_GetFormFieldFlags(handle, annot);
                    field.ReadOnly = (field.Flags & 1) != 0 || (annotationFlags & 64) != 0
                        || (Pdfium.FPDF_GetDocPermissions(document) & 0x100) == 0;
                    field.Name = Pdfium.GetUtf16((b, n) => Pdfium.FPDFAnnot_GetFormFieldName(handle, annot, b, n));
                    field.Label = Pdfium.GetUtf16((b, n) => Pdfium.FPDFAnnot_GetFormFieldAlternateName(handle, annot, b, n));
                    if (string.IsNullOrEmpty(field.Label)) field.Label = field.Name;
                    if (string.IsNullOrEmpty(field.Label)) field.Label = "Field " + (i + 1);
                    field.Value = Pdfium.GetUtf16((b, n) => Pdfium.FPDFAnnot_GetFormFieldValue(handle, annot, b, n));
                    field.ExportValue = Pdfium.GetUtf16((b, n) => Pdfium.FPDFAnnot_GetFormFieldExportValue(handle, annot, b, n));
                    field.Checked = Pdfium.FPDFAnnot_IsChecked(handle, annot) != 0;
                    Pdfium.FPDFAnnot_GetRect(annot, out field.Bounds);
                    int options = Pdfium.FPDFAnnot_GetOptionCount(handle, annot);
                    for (int j = 0; j < options; j++)
                    {
                        int option = j;
                        field.Options.Add(Pdfium.GetUtf16((b, n) => Pdfium.FPDFAnnot_GetOptionLabel(handle, annot, option, b, n)));
                        if (Pdfium.FPDFAnnot_IsOptionSelected(handle, annot, j) != 0) field.Selected.Add(j);
                    }
                    result.Add(field);
                }
                finally { Pdfium.FPDFPage_CloseAnnot(annot); }
            }
            return result;
        }

        public void Apply(int pageIndex, PdfFormEdit edit)
        {
            var field = Read(pageIndex).FirstOrDefault(f => f.Index == edit.Index);
            if (field == null || !field.Supported || field.ReadOnly)
                throw new InvalidOperationException("This form field cannot be edited.");
            IntPtr page = Page(pageIndex);
            IntPtr annot = Pdfium.FPDFPage_GetAnnot(page, edit.Index);
            try
            {
                if (Pdfium.FORM_SetFocusedAnnot(handle, annot) == 0)
                    throw new IOException("Could not focus the form field.");
                if (field.Type == 6 || field.EditableChoice && edit.Selected.Length == 0)
                {
                    Pdfium.FORM_SelectAllText(handle, page);
                    Pdfium.FORM_ReplaceSelection(handle, page, edit.Text ?? "");
                }
                else if (field.Type == 2 || field.Type == 3)
                {
                    if (field.Checked != edit.Checked && (field.Type != 3 || edit.Checked))
                        if (Pdfium.FORM_OnChar(handle, page, 32, 0) == 0)
                            throw new IOException("Could not change the form button.");
                }
                else
                {
                    if (edit.Selected.Any(i => i < 0 || i >= field.Options.Count) || (!field.Multiple && edit.Selected.Length > 1))
                        throw new ArgumentException("Choose a valid form option.");
                    foreach (int i in field.Selected)
                        if (!edit.Selected.Contains(i)) Pdfium.FORM_SetIndexSelected(handle, page, i, 0);
                    foreach (int i in edit.Selected)
                        if (Pdfium.FORM_SetIndexSelected(handle, page, i, 1) == 0)
                            throw new IOException("Could not select the form option.");
                }
                Pdfium.FORM_ForceToKillFocus(handle);
                if (field.Type == 6 || field.EditableChoice && edit.Selected.Length == 0)
                {
                    string actual = Pdfium.GetUtf16((b, n) => Pdfium.FPDFAnnot_GetFormFieldValue(handle, annot, b, n));
                    if (NormalizeLines(actual) != NormalizeLines(edit.Text ?? ""))
                        throw new InvalidOperationException("The field did not accept all of the text. Check its maximum length and try again.");
                }
            }
            finally { Pdfium.FPDFPage_CloseAnnot(annot); }
        }

        private static string NormalizeLines(string value) { return value.Replace("\r\n", "\n").Replace('\r', '\n'); }

        public void Draw(int pageIndex, IntPtr bitmap, int x, int y, int width, int height, int rotation, int flags)
        {
            Pdfium.FPDF_FFLDraw(handle, bitmap, Page(pageIndex), x, y, width, height, rotation, flags);
        }

        public void Dispose()
        {
            if (handle != IntPtr.Zero)
            {
                Pdfium.FORM_ForceToKillFocus(handle);
                foreach (IntPtr page in pages.Values) Pdfium.FORM_OnBeforeClosePage(page, handle);
                Pdfium.FPDFDOC_ExitFormFillEnvironment(handle);
                handle = IntPtr.Zero;
            }
            foreach (IntPtr page in pages.Values) Pdfium.FPDF_ClosePage(page);
            pages.Clear();
            if (info != IntPtr.Zero) { Marshal.FreeHGlobal(info); info = IntPtr.Zero; }
            GC.KeepAlive(getPage);
        }
    }
}
