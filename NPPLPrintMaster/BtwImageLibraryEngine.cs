using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        public int Processed { get; set; }
        public int Total { get; set; }
        public int NewFiles { get; set; }
        public int ChangedFiles { get; set; }
        public int UnchangedFiles { get; set; }
        public int ImagesCreated { get; set; }
        public int Errors { get; set; }
        public string CurrentFile { get; set; }

        public int ExtractionProcessed { get; set; }
        public int ExtractionTotal { get; set; }
        public int BarcodeProcessed { get; set; }
        public int BarcodeTotal { get; set; }
        public int ExistingImagesIndexed { get; set; }
    }

    public sealed class BtwImageLibraryRefreshResult
    {
        public int TotalFiles { get; set; }
        public int NewFiles { get; set; }
        public int ChangedFiles { get; set; }
        public int UnchangedFiles { get; set; }
        public int RemovedFiles { get; set; }
        public int ImagesCreated { get; set; }
        public int Errors { get; set; }
        public int DuplicateBarcodes { get; set; }
        public int ExistingImagesIndexed { get; set; }
        public int BarcodeProcessed { get; set; }
    }

    /// <summary>
    /// BTW Image Library engine.
    ///
    /// Architecture:
    ///   1. Fast filesystem scan + SQLite incremental comparison.
    ///   2. One BarTender COM instance for extraction only.
    ///   3. Barcode decoding runs in parallel worker threads.
    ///   4. A single SQLite writer queue prevents write contention.
    ///
    /// BarTender COM is never shared between threads.
    /// </summary>
    public sealed class BtwImageLibraryEngine
    {
        private const int DefaultBarcodeWorkerCount = 4;
        private const int MaxBarcodeWorkerCount = 8;
        private const int SqliteBatchSize = 100;

        private readonly object syncRoot = new object();
        private readonly string databaseFile;
        private readonly string imageFolder;
        private readonly string connectionString;

        private int cachedImageCount;
        private int cachedErrorCount;
        private int cachedMissingImageCount;

        public BtwImageLibraryEngine(
            string indexFile,
            string imageFolder)
        {
            databaseFile = Path.ChangeExtension(indexFile, ".db");
            this.imageFolder = imageFolder;

            string dir = Path.GetDirectoryName(databaseFile);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            Directory.CreateDirectory(imageFolder);

            connectionString =
                "Data Source=" +
                databaseFile +
                ";Version=3;foreign keys=true;busy_timeout=30000;";

            InitializeDatabase();
            RebuildStatistics();
        }

        public int BarcodeWorkerCount { get; set; } =
            DefaultBarcodeWorkerCount;

        public int Count
        {
            get
            {
                using (SQLiteConnection c = OpenConnection())
                using (SQLiteCommand cmd =
                    new SQLiteCommand(
                        "SELECT COUNT(*) FROM BtwTemplates;",
                        c))
                    return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        public int ImageCount { get { return cachedImageCount; } }
        public int ErrorCount { get { return cachedErrorCount; } }
        public int MissingImageCount { get { return cachedMissingImageCount; } }

        public int DuplicateBarcodeCount
        {
            get
            {
                using (SQLiteConnection c = OpenConnection())
                using (SQLiteCommand cmd =
                    new SQLiteCommand(
                        "SELECT COUNT(*) FROM (" +
                        "SELECT Barcode FROM BtwTemplates " +
                        "WHERE Barcode IS NOT NULL AND Barcode <> '' " +
                        "GROUP BY Barcode HAVING COUNT(*) > 1);",
                        c))
                    return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        public List<BtwImageLibraryRecord> Search(
            string query,
            string statusFilter)
        {
            query = (query ?? "").Trim();
            statusFilter =
                string.IsNullOrWhiteSpace(statusFilter)
                    ? "All"
                    : statusFilter;

            List<BtwImageLibraryRecord> result =
                new List<BtwImageLibraryRecord>();

            using (SQLiteConnection c = OpenConnection())
            using (SQLiteCommand cmd = c.CreateCommand())
            {
                StringBuilder sql = new StringBuilder(
                    "SELECT FilePath,FileName,ProductName,Barcode," +
                    "ImagePath,Status,ErrorMessage,FileSize," +
                    "LastWriteTicks,LastProcessedUtc " +
                    "FROM BtwTemplates WHERE 1=1 ");

                if (!string.Equals(
                    statusFilter,
                    "All",
                    StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(
                        statusFilter,
                        "Missing Image",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        sql.Append(
                            "AND (ImagePath IS NULL OR ImagePath='' " +
                            "OR NOT EXISTS " +
                            "(SELECT 1 WHERE 1=1)) ");
                    }
                    else if (string.Equals(
                        statusFilter,
                        "Duplicate Barcode",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        sql.Append(
                            "AND Barcode IS NOT NULL AND Barcode<>'' " +
                            "AND Barcode IN (" +
                            "SELECT Barcode FROM BtwTemplates " +
                            "WHERE Barcode IS NOT NULL AND Barcode<>'' " +
                            "GROUP BY Barcode HAVING COUNT(*)>1) ");
                    }
                    else if (string.Equals(
                        statusFilter,
                        "Ready",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        sql.Append(
                            "AND Status LIKE 'Ready%' ");
                    }
                    else
                    {
                        sql.Append("AND Status=@status ");
                        cmd.Parameters.AddWithValue(
                            "@status",
                            statusFilter);
                    }
                }

                if (!string.IsNullOrWhiteSpace(query))
                {
                    sql.Append(
                        "AND (Barcode LIKE @q OR " +
                        "ProductName LIKE @q OR " +
                        "FileName LIKE @q OR " +
                        "FilePath LIKE @q) ");

                    cmd.Parameters.AddWithValue(
                        "@q",
                        "%" + query + "%");
                }

                sql.Append(
                    "ORDER BY ProductName COLLATE NOCASE, " +
                    "FilePath COLLATE NOCASE LIMIT 1000;");

                cmd.CommandText = sql.ToString();

                using (SQLiteDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        BtwImageLibraryRecord record =
                            ReadRecord(r);

                        if (string.Equals(
                            statusFilter,
                            "Missing Image",
                            StringComparison.OrdinalIgnoreCase))
                        {
                            if (!string.IsNullOrWhiteSpace(
                                record.ImagePath) &&
                                File.Exists(record.ImagePath))
                                continue;
                        }

                        result.Add(record);
                    }
                }
            }

            return result;
        }

        public BtwImageLibraryRecord Get(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            using (SQLiteConnection c = OpenConnection())
            using (SQLiteCommand cmd = c.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT FilePath,FileName,ProductName,Barcode," +
                    "ImagePath,Status,ErrorMessage,FileSize," +
                    "LastWriteTicks,LastProcessedUtc " +
                    "FROM BtwTemplates WHERE FilePath=@p;";

                cmd.Parameters.AddWithValue("@p", path);

                using (SQLiteDataReader r = cmd.ExecuteReader())
                    return r.Read() ? ReadRecord(r) : null;
            }
        }

        public void ClearIndex()
        {
            lock (syncRoot)
            {
                using (SQLiteConnection c = OpenConnection())
                using (SQLiteCommand cmd = c.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM BtwTemplates;";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText =
                        "DELETE FROM sqlite_sequence " +
                        "WHERE name='BtwTemplates';";
                    try { cmd.ExecuteNonQuery(); } catch { }
                }
            }

            RebuildStatistics();
        }

        public BtwImageLibraryRefreshResult Refresh(
            IEnumerable<string> includeFolders,
            IEnumerable<string> excludeFolders,
            int dpi,
            Action<BtwImageLibraryProgress> progress,
            CancellationToken cancellationToken)
        {
            List<string> includes =
                NormalizeFolders(includeFolders);
            List<string> excludes =
                NormalizeFolders(excludeFolders);

            if (includes.Count == 0)
                throw new InvalidOperationException(
                    "Add at least one BTW Include Folder.");

            List<string> unavailable =
                includes.Where(
                    x => !Directory.Exists(x)).ToList();

            if (unavailable.Count > 0)
                throw new DirectoryNotFoundException(
                    "These BTW Include Folders are unavailable:\r\n\r\n" +
                    string.Join("\r\n", unavailable));

            List<string> files =
                EnumerateBtwFiles(includes, excludes);

            BtwImageLibraryRefreshResult result =
                new BtwImageLibraryRefreshResult
                {
                    TotalFiles = files.Count
                };

            Dictionary<string, BtwImageLibraryRecord> previous =
                LoadAllRecords();

            HashSet<string> current =
                new HashSet<string>(
                    files,
                    StringComparer.OrdinalIgnoreCase);

            RemoveDeleted(previous, current, result);

            List<ExtractionJob> extractionJobs =
                new List<ExtractionJob>();

            List<BarcodeJob> barcodeJobs =
                new List<BarcodeJob>();

            int scanProcessed = 0;

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
                    scanProcessed++;
                    Report(
                        progress,
                        scanProcessed,
                        files.Count,
                        result,
                        0,
                        0,
                        file);
                    continue;
                }

                BtwImageLibraryRecord old;
                bool exists =
                    previous.TryGetValue(file, out old);

                string expectedImage =
                    GetImagePath(file);

                bool sourceUnchanged =
                    exists &&
                    old.FileSize == info.Length &&
                    old.LastWriteTicks ==
                        info.LastWriteTimeUtc.Ticks;

                bool imageExists =
                    File.Exists(expectedImage);

                if (sourceUnchanged &&
                    imageExists)
                {
                    result.UnchangedFiles++;

                    if (string.IsNullOrWhiteSpace(
                        old.Barcode))
                    {
                        barcodeJobs.Add(
                            new BarcodeJob
                            {
                                FilePath = file,
                                ImagePath = expectedImage,
                                BaseRecord = old
                            });
                    }

                    scanProcessed++;

                    Report(
                        progress,
                        scanProcessed,
                        files.Count,
                        result,
                        result.TotalFiles,
                        barcodeJobs.Count,
                        file);

                    continue;
                }

                if (exists)
                    result.ChangedFiles++;
                else
                    result.NewFiles++;

                // Critical optimization:
                // If the SQLite index is new/empty but the PNG was already
                // generated by the previous library, don't run BarTender again.
                if (imageExists)
                {
                    BtwImageLibraryRecord baseRecord =
                        CreateBaseRecord(
                            file,
                            info,
                            expectedImage,
                            "Ready - Barcode Pending");

                    UpsertRecord(baseRecord);

                    barcodeJobs.Add(
                        new BarcodeJob
                        {
                            FilePath = file,
                            ImagePath = expectedImage,
                            BaseRecord = baseRecord
                        });

                    result.ExistingImagesIndexed++;

                    scanProcessed++;

                    Report(
                        progress,
                        scanProcessed,
                        files.Count,
                        result,
                        extractionJobs.Count,
                        barcodeJobs.Count,
                        file);

                    continue;
                }

                extractionJobs.Add(
                    new ExtractionJob
                    {
                        FilePath = file,
                        FileInfo = info,
                        Previous = old,
                        OutputPath = expectedImage
                    });

                scanProcessed++;

                Report(
                    progress,
                    scanProcessed,
                    files.Count,
                    result,
                    extractionJobs.Count,
                    barcodeJobs.Count,
                    file);
            }

            // Barcode workers can run while BarTender extracts.
            using (BlockingCollection<BarcodeJob> barcodeQueue =
                new BlockingCollection<BarcodeJob>(
                    new ConcurrentQueue<BarcodeJob>()))
            using (BlockingCollection<BtwImageLibraryRecord> writeQueue =
                new BlockingCollection<BtwImageLibraryRecord>(
                    new ConcurrentQueue<BtwImageLibraryRecord>()))
            {
                foreach (BarcodeJob job in barcodeJobs)
                    barcodeQueue.Add(job);

                Task writer =
                    Task.Run(
                        () => RunSqliteWriter(
                            writeQueue,
                            cancellationToken),
                        cancellationToken);

                int barcodeTotal =
                    barcodeJobs.Count +
                    extractionJobs.Count;

                int barcodeProcessed = 0;
                int extractionProcessed = 0;

                int workerCount =
                    Math.Max(
                        1,
                        Math.Min(
                            MaxBarcodeWorkerCount,
                            BarcodeWorkerCount));

                Task[] barcodeWorkers =
                    new Task[workerCount];

                for (int i = 0;
                    i < workerCount;
                    i++)
                {
                    barcodeWorkers[i] =
                        Task.Run(
                            () =>
                                BarcodeWorker(
                                    barcodeQueue,
                                    writeQueue,
                                    cancellationToken,
                                    () =>
                                    {
                                        int done =
                                            Interlocked.Increment(
                                                ref barcodeProcessed);

                                        Report(
                                            progress,
                                            scanProcessed,
                                            files.Count,
                                            result,
                                            extractionProcessed,
                                            done,
                                            "Barcode indexing");
                                    }),
                            cancellationToken);
                }

                // ONE BarTender instance, on ONE thread.
                RunBarTenderExtraction(
                    extractionJobs,
                    dpi,
                    cancellationToken,
                    writeQueue,
                    barcodeQueue,
                    result,
                    () =>
                    {
                        int done =
                            Interlocked.Increment(
                                ref extractionProcessed);

                        Report(
                            progress,
                            scanProcessed,
                            files.Count,
                            result,
                            done,
                            barcodeProcessed,
                            "BarTender extraction");
                    });

                barcodeQueue.CompleteAdding();

                try
                {
                    Task.WaitAll(barcodeWorkers);
                }
                catch (AggregateException)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw;
                }

                writeQueue.CompleteAdding();

                try
                {
                    writer.Wait();
                }
                catch (AggregateException)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw;
                }

                result.BarcodeProcessed =
                    barcodeProcessed;
            }

            RebuildStatistics();

            result.DuplicateBarcodes =
                DuplicateBarcodeCount;

            return result;
        }

        private void RunBarTenderExtraction(
            List<ExtractionJob> jobs,
            int dpi,
            CancellationToken cancellationToken,
            BlockingCollection<BtwImageLibraryRecord> writeQueue,
            BlockingCollection<BarcodeJob> barcodeQueue,
            BtwImageLibraryRefreshResult result,
            Action extractionProgress)
        {
            if (jobs.Count == 0)
                return;

            dynamic btApp = null;

            try
            {
                Type btType =
                    Type.GetTypeFromProgID(
                        "BarTender.Application");

                if (btType == null)
                    throw new InvalidOperationException(
                        "BarTender.Application could not be started.");

                // Deliberately ONE COM instance.
                btApp =
                    Activator.CreateInstance(btType);

                // Match the known-working BarTender workflow.
                btApp.Visible = true;

                foreach (ExtractionJob job in jobs)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        Directory.CreateDirectory(
                            Path.GetDirectoryName(
                                job.OutputPath));

                        ExportOneBtw(
                            btApp,
                            job.FilePath,
                            job.OutputPath,
                            dpi,
                            cancellationToken);

                        BtwImageLibraryRecord record =
                            CreateBaseRecord(
                                job.FilePath,
                                job.FileInfo,
                                job.OutputPath,
                                "Ready - Barcode Pending");

                        writeQueue.Add(
                            record,
                            cancellationToken);

                        barcodeQueue.Add(
                            new BarcodeJob
                            {
                                FilePath = job.FilePath,
                                ImagePath = job.OutputPath,
                                BaseRecord = record
                            },
                            cancellationToken);

                        result.ImagesCreated++;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        BtwImageLibraryRecord error =
                            CreateErrorRecord(
                                job.FilePath,
                                job.FileInfo,
                                ex);

                        writeQueue.Add(
                            error,
                            cancellationToken);

                        result.Errors++;
                    }

                    extractionProgress();
                }
            }
            finally
            {
                if (btApp != null)
                {
                    try
                    {
                        btApp.Quit(2);
                    }
                    catch
                    {
                    }

                    btApp = null;
                }
            }
        }

        private void BarcodeWorker(
            BlockingCollection<BarcodeJob> barcodeQueue,
            BlockingCollection<BtwImageLibraryRecord> writeQueue,
            CancellationToken cancellationToken,
            Action completed)
        {
            try
            {
                foreach (BarcodeJob job in
                    barcodeQueue.GetConsumingEnumerable(
                        cancellationToken))
                {
                    BtwImageLibraryRecord record =
                        job.BaseRecord ?? Get(job.FilePath);

                    if (record == null)
                        continue;

                    try
                    {
                        record.Barcode =
                            DecodeBarcode(job.ImagePath);

                        record.Status =
                            string.IsNullOrWhiteSpace(
                                record.Barcode)
                                ? "Ready - Barcode Not Detected"
                                : "Ready";

                        record.ErrorMessage = "";
                    }
                    catch (Exception ex)
                    {
                        record.Status = "Error";
                        record.ErrorMessage =
                            ex.Message;
                    }

                    record.LastProcessedUtc =
                        DateTime.UtcNow.ToString("o");

                    writeQueue.Add(
                        record,
                        cancellationToken);

                    completed();
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
        }

        private void RunSqliteWriter(
            BlockingCollection<BtwImageLibraryRecord> writeQueue,
            CancellationToken cancellationToken)
        {
            using (SQLiteConnection c = OpenConnection())
            {
                SQLiteTransaction tx = c.BeginTransaction();
                int batch = 0;

                try
                {
                    foreach (BtwImageLibraryRecord record in
                        writeQueue.GetConsumingEnumerable(
                            cancellationToken))
                    {
                        UpsertRecord(c, tx, record);
                        batch++;

                        if (batch >= SqliteBatchSize)
                        {
                            tx.Commit();
                            tx.Dispose();

                            tx = c.BeginTransaction();
                            batch = 0;
                        }
                    }

                    if (batch > 0)
                        tx.Commit();
                }
                finally
                {
                    try { tx.Dispose(); } catch { }
                }
            }
        }

        private void UpsertRecord(
            BtwImageLibraryRecord record)
        {
            using (SQLiteConnection c = OpenConnection())
            using (SQLiteTransaction tx =
                c.BeginTransaction())
            {
                UpsertRecord(c, tx, record);
                tx.Commit();
            }
        }

        private static void UpsertRecord(
            SQLiteConnection c,
            SQLiteTransaction tx,
            BtwImageLibraryRecord r)
        {
            using (SQLiteCommand cmd = c.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText =
                    "INSERT INTO BtwTemplates " +
                    "(FilePath,FileName,ProductName,Barcode," +
                    "ImagePath,Status,ErrorMessage,FileSize," +
                    "LastWriteTicks,LastProcessedUtc) " +
                    "VALUES " +
                    "(@FilePath,@FileName,@ProductName,@Barcode," +
                    "@ImagePath,@Status,@ErrorMessage,@FileSize," +
                    "@LastWriteTicks,@LastProcessedUtc) " +
                    "ON CONFLICT(FilePath) DO UPDATE SET " +
                    "FileName=excluded.FileName," +
                    "ProductName=excluded.ProductName," +
                    "Barcode=excluded.Barcode," +
                    "ImagePath=excluded.ImagePath," +
                    "Status=excluded.Status," +
                    "ErrorMessage=excluded.ErrorMessage," +
                    "FileSize=excluded.FileSize," +
                    "LastWriteTicks=excluded.LastWriteTicks," +
                    "LastProcessedUtc=excluded.LastProcessedUtc;";

                AddParameters(cmd, r);
                cmd.ExecuteNonQuery();
            }
        }

        private static void AddParameters(
            SQLiteCommand cmd,
            BtwImageLibraryRecord r)
        {
            cmd.Parameters.AddWithValue(
                "@FilePath", r.FilePath ?? "");
            cmd.Parameters.AddWithValue(
                "@FileName", r.FileName ?? "");
            cmd.Parameters.AddWithValue(
                "@ProductName", r.ProductName ?? "");
            cmd.Parameters.AddWithValue(
                "@Barcode", r.Barcode ?? "");
            cmd.Parameters.AddWithValue(
                "@ImagePath", r.ImagePath ?? "");
            cmd.Parameters.AddWithValue(
                "@Status", r.Status ?? "");
            cmd.Parameters.AddWithValue(
                "@ErrorMessage", r.ErrorMessage ?? "");
            cmd.Parameters.AddWithValue(
                "@FileSize", r.FileSize);
            cmd.Parameters.AddWithValue(
                "@LastWriteTicks", r.LastWriteTicks);
            cmd.Parameters.AddWithValue(
                "@LastProcessedUtc",
                r.LastProcessedUtc ?? "");
        }

        private Dictionary<string, BtwImageLibraryRecord>
            LoadAllRecords()
        {
            Dictionary<string, BtwImageLibraryRecord> result =
                new Dictionary<string, BtwImageLibraryRecord>(
                    StringComparer.OrdinalIgnoreCase);

            using (SQLiteConnection c = OpenConnection())
            using (SQLiteCommand cmd =
                new SQLiteCommand(
                    "SELECT FilePath,FileName,ProductName,Barcode," +
                    "ImagePath,Status,ErrorMessage,FileSize," +
                    "LastWriteTicks,LastProcessedUtc " +
                    "FROM BtwTemplates;",
                    c))
            using (SQLiteDataReader r =
                cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    BtwImageLibraryRecord record =
                        ReadRecord(r);

                    result[record.FilePath] =
                        record;
                }
            }

            return result;
        }

        private void RemoveDeleted(
            Dictionary<string, BtwImageLibraryRecord> previous,
            HashSet<string> current,
            BtwImageLibraryRefreshResult result)
        {
            List<string> deleted =
                previous.Keys
                    .Where(x => !current.Contains(x))
                    .ToList();

            if (deleted.Count == 0)
                return;

            using (SQLiteConnection c = OpenConnection())
            using (SQLiteTransaction tx =
                c.BeginTransaction())
            using (SQLiteCommand cmd = c.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText =
                    "DELETE FROM BtwTemplates " +
                    "WHERE FilePath=@p;";

                SQLiteParameter p =
                    cmd.Parameters.Add(
                        "@p",
                        System.Data.DbType.String);

                foreach (string path in deleted)
                {
                    p.Value = path;
                    cmd.ExecuteNonQuery();
                    result.RemovedFiles++;
                }

                tx.Commit();
            }
        }

        private BtwImageLibraryRecord
            CreateBaseRecord(
                string file,
                FileInfo info,
                string imagePath,
                string status)
        {
            return new BtwImageLibraryRecord
            {
                FilePath = file,
                FileName = Path.GetFileName(file),
                ProductName =
                    Path.GetFileNameWithoutExtension(file),
                Barcode = "",
                ImagePath = imagePath,
                Status = status,
                ErrorMessage = "",
                FileSize = info.Length,
                LastWriteTicks =
                    info.LastWriteTimeUtc.Ticks,
                LastProcessedUtc =
                    DateTime.UtcNow.ToString("o")
            };
        }

        private static BtwImageLibraryRecord
            CreateErrorRecord(
                string file,
                FileInfo info,
                Exception ex)
        {
            return new BtwImageLibraryRecord
            {
                FilePath = file,
                FileName = Path.GetFileName(file),
                ProductName =
                    Path.GetFileNameWithoutExtension(file),
                Barcode = "",
                ImagePath = "",
                Status = "Error",
                ErrorMessage =
                    ex == null
                        ? "Unknown error."
                        : ex.Message,
                FileSize = info.Length,
                LastWriteTicks =
                    info.LastWriteTimeUtc.Ticks,
                LastProcessedUtc =
                    DateTime.UtcNow.ToString("o")
            };
        }

        private static void ExportOneBtw(
            dynamic btApp,
            string btwPath,
            string outputPath,
            int dpi,
            CancellationToken cancellationToken)
        {
            dynamic btFormat = null;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                btFormat =
                    btApp.Formats.Open(
                        btwPath,
                        false,
                        "");

                if (btFormat == null)
                    throw new InvalidOperationException(
                        "BarTender could not open the BTW template.");

                cancellationToken.ThrowIfCancellationRequested();

                btFormat.ExportToFile(
                    outputPath,
                    "PNG",
                    4,
                    dpi,
                    2);
            }
            finally
            {
                if (btFormat != null)
                {
                    try
                    {
                        btFormat.Close(2);
                    }
                    catch
                    {
                    }

                    btFormat = null;
                }
            }
        }

        private static string DecodeBarcode(
            string file)
        {
            try
            {
                using (Bitmap bmp =
                    new Bitmap(file))
                {
                    BarcodeReader fast =
                        new BarcodeReader
                        {
                            AutoRotate = true,
                            Options =
                                new DecodingOptions
                                {
                                    TryHarder = false
                                }
                        };

                    Result result =
                        fast.Decode(bmp);

                    if (result != null &&
                        !string.IsNullOrWhiteSpace(
                            result.Text))
                        return result.Text.Trim();

                    BarcodeReader thorough =
                        new BarcodeReader
                        {
                            AutoRotate = true,
                            Options =
                                new DecodingOptions
                                {
                                    TryHarder = true
                                }
                        };

                    result =
                        thorough.Decode(bmp);

                    return result == null
                        ? ""
                        : (result.Text ?? "").Trim();
                }
            }
            catch
            {
                return "";
            }
        }

        private static List<string> EnumerateBtwFiles(
            IEnumerable<string> includeFolders,
            IEnumerable<string> excludeFolders)
        {
            HashSet<string> excluded =
                new HashSet<string>(
                    NormalizeFolders(excludeFolders),
                    StringComparer.OrdinalIgnoreCase);

            HashSet<string> files =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (string root in
                NormalizeFolders(includeFolders))
            {
                if (!Directory.Exists(root))
                    continue;

                Stack<string> stack =
                    new Stack<string>();

                stack.Push(root);

                while (stack.Count > 0)
                {
                    string dir = stack.Pop();

                    if (IsExcluded(dir, excluded))
                        continue;

                    try
                    {
                        foreach (string file in
                            Directory.GetFiles(
                                dir,
                                "*.btw",
                                SearchOption.TopDirectoryOnly))
                        {
                            files.Add(
                                Path.GetFullPath(file));
                        }
                    }
                    catch
                    {
                    }

                    try
                    {
                        foreach (string child in
                            Directory.GetDirectories(
                                dir,
                                "*",
                                SearchOption.TopDirectoryOnly))
                        {
                            if (!IsExcluded(child, excluded))
                                stack.Push(child);
                        }
                    }
                    catch
                    {
                    }
                }
            }

            return files
                .OrderBy(
                    x => x,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool IsExcluded(
            string path,
            HashSet<string> excluded)
        {
            string full =
                Path.GetFullPath(path).TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);

            foreach (string root in excluded)
            {
                string e =
                    root.TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar);

                if (string.Equals(
                        full,
                        e,
                        StringComparison.OrdinalIgnoreCase) ||
                    full.StartsWith(
                        e + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static List<string> NormalizeFolders(
            IEnumerable<string> folders)
        {
            return (folders ?? Enumerable.Empty<string>())
                .Where(
                    x => !string.IsNullOrWhiteSpace(x))
                .Select(
                    x =>
                    {
                        try
                        {
                            return Path.GetFullPath(
                                x.Trim());
                        }
                        catch
                        {
                            return "";
                        }
                    })
                .Where(
                    x => !string.IsNullOrWhiteSpace(x))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void Report(
            Action<BtwImageLibraryProgress> progress,
            int scanProcessed,
            int scanTotal,
            BtwImageLibraryRefreshResult result,
            int extractionProcessed,
            int barcodeProcessed,
            string current)
        {
            if (progress == null)
                return;

            progress(
                new BtwImageLibraryProgress
                {
                    Processed = scanProcessed,
                    Total = scanTotal,
                    NewFiles = result.NewFiles,
                    ChangedFiles = result.ChangedFiles,
                    UnchangedFiles =
                        result.UnchangedFiles,
                    ImagesCreated =
                        result.ImagesCreated,
                    Errors = result.Errors,
                    CurrentFile = current,
                    ExtractionProcessed =
                        extractionProcessed,
                    ExtractionTotal = 0,
                    BarcodeProcessed =
                        barcodeProcessed,
                    BarcodeTotal = 0,
                    ExistingImagesIndexed =
                        result.ExistingImagesIndexed
                });
        }

        private static BtwImageLibraryRecord
            ReadRecord(SQLiteDataReader r)
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

        private static string GetString(
            SQLiteDataReader r,
            int index)
        {
            return r.IsDBNull(index)
                ? ""
                : Convert.ToString(r.GetValue(index));
        }

        private static long GetInt64(
            SQLiteDataReader r,
            int index)
        {
            return r.IsDBNull(index)
                ? 0
                : Convert.ToInt64(r.GetValue(index));
        }

        private SQLiteConnection OpenConnection()
        {
            SQLiteConnection c =
                new SQLiteConnection(connectionString);

            c.Open();

            using (SQLiteCommand cmd = c.CreateCommand())
            {
                cmd.CommandText =
                    "PRAGMA journal_mode=WAL;" +
                    "PRAGMA synchronous=NORMAL;" +
                    "PRAGMA temp_store=MEMORY;" +
                    "PRAGMA cache_size=-20000;";
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

                    "CREATE INDEX IF NOT EXISTS IX_BtwTemplates_Barcode " +
                    "ON BtwTemplates(Barcode);" +

                    "CREATE INDEX IF NOT EXISTS IX_BtwTemplates_Status " +
                    "ON BtwTemplates(Status);" +

                    "CREATE INDEX IF NOT EXISTS IX_BtwTemplates_ProductName " +
                    "ON BtwTemplates(ProductName);" +

                    "CREATE INDEX IF NOT EXISTS IX_BtwTemplates_FileName " +
                    "ON BtwTemplates(FileName);" +

                    "CREATE INDEX IF NOT EXISTS IX_BtwTemplates_FilePath " +
                    "ON BtwTemplates(FilePath);";

                cmd.ExecuteNonQuery();
            }
        }

        private void RebuildStatistics()
        {
            int images = 0;
            int errors = 0;
            int missing = 0;

            using (SQLiteConnection c = OpenConnection())
            using (SQLiteCommand cmd =
                new SQLiteCommand(
                    "SELECT ImagePath,Status FROM BtwTemplates;",
                    c))
            using (SQLiteDataReader r =
                cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    string image =
                        GetString(r, 0);
                    string status =
                        GetString(r, 1);

                    if (!string.IsNullOrWhiteSpace(image) &&
                        File.Exists(image))
                        images++;
                    else
                        missing++;

                    if (string.Equals(
                        status,
                        "Error",
                        StringComparison.OrdinalIgnoreCase))
                        errors++;
                }
            }

            cachedImageCount = images;
            cachedErrorCount = errors;
            cachedMissingImageCount = missing;
        }

        private string GetImagePath(
            string btwPath)
        {
            string key;

            using (SHA1 sha =
                SHA1.Create())
            {
                byte[] bytes =
                    Encoding.UTF8.GetBytes(
                        Path.GetFullPath(btwPath)
                            .ToUpperInvariant());

                key =
                    BitConverter.ToString(
                        sha.ComputeHash(bytes))
                    .Replace("-", "")
                    .Substring(0, 20);
            }

            string safeName =
                Path.GetFileNameWithoutExtension(
                    btwPath);

            foreach (char invalid
                in Path.GetInvalidFileNameChars())
                safeName =
                    safeName.Replace(
                        invalid,
                        '_');

            if (string.IsNullOrWhiteSpace(
                safeName))
                safeName = "BTW_Image";

            string folder =
                Path.Combine(
                    imageFolder,
                    key);

            return Path.Combine(
                folder,
                safeName + ".png");
        }

        private sealed class ExtractionJob
        {
            public string FilePath;
            public FileInfo FileInfo;
            public BtwImageLibraryRecord Previous;
            public string OutputPath;
        }

        private sealed class BarcodeJob
        {
            public string FilePath;
            public string ImagePath;
            public BtwImageLibraryRecord BaseRecord;
        }
    }
}
