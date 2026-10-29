using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using NPPLPrintMaster;

class VerifyOriginalPreview
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static object Call(object target, string method, params object[] args)
    { return target.GetType().GetMethod(method, Flags).Invoke(target, args); }
    static object Field(object target, string name) { return target.GetType().GetField(name, Flags).GetValue(target); }
    static float Zoom(object target) { return (float)target.GetType().GetProperty("Zoom").GetValue(target, null); }
    static void Check(bool value, string error) { if (!value) throw new Exception(error); }
    static void PumpUntil(Func<bool> done)
    {
        var wait = System.Diagnostics.Stopwatch.StartNew();
        while (!done() && wait.ElapsedMilliseconds < 10000) { Application.DoEvents(); Thread.Sleep(5); }
        Check(done(), "Preview did not finish loading");
    }

    [STAThread] static void Main()
    {
        string root = Path.GetFullPath("tmp/pdfs/preview-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "Full resolution label.png");
        var assembly = typeof(CollageArrangementForm).Assembly;
        var viewerType = assembly.GetType("NPPLPrintMaster.CollageImagePreviewForm");
        var canvasType = assembly.GetType("NPPLPrintMaster.OriginalImageCanvas");
        try
        {
            using (var source = new Bitmap(2400, 1200))
            using (var g = Graphics.FromImage(source))
            using (var title = new Font("Arial", 32, FontStyle.Bold))
            using (var small = new Font("Arial", 10))
            {
                g.Clear(Color.White);
                g.DrawString("NPPL - ORIGINAL QUALITY", title, Brushes.Black, 850, 420);
                g.DrawString("Small text: Batch 123456 / 50 x 38 mm", small, Brushes.Black, 900, 470);
                for (int x = 900; x < 1300; x += 2) g.FillRectangle(Brushes.Black, x, 500, 1, 200);
                source.SetPixel(0, 0, Color.Transparent);
                source.Save(path, ImageFormat.Png);
            }
            var load = viewerType.GetMethod("LoadOriginal", BindingFlags.Static | BindingFlags.NonPublic);
            using (var original = (Bitmap)load.Invoke(null, new object[] { path }))
            {
                Check(original.Size == new Size(2400, 1200), "Original image was resized");
                Check(original.GetPixel(900, 600).ToArgb() == Color.Black.ToArgb() &&
                    original.GetPixel(901, 600).ToArgb() == Color.White.ToArgb(), "Source pixel detail was lost");
                Check(original.GetPixel(0, 0).A == 0, "Transparency was lost");
                using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                using (var canvas = (Control)Activator.CreateInstance(canvasType))
                {
                    canvas.Size = new Size(800, 400);
                    Call(canvas, "SetImage", new Bitmap(original));
                    Check(Zoom(canvas) < 1 && Zoom(canvas) * 2400 <= 760.1, "Fit-to-window bounds");
                    Call(canvas, "ActualSize");
                    Check(Zoom(canvas) == 1, "100% must map to source pixels");
                    using (var rendered = new Bitmap(800, 400))
                    {
                        canvas.DrawToBitmap(rendered, new Rectangle(0, 0, 800, 400));
                        Check(rendered.GetPixel(100, 200).ToArgb() == Color.Black.ToArgb() &&
                            rendered.GetPixel(101, 200).ToArgb() == Color.White.ToArgb(), "100% rendering blurred source pixels");
                    }
                    var before = (PointF)Field(canvas, "_offset");
                    var anchor = new Point(300, 200);
                    float sourceX = (anchor.X - before.X) / Zoom(canvas);
                    Call(canvas, "ZoomAt", anchor, 1.25f);
                    var after = (PointF)Field(canvas, "_offset");
                    Check(Math.Abs((anchor.X - after.X) / Zoom(canvas) - sourceX) < 0.01, "Zoom pointer anchor drifted");
                    Call(canvas, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, 300, 200, 0));
                    Call(canvas, "OnMouseMove", new MouseEventArgs(MouseButtons.Left, 0, 350, 230, 0));
                    var panned = (PointF)Field(canvas, "_offset");
                    Check(Math.Abs(panned.X - after.X - 50) < 0.01, "Drag pan failed");
                    Call(canvas, "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, 350, 230, 0));
                    Check(!canvas.Capture, "Pan capture was not released");
                    Call(canvas, "ZoomAt", anchor, 100000f); Check(Zoom(canvas) == 16, "Maximum zoom");
                    Call(canvas, "ZoomAt", anchor, 0.00000001f); Check(Zoom(canvas) >= 0.001f, "Minimum zoom");
                    Call(canvas, "FitImage"); canvas.Size = new Size(1000, 600);
                    Check(Zoom(canvas) * 2400 <= 960.1 && Zoom(canvas) * 1200 <= 560.1, "Fit after resizing");
                }
            }

            var theme = ThemeManager.GetTheme("Forest Graphite");
            string secondPath = Path.Combine(root, "Second label.png");
            File.Copy(path, secondPath);
            using (var editor = new CollageArrangementForm(new[] { path, secondPath }, new CollagePageSetup(), null))
            {
                var button = (Button)Field(editor, "_previewButton");
                Check(!button.Enabled, "Preview should require a selected image");
                Call(editor, "SetInspected", (object)new[] { path });
                Check(button.Enabled, "Single selected image should enable preview");
                Call(editor, "SetInspected", (object)new[] { path, secondPath });
                Check(!button.Enabled, "Multi-selection should not preview an arbitrary image");
            }
            using (var viewer = (Form)Activator.CreateInstance(viewerType, path, theme, SystemFonts.MessageBoxFont))
            {
                viewer.Opacity = 0; viewer.ShowInTaskbar = false; viewer.Show();
                var canvas = (Control)Field(viewer, "_canvas");
                PumpUntil(() => !((Size)canvasType.GetProperty("ImageSize").GetValue(canvas, null)).IsEmpty);
                Call(canvas, "ActualSize");
                Check(((Label)Field(viewer, "_status")).Text.Contains("100%"), "Viewer status should show actual zoom");
                using (var bitmap = new Bitmap(viewer.Width, viewer.Height))
                { viewer.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save("tmp/pdfs/original-preview-proof.png"); }
                viewer.Hide();
            }
            using (var viewer = (Form)Activator.CreateInstance(viewerType, path + ".missing", theme, SystemFonts.MessageBoxFont))
            {
                viewer.Opacity = 0; viewer.ShowInTaskbar = false; viewer.Show();
                PumpUntil(() => ((Label)Field(viewer, "_status")).Text.StartsWith("Unable to load"));
                viewer.Hide();
            }
            using (var viewer = (Form)Activator.CreateInstance(viewerType, path, theme, SystemFonts.MessageBoxFont))
            { viewer.Opacity = 0; viewer.ShowInTaskbar = false; viewer.Show(); }
            var finish = System.Diagnostics.Stopwatch.StartNew();
            while (finish.ElapsedMilliseconds < 300) { Application.DoEvents(); Thread.Sleep(5); }
            Console.WriteLine("PASS: native resolution, file released, transparency, pixel-exact 100%, fit/resize, anchored zoom, pan/capture, zoom limits, async viewer/status, missing file, close while loading.");
        }
        finally
        {
            string parent = Path.GetFullPath("tmp/pdfs") + Path.DirectorySeparatorChar;
            if (root.StartsWith(parent, StringComparison.OrdinalIgnoreCase)) Directory.Delete(root, true);
        }
    }
}
