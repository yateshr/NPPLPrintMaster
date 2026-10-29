using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using NPPLPrintMaster;
class UpdatesQuietForm : Form1
{
    protected override void OnLoad(EventArgs e) { }
    protected override void OnShown(EventArgs e) { }
}
class VerifyUpdatesUi
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Get(Form1 form, string name) { return typeof(Form1).GetField(name, Flags).GetValue(form); }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    [STAThread] static void Main()
    {
        Application.EnableVisualStyles();
        using (var form = new UpdatesQuietForm { Opacity = 0, ShowInTaskbar = false })
        {
            form.Show();
            Check(Get(form, "updatesWindow") == null, "No unsolicited updates window");
            Check(form.MainMenuStrip.Items.OfType<ToolStripMenuItem>().Single(i => i.Text == "Help").DropDownItems.OfType<ToolStripMenuItem>().Any(i => i.Text == "Software Updates"), "Updates menu");
            typeof(Form1).GetMethod("ShowUpdates", Flags).Invoke(form, null);
            var window = (Form)Get(form, "updatesWindow"); window.Opacity = 0;
            Check(((Button)Get(form, "checkUpdatesButton")).Enabled, "Manual retry enabled");
            Check(!((Button)Get(form, "downloadUpdateButton")).Enabled, "No download without release");
            var releaseType = typeof(Form1).Assembly.GetType("NPPLPrintMaster.AppRelease");
            var release = Activator.CreateInstance(releaseType, true);
            releaseType.GetField("Version").SetValue(release, new Version(2, 0, 0, 0));
            typeof(Form1).GetField("availableRelease", Flags).SetValue(form, release);
            typeof(Form1).GetField("updateStatus", Flags).SetValue(form, "Version 2.0.0.0 is available.");
            typeof(Form1).GetMethod("RefreshUpdates", Flags).Invoke(form, null);
            Check(((Button)Get(form, "downloadUpdateButton")).Enabled && ((Button)Get(form, "installUpdateButton")).Enabled, "Both update actions available");
            window.Close();
            Check(Get(form, "updatesWindow") == null, "Dismiss update without affecting main app");
            form.Close();
        }
        Console.WriteLine("PASS: dedicated Updates menu, quiet initial state, manual retry, download/install availability and dismissal.");
    }
}
