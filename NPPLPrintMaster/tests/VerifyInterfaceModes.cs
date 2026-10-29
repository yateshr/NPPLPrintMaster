using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using NPPLPrintMaster;

class InterfaceTestMain : Form1
{
    protected override void OnLoad(EventArgs e) { }
    protected override void OnShown(EventArgs e) { }
}
class VerifyInterfaceModes
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static object Field(object value, string name)
    { return (value is Form1 ? typeof(Form1) : value.GetType()).GetField(name, Flags).GetValue(value); }
    static object Call(Form1 form, string name, params object[] args)
    { return typeof(Form1).GetMethod(name, Flags).Invoke(form, args); }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static int Count(Control parent) { return 1 + parent.Controls.Cast<Control>().Sum(Count); }
    static void Capture(Form form, string name)
    {
        Application.DoEvents();
        using (var bitmap = new Bitmap(form.Width, form.Height))
        { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save("tmp/pdfs/interface-check/" + name + ".png"); }
    }

    [STAThread] static void Main()
    {
        string root = Path.GetFullPath("tmp/pdfs/interface-check"); Directory.CreateDirectory(root);
        var settingsPath = typeof(SettingsManager).GetField("settingsFile", BindingFlags.Static | BindingFlags.NonPublic);
        string original = (string)settingsPath.GetValue(null);
        string temporary = Path.Combine(root, "settings-test.ini");
        settingsPath.SetValue(null, temporary);
        try
        {
            File.WriteAllText(temporary, "Theme=Forest Graphite\nDefaultDpi=800\n");
            Check(SettingsManager.Load().InterfaceStyle == InterfaceAppearance.Current, "Older settings must start in Current UI");
            File.AppendAllText(temporary, "InterfaceStyle=unsupported future interface\n");
            Check(SettingsManager.Load().InterfaceStyle == InterfaceAppearance.Current, "Unknown setting must fall back to Current UI");
            File.WriteAllText(temporary, "Theme=Forest Graphite\nDefaultDpi=800\n");
            using (var main = new InterfaceTestMain())
            {
                main.Opacity = 0; main.ShowInTaskbar = false; main.Size = new Size(1400, 1000); main.Show();
                Call(main, "ApplyTheme", "Forest Graphite");
                var selector = (ComboBox)Field(main, "cmbInterfaceStyle");
                var nav = (List<Button>)Field(main, "navButtons");
                Check(selector.SelectedItem.ToString() == InterfaceAppearance.Current, "Current must be default");
                Check(Field(main, "modernHome") == null, "Modern UI must not be built in Current mode");
                var caption = (TextBox)Field(main, "txtCollageHeader"); caption.Text = "Keep this unsaved job";
                var columns = (ComboBox)Field(main, "cmbCollageColumns"); columns.SelectedIndex = 3;
                Control originalControl = caption;
                int sidebarWidth = ((Panel)Field(main, "pnlSidebar")).Width;
                var bounds = nav.Select(b => b.Bounds).ToArray();
                var texts = nav.Select(b => b.Text).ToArray();
                selector.SelectedItem = InterfaceAppearance.Modern; Application.DoEvents();
                Check(Field(main, "modernHome") != null, "Modern home not created");
                Check(((Label)Field(main, "interfacePreviewBadge")).Visible, "Modern mode needs a preview indicator");
                Check(!((ComboBox)Field(main, "cmbTheme")).Enabled, "Current theme preference must be preserved separately");
                Check(SettingsManager.Load().InterfaceStyle == InterfaceAppearance.Current, "Preview should not implicitly write preferences");
                nav[0].PerformClick(); Capture(main, "modern-overview");
                Call(main, "OpenCollagePage"); Capture(main, "modern-collage");
                nav[nav.Count - 1].PerformClick(); Capture(main, "interface-setting");
                Check(!((ComboBox)Field(main, "cmbInterfaceStyle")).Visible, "Modern settings should hide the old settings form");
                var modernExport1 = (TextBox)Field(main, "modernDefaultExport1");
                modernExport1.Text = "test/extraction";
                Check(((TextBox)Field(main, "txtDefExport1")).Text == "test/extraction", "Modern preferences must update the existing values");
                ((TextBox)Field(main, "txtDefExport2")).Text = "test/format";
                Check(((TextBox)Field(main, "modernDefaultExport2")).Text == "test/format", "Preference binding must work both ways");
                ((NumericUpDown)Field(main, "modernDefaultDpi")).Value = 1200;
                Check(((NumericUpDown)Field(main, "numDefDpi")).Value == 1200, "Modern DPI control must preserve the shared setting");
                int count = Count(main);
                for (int i = 0; i < 12; i++)
                {
                    selector.SelectedItem = InterfaceAppearance.Current;
                    Check(((Panel)Field(main, "pnlSidebar")).Width == sidebarWidth, "Sidebar width must restore");
                    Check(nav.Select(b => b.Bounds).SequenceEqual(bounds), "Navigation geometry must restore");
                    Check(nav.Select(b => b.Text).SequenceEqual(texts), "Navigation labels must restore");
                    selector.SelectedItem = InterfaceAppearance.Modern;
                }
                Check(Count(main) == count, "Switching must not recreate or duplicate controls");
                Check(object.ReferenceEquals(originalControl, Field(main, "txtCollageHeader")) && caption.Text == "Keep this unsaved job" && columns.SelectedIndex == 3,
                    "Switching must preserve controls and job values");
                var current = (AppSettings)Field(main, "currentSettings");
                Call(main, "SavePreferences", false);
                var saved = SettingsManager.Load();
                Check(saved.InterfaceStyle == InterfaceAppearance.Modern && saved.Theme == "Forest Graphite" && saved.DefaultExport1 == "test/extraction" && saved.DefaultDpi == 1200,
                    "Interface choice must persist independently of existing settings");
                ((Button)Field(main, "modernRestoreCurrent")).PerformClick();
                Check(selector.SelectedItem.ToString() == InterfaceAppearance.Current && !caption.IsDisposed, "Return-to-current action");
                var theme = (ComboBox)Field(main, "cmbTheme"); theme.SelectedItem = "BarTender 10 Classic";
                int header = ((Panel)Field(main, "pnlHeader")).Height;
                Padding padding = ((Panel)Field(main, "pnlContent")).Padding;
                selector.SelectedItem = InterfaceAppearance.Modern; selector.SelectedItem = InterfaceAppearance.Current;
                Check(((Panel)Field(main, "pnlHeader")).Height == header && ((Panel)Field(main, "pnlContent")).Padding == padding,
                    "Current classic appearance geometry must restore");
                Check(theme.SelectedItem.ToString() == "BarTender 10 Classic" && theme.Enabled, "Original theme selection must remain available");
                nav[nav.Count - 1].PerformClick(); Capture(main, "current-restored");
                main.Hide();
            }
            using (var main = new InterfaceTestMain())
            {
                Check(((ComboBox)Field(main, "cmbInterfaceStyle")).SelectedItem.ToString() == InterfaceAppearance.Modern,
                    "Modern selection must survive restart");
                main.Opacity = 0; main.ShowInTaskbar = false; main.Show();
                Call(main, "ChangeInterfaceStyle");
                Check(Field(main, "modernHome") != null, "Saved modern mode must initialize on startup");
                var selector = (ComboBox)Field(main, "cmbInterfaceStyle"); selector.SelectedItem = InterfaceAppearance.Current;
                SettingsManager.Save((AppSettings)Field(main, "currentSettings"));
                Check(SettingsManager.Load().InterfaceStyle == InterfaceAppearance.Current, "Current selection must persist");
                main.Hide();
            }
            using (var editor = new CollageArrangementForm(new string[0], new CollagePageSetup(),
                new AppSettings { InterfaceStyle = InterfaceAppearance.Modern }))
            {
                var palette = (ThemeColors)Field(editor, "_theme");
                Check(palette.ContentBg == Color.White, "Collage editor must follow the selected interface");
            }
            Console.WriteLine("PASS: safe default and legacy fallback, lazy preview creation, reversible navigation/geometry/themes, 12 switches without duplicates, unsaved job preservation, independent settings persistence, restart and current-mode restore, matching collage editor.");
        }
        finally
        {
            settingsPath.SetValue(null, original);
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
