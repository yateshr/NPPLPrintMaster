using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NPPLPrintMaster
{
    internal sealed class CollageArrangementSnapshot
    {
        public int Columns;
        public int Rows;
        public string[] Images;
        public string[] Slots;
        public Dictionary<string, string> Remarks;
        public Dictionary<string, string> Notes;
    }

    /// <summary>
    /// The arrangement being edited in the collage workspace.
    ///
    /// Slots are the grid cells of every PDF page in reading order. A null
    /// slot is an empty cell. An image that is in no slot is "unplaced": it
    /// stays in the library and is left out of the PDF.
    /// </summary>
    internal sealed class CollageArrangementModel
    {
        private readonly List<string> _images;
        private readonly HashSet<string> _imageSet;
        private readonly List<string> _slots = new List<string>();
        private readonly Dictionary<string, int> _slotByPath =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _remarks =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _notes =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private int _updateDepth;
        private bool _changedDuringUpdate;

        // The grid and everything else about the sheet. The grid is changed
        // through SetGrid so the cells are re-flowed with it.
        public CollagePageSetup Page { get; private set; }

        public int CellsPerPage
        {
            get { return Page.Columns * Page.Rows; }
        }

        // Goes up whenever images are added to or removed from the collage.
        public int ImageSetVersion { get; private set; }

        public event EventHandler Changed;

        public CollageArrangementModel(
            IList<string> orderedImages,
            CollagePageSetup page,
            IList<string> initialSlots,
            IDictionary<string, string> initialCaptions,
            IDictionary<string, string> initialCustomNames,
            IDictionary<string, string> initialNotes)
        {
            Page = page;

            _images = orderedImages
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            _imageSet =
                new HashSet<string>(
                    _images,
                    StringComparer.OrdinalIgnoreCase);

            if (initialSlots == null)
            {
                _slots.AddRange(_images);
            }
            else
            {
                // Keep a saved arrangement exactly as it was saved. Images
                // that were left unplaced (or were added to the source later)
                // stay unplaced instead of being pushed back onto a page.
                HashSet<string> seen =
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (string path in initialSlots)
                {
                    bool usable =
                        !string.IsNullOrWhiteSpace(path) &&
                        _imageSet.Contains(path) &&
                        seen.Add(path);

                    _slots.Add(usable ? path : null);
                }
            }

            // A saved arrangement may have been made for another grid; the
            // cells keep their order and simply flow into this one.
            TrimTrailingEmptyCells();
            PadToWholePages();
            Reindex();

            LoadRemarks(initialCustomNames);
            LoadRemarks(initialCaptions);

            if (initialNotes != null)
            {
                foreach (KeyValuePair<string, string> pair in initialNotes)
                {
                    if (Contains(pair.Key) &&
                        !string.IsNullOrWhiteSpace(pair.Value))
                    {
                        _notes[pair.Key] = pair.Value.Trim();
                    }
                }
            }
        }

        public IList<string> Images
        {
            get { return _images; }
        }

        public int SlotCount
        {
            get { return _slots.Count; }
        }

        public int PageCount
        {
            get { return _slots.Count / CellsPerPage; }
        }

        public int PlacedCount
        {
            get { return _slotByPath.Count; }
        }

        public int UnplacedCount
        {
            get { return _images.Count - _slotByPath.Count; }
        }

        public bool Contains(string path)
        {
            return !string.IsNullOrWhiteSpace(path) &&
                   _imageSet.Contains(path);
        }

        public string GetSlot(int slot)
        {
            return slot >= 0 && slot < _slots.Count
                ? _slots[slot]
                : null;
        }

        public int IndexOf(string path)
        {
            int slot;

            return !string.IsNullOrWhiteSpace(path) &&
                   _slotByPath.TryGetValue(path, out slot)
                ? slot
                : -1;
        }

        public bool IsPlaced(string path)
        {
            return IndexOf(path) >= 0;
        }

        public int CountOnPage(int page)
        {
            int count = 0;
            int start = page * CellsPerPage;

            for (int i = start; i < start + CellsPerPage && i < _slots.Count; i++)
            {
                if (_slots[i] != null)
                    count++;
            }

            return count;
        }

        // ------------------------------------------------------------
        // Remarks
        // ------------------------------------------------------------
        public static string GetFileTitle(string path)
        {
            return Path.GetFileNameWithoutExtension(path) ?? string.Empty;
        }

        // The custom text typed by the user, or null when the image still
        // uses its filename.
        public string GetRemark(string path)
        {
            string remark;

            return !string.IsNullOrWhiteSpace(path) &&
                   _remarks.TryGetValue(path, out remark)
                ? remark
                : null;
        }

        public string GetEffectiveRemark(string path)
        {
            return GetRemark(path) ?? GetFileTitle(path);
        }

        public bool SetRemark(string path, string text)
        {
            if (!Contains(path))
                return false;

            string value = (text ?? string.Empty).Trim();
            string current = GetRemark(path);

            bool isDefault =
                value.Length == 0 ||
                string.Equals(
                    value,
                    GetFileTitle(path),
                    StringComparison.Ordinal);

            if (isDefault)
            {
                if (current == null)
                    return false;

                _remarks.Remove(path);
            }
            else
            {
                if (string.Equals(current, value, StringComparison.Ordinal))
                    return false;

                _remarks[path] = value;
            }

            OnChanged();
            return true;
        }

        // The additional remark printed on its own line below the remark,
        // or null when there is none.
        public string GetNote(string path)
        {
            string note;

            return !string.IsNullOrWhiteSpace(path) &&
                   _notes.TryGetValue(path, out note)
                ? note
                : null;
        }

        public bool SetNote(string path, string text)
        {
            if (!Contains(path))
                return false;

            string value = (text ?? string.Empty).Trim();
            string current = GetNote(path);

            if (value.Length == 0)
            {
                if (current == null)
                    return false;

                _notes.Remove(path);
            }
            else
            {
                if (string.Equals(current, value, StringComparison.Ordinal))
                    return false;

                _notes[path] = value;
            }

            OnChanged();
            return true;
        }

        private void LoadRemarks(IDictionary<string, string> source)
        {
            if (source == null)
                return;

            foreach (KeyValuePair<string, string> pair in source)
            {
                if (!Contains(pair.Key) ||
                    string.IsNullOrWhiteSpace(pair.Value))
                    continue;

                string value = pair.Value.Trim();

                if (string.Equals(
                    value,
                    GetFileTitle(pair.Key),
                    StringComparison.Ordinal))
                {
                    _remarks.Remove(pair.Key);
                }
                else
                {
                    _remarks[pair.Key] = value;
                }
            }
        }

        // ------------------------------------------------------------
        // Placing images
        // ------------------------------------------------------------

        // Drops one or more images onto a cell and returns the cell the
        // first image ended up in.
        //
        // Swap mode:   the dropped image takes the cell. If it came from
        //              another cell the two trade places; if it came from the
        //              library the previous occupant becomes unplaced.
        // Insert mode: the dropped image takes the cell and the images in the
        //              way shift along by one, towards the cell it left (or
        //              forward into the next empty cell when it came from the
        //              library). Nothing is ever unplaced.
        public int Place(IList<string> paths, int targetSlot, bool insert)
        {
            List<string> incoming = Normalize(paths);

            if (incoming.Count == 0 || targetSlot < 0)
                return -1;

            EnsureSlotExists(targetSlot);

            for (int i = 0; i < incoming.Count; i++)
            {
                string path = incoming[i];
                int source = FindSlot(path);

                if (i == 0)
                {
                    if (insert)
                        InsertAt(path, source, targetSlot);
                    else
                        SwapInto(path, source, targetSlot);

                    continue;
                }

                // The rest of a multi-image drop follows the first image.
                if (source >= 0)
                    _slots[source] = null;

                int after = FindSlot(incoming[i - 1]) + 1;

                if (insert)
                {
                    EnsureSlotExists(after);
                    InsertAt(path, -1, after);
                }
                else
                {
                    _slots[NextEmptySlot(after, false)] = path;
                }
            }

            AfterMutation();
            return IndexOf(incoming[0]);
        }

        // Puts unplaced images into empty cells, starting at startSlot and
        // wrapping to the first page before a new page is added.
        public int PlaceInNextEmpty(IList<string> paths, int startSlot)
        {
            List<string> incoming =
                Normalize(paths)
                    .Where(p => FindSlot(p) < 0)
                    .ToList();

            if (incoming.Count == 0)
                return -1;

            int first = -1;
            int cursor = Math.Max(0, startSlot);

            foreach (string path in incoming)
            {
                int slot = NextEmptySlot(cursor, true);
                _slots[slot] = path;

                if (first < 0)
                    first = slot;

                cursor = slot + 1;
            }

            AfterMutation();
            return first;
        }

        public int Unplace(IEnumerable<string> paths)
        {
            int removed = 0;

            foreach (string path in Normalize(paths))
            {
                int slot = FindSlot(path);

                if (slot < 0)
                    continue;

                _slots[slot] = null;
                removed++;
            }

            if (removed > 0)
                AfterMutation();

            return removed;
        }

        public int FillEmptyCells()
        {
            List<string> unplaced =
                _images
                    .Where(p => !_slotByPath.ContainsKey(p))
                    .ToList();

            if (unplaced.Count == 0)
                return 0;

            int cursor = 0;

            foreach (string path in unplaced)
            {
                int slot = NextEmptySlot(cursor, false);
                _slots[slot] = path;
                cursor = slot + 1;
            }

            AfterMutation();
            return unplaced.Count;
        }

        public void CloseGaps()
        {
            List<string> placed =
                _slots
                    .Where(p => p != null)
                    .ToList();

            _slots.Clear();
            _slots.AddRange(placed);
            AfterMutation();
        }

        public void ResetToDefaultOrder()
        {
            _slots.Clear();
            _slots.AddRange(_images);
            AfterMutation();
        }

        public void ClearAll()
        {
            for (int i = 0; i < _slots.Count; i++)
                _slots[i] = null;

            AfterMutation();
        }

        // ------------------------------------------------------------
        // Images in the collage
        // ------------------------------------------------------------

        // Adds images that are not in the collage yet and returns them.
        // They start unplaced.
        public List<string> AddImages(IEnumerable<string> paths)
        {
            List<string> added = new List<string>();

            if (paths == null)
                return added;

            foreach (string path in paths)
            {
                if (string.IsNullOrWhiteSpace(path) || !_imageSet.Add(path))
                    continue;

                _images.Add(path);
                added.Add(path);
            }

            if (added.Count > 0)
            {
                ImageSetVersion++;
                AfterMutation();
            }

            return added;
        }

        // Takes images out of the collage altogether, together with their
        // cells and remarks.
        public int RemoveImages(IEnumerable<string> paths)
        {
            List<string> leaving = Normalize(paths);

            if (leaving.Count == 0)
                return 0;

            foreach (string path in leaving)
            {
                int slot = FindSlot(path);

                if (slot >= 0)
                    _slots[slot] = null;

                _imageSet.Remove(path);
                _remarks.Remove(path);
                _notes.Remove(path);
            }

            _images.RemoveAll(p => !_imageSet.Contains(p));

            ImageSetVersion++;
            AfterMutation();
            return leaving.Count;
        }

        // ------------------------------------------------------------
        // Grid
        // ------------------------------------------------------------

        // Changes how many columns and rows a page has. The images keep
        // their order and flow into the new grid; nothing is unplaced.
        public bool SetGrid(int columns, int rows)
        {
            columns = Math.Max(1, Math.Min(CollagePageSetup.MaxColumns, columns));
            rows = Math.Max(1, Math.Min(CollagePageSetup.MaxRows, rows));

            if (columns == Page.Columns && rows == Page.Rows)
                return false;

            Page.Columns = columns;
            Page.Rows = rows;

            TrimTrailingEmptyCells();
            AfterMutation();
            return true;
        }

        // ------------------------------------------------------------
        // Pages
        // ------------------------------------------------------------
        public int AddPage()
        {
            AppendPage();
            AfterMutation();
            return PageCount - 1;
        }

        public void InsertPage(int beforePage)
        {
            int page = Math.Max(0, Math.Min(PageCount, beforePage));

            _slots.InsertRange(
                page * CellsPerPage,
                new string[CellsPerPage]);

            AfterMutation();
        }

        // Removes the page; any images on it become unplaced.
        public void DeletePage(int page)
        {
            if (page < 0 || page >= PageCount)
                return;

            _slots.RemoveRange(page * CellsPerPage, CellsPerPage);
            AfterMutation();
        }

        public void ClearPage(int page)
        {
            if (page < 0 || page >= PageCount)
                return;

            int start = page * CellsPerPage;

            for (int i = start; i < start + CellsPerPage; i++)
                _slots[i] = null;

            AfterMutation();
        }

        public int RemoveEmptyPages()
        {
            int removed = 0;

            for (int page = PageCount - 1; page >= 0; page--)
            {
                if (CountOnPage(page) > 0)
                    continue;

                _slots.RemoveRange(page * CellsPerPage, CellsPerPage);
                removed++;
            }

            AfterMutation();
            return removed;
        }

        // ------------------------------------------------------------
        // Undo and results
        // ------------------------------------------------------------
        public CollageArrangementSnapshot Capture()
        {
            return new CollageArrangementSnapshot
            {
                Columns = Page.Columns,
                Rows = Page.Rows,
                Images = _images.ToArray(),
                Slots = _slots.ToArray(),
                Remarks =
                    new Dictionary<string, string>(
                        _remarks,
                        StringComparer.OrdinalIgnoreCase),
                Notes =
                    new Dictionary<string, string>(
                        _notes,
                        StringComparer.OrdinalIgnoreCase)
            };
        }

        public void Restore(CollageArrangementSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            Page.Columns = snapshot.Columns;
            Page.Rows = snapshot.Rows;

            if (!snapshot.Images.SequenceEqual(_images))
            {
                _images.Clear();
                _images.AddRange(snapshot.Images);

                _imageSet.Clear();
                _imageSet.UnionWith(snapshot.Images);

                ImageSetVersion++;
            }

            _slots.Clear();
            _slots.AddRange(snapshot.Slots);

            _remarks.Clear();
            foreach (KeyValuePair<string, string> pair in snapshot.Remarks)
                _remarks[pair.Key] = pair.Value;

            _notes.Clear();
            foreach (KeyValuePair<string, string> pair in snapshot.Notes)
                _notes[pair.Key] = pair.Value;

            AfterMutation();
        }

        // Trailing empty pages are dropped so the PDF never ends with
        // blank sheets.
        public List<string> BuildResultSlots()
        {
            List<string> result = new List<string>(_slots);

            while (result.Count >= CellsPerPage &&
                   result
                       .Skip(result.Count - CellsPerPage)
                       .All(p => p == null))
            {
                result.RemoveRange(
                    result.Count - CellsPerPage,
                    CellsPerPage);
            }

            return result;
        }

        public Dictionary<string, string> BuildResultRemarks()
        {
            return new Dictionary<string, string>(
                _remarks,
                StringComparer.OrdinalIgnoreCase);
        }

        public Dictionary<string, string> BuildResultNotes()
        {
            return new Dictionary<string, string>(
                _notes,
                StringComparer.OrdinalIgnoreCase);
        }

        // ------------------------------------------------------------
        // Internals
        // ------------------------------------------------------------
        private List<string> Normalize(IEnumerable<string> paths)
        {
            return paths == null
                ? new List<string>()
                : paths
                    .Where(Contains)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
        }

        // Used while a mutation is in progress, when the path index is stale.
        private int FindSlot(string path)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (string.Equals(
                    _slots[i],
                    path,
                    StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private void SwapInto(string path, int source, int target)
        {
            if (source == target)
                return;

            string occupant = _slots[target];
            _slots[target] = path;

            if (source >= 0)
                _slots[source] = occupant;
        }

        private void InsertAt(string path, int source, int target)
        {
            if (source == target)
                return;

            if (source >= 0)
                _slots[source] = null;

            if (_slots[target] == null)
            {
                _slots[target] = path;
                return;
            }

            if (source >= 0 && source < target)
            {
                // Moved forward: the images in between slide back into the
                // cell it left.
                int hole = target - 1;

                while (_slots[hole] != null)
                    hole--;

                for (int i = hole; i < target; i++)
                    _slots[i] = _slots[i + 1];

                _slots[target] = path;
                return;
            }

            int empty = target + 1;

            while (empty < _slots.Count && _slots[empty] != null)
                empty++;

            if (empty >= _slots.Count)
                AppendPage();

            for (int i = empty; i > target; i--)
                _slots[i] = _slots[i - 1];

            _slots[target] = path;
        }

        private int NextEmptySlot(int from, bool wrap)
        {
            for (int i = Math.Max(0, from); i < _slots.Count; i++)
            {
                if (_slots[i] == null)
                    return i;
            }

            if (wrap)
            {
                for (int i = 0; i < from && i < _slots.Count; i++)
                {
                    if (_slots[i] == null)
                        return i;
                }
            }

            int appended = _slots.Count;
            AppendPage();
            return appended;
        }

        private void EnsureSlotExists(int slot)
        {
            while (slot >= _slots.Count)
                AppendPage();
        }

        private void AppendPage()
        {
            _slots.AddRange(new string[CellsPerPage]);
        }

        // Empty cells after the last image mean nothing; dropping them
        // keeps a re-flowed arrangement from growing blank pages.
        private void TrimTrailingEmptyCells()
        {
            while (_slots.Count > 0 && _slots[_slots.Count - 1] == null)
                _slots.RemoveAt(_slots.Count - 1);
        }

        private void PadToWholePages()
        {
            if (_slots.Count == 0)
                AppendPage();

            while (_slots.Count % CellsPerPage != 0)
                _slots.Add(null);
        }

        private void Reindex()
        {
            _slotByPath.Clear();

            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i] != null)
                    _slotByPath[_slots[i]] = i;
            }
        }

        private void AfterMutation()
        {
            PadToWholePages();
            Reindex();
            OnChanged();
        }

        // Several changes made between BeginUpdate and EndUpdate are
        // announced once, at the end. Used when the same text is applied to
        // many images, or remarks are loaded from a sheet.
        public void BeginUpdate()
        {
            _updateDepth++;
        }

        public void EndUpdate()
        {
            if (_updateDepth > 0)
                _updateDepth--;

            if (_updateDepth == 0 && _changedDuringUpdate)
            {
                _changedDuringUpdate = false;
                OnChanged();
            }
        }

        private void OnChanged()
        {
            if (_updateDepth > 0)
            {
                _changedDuringUpdate = true;
                return;
            }

            EventHandler handler = Changed;

            if (handler != null)
                handler(this, EventArgs.Empty);
        }
    }
}
