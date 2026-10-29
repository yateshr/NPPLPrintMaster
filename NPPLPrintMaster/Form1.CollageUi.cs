using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace NPPLPrintMaster
{
    public partial class Form1
    {
        private Label collageExportStatus;
        private Panel collageWorkspace;
        private CollageCard collageImportCard, collageLayoutCard, collageExportCard;
        private readonly List<Label> collageSecondaryLabels = new List<Label>();
        private readonly List<CollageTextField> collageFields = new List<CollageTextField>();
        private readonly List<Control> collageDetailControls = new List<Control>();
        private CollageTextField collageSourceField, collageOutputField, collageNameField, collageHeaderField;
        private Button collageChooseFiles, collageChooseFolder, collageBrowseOutput, collageDetailsToggle;
        private Label collageSourceLabel, collageFormatLabel, collageGridLabel, collageOrientationLabel;
        private Label collageOutputLabel, collageNameLabel, collagePaperSummary;
        private bool collageDetailsVisible, collageLayingOut;

        private void OpenCollagePage() { SwitchPage(pageCollage, btnNavCollage, "Image Collage"); }

        private void BuildCollageWorkspace(GroupBox oldGroup, params Control[][] rows)
        {
            oldGroup.Controls.Clear(); oldGroup.Dispose();
            collageWorkspace = new Panel { Dock = DockStyle.Top, MinimumSize = new Size(520, 0) };
            collageImportCard = MakeCollageCard(1, "Import images", "Choose your source images or continue a saved job.");
            collageLayoutCard = MakeCollageCard(2, "Layout and appearance", "Choose your sheet, then fine-tune the arrangement.");
            collageExportCard = MakeCollageCard(3, "Export collage", "Save your finished print-ready file.");
            var title = new Label { Text = "Create a print-ready collage", AutoSize = true,
                Font = new Font("Segoe UI", 18, FontStyle.Bold), Location = new Point(0, 0) };
            var subtitle = CollageLabel("Import your images, arrange the sheet, and export when ready.", true);
            subtitle.Location = new Point(1, 37); subtitle.AutoSize = true;
            collageWorkspace.Controls.Add(title); collageWorkspace.Controls.Add(subtitle);
            collageWorkspace.Controls.Add(collageImportCard);
            collageWorkspace.Controls.Add(collageLayoutCard);
            collageWorkspace.Controls.Add(collageExportCard);
            collageChooseFiles = (Button)rows[0][1]; collageChooseFolder = (Button)rows[0][2];
            collageBrowseOutput = (Button)rows[6][1];
            collageChooseFiles.Text = "Choose files"; collageChooseFolder.Text = "Choose folder";
            btnImportCollageLayout.Text = "Open saved layout / PDF";
            btnClearCollageArrangement.Text = "Reset arrangement";
            collageBrowseOutput.Text = "Browse";
            btnGenerateCollage.Tag = "PrimaryAction";
            btnArrangeCollage.Tag = "PrimaryAction";
            collageSourceField = MakeCollageField(txtCollageSource);
            collageOutputField = MakeCollageField(txtCollageOutput);
            collageNameField = MakeCollageField(txtCollageFileName);
            collageNameField.DisabledCaption = "Generated automatically";
            collageHeaderField = MakeCollageField(txtCollageHeader);
            collageSourceLabel = CollageLabel("Source images");
            collageFormatLabel = CollageLabel("Output format");
            collageGridLabel = CollageLabel("Grid / columns x rows");
            collageOrientationLabel = CollageLabel("Orientation");
            collageOutputLabel = CollageLabel("Destination folder");
            collageNameLabel = CollageLabel("Custom file name");
            collagePaperSummary = CollageLabel(string.Empty, true);
            chkCollageAutoFileName.Text = "Automatic file name";
            collageExportStatus = CollageLabel("Choose images to begin.", true);
            collageExportStatus.AutoEllipsis = true;
            collageDetailsToggle = new CollageUiButton { Text = "+ Caption and Page Details" };
            collageDetailsToggle.Click += (s,e) => {
                collageDetailsVisible = !collageDetailsVisible;
                collageDetailsToggle.Text = (collageDetailsVisible ? "- Hide" : "+") + " Caption and Page Details";
                LayoutCollageWorkspace();
            };
            collageImportCard.Controls.AddRange(new Control[] { collageSourceLabel, collageSourceField,
                collageChooseFiles, collageChooseFolder, chkCollageSubDirs, btnImportCollageLayout });
            collageLayoutCard.Controls.AddRange(new Control[] { collageFormatLabel, cmbCollageOutputType,
                collageGridLabel, cmbCollageColumns, rows[2][4], cmbCollageRows,
                collageOrientationLabel, cmbCollageOrientation, btnCollagePaper, collagePaperSummary,
                btnArrangeCollage, btnClearCollageArrangement, collageDetailsToggle });
            foreach(Control control in rows[4]) collageDetailControls.Add(control);
            collageDetailControls.Add(rows[5][0]); collageDetailControls.Add(collageHeaderField);
            collageDetailControls.Add(chkCollageDate); collageDetailControls.Add(chkCollagePageNumbers);
            foreach(Control control in collageDetailControls) collageLayoutCard.Controls.Add(control);
            collageExportCard.Controls.AddRange(new Control[] { collageOutputLabel, collageOutputField, collageBrowseOutput,
                chkCollageAutoFileName, collageNameLabel, collageNameField, btnGenerateCollage, collageExportStatus });
            var exportHint = CollageLabel("Need to edit later? Save the layout in Arrange Images.", true);
            exportHint.Name = "EditableLayoutHint";
            collageExportCard.Controls.Add(exportHint);
            foreach(Control control in WalkCollageControls(collageWorkspace))
            {
                if (!(control is CollageCard) && !(control is Label)) control.Font = new Font("Segoe UI", 9.5f);
                if (control is Button) { control.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold); control.Cursor = Cursors.Hand; }
                var combo = control as ComboBox;
                if (combo != null) combo.ItemHeight = 30;
            }
            pageCollage.Controls.Add(collageWorkspace);
            collageWorkspace.SizeChanged += (s,e) => LayoutCollageWorkspace();
            cmbCollageOrientation.SelectedIndexChanged += (s,e) => UpdateCollagePaperSummary();
            btnCollagePaper.TextChanged += (s,e) => UpdateCollagePaperSummary();
            LayoutCollageWorkspace();
        }

        private CollageCard MakeCollageCard(int step, string title, string description)
        {
            var card = new CollageCard { Step = step };
            var heading = new Label { Text = title, Location = new Point(64, 20), Size = new Size(260, 26),
                Font = new Font("Segoe UI", 12, FontStyle.Bold), AutoEllipsis = true };
            var hint = CollageLabel(description, true);
            hint.Location = new Point(64, 49); hint.Size = new Size(300, 26); hint.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            card.Controls.Add(heading); card.Controls.Add(hint);
            return card;
        }

        private Label CollageLabel(string text, bool secondary = false)
        {
            var label = new Label { Text = text, Font = new Font("Segoe UI", 9.5f), Size = new Size(250, 22),
                AutoEllipsis = true, BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleLeft };
            if (secondary) collageSecondaryLabels.Add(label);
            return label;
        }

        private CollageTextField MakeCollageField(TextBox text)
        {
            var field = new CollageTextField(text) { Font = new Font("Segoe UI", 9.5f) };
            collageFields.Add(field); return field;
        }

        private static IEnumerable<Control> WalkCollageControls(Control parent)
        {
            foreach(Control control in parent.Controls) {
                yield return control;
                foreach(Control child in WalkCollageControls(control)) yield return child;
            }
        }

        private static void PlaceCollage(Control control, int x, int y, int width, int height = 38)
        { control.SetBounds(x, y, Math.Max(20, width), height); }

        private void LayoutCollageWorkspace()
        {
            if (collageWorkspace == null || collageLayingOut) return;
            collageLayingOut = true;
            try {
                int width = Math.Max(520, collageWorkspace.ClientSize.Width);
                bool wide = width >= 960;
                int exportWidth = wide ? 340 : width;
                int leftWidth = wide ? width - exportWidth - 24 : width;
                int layoutHeight = collageDetailsVisible ? 450 : 290;
                collageImportCard.SetBounds(0, 76, leftWidth, 216);
                collageLayoutCard.SetBounds(0, 308, leftWidth, layoutHeight);
                collageExportCard.SetBounds(wide ? leftWidth + 24 : 0,
                    wide ? 76 : 324 + layoutHeight, exportWidth, 466);
                collageWorkspace.Height = wide ? Math.Max(308 + layoutHeight, 542) + 24 : 814 + layoutHeight;
                foreach(var card in new[] { collageImportCard, collageLayoutCard, collageExportCard }) {
                    card.Controls[0].Width = Math.Max(100, card.Width - 88);
                    card.Controls[1].Width = Math.Max(100, card.Width - 88);
                }
                int w = leftWidth;
                PlaceCollage(collageSourceLabel,24,84,w-48,22);
                PlaceCollage(collageSourceField,24,110,w-302);
                PlaceCollage(collageChooseFiles,w-266,110,110);
                PlaceCollage(collageChooseFolder,w-144,110,120);
                PlaceCollage(chkCollageSubDirs,24,167,190,24);
                PlaceCollage(btnImportCollageLayout,w-244,160,220,36);
                int column=(w-80)/3;
                PlaceCollage(collageFormatLabel,24,86,column,22);
                PlaceCollage(cmbCollageOutputType,24,112,column);
                PlaceCollage(collageGridLabel,40+column,86,column,22);
                PlaceCollage(cmbCollageColumns,40+column,112,(column-28)/2);
                PlaceCollage(cmbCollageRows,40+column+(column+28)/2,112,(column-28)/2);
                Control by = collageLayoutCard.Controls[6];
                PlaceCollage(by,40+column+(column-28)/2+7,119,20,24);
                PlaceCollage(collageOrientationLabel,56+column*2,86,column,22);
                PlaceCollage(cmbCollageOrientation,56+column*2,112,column);
                PlaceCollage(btnCollagePaper,24,164,column);
                PlaceCollage(collagePaperSummary,40+column,168,w-column-64,30);
                PlaceCollage(btnArrangeCollage,24,222,150,40);
                PlaceCollage(btnClearCollageArrangement,186,222,132,40);
                PlaceCollage(collageDetailsToggle,330,222,w-354,40);
                foreach(Control control in collageDetailControls) control.Visible=collageDetailsVisible;
                PlaceCollage(chkCollageShowFilename,24,284,w-48,24);
                PlaceCollage(chkCollageBorders,24,316,190,24);
                PlaceCollage(collageDetailControls[2],240,316,64,24);
                PlaceCollage(cmbCollageTextSize,306,312,90);
                PlaceCollage(collageDetailControls[4],24,355,88,22);
                PlaceCollage(collageHeaderField,112,350,w-136);
                PlaceCollage(chkCollageDate,24,394,90,24);
                PlaceCollage(chkCollagePageNumbers,132,394,160,24);
                w=exportWidth;
                PlaceCollage(collageOutputLabel,24,86,w-48,22);
                PlaceCollage(collageOutputField,24,112,w-144);
                PlaceCollage(collageBrowseOutput,w-108,112,84);
                PlaceCollage(chkCollageAutoFileName,24,174,w-48,26);
                PlaceCollage(collageNameLabel,24,214,w-48,22);
                PlaceCollage(collageNameField,24,240,w-48);
                PlaceCollage(collageExportStatus,24,304,w-48,40);
                PlaceCollage(btnGenerateCollage,24,358,w-48,44);
                PlaceCollage(collageExportCard.Controls["EditableLayoutHint"],24,418,w-48,34);
                UpdateCollagePaperSummary();
            } finally { collageLayingOut=false; }
        }

        private void UpdateCollagePaperSummary()
        {
            if (collagePaperSummary == null || cmbCollageOrientation == null) return;
            var page=collagePaperSetup.Clone(); page.Landscape=cmbCollageOrientation.SelectedIndex==1;
            collagePaperSummary.Text=page.PaperDescription;
        }

        private void ApplyCollageDesign()
        {
            if (collageWorkspace == null || activeTheme == null) return;
            var theme=activeTheme;
            collageWorkspace.BackColor=pageCollage.BackColor;
            foreach(var card in new[]{collageImportCard,collageLayoutCard,collageExportCard}) {
                card.BackColor=theme.ContentBg; card.BorderColor=theme.BorderColor; card.AccentColor=theme.ActiveColor; card.Invalidate();
            }
            foreach(Control control in WalkCollageControls(collageWorkspace)) {
                if (control is Label) control.ForeColor=theme.TextColor;
                var combo=control as CollageThemedComboBox; if(combo!=null) { combo.RoundedAppearance=IsModernInterface; combo.SetTheme(theme); combo.ItemHeight=30; }
                var button=control as CollageUiButton;
                if(button!=null) {
                    bool primary=object.Equals(button.Tag,"PrimaryAction");
                    button.BackColor=primary?theme.ActiveColor:theme.ControlBg;
                    button.ForeColor=primary?ThemeManager.GetContrastTextColor(theme.ActiveColor):theme.TextColor;
                    button.MutedColor=theme.MutedTextColor;
                    button.FlatAppearance.BorderColor=primary?theme.ActiveColor:theme.BorderColor;
                    button.FlatAppearance.MouseOverBackColor=primary ? CollageWorkspaceUi.Blend(theme.ActiveColor,
                        button.ForeColor==Color.White?Color.Black:Color.White,0.08f) : theme.ButtonHover;
                    button.Invalidate();
                }
            }
            foreach(var label in collageSecondaryLabels) label.ForeColor=theme.MutedTextColor;
            foreach(var field in collageFields) {
                field.BackColor=theme.ReadOnlyBg; field.ForeColor=theme.ActiveColor;
                field.BorderColor=theme.BorderColor; field.MutedColor=theme.MutedTextColor;
                foreach(Control child in field.Controls) {
                    child.BackColor=theme.ReadOnlyBg; child.ForeColor=theme.TextColor;
                    var text=child as TextBox; if(text!=null) { text.BorderStyle=BorderStyle.None; text.AutoSize=false; }
                }
                field.Invalidate();
            }
            LayoutCollageWorkspace();
        }
    }
}
