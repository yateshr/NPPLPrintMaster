using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using ZXing;
using ZXing.Common;

namespace NPPLPrintMaster
{
    public sealed class SmartImageIndexProgress
    {
        public int Processed { get; set; }
        public int Total { get; set; }
        public int Decoded { get; set; }
        public int Failed { get; set; }
        public int Unchanged { get; set; }
        public string CurrentFile { get; set; }
    }

    public sealed class SmartImageRefreshResult
    {
        public int TotalFiles { get; set; }
        public int NewFiles { get; set; }
        public int ChangedFiles { get; set; }
        public int RemovedFiles { get; set; }
        public int UnchangedFiles { get; set; }
        public int DecodedFiles { get; set; }
        public int FailedFiles { get; set; }

        public override string ToString()
        {
            return string.Format(
                "{0} total | {1} new | {2} changed | {3} removed | {4} unchanged | {5} decoded | {6} failed",
                TotalFiles, NewFiles, ChangedFiles, RemovedFiles,
                UnchangedFiles, DecodedFiles, FailedFiles);
        }
    }

    internal sealed class SmartImageIndexRecord
    {
        public string FilePath { get; set; }
        public long FileSize { get; set; }
        public long LastWriteTicks { get; set; }
        public string Barcode { get; set; }
        public bool DecodeFailed { get; set; }
    }

    public sealed class SmartImageFinderEngine
    {
        private readonly object syncRoot = new object();
        private readonly string indexName;
        private readonly Dictionary<string, SmartImageIndexRecord> records =
            new Dictionary<string, SmartImageIndexRecord>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<string>> lookup =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private string indexedRoot = "";

        public SmartImageFinderEngine(string indexName)
        {
            this.indexName = SafeName(string.IsNullOrWhiteSpace(indexName) ? "SmartImageIndex" : indexName);
            LoadCache();
        }

        public string IndexedRoot { get { lock (syncRoot) return indexedRoot; } }
        public int CodeCount { get { lock (syncRoot) return lookup.Count; } }
        public int FileCount { get { lock (syncRoot) return records.Count; } }
        public int DecodedFileCount { get { lock (syncRoot) return records.Values.Count(r => !r.DecodeFailed && !string.IsNullOrWhiteSpace(r.Barcode)); } }
        public int FailedFileCount { get { lock (syncRoot) return records.Values.Count(r => r.DecodeFailed); } }
        public int DuplicateCodeCount { get { lock (syncRoot) return lookup.Count(x => x.Value.Count > 1); } }

        public List<string> GetFailedFiles()
        {
            lock (syncRoot)
            {
                return records.Values.Where(r => r.DecodeFailed && File.Exists(r.FilePath))
                    .OrderByDescending(r => LastWrite(r.FilePath))
                    .Select(r => r.FilePath).ToList();
            }
        }

        public List<string> FindExact(string code)
        {
            string key = NormalizeCode(code);
            if (string.IsNullOrWhiteSpace(key)) return new List<string>();

            lock (syncRoot)
            {
                List<string> paths;
                if (!lookup.TryGetValue(key, out paths)) return new List<string>();
                return paths.Where(File.Exists)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(LastWrite)
                    .ThenBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
            }
        }

        public SmartImageRefreshResult Rebuild(string rootFolder, Action<SmartImageIndexProgress> progress = null)
        {
            return RefreshIncremental(rootFolder, progress, true);
        }

