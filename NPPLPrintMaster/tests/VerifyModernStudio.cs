using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using NPPLPrintMaster;
class StudioQuietForm : Form1
{
    protected override void OnLoad(EventArgs e) { }
    protected override void OnShown(EventArgs e) { }
}
class VerifyModernStudio
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Field(Form1 f, string n) { return typeof(Form1).GetField(n, Flags).GetValue(f); }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    class Original
    {
        internal Control Control, Parent; internal Rectangle Bounds; internal DockStyle Dock; internal AnchorStyles Anchor; internal int Tab; internal string Font; internal Size ParentSize;
    }
    static void CaptureOriginal(Control root, List<Original> result)
    {
        foreach (Control c in root.Controls)
        {
            result.Add(new Original { Control = c, Parent = root, Bounds = c.Bounds, Dock = c.Dock, Anchor = c.Anchor, Tab = c.TabIndex, Font = c.Font.ToString(), ParentSize = root.Size });
            if (!(c is FlowLayoutPanel) && (c is Panel || c is GroupBox || c is SplitContainer || c is TabControl || c is TabPage)) CaptureOriginal(c, result);
        }
    }
    static IEnumerable<Control> All(Control root)
    {
        foreach (Control c in root.Controls)
        { yield return c; if (!(c is NumericUpDown) && !(c is DataGridView)) foreach (var child in All(c)) yield return child; }
    }
    static void Screenshot(Form form, string name)
    {
        Application.DoEvents();
        using (var image = new Bitmap(form.Width, form.Height))
        { form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save("tmp/modern-studio/" + name + ".png"); }
    }
    [STAThread] static void Main()
    {
        Directory.CreateDirectory("tmp/modern-studio");
        var settingsFile = typeof(SettingsManager).GetField("settingsFile", BindingFlags.NonPublic | BindingFlags.Static);
        string originalFile = (string)settingsFile.GetValue(null); settingsFile.SetValue(null, Path.GetFullPath("tmp/modern-studio/preferences.ini"));
        try
        {
            SettingsManager.Save(new AppSettings()); Application.EnableVisualStyles();
            using (var f = new StudioQuietForm { Opacity = 0, ShowInTaskbar = false, Size = new Size(1440, 1000) })
            {
                f.Show(); typeof(Form1).GetMethod("ApplyTheme", Flags).Invoke(f, new object[] { "Forest Graphite" }); Application.DoEvents();
                var initialNav = (List<Button>)Field(f, "navButtons");
                foreach (int page in new[] { 1, 2, 3, 4 }) { initialNav[page].PerformClick(); Application.DoEvents(); }
                Size originalSize = f.Size;
                var originals = new List<Original>();
                foreach (string page in new[] { "pageExtract", "pageFormat", "pageCompose", "pageBtwLibrary" }) CaptureOriginal((Control)Field(f, page), originals);
                var source = (TextBox)Field(f, "txtSource1"); source.Text = "Keep selected templates";
                var caption = (TextBox)Field(f, "txtLane1Text"); caption.Text = "Keep custom job caption";
                var format = (ComboBox)Field(f, "cmbFormat"); format.SelectedItem = "png";
                var selector = (ComboBox)Field(f, "cmbInterfaceStyle"); var nav = (List<Button>)Field(f, "navButtons");
                selector.SelectedItem = InterfaceAppearance.Modern; Application.DoEvents();
                int count = All(f).Count();
                foreach (int page in new[] { 0, 1, 2, 3, 4, 5, 7 })
                { nav[page].PerformClick(); Screenshot(f, "page-" + page); }
                nav[4].PerformClick();
                var libraryLayout = (TableLayoutPanel)Field(f, "studioLibraryLayout");
                var folderToggle = (Button)Field(f, "studioLibraryFoldersToggle");
                Check(folderToggle.Visible && folderToggle.Parent.ClientRectangle.Contains(folderToggle.Bounds), "Source folder control must be visible and reachable");
                folderToggle.PerformClick(); Check(libraryLayout.GetControlFromPosition(0, 3).Visible, "Library folder disclosure");
                Screenshot(f, "library-source-folders"); folderToggle.PerformClick();
                var cancelLibrary = (Button)Field(f, "btnBtwLibraryCancel"); cancelLibrary.Enabled = true;
                Check(libraryLayout.GetControlFromPosition(0, 2).Visible, "Library progress must be shown during processing");
                cancelLibrary.Enabled = false; Check(!libraryLayout.GetControlFromPosition(0, 2).Visible, "Idle library should prioritise results");
                nav[1].PerformClick();
                var find = All((Control)Field(f, "pageExtract")).OfType<Button>().Single(b => b.Text == "Find by name");
                find.PerformClick(); Screenshot(f, "find-by-name");
                Check(((Button)Field(f, "modernHeaderTarget")) == Field(f, "btnFindExtract"), "Header action must follow selected extraction workflow");
                ((Button)Field(f, "btnFindExtract")).Enabled = false;
                Check(!((Button)Field(f, "modernHeaderAction")).Enabled, "Header action must respect busy/disabled processing state");
                ((Button)Field(f, "btnFindExtract")).Enabled = true;
                nav[3].PerformClick(); All((Control)Field(f, "pageCompose")).OfType<Button>().Single(b => b.Text == "Find images by barcode  +").PerformClick();
                Screenshot(f, "job-image-finder");
                f.Size = new Size(900, 600); Application.DoEvents(); nav[1].PerformClick(); Screenshot(f, "narrow-extraction");
                Check(((Control)Field(f, "modernHeaderAction")).Right < ((Control)Field(f, "modernAccountButton")).Left, "Narrow header actions overlap");
                nav[4].PerformClick(); Application.DoEvents(); Screenshot(f, "narrow-library");
                var searchFlow = (Control)((Control)Field(f, "txtBtwLibrarySearch")).Parent;
                Check(searchFlow.Right <= searchFlow.Parent.ClientSize.Width, "Narrow library commands overflow their host");
                f.Size = originalSize; Application.DoEvents();
                for (int i = 0; i < 8; i++)
                {
                    selector.SelectedItem = InterfaceAppearance.Current; Application.DoEvents();
                    foreach (int page in new[] { 1, 2, 3, 4 }) { nav[page].PerformClick(); Application.DoEvents(); }
                    foreach (var o in originals)
                    {
                        Check(o.Control.Parent == o.Parent, "Current parent changed: " + o.Control.GetType().Name);
                        if (o.Dock == DockStyle.None)
                            Check(o.Control.Bounds == o.Bounds, "Current geometry changed: " + o.Control.GetType().Name + " " + o.Control.Text + " expected " + o.Bounds + " got " + o.Control.Bounds + " font " + o.Font + " -> " + o.Control.Font + " parent " + o.ParentSize + " -> " + o.Parent.Size);
                        Check(o.Control.Dock == o.Dock && o.Control.Anchor == o.Anchor && o.Control.TabIndex == o.Tab, "Current layout/keyboard order changed");
                    }
                    Check(source.Text == "Keep selected templates" && caption.Text == "Keep custom job caption" && format.SelectedItem.ToString() == "png", "Unsaved workflow values lost");
                    selector.SelectedItem = InterfaceAppearance.Modern; Application.DoEvents();
                    Check(All(f).Count() == count, "Repeated switches duplicated controls");
                }
                selector.SelectedItem = InterfaceAppearance.Current; nav[1].PerformClick(); Screenshot(f, "current-restored");
                Check(SettingsManager.Load().InterfaceStyle == InterfaceAppearance.Current, "Preview implicitly overwrote user preferences");
            }
            Console.WriteLine("PASS: all Modern workspaces, responsive screenshots, extraction task tabs, busy-aware persistent actions, eight full reversions of original parents/geometry/keyboard order, unchanged unsaved data and no duplicate controls.");
        }
        finally { settingsFile.SetValue(null, originalFile); }
    }
}
