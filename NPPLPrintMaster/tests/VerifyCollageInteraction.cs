using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using NPPLPrintMaster;

class VerifyCollageInteraction
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static object Call(object target, string name, params object[] args)
    { return target.GetType().GetMethod(name, Flags).Invoke(target, args); }
    static object Field(object target, string name) { return target.GetType().GetField(name, Flags).GetValue(target); }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static IList<string> Selection(object canvas)
    { return (IList<string>)canvas.GetType().GetProperty("SelectedPaths").GetValue(canvas, null); }

    [STAThread] static void Main()
    {
        string root = Path.GetFullPath("tmp/pdfs/interaction-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var paths = new List<string>();
            for (int i = 0; i < 6; i++)
            {
                string path = Path.Combine(root, "Label " + i + ".png");
                paths.Add(path);
                using (var bitmap = new Bitmap(500, 200))
                using (var g = Graphics.FromImage(bitmap))
                using (var font = new Font("Arial", 20))
                { g.Clear(Color.White); g.DrawString("IMAGE " + i, font, Brushes.Black, 30, 30); bitmap.Save(path); }
            }
            var slots = new List<string> { paths[0], null, paths[1], paths[2], paths[3], paths[4], paths[5], null, null, null, null, null };
            var page = new CollagePageSetup { Columns = 2, Rows = 2, HeaderText = "Selection / recovery check", CaptionFontPoints = 12, ShowPageNumbers = true };
            string draft = Path.Combine(root, "draft.npplcollage");
            using (var form = new CollageArrangementForm(paths, page, null, slots))
            {
                form.GetType().GetField("_recoveryPath", Flags).SetValue(form, draft);
                object model = Field(form, "_model"), canvas = Field(form, "_canvas");
                Check((int)model.GetType().GetProperty("PageCount").GetValue(model, null) == 3, "Trailing empty page was lost");
                Call(canvas, "SetSelectedSlot", 0, true);
                Call(canvas, "ExtendSelection", 4, true, false);
                Check(Selection(canvas).SequenceEqual(new[] { paths[0], paths[3] }), "Ctrl selection across pages");
                Check(((IList)Field(form, "_inspected")).Count == 2, "Inspector does not follow cell selection");
                Call(canvas, "ExtendSelection", 0, true, false);
                Check(Selection(canvas).SequenceEqual(new[] { paths[3] }), "Ctrl toggle off");
                Call(canvas, "SetSelectedSlot", 3, true);
                Call(canvas, "ExtendSelection", 6, false, true);
                Check(Selection(canvas).SequenceEqual(paths.Skip(2)), "Shift range across pages");
                Call(canvas, "SetSelectedSlot", 0, true);
                Call(canvas, "ExtendSelection", 4, true, false);
                Call(canvas, "ExtendSelection", 6, true, true);
                Check(Selection(canvas).SequenceEqual(new[] { paths[0], paths[3], paths[4], paths[5] }), "Additive Shift range");
                Call(canvas, "SelectAllCells", false);
                Check(Selection(canvas).Count == 6, "Select all image cells");
                Call(canvas, "ClearCellSelection");
                Check(Selection(canvas).Count == 0 && ((IList)Field(form, "_inspected")).Count == 0, "Clear selection");
                Call(canvas, "SelectAllCells", true);
                Check(Selection(canvas).Count == 3, "Current page selection");
                Check(!(bool)Field(form, "_autosavePending"), "Selection alone created a pending edit");

                var selected = new List<string> { paths[0], paths[5] };
                object library = Field(form, "_library");
                Call(library, "SelectMany", selected);
                Call(form, "Library_SelectionChanged", library, EventArgs.Empty);
                Check(Selection(canvas).SequenceEqual(selected), "Library/canvas selection synchronization");
                var remark = (TextBox)Field(form, "_remarkBox");
                var note = (TextBox)Field(form, "_noteBox");
                remark.Text = "Cross-page text"; remark.Modified = true;
                Call(form, "CommitBulkText", remark);
                Check((string)Call(model, "GetRemark", paths[0]) == "Cross-page text" &&
                    (string)Call(model, "GetRemark", paths[5]) == "Cross-page text" &&
                    Call(model, "GetRemark", paths[1]) == null, "Bulk edit selected cells only");
                Check(((IList)Field(form, "_undo")).Count == 1, "Bulk edit should have one undo step");
                Call(form, "Undo");
                Check(Call(model, "GetRemark", paths[0]) == null && Selection(canvas).Count == 2, "Undo/selection preservation");
                Call(canvas, "OnKeyDown", new KeyEventArgs(Keys.Delete));
                Check((int)model.GetType().GetProperty("PlacedCount").GetValue(model, null) == 4, "Delete must remove all selected cells");
                Call(form, "Undo");
                Check(Selection(canvas).Count == 2, "Undo must restore selected cell highlights");

                remark.Text = "Still typing"; remark.Modified = true;
                note.Text = "Pending extra line"; note.Modified = true;
                var snapshot = (CollageLayout)Call(form, "BuildRecoveryLayout");
                Check(snapshot.Remarks[paths[0]] == "Still typing" && snapshot.Notes[paths[5]] == "Pending extra line", "Pending inspector text recovery");
                Check(Call(model, "GetRemark", paths[0]) == null, "Autosave must not commit typing");
                Check((bool)Call(form, "SaveRecoveryDraft", true), "Initial autosave failed");
                var loaded = CollageLayoutFile.Load(draft);
                Check(loaded.Slots.Count == 12 && loaded.Slots[1] == null && loaded.Page.CaptionFontPoints == 12 &&
                    loaded.Remarks[paths[5]] == "Still typing", "Autosave state round trip");
                remark.Modified = false; note.Modified = false;
                Call(canvas, "BeginEdit", paths[2], true);
                var editor = (TextBox)Field(canvas, "_editor");
                editor.Text = "Uncommitted inline text";
                snapshot = (CollageLayout)Call(form, "BuildRecoveryLayout");
                Check(snapshot.Notes[paths[2]] == "Uncommitted inline text" && Call(model, "GetNote", paths[2]) == null,
                    "Inline typing recovery without model mutation");
                Call(canvas, "EndEdit", false);
                Call(canvas, "SelectAllCells", false);
                Check((bool)Call(form, "SaveRecoveryDraft", true), "Second autosave failed");
                Check(File.Exists(draft + ".bak"), "Previous valid draft backup missing");
                Check(((Timer)Field(form, "_autosaveTimer")).Interval == 10000, "Autosave cadence");

            }
            var store = typeof(CollageArrangementForm).Assembly.GetType("NPPLPrintMaster.CollageRecoveryStore");
            File.WriteAllText(draft, "<broken");
            File.WriteAllText(draft + ".tmp", "interrupted write");
            string recovered = (string)store.GetMethod("FindLatest").Invoke(null, new object[] { root });
            Check(recovered == draft + ".bak", "Corrupt latest draft must fall back to backup");
            var saved = CollageLayoutFile.Load(recovered);
            using (var reopened = new CollageArrangementForm(saved.Images, saved.Page, null, saved.Slots, saved.Remarks, null, saved.DisplayMode, saved.Notes))
            {
                var restored = (CollageLayout)Call(reopened, "BuildRecoveryLayout");
                Check(restored.Slots.Count == 12 && restored.Remarks[paths[5]] == "Still typing" &&
                    restored.Notes[paths[0]] == "Pending extra line", "Restart/reopen restores layout and text");
                // Render a hidden test-owned editor; never use the real recovery directory.
                reopened.GetType().GetField("_recoveryPath", Flags).SetValue(reopened, Path.Combine(root, "visual.npplcollage"));
                reopened.WindowState = FormWindowState.Normal;
                reopened.Size = new Size(1320, 840);
                reopened.Opacity = 0; reopened.ShowInTaskbar = false;
                reopened.Show();
                object canvas = Field(reopened, "_canvas");
                Call(canvas, "SelectAllCells", false);
                var wait = System.Diagnostics.Stopwatch.StartNew();
                while (wait.ElapsedMilliseconds < 500) Application.DoEvents();
                using (var bitmap = new Bitmap(reopened.Width, reopened.Height))
                { reopened.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save("tmp/pdfs/multiselect-proof.png"); }
                Check(((Timer)Field(reopened, "_autosaveTimer")).Enabled, "Autosave timer must start when the editor opens");
                reopened.Hide();
            }
            store.GetMethod("Delete").Invoke(null, new object[] { draft });
            Check(!File.Exists(draft) && !File.Exists(draft + ".bak") && !File.Exists(draft + ".tmp"), "Discard cleanup");
            Console.WriteLine("PASS: Ctrl toggle, cross-page Shift range, additive range, all/current-page/clear selection, library sync, bulk text + undo, pending typing, atomic autosave + backup, corrupted-draft fallback, reopen/recovery, discard cleanup.");
        }
        finally
        {
            string parent = Path.GetFullPath("tmp/pdfs") + Path.DirectorySeparatorChar;
            if (root.StartsWith(parent, StringComparison.OrdinalIgnoreCase)) Directory.Delete(root, true);
        }
    }
}
