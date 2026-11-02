using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using NPPLPrintMaster;
class HelpSidebarQuietForm : Form1
{
    protected override void OnLoad(EventArgs e) { }
    protected override void OnShown(EventArgs e) { }
}
class VerifyHelpAndSidebar
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Field(Form1 form, string name) { return typeof(Form1).GetField(name, Flags).GetValue(form); }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    [STAThread] static void Main()
    {
        string root = Path.GetFullPath("tmp/help-sidebar-check"); Directory.CreateDirectory(root);
        var load = typeof(HelpViewer).GetMethod("LoadMarkdown", BindingFlags.Static | BindingFlags.NonPublic);
        foreach (string file in new[] { "faq.md", "about.md", "license.md", "requirements.md", "troubleshooting.md", "releasenotes.md" })
        {
            string expected = File.ReadAllText(Path.Combine("../Docs", file));
            Check(File.Exists(Path.Combine(Application.StartupPath, "Documents", file)), "Missing packaged Help file: " + file);
            Check((string)load.Invoke(null, new object[] { file, null }) == expected, "Packaged document mismatch: " + file);
            Check((string)load.Invoke(null, new object[] { file, root }) == expected, "Embedded fallback mismatch: " + file);
        }
        Check(load.Invoke(null, new object[] { "missing.md", root }) == null, "Unknown document handling");
        Application.EnableVisualStyles();
        using (var form = new HelpSidebarQuietForm { Opacity = 0, ShowInTaskbar = false })
        {
            form.Show(); typeof(Form1).GetMethod("ApplyTheme", Flags).Invoke(form, new object[] { "Forest Graphite" });
            var selector = (ComboBox)Field(form, "cmbInterfaceStyle");
            var nav = (List<Button>)Field(form, "navButtons");
            var settings = (Button)Field(form, "sidebarSettingsButton");
            Check((bool)settings.GetType().GetField("ShowIconInCurrentUI").GetValue(settings), "Missing Current UI settings icon");
            Check((int)settings.GetType().GetField("IconIndex").GetValue(settings) == 7, "Incorrect settings icon");
            foreach (string mode in new[] { InterfaceAppearance.Current, InterfaceAppearance.Modern, InterfaceAppearance.Current })
            foreach (Size size in new[] { new Size(1400, 900), new Size(900, 600) })
            {
                selector.SelectedItem = mode; form.Size = size; Application.DoEvents();
                Control footer = mode == InterfaceAppearance.Modern ? (Control)Field(form, "modernSidebarFooter") :
                    ((Panel)Field(form, "pnlSidebar")).Controls.OfType<Label>().Single(l => l.Text == "Built by Yatesh Rohit");
                Check(settings.Bottom <= footer.Top && footer.Top - settings.Bottom <= 8, "Settings must sit directly above credit/footer");
                Check(settings.Top >= nav[6].Bottom, "Settings overlaps main navigation");
                Check(settings.Parent.Controls.OfType<Button>().Count(b => b.Text.Trim() == "Settings") == 1, "Duplicate settings navigation");
                settings.PerformClick(); Check(((Panel)Field(form, "pageSettings")).Visible, "Bottom settings action must work");
                if (size.Width == 1400)
                    using (var bitmap = new Bitmap(form.Width, form.Height))
                    { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(root, mode.StartsWith("Current") ? "current.png" : "modern.png")); }
            }
        }
        foreach (string document in new[] { "faq.md", "requirements.md" })
        {
        bool rendered = false; string renderError = null;
        var started = DateTime.UtcNow;
        using (var timer = new Timer { Interval = 100 })
        {
            timer.Tick += (s, e) =>
            {
                var help = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.Text == "Help document smoke check");
                if (help == null) return;
                help.Opacity = 0;
                try
                {
                    var browser = help.Controls.OfType<WebBrowser>().Single();
                    string text = browser.Document == null || browser.Document.Body == null ? "" : browser.Document.Body.InnerText ?? "";
                    rendered = document == "faq.md" ? text.Contains("Frequently Asked Questions") :
                        text.Contains("System Requirements") && browser.Document.GetElementsByTagName("table").Count >= 2;
                    rendered = rendered && !text.Contains("Document Not Found");
                    if (rendered || (DateTime.UtcNow - started).TotalSeconds > 15) { timer.Stop(); help.Close(); }
                }
                catch (Exception ex) { renderError = ex.Message; timer.Stop(); help.Close(); }
            };
            timer.Start(); HelpViewer.ShowDocument("Help document smoke check", document);
        }
        Check(rendered, document + " browser rendering failed: " + renderError);
        }
        Console.WriteLine("PASS: all six Help documents, embedded fallback, actual FAQ and requirements/table rendering, bottom Settings with gear icon, resize and Current/Modern switching.");
    }
}
