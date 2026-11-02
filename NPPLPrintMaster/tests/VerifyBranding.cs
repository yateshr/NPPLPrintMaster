using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using NPPLPrintMaster;
class BrandQuietForm : Form1
{
    protected override void OnLoad(EventArgs e) { }
    protected override void OnShown(EventArgs e) { }
}
class VerifyBranding
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern uint ExtractIconEx(string file, int index, IntPtr[] large, IntPtr[] small, uint count);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
    static Icon ReadExecutableIcon(string file)
    {
        // Extract the PE resource directly; the shell association cache can
        // still contain the previous logo after a local rebuild.
        var large = new IntPtr[1]; var small = new IntPtr[1];
        Check(ExtractIconEx(file, 0, large, small, 1) > 0, "Missing executable icon resource");
        try { using (var borrowed = Icon.FromHandle(large[0])) return (Icon)borrowed.Clone(); }
        finally { if (large[0] != IntPtr.Zero) DestroyIcon(large[0]); if (small[0] != IntPtr.Zero) DestroyIcon(small[0]); }
    }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void CheckArtwork(Icon icon, string context = "window icon")
    {
        Check(icon != null, "Missing icon");
        using (var bitmap = icon.ToBitmap())
        {
            int orange = 0;
            for (int y = 0; y < bitmap.Height; y++) for (int x = 0; x < bitmap.Width; x++)
            {
                Color c = bitmap.GetPixel(x, y);
                if (c.A > 100 && c.R > 210 && c.G > 60 && c.G < 180 && c.B < 70) orange++;
            }
            Check(orange > bitmap.Width * bitmap.Height / 4, "Expected approved orange page/P logo in " + context + "; found " + orange + " orange pixels in " + bitmap.Size);
        }
    }
    [STAThread] static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        using (var icon = AppBranding.CreateIcon()) CheckArtwork(icon, "embedded resource");
        using (var icon = ReadExecutableIcon(typeof(Form1).Assembly.Location)) CheckArtwork(icon, "executable");
        if (args.Length > 0) using (var icon = ReadExecutableIcon(args[0])) CheckArtwork(icon);
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
