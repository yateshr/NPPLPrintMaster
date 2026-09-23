using System;
#pragma warning disable IDE0270
#pragma warning disable IDE0060
#pragma warning disable IDE0059
#pragma warning disable IDE0039
#pragma warning disable IDE0038
#pragma warning disable IDE0031
#pragma warning disable IDE0019
#pragma warning disable IDE0018
#pragma warning disable IDE0017
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZXing;
using ZXing.Common;

namespace NPPLPrintMaster
{
    public sealed class BtwImageLibraryRecord
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string ProductName { get; set; }
        public string Barcode { get; set; }
        public string ImagePath { get; set; }
        public string Status { get; set; }
        public string ErrorMessage { get; set; }
        public long FileSize { get; set; }
        public long LastWriteTicks { get; set; }
        public string LastProcessedUtc { get; set; }
    }

    public sealed class BtwImageLibraryProgress
    {
        public string Operation { get; set; }
        public int Processed { get; set; }
        public int Total { get; set; }
        public int Found { get; set; }
        public int Created { get; set; }
        public int Indexed { get; set; }
        public int Detected { get; set; }
        public int NotDetected { get; set; }
        public int Errors { get; set; }
        public int Duplicates { get; set; }
        public string CurrentFile { get; set; }
        public bool Completed { get; set; }
    }

    public sealed class BtwImageLibraryRefreshResult
    {
        public int TotalFiles { get; set; }
        public int IndexedFiles { get; set; }
        public int ImagesCreated { get; set; }
        public int ImagesFailed { get; set; }
        public int BarcodeProcessed { get; set; }
        public int BarcodeDetected { get; set; }
        public int BarcodeNotDetected { get; set; }
        public int Errors { get; set; }
        public int DuplicateBarcodes { get; set; }
    }

    /// <summary>
    /// Manual three-stage BTW library:
    /// 1) Index BTW files only.
    /// 2) Generate clean PNG images for indexed BTW files.
    /// 3) Scan all indexed images for barcodes.
    ///
    /// There is intentionally NO incremental decision-making in this version.
    /// BarTender uses exactly one COM instance on one thread.
    /// Barcode decoding is parallel and does not use BarTender COM.
    /// </summary>
    public sealed class BtwImageLibraryEngine
    {
        private const int DefaultBarcodeWorkers = 4;
        private const int MaxBarcodeWorkers = 8;

        private readonly string databaseFile;
        private readonly string imageFolder;
        private readonly string connectionString;

        private int imageCount;
        private int errorCount;
        private int missingImageCount;

        public BtwImageLibraryEngine(string indexFile, string imageFolder)
        {
            databaseFile = Path.ChangeExtension(indexFile, ".db");
            this.imageFolder = imageFolder;

            string dir = Path.GetDirectoryName(databaseFile);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            Directory.CreateDirectory(imageFolder);

            connectionString =
                "Data Source=" + databaseFile +
                ";Version=3;foreign keys=true;busy_timeout=30000;";

            InitializeDatabase();
            RebuildStatistics();
        }

        public int BarcodeWorkerCount { get; set; } = DefaultBarcodeWorkers;

        public int Count
        {
            get
            {
                using (SQLiteConnection c = OpenConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(
                    "SELECT COUNT(*) FROM BtwTemplates;", c))
                    return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        public int ImageCount { get { return imageCount; } }
        public int ErrorCount { get { return errorCount; } }
        public int MissingImageCount { get { return missingImageCount; } }

        public int DuplicateBarcodeCount
        {
            get
            {
                using (SQLiteConnection c = OpenConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(
                    "SELECT COUNT(*) FROM (" +
                    "SELECT Barcode FROM BtwTemplates " +
                    "WHERE Barcode IS NOT NULL AND Barcode<>'' " +
                    "GROUP BY Barcode HAVING COUNT(*)>1);", c))
                    return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        public List<BtwImageLibraryRecord> Search(string query, string statusFilter)
        {
            query = (query ?? "").Trim();
            statusFilter = string.IsNullOrWhiteSpace(statusFilter) ? "All" : statusFilter;

            List<BtwImageLibraryRecord> list = new List<BtwImageLibraryRecord>();

            using (SQLiteConnection c = OpenConnection())
            using (SQLiteCommand cmd = c.CreateCommand())
            {
                StringBuilder sql = new StringBuilder(
                    "SELECT FilePath,FileName,ProductName,Barcode,ImagePath," +
                    "Status,ErrorMessage,FileSize,LastWriteTicks,LastProcessedUtc " +
                    "FROM BtwTemplates WHERE 1=1");

                if (!string.Equals(statusFilter, "All", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(statusFilter, "Missing Image", StringComparison.OrdinalIgnoreCase))
                    {
                        // Missing-image filtering is performed after reading the
                        // rows because SQLite cannot test the local filesystem.
                        // Do not append SQL here; the base query already contains
                        // WHERE 1=1.
                    }
                    else if (string.Equals(statusFilter, "Duplicate Barcode", StringComparison.OrdinalIgnoreCase))
                    {
                        sql.Append("AND Barcode IS NOT NULL AND Barcode<>'' AND Barcode IN (" +
                                   "SELECT Barcode FROM BtwTemplates " +
                                   "WHERE Barcode IS NOT NULL AND Barcode<>'' " +
                                   "GROUP BY Barcode HAVING COUNT(*)>1) ");
                    }
                    else if (string.Equals(statusFilter, "Ready", StringComparison.OrdinalIgnoreCase))
                    {
                        sql.Append("AND Status='Ready' ");
                    }
                    else
                    {
                        sql.Append("AND Status=@status ");
                        cmd.Parameters.AddWithValue("@status", statusFilter);
                    }
                }

                if (!string.IsNullOrWhiteSpace(query))
                {
                    sql.Append("AND (Barcode LIKE @q OR ProductName LIKE @q OR " +
                               "FileName LIKE @q OR FilePath LIKE @q) ");
                    cmd.Parameters.AddWithValue("@q", "%" + query + "%");
                }

                sql.Append(" ORDER BY ProductName COLLATE NOCASE, " +
                           "FileName COLLATE NOCASE LIMIT 5000;");

                cmd.CommandText = sql.ToString();

                using (SQLiteDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        BtwImageLibraryRecord record = ReadRecord(r);

                        if (string.Equals(statusFilter, "Missing Image",
                            StringComparison.OrdinalIgnoreCase))
                        {
                            if (!string.IsNullOrWhiteSpace(record.ImagePath) &&
                                File.Exists(record.ImagePath))
                                continue;
                        }

                        list.Add(record);
                    }
                }
            }

            return list;
        }

        public BtwImageLibraryRecord Get(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            using (SQLiteConnection c = OpenConnection())
            using (SQLiteCommand cmd = c.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT FilePath,FileName,ProductName,Barcode,ImagePath," +
                    "Status,ErrorMessage,FileSize,LastWriteTicks,LastProcessedUtc " +
                    "FROM BtwTemplates WHERE FilePath=@p;";
                cmd.Parameters.AddWithValue("@p", path);

                using (SQLiteDataReader r = cmd.ExecuteReader())
                    return r.Read() ? ReadRecord(r) : null;
            }
        }

        public void ClearIndex()
        {
            using (SQLiteConnection c = OpenConnection())
            using (SQLiteCommand cmd = c.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM BtwTemplates;";
                cmd.ExecuteNonQuery();
            }

            RebuildStatistics();
        }

        public BtwImageLibraryRefreshResult IndexBtwTemplates(
            IEnumerable<string> includeFolders,
            IEnumerable<string> excludeFolders,
            Action<BtwImageLibraryProgress> progress,
            CancellationToken cancellationToken)
        {
            List<string> includes = NormalizeFolders(includeFolders);
            List<string> excludes = NormalizeFolders(excludeFolders);

            ValidateIncludeFolders(includes);

            List<string> files = EnumerateBtwFiles(includes, excludes);
            BtwImageLibraryRefreshResult result =
                new BtwImageLibraryRefreshResult { TotalFiles = files.Count };

            // This is a deliberate manual full index. It does not compare
            // timestamps, hashes or old rows.
            using (SQLiteConnection c = OpenConnection())
            using (SQLiteTransaction tx = c.BeginTransaction())
            using (SQLiteCommand delete = c.CreateCommand())
            using (SQLiteCommand insert = c.CreateCommand())
            {
                delete.Transaction = tx;
                delete.CommandText = "DELETE FROM BtwTemplates;";
                delete.ExecuteNonQuery();

                insert.Transaction = tx;
                insert.CommandText =
                    "INSERT INTO BtwTemplates " +
                    "(FilePath,FileName,ProductName,Barcode,ImagePath,Status," +
                    "ErrorMessage,FileSize,LastWriteTicks,LastProcessedUtc) " +
                    "VALUES (@path,@name,@product,'',@image,'New','',@size,@ticks,'');";

                foreach (string file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    FileInfo info;
                    try
                    {
                        info = new FileInfo(file);
                    }
                    catch
                    {
                        result.Errors++;
                        Report(progress, "Index BTW", result.IndexedFiles,
                            files.Count, result, file, false);
                        continue;
                    }

                    insert.Parameters.Clear();
                    insert.Parameters.AddWithValue("@path", file);
                    insert.Parameters.AddWithValue("@name", Path.GetFileName(file));
                    insert.Parameters.AddWithValue("@product",
                        Path.GetFileNameWithoutExtension(file));
                    insert.Parameters.AddWithValue("@image", GetImagePath(file));
                    insert.Parameters.AddWithValue("@size", info.Length);
                    insert.Parameters.AddWithValue("@ticks", info.LastWriteTimeUtc.Ticks);
                    insert.ExecuteNonQuery();

                    result.IndexedFiles++;

                    Report(progress, "Index BTW", result.IndexedFiles,
                        files.Count, result, file, false);
                }

                tx.Commit();
            }

            RebuildStatistics();

            result.DuplicateBarcodes = DuplicateBarcodeCount;
            Report(progress, "Index BTW", result.IndexedFiles, files.Count,
                result, "Index complete", true);

            return result;
        }

        public BtwImageLibraryRefreshResult CreateImages(
            int dpi,
            Action<BtwImageLibraryProgress> progress,
            CancellationToken cancellationToken)
        {
            return CreateImages(
                dpi,
                "PNG",
                progress,
                cancellationToken);
        }

        public BtwImageLibraryRefreshResult CreateImages(
            int dpi,
            string imageExtension,
            Action<BtwImageLibraryProgress> progress,
            CancellationToken cancellationToken)
        {
            imageExtension =
                BtwImageLibraryModuleSettings.NormalizeExtension(
                    imageExtension);

            List<BtwImageLibraryRecord> records = LoadAllRecords();

            BtwImageLibraryRefreshResult result =
                new BtwImageLibraryRefreshResult
                {
                    TotalFiles = records.Count
                };

            if (records.Count == 0)
            {
                Report(progress, "Create Images", 0, 0, result,
                    "No BTW templates are indexed.", true);
                return result;
            }

            dynamic btApp = null;

            try
            {
                Type btType = Type.GetTypeFromProgID("BarTender.Application");
                if (btType == null)
                    throw new InvalidOperationException(
                        "BarTender.Application could not be started.");

                // Exactly one BarTender COM instance.
                btApp = Activator.CreateInstance(btType);

                // Match the known working BarTender workflow.
                btApp.Visible = true;

                for (int i = 0; i < records.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    BtwImageLibraryRecord r = records[i];

                    try
                    {
                        r.ImagePath =
                            GetImagePath(
                                r.FilePath,
                                imageExtension);

                        Directory.CreateDirectory(
                            Path.GetDirectoryName(r.ImagePath));

                        ExportOneBtw(
                            btApp,
                            r.FilePath,
                            r.ImagePath,
                            dpi,
                            imageExtension,
                            cancellationToken);

                        r.Status = "Image Ready - Barcode Pending";
                        r.ErrorMessage = "";
                        r.Barcode = "";
                        r.LastProcessedUtc = DateTime.UtcNow.ToString("o");

                        UpdateRecord(r);
                        result.ImagesCreated++;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        r.Status = "Error";
                        r.ErrorMessage = ex.Message;
                        r.LastProcessedUtc = DateTime.UtcNow.ToString("o");
                        UpdateRecord(r);
                        result.Errors++;
                    }

                    int processed = i + 1;
                    Report(progress, "Create Images", processed,
                        records.Count, result, r.FilePath, false);
                }
            }
            finally
            {
                if (btApp != null)
                {
                    try { btApp.Quit(2); } catch { }
                    btApp = null;
                }
            }

            RebuildStatistics();
            result.DuplicateBarcodes = DuplicateBarcodeCount;

            Report(progress, "Create Images", records.Count,
                records.Count, result, "Image creation complete", true);

            return result;
        }

        public BtwImageLibraryRefreshResult IndexBarcodes(
            Action<BtwImageLibraryProgress> progress,
            CancellationToken cancellationToken)
        {
            List<BtwImageLibraryRecord> records = LoadAllRecords()
                .Where(r => !string.IsNullOrWhiteSpace(r.ImagePath) &&
                            File.Exists(r.ImagePath))
                .ToList();

            BtwImageLibraryRefreshResult result =
                new BtwImageLibraryRefreshResult
                {
                    TotalFiles = records.Count
                };

            if (records.Count == 0)
            {
                Report(progress, "Index Barcodes", 0, 0, result,
                    "No generated images are available.", true);
                return result;
            }

            ConcurrentQueue<BtwImageLibraryRecord> queue =
                new ConcurrentQueue<BtwImageLibraryRecord>(records);

            int workers = Math.Max(1, Math.Min(MaxBarcodeWorkers, BarcodeWorkerCount));
            int processed = 0;

            Task[] tasks = new Task[workers];

            for (int i = 0; i < workers; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    BtwImageLibraryRecord record;

                    while (queue.TryDequeue(out record))
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        try
                        {
                            string barcode = DecodeBarcode(record.ImagePath);

                            record.Barcode = barcode;
                            record.ErrorMessage = "";
                            record.Status = string.IsNullOrWhiteSpace(barcode)
                                ? "Ready - Barcode Not Detected"
                                : "Ready";
                        }
                        catch (Exception ex)
                        {
                            record.Status = "Error";
                            record.ErrorMessage = ex.Message;
                            result.Errors++;
                        }

                        record.LastProcessedUtc = DateTime.UtcNow.ToString("o");
                        UpdateRecord(record);

                        int done = Interlocked.Increment(ref processed);

                        if (string.IsNullOrWhiteSpace(record.Barcode))
                            result.BarcodeNotDetected++;
                        else
                            result.BarcodeDetected++;

                        result.BarcodeProcessed++;

                        Report(progress, "Index Barcodes", done,
                            records.Count, result, record.FilePath, false);
                    }
                }, cancellationToken);
            }

            try
            {
                Task.WaitAll(tasks);
            }
            catch (AggregateException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw;
            }

            RebuildStatistics();
            result.DuplicateBarcodes = DuplicateBarcodeCount;

            Report(progress, "Index Barcodes", records.Count,
                records.Count, result, "Barcode indexing complete", true);

            return result;
        }

        // Kept as a compatibility wrapper for any older caller.
        // The new UI does not use it.
        public BtwImageLibraryRefreshResult Refresh(
            IEnumerable<string> includeFolders,
            IEnumerable<string> excludeFolders,
            int dpi,
            Action<BtwImageLibraryProgress> progress,
            CancellationToken cancellationToken)
        {
            BtwImageLibraryRefreshResult result =
                IndexBtwTemplates(includeFolders, excludeFolders,
                    progress, cancellationToken);

            BtwImageLibraryRefreshResult images =
                CreateImages(dpi, progress, cancellationToken);

            result.ImagesCreated = images.ImagesCreated;
            result.Errors += images.Errors;

            BtwImageLibraryRefreshResult barcodes =
                IndexBarcodes(progress, cancellationToken);

            result.BarcodeProcessed = barcodes.BarcodeProcessed;
            result.BarcodeDetected = barcodes.BarcodeDetected;
            result.BarcodeNotDetected = barcodes.BarcodeNotDetected;
            result.Errors += barcodes.Errors;
            result.DuplicateBarcodes = barcodes.DuplicateBarcodes;

            return result;
        }

        private void ValidateIncludeFolders(List<string> includes)
        {
            if (includes.Count == 0)
                throw new InvalidOperationException(
                    "Add at least one BTW Include Folder.");

            List<string> unavailable = includes
                .Where(x => !Directory.Exists(x))
                .ToList();

            if (unavailable.Count > 0)
                throw new DirectoryNotFoundException(
                    "These BTW Include Folders are unavailable:\r\n\r\n" +
                    string.Join("\r\n", unavailable));
        }

        private void UpdateRecord(BtwImageLibraryRecord r)
        {
            using (SQLiteConnection c = OpenConnection())
            using (SQLiteCommand cmd = c.CreateCommand())
            {
                cmd.CommandText =
                    "UPDATE BtwTemplates SET Barcode=@barcode,ImagePath=@image," +
                    "Status=@status,ErrorMessage=@error,LastProcessedUtc=@utc " +
                    "WHERE FilePath=@path;";

                cmd.Parameters.AddWithValue("@barcode", r.Barcode ?? "");
                cmd.Parameters.AddWithValue("@image", r.ImagePath ?? "");
                cmd.Parameters.AddWithValue("@status", r.Status ?? "");
                cmd.Parameters.AddWithValue("@error", r.ErrorMessage ?? "");
                cmd.Parameters.AddWithValue("@utc", r.LastProcessedUtc ?? "");
                cmd.Parameters.AddWithValue("@path", r.FilePath ?? "");
                cmd.ExecuteNonQuery();
            }
        }

        private List<BtwImageLibraryRecord> LoadAllRecords()
        {
            List<BtwImageLibraryRecord> result =
                new List<BtwImageLibraryRecord>();

            using (SQLiteConnection c = OpenConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(
                "SELECT FilePath,FileName,ProductName,Barcode,ImagePath," +
                "Status,ErrorMessage,FileSize,LastWriteTicks,LastProcessedUtc " +
                "FROM BtwTemplates ORDER BY FilePath COLLATE NOCASE;", c))
            using (SQLiteDataReader r = cmd.ExecuteReader())
            {
                while (r.Read())
                    result.Add(ReadRecord(r));
            }

            return result;
        }

        private static void ExportOneBtw(
            dynamic btApp,
            string btwPath,
            string outputPath,
            int dpi,
            string imageExtension,
            CancellationToken cancellationToken)
        {
            dynamic btFormat = null;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                btFormat = btApp.Formats.Open(btwPath, false, "");

                if (btFormat == null)
                    throw new InvalidOperationException(
                        "BarTender could not open the BTW template.");

                cancellationToken.ThrowIfCancellationRequested();

                string exportFormat =
                    imageExtension == "JPG" ? "JPEG" :
                    imageExtension == "BMP" ? "BMP" :
                    imageExtension == "TIFF" ? "TIFF" :
                    "PNG";

                btFormat.ExportToFile(
                    outputPath,
                    exportFormat,
                    4,
                    dpi,
                    2);
            }
            finally
            {
                if (btFormat != null)
                {
                    try { btFormat.Close(2); } catch { }
                    btFormat = null;
                }
            }
        }

        private static string DecodeBarcode(string file)
        {
            using (Bitmap bmp = new Bitmap(file))
            {
                BarcodeReader reader = new BarcodeReader
                {
                    AutoRotate = true,
                    Options = new DecodingOptions { TryHarder = false }
                };

                Result result = reader.Decode(bmp);

                if (result == null || string.IsNullOrWhiteSpace(result.Text))
                {
                    reader = new BarcodeReader
                    {
                        AutoRotate = true,
                        Options = new DecodingOptions { TryHarder = true }
                    };
                    result = reader.Decode(bmp);
                }

                return result == null ? "" : (result.Text ?? "").Trim();
            }
        }

        private static List<string> EnumerateBtwFiles(
            IEnumerable<string> includeFolders,
            IEnumerable<string> excludeFolders)
        {
            HashSet<string> excluded =
                new HashSet<string>(NormalizeFolders(excludeFolders),
                    StringComparer.OrdinalIgnoreCase);

            HashSet<string> files =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string root in NormalizeFolders(includeFolders))
            {
                if (!Directory.Exists(root))
                    continue;

                Stack<string> dirs = new Stack<string>();
                dirs.Push(root);

                while (dirs.Count > 0)
                {
                    string dir = dirs.Pop();

                    if (IsExcluded(dir, excluded))
                        continue;

                    try
                    {
                        foreach (string file in Directory.GetFiles(
                            dir, "*.btw", SearchOption.TopDirectoryOnly))
                            files.Add(Path.GetFullPath(file));
                    }
                    catch { }

                    try
                    {
                        foreach (string child in Directory.GetDirectories(
                            dir, "*", SearchOption.TopDirectoryOnly))
                        {
                            if (!IsExcluded(child, excluded))
                                dirs.Push(child);
                        }
                    }
                    catch { }
                }
            }

            return files.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static bool IsExcluded(string path, HashSet<string> excluded)
        {
            string full = Path.GetFullPath(path).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            foreach (string root in excluded)
            {
                string e = root.TrimEnd(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                if (string.Equals(full, e, StringComparison.OrdinalIgnoreCase) ||
                    full.StartsWith(e + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static List<string> NormalizeFolders(IEnumerable<string> folders)
        {
            return (folders ?? Enumerable.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x =>
                {
                    try { return Path.GetFullPath(x.Trim()); }
                    catch { return ""; }
                })
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void Report(
            Action<BtwImageLibraryProgress> progress,
            string operation,
            int processed,
            int total,
            BtwImageLibraryRefreshResult result,
            string current,
            bool completed)
        {
            if (progress == null)
                return;

            progress(new BtwImageLibraryProgress
            {
                Operation = operation,
                Processed = processed,
                Total = total,
                Found = result.TotalFiles,
                Created = result.ImagesCreated,
                Indexed = result.IndexedFiles,
                Detected = result.BarcodeDetected,
                NotDetected = result.BarcodeNotDetected,
                Errors = result.Errors,
                Duplicates = result.DuplicateBarcodes,
                CurrentFile = current,
                Completed = completed
            });
        }

        private BtwImageLibraryRecord ReadRecord(SQLiteDataReader r)
        {
            return new BtwImageLibraryRecord
            {
                FilePath = GetString(r, 0),
                FileName = GetString(r, 1),
                ProductName = GetString(r, 2),
                Barcode = GetString(r, 3),
                ImagePath = GetString(r, 4),
                Status = GetString(r, 5),
                ErrorMessage = GetString(r, 6),
                FileSize = GetInt64(r, 7),
                LastWriteTicks = GetInt64(r, 8),
                LastProcessedUtc = GetString(r, 9)
            };
        }

        private static string GetString(SQLiteDataReader r, int i)
        {
            return r.IsDBNull(i) ? "" : Convert.ToString(r.GetValue(i));
        }

        private static long GetInt64(SQLiteDataReader r, int i)
        {
            return r.IsDBNull(i) ? 0 : Convert.ToInt64(r.GetValue(i));
        }

        private SQLiteConnection OpenConnection()
        {
            SQLiteConnection c = new SQLiteConnection(connectionString);
            c.Open();

            using (SQLiteCommand cmd = c.CreateCommand())
            {
                cmd.CommandText = "PRAGMA journal_mode=WAL;";
                cmd.ExecuteNonQuery();

                cmd.CommandText = "PRAGMA synchronous=NORMAL;";
                cmd.ExecuteNonQuery();

                cmd.CommandText = "PRAGMA temp_store=MEMORY;";
                cmd.ExecuteNonQuery();

                cmd.CommandText = "PRAGMA cache_size=-20000;";
                cmd.ExecuteNonQuery();
            }

            return c;
        }

        private void InitializeDatabase()
        {
            using (SQLiteConnection c = OpenConnection())
            using (SQLiteCommand cmd = c.CreateCommand())
            {
                cmd.CommandText =
                    "CREATE TABLE IF NOT EXISTS BtwTemplates (" +
                    "Id INTEGER PRIMARY KEY AUTOINCREMENT," +
                    "FilePath TEXT NOT NULL COLLATE NOCASE UNIQUE," +
                    "FileName TEXT," +
                    "ProductName TEXT," +
                    "Barcode TEXT," +
                    "ImagePath TEXT," +
                    "Status TEXT," +
                    "ErrorMessage TEXT," +
                    "FileSize INTEGER NOT NULL DEFAULT 0," +
                    "LastWriteTicks INTEGER NOT NULL DEFAULT 0," +
                    "LastProcessedUtc TEXT);" +
                    "CREATE INDEX IF NOT EXISTS IX_Btw_Barcode ON BtwTemplates(Barcode);" +
                    "CREATE INDEX IF NOT EXISTS IX_Btw_Status ON BtwTemplates(Status);" +
                    "CREATE INDEX IF NOT EXISTS IX_Btw_Product ON BtwTemplates(ProductName);" +
                    "CREATE INDEX IF NOT EXISTS IX_Btw_FileName ON BtwTemplates(FileName);" +
                    "CREATE INDEX IF NOT EXISTS IX_Btw_FilePath ON BtwTemplates(FilePath);";
                cmd.ExecuteNonQuery();
            }
        }

        private void RebuildStatistics()
        {
            int images = 0;
            int errors = 0;
            int missing = 0;

            using (SQLiteConnection c = OpenConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(
                "SELECT ImagePath,Status FROM BtwTemplates;", c))
            using (SQLiteDataReader r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    string image = GetString(r, 0);
                    string status = GetString(r, 1);

                    if (!string.IsNullOrWhiteSpace(image) && File.Exists(image))
                        images++;
                    else
                        missing++;

                    if (string.Equals(status, "Error",
                        StringComparison.OrdinalIgnoreCase))
                        errors++;
                }
            }

            imageCount = images;
            errorCount = errors;
            missingImageCount = missing;
        }

        private string GetImagePath(string btwPath)
        {
            return GetImagePath(btwPath, "PNG");
        }

        private string GetImagePath(
            string btwPath,
            string imageExtension)
        {
            string key;

            using (SHA1 sha = SHA1.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(
                    Path.GetFullPath(btwPath).ToUpperInvariant());

                key = BitConverter.ToString(sha.ComputeHash(bytes))
                    .Replace("-", "").Substring(0, 20);
            }

            string safe = Path.GetFileNameWithoutExtension(btwPath);
            foreach (char invalid in Path.GetInvalidFileNameChars())
                safe = safe.Replace(invalid, '_');

            if (string.IsNullOrWhiteSpace(safe))
                safe = "BTW_Image";

            string folder = Path.Combine(imageFolder, key);

            string extension =
                BtwImageLibraryModuleSettings.NormalizeExtension(
                    imageExtension);

            string fileExtension =
                extension == "JPG" ? ".jpg" :
                extension == "BMP" ? ".bmp" :
                extension == "TIFF" ? ".tif" :
                ".png";

            return Path.Combine(
                folder,
                safe + fileExtension);
        }
    }
}
