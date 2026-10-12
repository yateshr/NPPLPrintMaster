using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NPPLPrintMaster
{
    /// <summary>
    /// Loads collage thumbnails on background threads so the workspace opens
    /// immediately and stays responsive with hundreds of images.
    ///
    /// Get() never blocks: it returns the thumbnail when it is ready, or null
    /// and moves that image to the front of the load queue. ThumbnailsReady is
    /// raised on the UI thread (in batches) as thumbnails arrive.
    /// </summary>
    internal sealed class CollageThumbnailCache : IDisposable
    {
        private sealed class Entry
        {
            public Bitmap Thumbnail;
            public Size OriginalSize;
        }

        private sealed class LoadResult
        {
            public string Path;
            public Bitmap Thumbnail;
            public Size OriginalSize;
        }

        // GDI+ serializes image work inside a process, so more threads do
        // not load thumbnails any faster.
        private const int MaxWorkers = 2;

        private readonly int _maxEdge;
        private readonly Timer _deliveryTimer;

        // Touched by the UI thread only.
        private readonly Dictionary<string, Entry> _entries =
            new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

        // Shared with the worker threads; guarded by _gate.
        private readonly object _gate = new object();
        private readonly Stack<string> _urgent = new Stack<string>();
        private readonly Queue<string> _background = new Queue<string>();
        private readonly HashSet<string> _queuedUrgent =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _queuedBackground =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _started =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private int _activeWorkers;
        private bool _disposed;

        private readonly ConcurrentQueue<LoadResult> _completed =
            new ConcurrentQueue<LoadResult>();

        public event EventHandler ThumbnailsReady;

        public CollageThumbnailCache(int maxEdge)
        {
            _maxEdge = Math.Max(64, maxEdge);

            _deliveryTimer = new Timer { Interval = 40 };
            _deliveryTimer.Tick += (s, e) => DeliverCompleted();
        }

        // Thumbnail size that keeps memory bounded for very large sets.
        public static int ChooseMaxEdge(int imageCount)
        {
            return imageCount <= 150
                ? 400
                : imageCount <= 400
                    ? 300
                    : 200;
        }

        public Bitmap Get(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            Entry entry;
            if (_entries.TryGetValue(path, out entry))
                return entry.Thumbnail;

            Request(path, true);
            return null;
        }

        // True once loading finished without producing a thumbnail.
        public bool IsUnreadable(string path)
        {
            Entry entry;

            return !string.IsNullOrWhiteSpace(path) &&
                   _entries.TryGetValue(path, out entry) &&
                   entry.Thumbnail == null;
        }

        public bool TryGetOriginalSize(string path, out Size size)
        {
            Entry entry;

            if (!string.IsNullOrWhiteSpace(path) &&
                _entries.TryGetValue(path, out entry) &&
                entry.Thumbnail != null)
            {
                size = entry.OriginalSize;
                return true;
            }

            size = Size.Empty;
            return false;
        }

        public void Preload(IEnumerable<string> paths)
        {
            if (paths == null)
                return;

            foreach (string path in paths)
                Request(path, false);
        }

        private void Request(string path, bool urgent)
        {
            lock (_gate)
            {
                if (_disposed || _started.Contains(path))
                    return;

                if (urgent)
                {
                    // Every repaint asks again; queue each image only once.
                    if (!_queuedUrgent.Add(path))
                        return;

                    _urgent.Push(path);
                }
                else
                {
                    if (!_queuedBackground.Add(path))
                        return;

                    _background.Enqueue(path);
                }

                while (_activeWorkers < MaxWorkers &&
                       _activeWorkers < _urgent.Count + _background.Count)
                {
                    _activeWorkers++;
                    Task.Run((Action)WorkerLoop);
                }
            }

            if (!_deliveryTimer.Enabled)
                _deliveryTimer.Start();
        }

        private void WorkerLoop()
        {
            while (true)
            {
                string path = null;

                lock (_gate)
                {
                    while (!_disposed && path == null)
                    {
                        if (_urgent.Count > 0)
                            path = _urgent.Pop();
                        else if (_background.Count > 0)
                            path = _background.Dequeue();
                        else
                            break;

                        if (!_started.Add(path))
                            path = null;
                    }

                    if (path == null)
                    {
                        _activeWorkers--;
                        return;
                    }
                }

                LoadResult result = Load(path);

                lock (_gate)
                {
                    if (_disposed)
                    {
                        if (result.Thumbnail != null)
                            result.Thumbnail.Dispose();

                        _activeWorkers--;
                        return;
                    }

                    _completed.Enqueue(result);
                }
            }
        }

        private LoadResult Load(string path)
        {
            LoadResult result = new LoadResult { Path = path };

            try
            {
                using (FileStream stream =
                    new FileStream(
                        path,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete))
                using (Image source = Image.FromStream(stream, false, false))
                {
                    result.OriginalSize = source.Size;

                    double scale =
                        Math.Min(
                            1.0,
                            _maxEdge /
                            (double)Math.Max(source.Width, source.Height));

                    int width =
                        Math.Max(1, (int)Math.Round(source.Width * scale));

                    int height =
                        Math.Max(1, (int)Math.Round(source.Height * scale));

                    Bitmap thumbnail =
                        new Bitmap(
                            width,
                            height,
                            PixelFormat.Format32bppPArgb);

                    try
                    {
                        if (scale < 0.4)
                        {
                            // High-quality scaling costs time in proportion
                            // to the source size. A large export is first
                            // reduced quickly to twice the thumbnail size,
                            // which more than halves the time per image.
                            using (Bitmap half =
                                new Bitmap(
                                    width * 2,
                                    height * 2,
                                    PixelFormat.Format32bppPArgb))
                            {
                                Scale(source, half, InterpolationMode.Bilinear);
                                Scale(half, thumbnail, InterpolationMode.HighQualityBicubic);
                            }
                        }
                        else
                        {
                            Scale(source, thumbnail, InterpolationMode.HighQualityBicubic);
                        }

                        result.Thumbnail = thumbnail;
                    }
                    catch
                    {
                        thumbnail.Dispose();
                        throw;
                    }
                }
            }
            catch
            {
                // Unreadable image: recorded without a thumbnail so the
                // views can show a placeholder instead of retrying forever.
                result.Thumbnail = null;
            }

            return result;
        }

        private static void Scale(
            Image source,
            Bitmap target,
            InterpolationMode interpolation)
        {
            using (Graphics g = Graphics.FromImage(target))
            using (ImageAttributes attributes = new ImageAttributes())
            {
                // Transparent images are shown on white, exactly as they
                // will print on the PDF page.
                g.Clear(Color.White);
                g.InterpolationMode = interpolation;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                attributes.SetWrapMode(WrapMode.TileFlipXY);

                g.DrawImage(
                    source,
                    new Rectangle(0, 0, target.Width, target.Height),
                    0,
                    0,
                    source.Width,
                    source.Height,
                    GraphicsUnit.Pixel,
                    attributes);
            }
        }

        private void DeliverCompleted()
        {
            bool any = false;
            LoadResult result;

            while (_completed.TryDequeue(out result))
            {
                _entries[result.Path] =
                    new Entry
                    {
                        Thumbnail = result.Thumbnail,
                        OriginalSize = result.OriginalSize
                    };

                any = true;
            }

            bool idle;

            lock (_gate)
            {
                idle =
                    _activeWorkers == 0 &&
                    _urgent.Count == 0 &&
                    _background.Count == 0;
            }

            if (idle && _completed.IsEmpty)
                _deliveryTimer.Stop();

            if (any)
            {
                EventHandler handler = ThumbnailsReady;

                if (handler != null)
                    handler(this, EventArgs.Empty);
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                    return;

                _disposed = true;
                _urgent.Clear();
                _background.Clear();
            }

            _deliveryTimer.Stop();
            _deliveryTimer.Dispose();

            LoadResult result;
            while (_completed.TryDequeue(out result))
            {
                if (result.Thumbnail != null)
                    result.Thumbnail.Dispose();
            }

            foreach (Entry entry in _entries.Values)
            {
                if (entry.Thumbnail != null)
                    entry.Thumbnail.Dispose();
            }

            _entries.Clear();
        }
    }
}
