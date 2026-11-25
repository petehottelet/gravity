using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Gravity
{
    /// <summary>
    /// P/Invoke bindings for the subset of the PDFium API that Gravity uses.
    /// PDFium is not thread-safe: every call must be made while holding <see cref="Lock"/>.
    /// </summary>
    internal static class Pdfium
    {
        private const string Dll = "pdfium.dll";
        private const CallingConvention CC = CallingConvention.StdCall;

        public static readonly object Lock = new object();
        private static bool initialized;

        public const int FPDF_ANNOT = 0x01;
        public const int FPDF_PRINTING = 0x800;
        public const int FPDFBitmap_BGRA = 4;
        public const uint FPDF_NO_INCREMENTAL = 2;
        public const uint FPDF_MATCHCASE = 1;
        public const uint FPDF_MATCHWHOLEWORD = 2;
        public const uint PDFACTION_GOTO = 1;
        public const uint PDFACTION_URI = 3;
        public const uint FPDF_ERR_PASSWORD = 4;

        public static void EnsureInitialized()
        {
            lock (Lock)
            {
                if (initialized) return;
                FPDF_InitLibrary();
                initialized = true;
            }
        }

        public static string ErrorText(uint err)
        {
            switch (err)
            {
                case 2: return "The file could not be opened.";
                case 3: return "The file is not a PDF or is damaged.";
                case 4: return "The password is missing or incorrect.";
                case 5: return "The document uses an unsupported security scheme.";
                case 6: return "A page could not be read.";
                default: return "The PDF could not be loaded (error " + err + ").";
            }
        }

        // ---- fpdfview.h ----------------------------------------------------
        [DllImport(Dll, CallingConvention = CC)] public static extern void FPDF_InitLibrary();
        [DllImport(Dll, CallingConvention = CC)] public static extern uint FPDF_GetLastError();
        [DllImport(Dll, CallingConvention = CC)] public static extern IntPtr FPDF_LoadMemDocument64(IntPtr data, UIntPtr size, byte[] password);
        [DllImport(Dll, CallingConvention = CC)] public static extern void FPDF_CloseDocument(IntPtr doc);
        [DllImport(Dll, CallingConvention = CC)] public static extern int FPDF_GetPageCount(IntPtr doc);
        [DllImport(Dll, CallingConvention = CC)] public static extern int FPDF_GetPageSizeByIndex(IntPtr doc, int index, out double width, out double height);
        [DllImport(Dll, CallingConvention = CC)] public static extern IntPtr FPDF_LoadPage(IntPtr doc, int index);
        [DllImport(Dll, CallingConvention = CC)] public static extern void FPDF_ClosePage(IntPtr page);
        [DllImport(Dll, CallingConvention = CC)] public static extern void FPDF_RenderPageBitmap(IntPtr bitmap, IntPtr page, int startX, int startY, int sizeX, int sizeY, int rotate, int flags);
        [DllImport(Dll, CallingConvention = CC)] public static extern int FPDF_PageToDevice(IntPtr page, int startX, int startY, int sizeX, int sizeY, int rotate, double pageX, double pageY, out int deviceX, out int deviceY);
        [DllImport(Dll, CallingConvention = CC)] public static extern int FPDF_DeviceToPage(IntPtr page, int startX, int startY, int sizeX, int sizeY, int rotate, int deviceX, int deviceY, out double pageX, out double pageY);
        [DllImport(Dll, CallingConvention = CC)] public static extern IntPtr FPDFBitmap_CreateEx(int width, int height, int format, IntPtr firstScan, int stride);
        [DllImport(Dll, CallingConvention = CC)] public static extern int FPDFBitmap_FillRect(IntPtr bitmap, int left, int top, int width, int height, uint color);
        [DllImport(Dll, CallingConvention = CC)] public static extern void FPDFBitmap_Destroy(IntPtr bitmap);
        [DllImport(Dll, CallingConvention = CC)] public static extern uint FPDF_GetMetaText(IntPtr doc, [MarshalAs(UnmanagedType.LPStr)] string tag, byte[] buffer, uint length);

        // ---- fpdf_edit.h / fpdf_ppo.h / fpdf_save.h ------------------------
        [DllImport(Dll, CallingConvention = CC)] public static extern IntPtr FPDF_CreateNewDocument();
        [DllImport(Dll, CallingConvention = CC)] public static extern IntPtr FPDFPage_New(IntPtr doc, int index, double width, double height);
        [DllImport(Dll, CallingConvention = CC)] public static extern void FPDFPage_Delete(IntPtr doc, int index);
        [DllImport(Dll, CallingConvention = CC)] public static extern int FPDFPage_GetRotation(IntPtr page);
        [DllImport(Dll, CallingConvention = CC)] public static extern void FPDFPage_SetRotation(IntPtr page, int rotate);
        [DllImport(Dll, CallingConvention = CC)] public static extern int FPDF_ImportPagesByIndex(IntPtr dest, IntPtr src, int[] indices, uint length, int index);
        [DllImport(Dll, CallingConvention = CC)] public static extern int FPDF_MovePages(IntPtr doc, int[] indices, uint length, int destIndex);
        [DllImport(Dll, CallingConvention = CC)] public static extern int FPDF_SaveAsCopy(IntPtr doc, IntPtr fileWrite, uint flags);
        [DllImport(Dll, CallingConvention = CC)] public static extern IntPtr FPDFText_LoadStandardFont(IntPtr doc, [MarshalAs(UnmanagedType.LPStr)] string font);
        [DllImport(Dll, CallingConvention = CC)] public static extern void FPDFFont_Close(IntPtr font);
        [DllImport(Dll, CallingConvention = CC)] public static extern IntPtr FPDFPageObj_CreateTextObj(IntPtr doc, IntPtr font, float size);
        [DllImport(Dll, CallingConvention = CC)] public static extern IntPtr FPDFPageObj_NewImageObj(IntPtr doc);
        [DllImport(Dll, CallingConvention = CC)] public static extern void FPDFPageObj_Destroy(IntPtr obj);
        [DllImport(Dll, CallingConvention = CC)] public static extern int FPDFImageObj_SetBitmap(IntPtr pages, int count, IntPtr obj, IntPtr bitmap);
        [DllImport(Dll, CallingConvention = CC)] public static extern int FPDFText_SetText(IntPtr textObj, [MarshalAs(UnmanagedType.LPWStr)] string text);
        [DllImport(Dll, CallingConvention = CC)] public static extern void FPDFPageObj_Transform(IntPtr obj, double a, double b, double c, double d, double e, double f);
        [DllImport(Dll, CallingConvention = CC)] public static extern void FPDFPage_InsertObject(IntPtr page, IntPtr obj);
        [DllImport(Dll, CallingConvention = CC)] public static extern int FPDFPage_GenerateContent(IntPtr page);
        [DllImport(Dll, CallingConvention = CC)] public static extern int FPDFPage_CountObjects(IntPtr page);
        [DllImport(Dll, CallingConvention = CC)] public static extern IntPtr FPDFPage_GetObject(IntPtr page, int index);
        [DllImport(Dll, CallingConvention = CC)] public static extern int FPDFPageObj_GetType(IntPtr obj);
        [DllImport(Dll, CallingConvention = CC)] public static extern int FPDFPageObj_GetBounds(IntPtr obj, out float left, out float bottom, out float right, out float top);
        [DllImport(Dll, CallingConvention = CC)] public static extern int FPDFPage_RemoveObject(IntPtr page, IntPtr obj);
        [DllImport(Dll, CallingConvention = CC)] public static extern uint FPDFTextObj_GetText(IntPtr obj, IntPtr textPage, byte[] buffer, uint length);
        [DllImport(Dll, CallingConvention = CC)] public static extern int FPDFTextObj_GetFontSize(IntPtr obj, out float size);
        [DllImport(Dll, CallingConvention = CC)] public static extern IntPtr FPDFText_GetTextObject(IntPtr textPage, int index);

        // ---- fpdf_text.h ---------------------------------------------------
        [DllImport(Dll, CallingConvention = CC)] public static extern IntPtr FPDFText_LoadPage(IntPtr page);
        [DllImport(Dll, CallingConvention = CC)] public static extern void FPDFText_ClosePage(IntPtr textPage);
        [DllImport(Dll, CallingConvention = CC)] public static extern int FPDFText_CountChars(IntPtr textPage);
        [DllImport(Dll, CallingConvention = CC)] public static extern int FPDFText_GetText(IntPtr textPage, int start, int count, byte[] result);
        [DllImport(Dll, CallingConvention = CC)] public static extern int FPDFText_GetCharIndexAtPos(IntPtr textPage, double x, double y, double xTolerance, double yTolerance);
        [DllImport(Dll, CallingConvention = CC)] public static extern int FPDFText_CountRects(IntPtr textPage, int start, int count);
        [DllImport(Dll, CallingConvention = CC)] public static extern int FPDFText_GetRect(IntPtr textPage, int index, out double left, out double top, out double right, out double bottom);
        [DllImport(Dll, CallingConvention = CC)] public static extern IntPtr FPDFText_FindStart(IntPtr textPage, [MarshalAs(UnmanagedType.LPWStr)] string what, uint flags, int startIndex);
        [DllImport(Dll, CallingConvention = CC)] public static extern int FPDFText_FindNext(IntPtr handle);
        [DllImport(Dll, CallingConvention = CC)] public static extern int FPDFText_FindPrev(IntPtr handle);
        [DllImport(Dll, CallingConvention = CC)] public static extern int FPDFText_GetSchResultIndex(IntPtr handle);
        [DllImport(Dll, CallingConvention = CC)] public static extern int FPDFText_GetSchCount(IntPtr handle);
        [DllImport(Dll, CallingConvention = CC)] public static extern void FPDFText_FindClose(IntPtr handle);

        // ---- fpdf_doc.h ----------------------------------------------------
        [DllImport(Dll, CallingConvention = CC)] public static extern IntPtr FPDFBookmark_GetFirstChild(IntPtr doc, IntPtr bookmark);
        [DllImport(Dll, CallingConvention = CC)] public static extern IntPtr FPDFBookmark_GetNextSibling(IntPtr doc, IntPtr bookmark);
        [DllImport(Dll, CallingConvention = CC)] public static extern uint FPDFBookmark_GetTitle(IntPtr bookmark, byte[] buffer, uint length);
        [DllImport(Dll, CallingConvention = CC)] public static extern IntPtr FPDFBookmark_GetDest(IntPtr doc, IntPtr bookmark);
        [DllImport(Dll, CallingConvention = CC)] public static extern IntPtr FPDFBookmark_GetAction(IntPtr bookmark);
        [DllImport(Dll, CallingConvention = CC)] public static extern uint FPDFAction_GetType(IntPtr action);
        [DllImport(Dll, CallingConvention = CC)] public static extern IntPtr FPDFAction_GetDest(IntPtr doc, IntPtr action);
        [DllImport(Dll, CallingConvention = CC)] public static extern uint FPDFAction_GetURIPath(IntPtr doc, IntPtr action, byte[] buffer, uint length);
        [DllImport(Dll, CallingConvention = CC)] public static extern int FPDFDest_GetDestPageIndex(IntPtr doc, IntPtr dest);
        [DllImport(Dll, CallingConvention = CC)] public static extern int FPDFDest_GetLocationInPage(IntPtr dest, out int hasX, out int hasY, out int hasZoom, out float x, out float y, out float zoom);
        [DllImport(Dll, CallingConvention = CC)] public static extern IntPtr FPDFLink_GetLinkAtPoint(IntPtr page, double x, double y);
        [DllImport(Dll, CallingConvention = CC)] public static extern IntPtr FPDFLink_GetDest(IntPtr doc, IntPtr link);
        [DllImport(Dll, CallingConvention = CC)] public static extern IntPtr FPDFLink_GetAction(IntPtr link);

        // ---- helpers -------------------------------------------------------

        /// <summary>Calls a PDFium "fill this UTF-16LE buffer" API twice (size, then data).</summary>
        public static string GetUtf16(Func<byte[], uint, uint> fn)
        {
            uint len = fn(null, 0);
            if (len <= 2 || len > 1 << 20) return "";
            var buf = new byte[len];
            fn(buf, len);
            return Encoding.Unicode.GetString(buf, 0, (int)len - 2);
        }

        public static string GetMetaText(IntPtr doc, string tag)
        {
            return GetUtf16(delegate (byte[] b, uint l) { return FPDF_GetMetaText(doc, tag, b, l); });
        }

        public static string GetText(IntPtr textPage, int start, int count)
        {
            if (count <= 0) return "";
            var buf = new byte[(count + 1) * 2];
            int n = FPDFText_GetText(textPage, start, count, buf);
            if (n <= 1) return "";
            return Encoding.Unicode.GetString(buf, 0, (n - 1) * 2);
        }

        // ---- AcroForm widgets (fpdf_annot.h / fpdf_formfill.h) ---------------
        [StructLayout(LayoutKind.Sequential)]
        internal struct FormFillInfo
        {
            public int Version;
            public IntPtr Release, Invalidate, OutputSelectedRect, SetCursor, SetTimer,
                KillTimer, GetLocalTime, OnChange, GetPage, GetCurrentPage, GetRotation,
                ExecuteNamedAction, SetTextFieldFocus, DoUriAction, DoGoToAction, JsPlatform;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PdfRect { public float Left, Top, Right, Bottom; }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate IntPtr FormGetPage(IntPtr info, IntPtr document, int index);

        [DllImport(Dll, CallingConvention = CC)] public static extern IntPtr FPDFDOC_InitFormFillEnvironment(IntPtr doc, IntPtr info);
        [DllImport(Dll, CallingConvention = CC)] public static extern void FPDFDOC_ExitFormFillEnvironment(IntPtr form);
        [DllImport(Dll, CallingConvention = CC)] public static extern void FORM_OnAfterLoadPage(IntPtr page, IntPtr form);
        [DllImport(Dll, CallingConvention = CC)] public static extern void FORM_OnBeforeClosePage(IntPtr page, IntPtr form);
        [DllImport(Dll, CallingConvention = CC)] public static extern void FPDF_FFLDraw(IntPtr form, IntPtr bitmap, IntPtr page, int x, int y, int width, int height, int rotation, int flags);
        [DllImport(Dll, CallingConvention = CC)] public static extern int FORM_SetFocusedAnnot(IntPtr form, IntPtr annot);
        [DllImport(Dll, CallingConvention = CC)] public static extern int FORM_ForceToKillFocus(IntPtr form);
        [DllImport(Dll, CallingConvention = CC)] public static extern int FORM_SelectAllText(IntPtr form, IntPtr page);
        [DllImport(Dll, CallingConvention = CC)] public static extern void FORM_ReplaceSelection(IntPtr form, IntPtr page, [MarshalAs(UnmanagedType.LPWStr)] string text);
        [DllImport(Dll, CallingConvention = CC)] public static extern int FORM_OnChar(IntPtr form, IntPtr page, int character, int modifiers);
        [DllImport(Dll, CallingConvention = CC)] public static extern int FORM_SetIndexSelected(IntPtr form, IntPtr page, int index, int selected);
        [DllImport(Dll, CallingConvention = CC)] public static extern int FPDFPage_GetAnnotCount(IntPtr page);
        [DllImport(Dll, CallingConvention = CC)] public static extern IntPtr FPDFPage_GetAnnot(IntPtr page, int index);
        [DllImport(Dll, CallingConvention = CC)] public static extern void FPDFPage_CloseAnnot(IntPtr annot);
        [DllImport(Dll, CallingConvention = CC)] public static extern int FPDFAnnot_GetSubtype(IntPtr annot);
        [DllImport(Dll, CallingConvention = CC)] public static extern int FPDFAnnot_GetFlags(IntPtr annot);
        [DllImport(Dll, CallingConvention = CC)] public static extern int FPDFAnnot_GetRect(IntPtr annot, out PdfRect rect);
        [DllImport(Dll, CallingConvention = CC)] public static extern int FPDFAnnot_GetFormFieldFlags(IntPtr form, IntPtr annot);
        [DllImport(Dll, CallingConvention = CC)] public static extern int FPDFAnnot_GetFormFieldType(IntPtr form, IntPtr annot);
        [DllImport(Dll, CallingConvention = CC)] public static extern uint FPDFAnnot_GetFormFieldName(IntPtr form, IntPtr annot, byte[] buffer, uint length);
        [DllImport(Dll, CallingConvention = CC)] public static extern uint FPDFAnnot_GetFormFieldAlternateName(IntPtr form, IntPtr annot, byte[] buffer, uint length);
        [DllImport(Dll, CallingConvention = CC)] public static extern uint FPDFAnnot_GetFormFieldValue(IntPtr form, IntPtr annot, byte[] buffer, uint length);
        [DllImport(Dll, CallingConvention = CC)] public static extern uint FPDFAnnot_GetFormFieldExportValue(IntPtr form, IntPtr annot, byte[] buffer, uint length);
        [DllImport(Dll, CallingConvention = CC)] public static extern int FPDFAnnot_GetOptionCount(IntPtr form, IntPtr annot);
        [DllImport(Dll, CallingConvention = CC)] public static extern uint FPDFAnnot_GetOptionLabel(IntPtr form, IntPtr annot, int index, byte[] buffer, uint length);
        [DllImport(Dll, CallingConvention = CC)] public static extern int FPDFAnnot_IsOptionSelected(IntPtr form, IntPtr annot, int index);
        [DllImport(Dll, CallingConvention = CC)] public static extern int FPDFAnnot_IsChecked(IntPtr form, IntPtr annot);
        [DllImport(Dll, CallingConvention = CC)] public static extern uint FPDF_GetDocPermissions(IntPtr doc);
        [DllImport(Dll, CallingConvention = CC)] public static extern int FPDF_GetFormType(IntPtr doc);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int WriteBlockFn(IntPtr self, IntPtr data, uint size);

        /// <summary>Serializes a document (full rewrite, not incremental) to a stream.</summary>
        public static void SaveDocument(IntPtr doc, Stream output)
        {
            var chunk = new byte[256 * 1024];
            Exception error = null;
            WriteBlockFn fn = delegate (IntPtr self, IntPtr data, uint size)
            {
                try
                {
                    long off = 0;
                    while (off < size)
                    {
                        int n = (int)Math.Min(chunk.Length, size - off);
                        Marshal.Copy(new IntPtr(data.ToInt64() + off), chunk, 0, n);
                        output.Write(chunk, 0, n);
                        off += n;
                    }
                    return 1;
                }
                catch (Exception ex)
                {
                    error = ex;
                    return 0;
                }
            };
            IntPtr fw = Marshal.AllocHGlobal(IntPtr.Size * 2);
            try
            {
                Marshal.WriteInt32(fw, 0, 1);
                Marshal.WriteIntPtr(fw, IntPtr.Size, Marshal.GetFunctionPointerForDelegate(fn));
                int ok = FPDF_SaveAsCopy(doc, fw, FPDF_NO_INCREMENTAL);
                GC.KeepAlive(fn);
                if (error != null) throw new IOException("Could not write the PDF: " + error.Message, error);
                if (ok == 0) throw new IOException("PDFium could not save the document.");
            }
            finally
            {
                Marshal.FreeHGlobal(fw);
            }
        }
    }
}
