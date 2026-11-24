using System;
using System.IO;

namespace Gravity
{
    internal static class AtomicFile
    {
        // Never fall back to copying over an existing file: a failed copy can destroy it.
        public static void Write(string path, Action<Stream> write, bool replaceExisting = true)
        {
            path = Path.GetFullPath(path);
            string temp = Path.Combine(Path.GetDirectoryName(path), "~gravity-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536))
                {
                    write(output);
                    output.Flush(true);
                }
                if (replaceExisting && File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
            }
            finally
            {
                try { if (File.Exists(temp)) File.Delete(temp); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
