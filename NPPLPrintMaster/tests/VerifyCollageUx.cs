using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using NPPLPrintMaster;

class QuietMain : Form1
{
    protected override void OnShown(EventArgs e) { }
    protected override void OnLoad(EventArgs e) { }
}
class VerifyCollageUx
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static object Field(Form1 form, string name) { return typeof(Form1).GetField(name, Flags).GetValue(form); }
    static object Call(Form1 form, string name, params object[] args) { return typeof(Form1).GetMethod(name, Flags).Invoke(form, args); }
    static IEnumerable<Control> All(Control parent)
    { foreach(Control c in parent.Controls) { yield return c; foreach(var nested in All(c)) yield return nested; } }
    static void Check(bool value, string message) { if(!value) throw new Exception(message); }
    [STAThread] static void Main()
    {
        Directory.CreateDirectory("tmp/pdfs/ux-check");
        using(var main = new QuietMain())
        {
            main.Opacity = 0; main.ShowInTaskbar = false; main.Size = new Size(1400, 1000); main.Show();
            Call(main, "ApplyTheme", "Forest Graphite"); Call(main, "OpenCollagePage"); Application.DoEvents();
            var page = (Panel)Field(main,"pageCollage");
            Check(page.Visible && !((Panel)Field(main,"pageFormat")).Visible,"Collage must have its own page");
            Check(All(page).Count(c=>c.GetType().Name=="CollageCard") == 3,"Three workflow sections");
            var sourceField=(Control)Field(main,"collageSourceField");
            Check(sourceField.Top==((Control)Field(main,"collageChooseFiles")).Top &&
                sourceField.Top==((Control)Field(main,"collageChooseFolder")).Top,"Import inputs must align");
            Check(((Control)Field(main,"cmbCollageOutputType")).Top==((Control)Field(main,"cmbCollageOrientation")).Top,"Settings inputs must align");
            Check(((Control)Field(main,"collageOutputField")).Top==((Control)Field(main,"collageBrowseOutput")).Top,"Export inputs must align");
            Check(((Control)Field(main,"collageExportCard")).Left>((Control)Field(main,"collageLayoutCard")).Right,"Wide layout should have an export sidebar");
            Check(ThemeManager.GetContrastTextColor(Color.FromArgb(72,181,108)).GetBrightness()<0.2,"Green active buttons need dark text for contrast");
            Check(!((TextBox)Field(main,"txtCollageSource")).IsDisposed,"Reused source controls must survive reparenting");
            Check(!((Button)Field(main,"btnGenerateCollage")).Enabled,"Export must wait for images");
            var naming=(CheckBox)Field(main,"chkCollageAutoFileName");
            naming.Checked=false;
            Check(((TextBox)Field(main,"txtCollageFileName")).Visible && ((TextBox)Field(main,"txtCollageFileName")).Enabled,"Custom filename must remain editable");
            naming.Checked=true;
            Check(!((TextBox)Field(main,"txtCollageFileName")).Enabled,"Automatic filenames must disable custom editing");
            Check(All(page).OfType<Label>().Any(c=>c.Text=="Import images"),"Import step missing");
            var more = All(page).OfType<Button>().First(b=>b.Text.Contains("Caption and Page Details"));
            more.PerformClick(); Application.DoEvents();
            Check(((TextBox)Field(main,"txtCollageHeader")).Visible,"Details must expand");
            using(var bitmap=new Bitmap(main.Width,main.Height))
            { main.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size)); bitmap.Save("tmp/pdfs/ux-check/expanded.png"); }
            more.PerformClick(); Check(!((TextBox)Field(main,"txtCollageHeader")).Visible,"Details must collapse");
            typeof(Form1).GetField("selectedCollageFiles",Flags).SetValue(main,new[]{"sample.png"});
            Call(main,"UpdateCollageArrangementButtons");
            Check(((Button)Field(main,"btnGenerateCollage")).Enabled,"Selected images enable export");
            Check(((Button)Field(main,"btnGenerateCollage")).Text=="Export PDF...","Explicit PDF export label");
            var format=(ComboBox)Field(main,"cmbCollageOutputType"); format.SelectedIndex=1;
            Check(((Button)Field(main,"btnGenerateCollage")).Text=="Export Word...","Export label follows format");
            format.SelectedIndex=0;
            foreach(string theme in new[]{"Forest Graphite","BarTender Blue","BarTender 10 Classic"})
            {
                Call(main,"ApplyTheme",theme); Application.DoEvents();
                Check(((TextBox)Field(main,"txtCollageSource")).BorderStyle==BorderStyle.None,"Theme changes must preserve input styling");
                var folder=(Button)Field(main,"collageChooseFolder");
                Check(TextRenderer.MeasureText(folder.Text,folder.Font).Width<=folder.Width-16,"Folder action text must not be clipped");
                using(var bitmap=new Bitmap(main.Width,main.Height))
                { main.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size)); bitmap.Save("tmp/pdfs/ux-check/"+theme.Replace(' ','-')+".png"); }
                Check(((Button)Field(main,"activeNavButton")).Text.Contains("Image Collage"),"Theme should retain active navigation");
            }
            main.Size=new Size(1000,800); Application.DoEvents();
            Check(page.Controls[0].Width<=page.ClientSize.Width,"Workflow should follow the viewport width");
            Check(((Control)Field(main,"collageExportCard")).Top>((Control)Field(main,"collageLayoutCard")).Bottom,"Narrow layout must stack cards");
            Call(main,"ApplyTheme","Forest Graphite");
            using(var bitmap=new Bitmap(main.Width,main.Height))
            { main.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size)); bitmap.Save("tmp/pdfs/ux-check/narrow.png"); }
            var nav=(List<Button>)Field(main,"navButtons");
            nav[2].PerformClick();Check(!page.Visible,"Switching pages must hide collage");
            ((Button)Field(main,"btnNavCollage")).PerformClick();Check(page.Visible,"Sidebar collage navigation");
            main.Hide();
        }
        Console.WriteLine("PASS: dedicated sidebar navigation, three workflow steps, expandable settings, preserved controls, source-aware export, PDF/Word labels, and dark/light/classic theme rendering.");
    }
}
