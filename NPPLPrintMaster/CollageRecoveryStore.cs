using System;
using System.IO;
using System.Linq;

namespace NPPLPrintMaster
{
    // Each editor owns its own draft, so another job or app instance cannot
    // overwrite it. Replace atomically and keep the previous valid version.
    internal static class CollageRecoveryStore
    {
        internal static string Folder
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NPPLPrintMaster", "CollageRecovery"); }
        }

        public static string NewDraftPath()
        {
            return Path.Combine(Folder, Guid.NewGuid().ToString("N") + CollageLayoutFile.Extension);
        }

        public static void Save(CollageLayout layout, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".tmp";
            try
            {
                CollageLayoutFile.Save(layout, temporary);
                // Flush the new data before replacing the last known good draft.
                using (var stream = new FileStream(temporary, FileMode.Open, FileAccess.ReadWrite))
                    stream.Flush(true);
                if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        public static string FindLatest(string folder = null)
        {
            folder = folder ?? Folder;
            if (!Directory.Exists(folder)) return null;
            foreach (var file in new DirectoryInfo(folder).GetFiles()
                .Where(f => f.Extension == CollageLayoutFile.Extension ||
                    f.Name.EndsWith(CollageLayoutFile.Extension + ".bak", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => f.LastWriteTimeUtc))
            {
                try
                {
                    if (CollageLayoutFile.Load(file.FullName) != null) return file.FullName;
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                catch (System.Xml.XmlException) { }
                catch (FormatException) { }
            }
            return null;
        }

        public static void Delete(string path)
        {
            foreach (string file in new[] { path, path + ".bak", path + ".tmp" })
                if (File.Exists(file)) File.Delete(file);
        }
    }
}
