using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using NPPLPrintMaster;
class BrandQuietForm : Form1
{
    protected override void OnLoad(EventArgs e) { }
    protected override void OnShown(EventArgs e) { }
}
class VerifyBranding
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void CheckArtwork(Icon icon)
    {
        Check(icon != null, "Missing icon");
        using (var bitmap = icon.ToBitmap())
        {
            int gold = 0;
            for (int y = 0; y < bitmap.Height; y++) for (int x = 0; x < bitmap.Width; x++)
            {
                Color c = bitmap.GetPixel(x, y);
                if (c.A > 100 && c.R > 150 && c.G > 90 && c.R > c.B * 1.25) gold++;
            }
            Check(gold > bitmap.Width * bitmap.Height / 10, "Expected existing gold/orange artwork");
        }
    }
    [STAThread] static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        using (var icon = AppBranding.CreateIcon()) CheckArtwork(icon);
        using (var icon = Icon.ExtractAssociatedIcon(typeof(Form1).Assembly.Location)) CheckArtwork(icon);
        if (args.Length > 0) using (var icon = Icon.ExtractAssociatedIcon(args[0])) CheckArtwork(icon);
        foreach (var type in typeof(Form1).Assembly.GetTypes().Where(t => typeof(Form).IsAssignableFrom(t)))
            Check(typeof(BrandedForm).IsAssignableFrom(type), "Unbranded form: " + type.Name);
        using (var main = new BrandQuietForm())
        using (var dialog = new BrandedForm())
        using (var picture = AppBranding.CreateLogo(new Rectangle(0, 0, 52, 52)))
        {
            CheckArtwork(main.Icon); CheckArtwork(dialog.Icon);
            Check(picture.Image.Width == 256 && picture.SizeMode == PictureBoxSizeMode.Zoom && !picture.TabStop, "Logo source and aspect ratio");
            main.Dispose(); // A closed window must not dispose shared branding.
            CheckArtwork(dialog.Icon);
        }
        Console.WriteLine("PASS: executable icon, all application forms/dialogs branded, native 256px logo, aspect-ratio scaling and independent icon lifetimes.");
    }
}
