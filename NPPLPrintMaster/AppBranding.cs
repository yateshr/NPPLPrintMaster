using System.Drawing;
using System.Windows.Forms;

namespace NPPLPrintMaster
{
    // Keep the existing logo embedded so portable copies and dialogs do not
    // depend on a loose logo file or the working directory.
    public static class AppBranding
    {
        public static Icon CreateIcon()
        {
            using (var stream = typeof(AppBranding).Assembly.GetManifestResourceStream("NPPLPrintMaster.Branding.PrintMaster.ico"))
            using (var icon = new Icon(stream)) return (Icon)icon.Clone();
        }

        public static PictureBox CreateLogo(Rectangle bounds)
        {
            Image image;
            using (var stream = typeof(AppBranding).Assembly.GetManifestResourceStream("NPPLPrintMaster.Branding.PrintMaster.png"))
            using (var source = Image.FromStream(stream)) image = new Bitmap(source);
            var picture = new PictureBox { Image = image, Bounds = bounds,
                SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent, TabStop = false,
                AccessibleName = "NPPL PrintMaster logo" };
            picture.Disposed += (s, e) => image.Dispose();
            return picture;
        }
    }

    public class BrandedForm : Form
    {
        private readonly Icon brandIcon;
        public BrandedForm()
        {
            brandIcon = AppBranding.CreateIcon();
            Icon = brandIcon;
        }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) brandIcon.Dispose();
        }
    }
}
