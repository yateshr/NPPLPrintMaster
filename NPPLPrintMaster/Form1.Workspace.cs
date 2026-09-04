using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Threading.Tasks;

namespace NPPLPrintMaster
{
    public partial class Form1 : Form
    {
        private WorkspaceData BuildCurrentWorkspaceData()
        {
            return new WorkspaceData
            {
                Lane1Files = GetFilesFromLane(pnlProducts),
                Lane2Files = GetFilesFromLane(pnlCartons),
                Lane3Files = GetFilesFromLane(pnlOthers),
                Text1 = txtLane1Text.Text,
                Text2 = txtLane2Text.Text,
                ShowText1 = chkShowText1.Checked,
                ShowText2 = chkShowText2.Checked,
                Font1 = fontLane1,
                Font2 = fontLane2,
                LayoutMemory = new List<string>(customLayoutMemory)
            };
        }

        private void SaveWorkspace()
        {
            using (SaveFileDialog sfd = new SaveFileDialog { Filter = "NPPL Project|*.nppl", FileName = "MyProject.nppl" })
            {
                if (DialogMemoryManager.ShowSaveDialog(sfd, "Workspace.SaveProject") == DialogResult.OK)
                {
                    WorkspaceData data = BuildCurrentWorkspaceData();
                    WorkspaceEngine.SaveToFile(sfd.FileName, data);
                    Logger.LogAction("WORKSPACE_SAVE", $"Saved to {sfd.FileName}");
                    MessageBox.Show("Workspace and Custom Layout saved successfully!", "Saved");
                }
            }
        }

        private void LoadWorkspace()
        {
            using (OpenFileDialog ofd = new OpenFileDialog { Filter = "NPPL Project|*.nppl" })
            {
                if (DialogMemoryManager.ShowOpenDialog(ofd, "Workspace.LoadPreviousProject") == DialogResult.OK)
                {
                    WorkspaceData data = WorkspaceEngine.LoadFromFile(ofd.FileName);
                    ClearLane(pnlProducts); ClearLane(pnlCartons); ClearLane(pnlOthers); customLayoutMemory.Clear();
                    foreach (string file in data.Lane1Files) AddThumbnail(pnlProducts, file);
                    foreach (string file in data.Lane2Files) AddThumbnail(pnlCartons, file);
                    foreach (string file in data.Lane3Files) AddThumbnail(pnlOthers, file);
                    txtLane1Text.Text = data.Text1; txtLane2Text.Text = data.Text2; chkShowText1.Checked = data.ShowText1; chkShowText2.Checked = data.ShowText2; fontLane1 = data.Font1; fontLane2 = data.Font2; customLayoutMemory = data.LayoutMemory;
                    Logger.LogAction("WORKSPACE_LOAD", $"Loaded from {ofd.FileName}");
                }
            }
        }

    }
}
