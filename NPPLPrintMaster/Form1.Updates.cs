using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NPPLPrintMaster
{
    public partial class Form1
    {
        private readonly AppUpdateService updates = new AppUpdateService();
        private readonly CancellationTokenSource updateLifetime = new CancellationTokenSource();
        private AppRelease availableRelease;
        private string updateStatus = "Updates have not been checked yet.";
        private bool checkingUpdates, downloadingUpdate;
        private Form updatesWindow;
        private Label updatesStatus;
        private Button checkUpdatesButton, downloadUpdateButton, installUpdateButton;
        private ProgressBar updateProgress;
        private string downloadedInstaller;

        private void InitializeUpdates(ToolStripMenuItem help)
        {
            help.DropDownItems.Add(new ToolStripSeparator());
            var item = new ToolStripMenuItem("Software Updates");
            item.Click += (s, e) => ShowUpdates(); help.DropDownItems.Add(item);
            Shown += async (s, e) => { await Task.Delay(1500); if (!IsDisposed) await CheckUpdatesAsync(true); };
            FormClosed += (s, e) => updateLifetime.Cancel();
        }
        private async Task CheckUpdatesAsync(bool startup)
        {
            if (checkingUpdates || downloadingUpdate) return;
            checkingUpdates = true; updateStatus = "Checking for updates…"; RefreshUpdates();
            try
            {
                var release = await updates.CheckAsync(updateLifetime.Token);
                if (availableRelease == null || release == null || availableRelease.Url != release.Url) downloadedInstaller = null;
                availableRelease = release;
                updateStatus = release == null ? "You are using the latest version." : "Version " + release.Version + " is available.";
                if (startup && release != null && !IsDisposed && (updatesWindow == null || !updatesWindow.Visible))
                {
                    // Only a confirmed newer, downloadable release can trigger a startup notice.
                    ShowUpdates();
                }
            }
            catch (OperationCanceledException) { updateStatus = "Update check was cancelled."; }
            catch { updateStatus = "Could not check for updates. Check your connection or try again later."; }
            finally { checkingUpdates = false; if (!IsDisposed) RefreshUpdates(); }
        }
        private void ShowUpdates()
        {
            if (updatesWindow != null && !updatesWindow.IsDisposed) { updatesWindow.Activate(); return; }
            updatesWindow = new Form { Text = "Software Updates", Size = new Size(580, 390), MinimumSize = new Size(580, 390),
                StartPosition = FormStartPosition.CenterParent, Font = new Font("Segoe UI", 10), BackColor = Color.White, MaximizeBox = false };
            var title = new Label { Text = availableRelease == null ? "Keep PrintMaster up to date" : "Update available", Font = new Font("Segoe UI", 17, FontStyle.Bold), AutoSize = true, Location = new Point(24, 24) };
            var version = new Label { Text = "Installed version: " + AppUpdateService.CurrentVersion, AutoSize = true, Location = new Point(26, 74) };
            updatesStatus = new Label { Location = new Point(26, 112), Size = new Size(510, 55) };
            updateProgress = new ProgressBar { Location = new Point(26, 174), Size = new Size(510, 18) };
            checkUpdatesButton = new Button { Text = "Check for updates", Location = new Point(26, 214), Size = new Size(155, 40) };
            downloadUpdateButton = new Button { Text = "Download now", Location = new Point(190, 214), Size = new Size(150, 40) };
            installUpdateButton = new Button { Text = "Download && install", Location = new Point(350, 214), Size = new Size(186, 40) };
            var hint = new Label { Text = "Startup checks are silent unless an update is available.\nThe installer will guide you through closing the app and updating.", Location = new Point(26, 278), Size = new Size(510, 55), ForeColor = Color.DimGray };
            checkUpdatesButton.Click += async (s, e) => await CheckUpdatesAsync(false);
            downloadUpdateButton.Click += async (s, e) => await DownloadUpdateAsync(false);
            installUpdateButton.Click += async (s, e) => await DownloadUpdateAsync(true);
            updatesWindow.Controls.AddRange(new Control[] { title, version, updatesStatus, updateProgress, checkUpdatesButton, downloadUpdateButton, installUpdateButton, hint });
            updatesWindow.FormClosed += (s, e) => { updatesWindow = null; };
            RefreshUpdates(); updatesWindow.Show(this);
        }
        private void RefreshUpdates()
        {
            if (updatesWindow == null || updatesWindow.IsDisposed) return;
            updatesStatus.Text = updateStatus;
            checkUpdatesButton.Enabled = !checkingUpdates && !downloadingUpdate;
            downloadUpdateButton.Enabled = installUpdateButton.Enabled = availableRelease != null && !checkingUpdates && !downloadingUpdate;
            installUpdateButton.Text = downloadedInstaller == null ? "Download && install" : "Install update";
        }
        private async Task DownloadUpdateAsync(bool install)
        {
            if (availableRelease == null || downloadingUpdate || checkingUpdates) return;
            downloadingUpdate = true; RefreshUpdates();
            try
            {
                if (downloadedInstaller == null)
                {
                    updateStatus = "Downloading update…"; RefreshUpdates();
                    downloadedInstaller = await updates.DownloadAsync(availableRelease, new Progress<int>(value =>
                    { if (updatesWindow != null && !updatesWindow.IsDisposed) { updateProgress.Value = value; updatesStatus.Text = "Downloading update… " + value + "%"; } }), updateLifetime.Token);
                }
                AppUpdateService.VerifyFile(downloadedInstaller, availableRelease.Digest);
                if (install)
                {
                    // Use an interactive installer; never force-close editors or discard work.
                    Process.Start(new ProcessStartInfo(downloadedInstaller, "/DIR=\"" + Application.StartupPath + "\"") { UseShellExecute = true });
                    updateStatus = "Installer opened. Follow its instructions to complete the update.";
                }
                else
                {
                    updateStatus = "Download complete. Choose Install update when ready.";
                    Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + downloadedInstaller + "\"") { UseShellExecute = true });
                }
            }
            catch (OperationCanceledException) { updateStatus = "Download cancelled."; }
            catch (Exception ex) { downloadedInstaller = null; updateStatus = "Update could not be completed. " + ex.Message; }
            finally { downloadingUpdate = false; if (!IsDisposed) RefreshUpdates(); }
        }
    }
}
