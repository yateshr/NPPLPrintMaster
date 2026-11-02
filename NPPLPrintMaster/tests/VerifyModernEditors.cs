using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using NPPLPrintMaster;
class VerifyModernEditors
{
    const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
    static void Check(bool ok, string text) { if (!ok) throw new Exception(text); }
    static void Mode(bool enabled)
    { typeof(Form1).Assembly.GetType("NPPLPrintMaster.ModernUiSession").GetMethod("SetEnabled", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { enabled }); }
    static void Capture(Form form, string name)
    {
        Application.DoEvents(); using (var bitmap = new Bitmap(form.Width, form.Height))
        { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save("tmp/modern-studio/" + name + ".png"); }
    }
    [STAThread] static void Main()
    {
        Directory.CreateDirectory("tmp/modern-studio"); Application.EnableVisualStyles();
        string[] samples = new string[10];
        Color[] colors = { Color.Coral, Color.Teal, Color.SteelBlue, Color.OliveDrab, Color.DarkOrange };
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = Path.GetFullPath("tmp/modern-studio/Product " + (i + 1) + ".png");
            using (var bitmap = new Bitmap(800, 500)) using (var g = Graphics.FromImage(bitmap))
            using (var ink = new SolidBrush(colors[i % colors.Length])) using (var font = new Font("Segoe UI", 32, FontStyle.Bold))
            {
                g.Clear(Color.White); g.FillRectangle(ink, 0, 0, 800, 160); g.DrawString("NPPL / COLLECTION", font, Brushes.White, 36, 40);
                g.DrawString("PRODUCT " + (i + 1).ToString("00"), font, Brushes.Black, 36, 220);
                for (int x = 40; x < 650; x += 13) g.FillRectangle(Brushes.Black, x, 350, x % 3 + 3, 90);
                bitmap.Save(samples[i]);
            }
        }
        Mode(true);
        using (var editor = new CollageArrangementForm(samples, new CollagePageSetup { PaperSize = "A3", Landscape = true, Columns = 3, Rows = 2 },
            new AppSettings { InterfaceStyle = InterfaceAppearance.Modern }))
        {
            typeof(CollageArrangementForm).GetField("_recoveryPath", Flags).SetValue(editor, Path.GetFullPath("tmp/modern-studio/editor-draft.npplcollage"));
            editor.WindowState = FormWindowState.Normal; editor.Size = new Size(1440, 940); editor.Opacity = 0; editor.ShowInTaskbar = false; editor.Show();
            for (int i = 0; i < 20; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(20); }
            var library = (Control)typeof(CollageArrangementForm).GetField("_library", Flags).GetValue(editor);
            var caption = (Control)typeof(CollageArrangementForm).GetField("_remarkBox", Flags).GetValue(editor);
            var canvas = (Control)typeof(CollageArrangementForm).GetField("_canvas", Flags).GetValue(editor);
            Check(library.Parent.Dock == DockStyle.Bottom, "Modern image tray must be below the canvas");
            Check(caption.Parent.Parent.Parent.Dock == DockStyle.Right, "Modern inspector must be on the right");
            Check(canvas.Width > 600 && canvas.Height > 240, "Modern canvas has no useful working area");
            canvas.GetType().GetMethod("SelectAllCells").Invoke(canvas, new object[] { false });
            Capture(editor, "modern-collage-editor");
        }
        Mode(false);
        using (var editor = new CollageArrangementForm(samples, new CollagePageSetup(), new AppSettings()))
        {
            var library = (Control)typeof(CollageArrangementForm).GetField("_library", Flags).GetValue(editor);
            Check(library.Parent.Dock == DockStyle.Left, "Current collage editor layout changed");
        }
        using (var window = new BrandedForm { Opacity = 0, ShowInTaskbar = false })
        {
            var input = new TextBox { Text = "Unsaved editor text", Bounds = new Rectangle(20, 20, 180, 23) };
            var button = new Button { Text = "Save", Bounds = new Rectangle(20, 60, 140, 32), BackColor = Color.Navy, ForeColor = Color.White };
            window.Controls.Add(input); window.Controls.Add(button); window.Show();
            var originalFont = input.Font; var originalBounds = input.Bounds; var originalStyle = button.FlatStyle;
            Mode(true); Check(button.BackColor == InterfaceAppearance.ModernTheme().ActiveColor, "Editor primary action styling");
            Mode(false); Check(input.Text == "Unsaved editor text" && input.Bounds == originalBounds && input.Font.Equals(originalFont) && button.BackColor == Color.Navy && button.FlatStyle == originalStyle, "Editor style reversion changed data or original presentation");
        }
        Mode(true);
        using (var freeform = new FreeformBuilderForm(appSettings: new AppSettings { InterfaceStyle = InterfaceAppearance.Modern }))
        {
            freeform.WindowState = FormWindowState.Normal; freeform.Size = new Size(1440, 940); freeform.Opacity = 0; freeform.ShowInTaskbar = false; freeform.Show();
            Capture(freeform, "modern-freeform-editor");
            Check(freeform.Controls.OfType<ToolStripPanel>().SelectMany(p => p.Controls.OfType<ToolStrip>()).All(t => t.BackColor == Color.White), "Freeform toolbar contrast");
        }
        Mode(false);
        Console.WriteLine("PASS: canvas-first Modern collage editor with usable canvas, unchanged Current arrangement, live editor style reversion and unsaved text, white/contrasting Freeform command strips.");
    }
}