        public SmartImageRefreshResult RefreshIncremental(
            string rootFolder,
            Action<SmartImageIndexProgress> progress = null,
            bool forceAll = false)
        {
            if (string.IsNullOrWhiteSpace(rootFolder) || !Directory.Exists(rootFolder))
                throw new DirectoryNotFoundException("The selected image directory does not exist.");

            rootFolder = Path.GetFullPath(rootFolder);
            List<string> files = EnumerateImages(rootFolder).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();

            Dictionary<string, SmartImageIndexRecord> previous;
            lock (syncRoot)
            {
                bool sameRoot = string.Equals(indexedRoot, rootFolder, StringComparison.OrdinalIgnoreCase);
                previous = sameRoot && !forceAll
                    ? new Dictionary<string, SmartImageIndexRecord>(records, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, SmartImageIndexRecord>(StringComparer.OrdinalIgnoreCase);
            }

            Dictionary<string, SmartImageIndexRecord> updated =
                new Dictionary<string, SmartImageIndexRecord>(StringComparer.OrdinalIgnoreCase);
            SmartImageRefreshResult result = new SmartImageRefreshResult { TotalFiles = files.Count };

            for (int i = 0; i < files.Count; i++)
            {
                string file = files[i];
                FileInfo fi;
                try { fi = new FileInfo(file); } catch { continue; }

                SmartImageIndexRecord old;
                bool hasOld = previous.TryGetValue(file, out old);
                bool unchanged = hasOld && old.FileSize == fi.Length && old.LastWriteTicks == fi.LastWriteTimeUtc.Ticks;
                SmartImageIndexRecord rec;

                if (unchanged)
                {
                    rec = old;
                    result.UnchangedFiles++;
                }
                else
                {
                    if (hasOld) result.ChangedFiles++; else result.NewFiles++;
                    string value = Decode(file);
                    rec = new SmartImageIndexRecord
                    {
                        FilePath = file,
                        FileSize = fi.Length,
                        LastWriteTicks = fi.LastWriteTimeUtc.Ticks,
                        Barcode = NormalizeCode(value),
                        DecodeFailed = string.IsNullOrWhiteSpace(value)
                    };
                }

                updated[file] = rec;

                if (progress != null)
                    progress(new SmartImageIndexProgress
                    {
                        Processed = i + 1,
                        Total = files.Count,
                        Decoded = updated.Values.Count(r => !r.DecodeFailed && !string.IsNullOrWhiteSpace(r.Barcode)),
                        Failed = updated.Values.Count(r => r.DecodeFailed),
                        Unchanged = result.UnchangedFiles,
                        CurrentFile = file
                    });
            }

            result.RemovedFiles = previous.Keys.Count(p => !updated.ContainsKey(p));
            result.DecodedFiles = updated.Values.Count(r => !r.DecodeFailed && !string.IsNullOrWhiteSpace(r.Barcode));
            result.FailedFiles = updated.Values.Count(r => r.DecodeFailed);

            lock (syncRoot)
            {
                records.Clear();
                foreach (var x in updated) records[x.Key] = x.Value;
                indexedRoot = rootFolder;
                RebuildLookup();
                SaveCache();
            }
            return result;
        }

        public static string NormalizeCode(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            StringBuilder sb = new StringBuilder();
            foreach (char c in value.Trim().ToUpperInvariant())
                if (!char.IsWhiteSpace(c)) sb.Append(c);
            return sb.ToString();
        }

        private static string Decode(string file)
        {
            try
            {
                using (Bitmap bmp = new Bitmap(file))
                {
                    BarcodeReader reader = new BarcodeReader
                    {
                        AutoRotate = true,
                        Options = new DecodingOptions { TryHarder = true }
                    };
                    Result r = reader.Decode(bmp);
                    return r == null ? "" : (r.Text ?? "");
                }
            }
            catch { return ""; }
        }

        private void RebuildLookup()
        {
            lookup.Clear();
            foreach (SmartImageIndexRecord r in records.Values)
            {
                if (r.DecodeFailed || string.IsNullOrWhiteSpace(r.Barcode)) continue;
                if (!lookup.ContainsKey(r.Barcode)) lookup[r.Barcode] = new List<string>();
                lookup[r.Barcode].Add(r.FilePath);
            }
        }

        private static IEnumerable<string> EnumerateImages(string root)
        {
            Stack<string> stack = new Stack<string>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                string folder = stack.Pop();
                string[] files = new string[0], dirs = new string[0];
                try { files = Directory.GetFiles(folder); } catch { }
                foreach (string f in files)
                {
                    string e = Path.GetExtension(f);
                    if (e.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                        e.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                        e.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                        e.Equals(".bmp", StringComparison.OrdinalIgnoreCase) ||
                        e.Equals(".tif", StringComparison.OrdinalIgnoreCase) ||
                        e.Equals(".tiff", StringComparison.OrdinalIgnoreCase))
                        yield return f;
                }
                try { dirs = Directory.GetDirectories(folder); } catch { }
                foreach (string d in dirs) stack.Push(d);
            }
        }

        private string CachePath()
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NPPLPrintMaster", "SmartImageFinder");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, indexName + ".index");
        }

        private void SaveCache()
        {
            using (StreamWriter sw = new StreamWriter(CachePath(), false, Encoding.UTF8))
            {
                sw.WriteLine("VERSION\t2");
                sw.WriteLine("ROOT\t" + B64(indexedRoot));
                foreach (SmartImageIndexRecord r in records.Values)
                    sw.WriteLine("ITEM\t" + B64(r.FilePath) + "\t" + r.FileSize + "\t" +
                        r.LastWriteTicks + "\t" + B64(r.Barcode) + "\t" + (r.DecodeFailed ? "1" : "0"));
            }
        }

        private void LoadCache()
        {
            lock (syncRoot)
            {
                records.Clear(); lookup.Clear(); indexedRoot = "";
                string path = CachePath();
                if (!File.Exists(path)) return;
                try
                {
                    string[] lines = File.ReadAllLines(path);
                    if (!lines.Any(x => x == "VERSION\t2")) return; // V1 cache rebuilt once.
                    foreach (string line in lines)
                    {
                        string[] p = line.Split('\t');
                        if (p.Length == 2 && p[0] == "ROOT") indexedRoot = UB64(p[1]);
                        else if (p.Length == 6 && p[0] == "ITEM")
                        {
                            long size, ticks;
                            if (!long.TryParse(p[2], out size) || !long.TryParse(p[3], out ticks)) continue;
                            string file = UB64(p[1]);
                            if (!File.Exists(file)) continue;
                            records[file] = new SmartImageIndexRecord
                            {
                                FilePath = file, FileSize = size, LastWriteTicks = ticks,
                                Barcode = UB64(p[4]), DecodeFailed = p[5] == "1"
                            };
                        }
                    }
                    RebuildLookup();
                }
                catch { records.Clear(); lookup.Clear(); indexedRoot = ""; }
            }
        }

        private static DateTime LastWrite(string p) { try { return File.GetLastWriteTimeUtc(p); } catch { return DateTime.MinValue; } }
        private static string B64(string s) { return Convert.ToBase64String(Encoding.UTF8.GetBytes(s ?? "")); }
        private static string UB64(string s) { return Encoding.UTF8.GetString(Convert.FromBase64String(s)); }
        private static string SafeName(string s) { foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_'); return s; }
    }
}
