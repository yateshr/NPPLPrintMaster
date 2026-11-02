using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
class VerifyInstallerContent
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    [STAThread] static void Main()
    {
        foreach (var file in new[] { "BeforeInstall.rtf", "AfterInstall.rtf" })
        using (var box = new RichTextBox())
        {
            box.LoadFile(Path.Combine("installer", file), RichTextBoxStreamType.RichText);
            Check(box.Text.Length > 500, "Missing installer guide content");
            Check(!box.Text.Contains("\\rtf") && !box.Text.Contains("\\cf1"), "Unparsed RTF");
            Check(box.Text.Contains("Built by Yatesh Rohit") && box.Text.Contains("Author and developer"), "Missing developer credit");
            Check(box.Text.Contains("Copyright \u00a9 2026 Yatesh Rohit"), "Missing copyright credit");
            box.Select(0, 1);
            Check(box.SelectionColor == Color.FromArgb(249, 115, 22), "Missing orange heading");
            Check(box.SelectionFont != null && box.SelectionFont.Bold && box.SelectionFont.Size >= 18, "Missing rich heading typography");
            if (file.StartsWith("Before")) Check(box.Text.Contains("\u2022") && box.Text.Contains("Save your work"), "Missing feature bullets or upgrade instructions");
            else Check(box.Text.Contains("1. Choose your interface") && box.Text.Contains("4. Keep PrintMaster current"), "Missing getting-started steps");
        }
        using (var image = Image.FromFile("Branding/InstallerWelcome.bmp"))
            Check(image.Width * 314 == image.Height * 164, "Portrait artwork aspect ratio");
        Console.WriteLine("PASS: both installer RTF guides render with orange bold headings, author/developer and copyright credits, feature bullets, getting-started content and correctly proportioned vertical artwork.");
    }
}
