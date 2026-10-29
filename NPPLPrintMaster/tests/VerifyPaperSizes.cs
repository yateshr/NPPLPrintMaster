using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using NPPLPrintMaster;

class VerifyPaperSizes
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static object Call(object target, string name, params object[] args)
    { return target.GetType().GetMethod(name, Flags).Invoke(target, args); }
    static object Field(object target, string name) { return target.GetType().GetField(name, Flags).GetValue(target); }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void SizeIs(CollagePageSetup page, float width, float height)
    { Check(Math.Abs(page.WidthMm - width) < 0.01 && Math.Abs(page.HeightMm - height) < 0.01, "Wrong sheet dimensions: " + page.PaperDescription); }

    [STAThread] static void Main()
    {
        string root = Path.GetFullPath("tmp/pdfs/paper-check");
        Directory.CreateDirectory(root);
        string image = Path.Combine(root, "Paper size sample.png");
        using (var bitmap = new Bitmap(600, 250))
        using (var g = Graphics.FromImage(bitmap))
        using (var font = new Font("Arial", 24))
        {
            g.Clear(Color.White); g.DrawString("NPPL paper size sample", font, Brushes.Black, 20, 20);
            for (int x = 25; x < 550; x += 7) g.FillRectangle(Brushes.Black, x, 80, x % 4 + 1, 100);
            bitmap.Save(image);
        }
        foreach (string paper in new[] { "A4", "A3", "A2", "Custom" })
        foreach (bool landscape in new[] { false, true })
        {
            var page = new CollagePageSetup { PaperSize = paper, Landscape = landscape,
                CustomWidthMm = 350.5f, CustomHeightMm = 700.25f, Columns = 2, Rows = 1,
                HeaderText = paper + " " + (landscape ? "Landscape" : "Portrait"), ShowPageNumbers = true, DrawBorders = true };
            page.Normalize();
            float shorter = paper == "A4" ? 210 : paper == "A3" ? 297 : paper == "A2" ? 420 : 350.5f;
            float longer = paper == "A4" ? 297 : paper == "A3" ? 420 : paper == "A2" ? 594 : 700.25f;
            SizeIs(page, landscape ? longer : shorter, landscape ? shorter : longer);
            string output = Path.Combine(root, paper + (landscape ? "-landscape" : "-portrait") + ".pdf");
            ImageCollageEngine.Generate(new[] { image }, output, "PDF", page, null);
            var saved = CollageLayoutFile.Load(output);
            Check(saved != null && saved.Page.PaperSize == paper && saved.Page.Landscape == landscape, "PDF layout paper round trip");
            SizeIs(saved.Page, page.WidthMm, page.HeightMm);
            using (var editor = new CollageArrangementForm(new[] { image }, page, null))
            {
                var canvas = (Control)Field(editor, "_canvas");
                var size = (SizeF)canvas.GetType().GetProperty("PageSize", Flags).GetValue(canvas, null);
                Check(Math.Abs(size.Width - page.WidthPoints * 96 / 72) < 0.02 &&
                    Math.Abs(size.Height - page.HeightPoints * 96 / 72) < 0.02, "Preview/PDF paper mismatch");
                var nav = (Control)Field(editor, "_navigator"); nav.Size = new Size(180, 600);
                Call(nav, "OnModelChanged");
                float units = (float)nav.GetType().GetProperty("UnitsPerPoint", Flags).GetValue(nav, null);
                Check(Math.Abs(units * page.WidthPoints - (int)Field(nav, "_thumbWidth")) < 0.1, "Navigator paper scale");
            }
        }
        var legacy = CollageLayoutFile.FromXml("<NpplCollageLayout version='2' columns='2' rows='2' orientation='Landscape' />", root);
        SizeIs(legacy.Page, 297, 210); Check(legacy.Page.PaperSize == "A4", "Legacy layouts must default to A4");
        var invalid = new CollagePageSetup { PaperSize = "unknown", CustomWidthMm = float.NaN, CustomHeightMm = float.PositiveInfinity };
        invalid.Normalize(); SizeIs(invalid, 210, 297);
        var custom = new CollagePageSetup { PaperSize = "Custom", CustomWidthMm = 2000, CustomHeightMm = 50, Landscape = true };
        custom.Normalize(); SizeIs(custom, 2000, 50);

        using (var editor = new CollageArrangementForm(new[] { image }, new CollagePageSetup(), null))
        {
            editor.GetType().GetField("_recoveryPath", Flags).SetValue(editor, Path.Combine(root, "draft.npplcollage"));
            Call(editor, "ApplyPaperSetup", new CollagePageSetup { PaperSize = "Custom", CustomWidthMm = 400, CustomHeightMm = 850, Landscape = true });
            var recovery = (CollageLayout)Call(editor, "BuildRecoveryLayout"); SizeIs(recovery.Page, 850, 400);
            Call(editor, "SaveRecoveryDraft", true);
            SizeIs(CollageLayoutFile.Load(Path.Combine(root, "draft.npplcollage")).Page, 850, 400);
            Call(editor, "Undo"); SizeIs(((CollageLayout)Call(editor, "BuildRecoveryLayout")).Page, 210, 297);
            Call(editor, "Redo"); SizeIs(((CollageLayout)Call(editor, "BuildRecoveryLayout")).Page, 850, 400);
            Call(editor, "ApplyPaperSetup", new CollagePageSetup { PaperSize = "Custom", CustomWidthMm = 1500, CustomHeightMm = 2000 });
            editor.WindowState = FormWindowState.Normal; editor.Size = new Size(1320, 840);
            editor.Opacity = 0; editor.ShowInTaskbar = false; editor.Show();
            var canvas = (Control)Field(editor, "_canvas");
            float zoom = (float)canvas.GetType().GetProperty("Zoom").GetValue(canvas, null);
            Check(zoom < 0.12f, "Large custom sheets should fit in the preview");
            using (var bitmap = new Bitmap(editor.Width, editor.Height))
            { editor.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(root, "paper-editor.png")); }
            editor.Hide();
        }
        using (var main = new Form1())
        {
            Call(main, "ApplyCollagePageSetup", new CollagePageSetup { PaperSize = "A2", Landscape = true });
            SizeIs((CollagePageSetup)Call(main, "GetCollagePageSetup"), 594, 420);
            var format = (ComboBox)Field(main, "cmbCollageOutputType");
            format.SelectedIndex = 1;
            Check(!((Button)Field(main, "btnCollagePaper")).Enabled, "PDF paper controls must not imply changes to Word output");
            format.SelectedIndex = 0;
            Check(((Button)Field(main, "btnCollagePaper")).Enabled, "Paper controls must be enabled for PDF");
        }
        var dialogType = typeof(CollageArrangementForm).Assembly.GetType("NPPLPrintMaster.CollagePaperSetupForm");
        using (var dialog = (Form)Activator.CreateInstance(dialogType, new CollagePageSetup()))
        {
            ((ComboBox)Field(dialog, "_paper")).SelectedItem = "Custom";
            ((NumericUpDown)Field(dialog, "_shortSide")).Value = 350.5m;
            ((NumericUpDown)Field(dialog, "_longSide")).Value = 700.25m;
            ((ComboBox)Field(dialog, "_orientation")).SelectedIndex = 1;
            var result = (CollagePageSetup)dialogType.GetProperty("ResultPage").GetValue(dialog, null);
            SizeIs(result, 700.25f, 350.5f);
            dialog.Opacity = 0; dialog.ShowInTaskbar = false; dialog.Show(); Application.DoEvents();
            using (var bitmap = new Bitmap(dialog.Width, dialog.Height))
            { dialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(root, "paper-dialog.png")); }
            dialog.Hide();
        }
        Console.WriteLine("PASS: A4/A3/A2/custom in both orientations, preview and navigator dimensions, PDF/layout and recovery round trips, undo/redo, legacy A4 default, invalid dimensions, large-sheet fit, main controls and paper dialog.");
    }
}
