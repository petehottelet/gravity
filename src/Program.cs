using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Gravity")]
[assembly: AssemblyProduct("Gravity")]
[assembly: AssemblyDescription("Fast PDF viewer and page organizer")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace Gravity
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            // Used after installation, including when the saved color predates registration.
            if (args.Length == 1 && args[0] == "--refresh-icons")
            {
                try { ShellIcons.Refresh(AppSettings.Load().IconColorHex); return 0; }
                catch (Exception) { return 1; }
            }
            if (args.Length == 1 && args[0] == "--register-file-types")
            {
                try { FileAssociations.Register(Application.ExecutablePath, AppSettings.Load().IconColorHex); return 0; }
                catch (Exception) { return 1; }
            }
            // GDI+ scans every installed font the first time any font is created. On machines with
            // thousands of fonts that takes most of a second, so start it immediately on another
            // thread and overlap it with instance detection and loading the documents.
            var fontWarmup = new Thread(delegate ()
            {
                try { using (new System.Drawing.Font("Segoe UI", 9f)) { } }
                catch (Exception) { }
            });
            fontWarmup.IsBackground = true;
            fontWarmup.Start();

            string id = InstanceId();
            bool separate = args.Contains("--new-window") || args.Contains("--screenshot");
            bool created;
            using (var mutex = new Mutex(true, "Local\\Gravity-" + id, out created))
            {
                if (!created && !separate && SingleInstance.Send(id, args)) return 0;

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (s, e) =>
                    MessageBox.Show(e.Exception.Message, "Gravity", MessageBoxButtons.OK, MessageBoxIcon.Error);

                if (!File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pdfium.dll")))
                {
                    MessageBox.Show("pdfium.dll is missing. It must be in the same folder as Gravity.exe.", "Gravity",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 1;
                }

                Preload(args);
                var form = new MainForm(args, args.Contains("--screenshot"));
                int si = Array.IndexOf(args, "--screenshot");
                if (si >= 0 && si + 1 < args.Length)
                {
                    form.ScreenshotPath = Path.GetFullPath(args[si + 1]);
                    form.ScreenshotOrganize = args.Contains("--organize");
                    int zi = Array.IndexOf(args, "--size");
                    if (zi >= 0 && zi + 1 < args.Length)
                    {
                        var wh = args[zi + 1].Split('x');
                        int w, h;
                        if (wh.Length == 2 && int.TryParse(wh[0], out w) && int.TryParse(wh[1], out h))
                        {
                            form.StartPosition = FormStartPosition.Manual;
                            form.WindowState = FormWindowState.Normal;
                            form.Bounds = new System.Drawing.Rectangle(40, 40, w, h);
                        }
                    }
                }
                if (created) SingleInstance.Listen(id, form);
                Application.Run(form);
                GC.KeepAlive(mutex);
            }
            return 0;
        }

        /// <summary>
        /// Opens the command-line documents (and warms PDFium's font mapping with a tiny render)
        /// while GDI+ is still scanning fonts. Anything that needs a password or fails is left for
        /// the normal path, which can show dialogs.
        /// </summary>
        private static void Preload(string[] args)
        {
            List<string> files;
            int page;
            MainForm.ParseArgs(args, out files, out page);
            foreach (var f in files)
            {
                try
                {
                    string path = Path.GetFullPath(f.Trim('"'));
                    if (!File.Exists(path) || ImageImport.Supports(path) || MainForm.Preloaded.ContainsKey(path)) continue;
                    var src = PdfSource.Open(path, null);
                    MainForm.Preloaded[path] = src;
                    if (src.PageCount > 0)
                    {
                        var warm = RenderService.RenderTile(new TileKey(src, 0, 0, 48, 64, 0, 0));
                        if (warm != null) warm.Dispose();
                    }
                }
                catch (Exception)
                {
                    // Left for MainForm.OpenFiles, which reports errors and asks for passwords.
                }
            }
        }

        /// <summary>A per-user, per-session id so instances only talk to their own user's window.</summary>
        private static string InstanceId()
        {
            string who;
            try
            {
                var sid = WindowsIdentity.GetCurrent().User;
                who = sid != null ? sid.Value : Environment.UserName;
            }
            catch (Exception) { who = Environment.UserName; }
            who += "|" + Process.GetCurrentProcess().SessionId;
            using (var sha = SHA256.Create())
            {
                var h = sha.ComputeHash(Encoding.UTF8.GetBytes(who));
                return BitConverter.ToString(h, 0, 8).Replace("-", "");
            }
        }
    }

    /// <summary>Forwards file arguments to an already-running window over a named pipe.</summary>
    internal static class SingleInstance
    {
        [DllImport("user32.dll")]
        private static extern bool AllowSetForegroundWindow(int processId);

        public static bool Send(string id, string[] args)
        {
            try
            {
                var resolved = new string[args.Length];
                for (int i = 0; i < args.Length; i++)
                {
                    string a = args[i];
                    bool flagValue = i > 0 && (args[i - 1] == "-page" || args[i - 1] == "--page");
                    resolved[i] = a.StartsWith("-") || flagValue ? a : Path.GetFullPath(a);
                }
                AllowSetForegroundWindow(-1);
                using (var c = new NamedPipeClientStream(".", "Gravity-" + id, PipeDirection.Out))
                {
                    c.Connect(2000);
                    var payload = Encoding.UTF8.GetBytes(string.Join("\n", resolved));
                    c.Write(payload, 0, payload.Length);
                    c.Flush();
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static void Listen(string id, MainForm form)
        {
            var t = new Thread(delegate ()
            {
                while (true)
                {
                    try
                    {
                        using (var s = new NamedPipeServerStream("Gravity-" + id, PipeDirection.In, 1))
                        {
                            s.WaitForConnection();
                            string text;
                            using (var ms = new MemoryStream())
                            {
                                s.CopyTo(ms);
                                text = Encoding.UTF8.GetString(ms.ToArray());
                            }
                            var args = text.Split('\n').Where(a => a.Length > 0).ToArray();
                            while (!form.IsHandleCreated && !form.IsDisposed) Thread.Sleep(50);
                            if (form.IsDisposed) return;
                            form.BeginInvoke((Action)delegate { form.OpenFromOtherInstance(args); });
                        }
                    }
                    catch (ObjectDisposedException) { return; }
                    catch (InvalidOperationException) { return; }
                    catch (IOException) { Thread.Sleep(200); }
                }
            });
            t.IsBackground = true;
            t.Name = "Gravity instance listener";
            t.Start();
        }
    }
}
