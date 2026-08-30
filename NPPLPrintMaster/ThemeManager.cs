using System.Drawing;
using System.Windows.Forms;

namespace NPPLPrintMaster
{
    public class ThemeColors
    {
        public Color SidebarColor, SidebarHover, ActiveColor, BgLight, ContentBg, TextColor, ControlBg;
    }

    public static class ThemeManager
    {
        public static ThemeColors GetTheme(string themeName)
        {
            ThemeColors t = new ThemeColors();
            if (themeName == "Midnight Dark")
            {
                t.SidebarColor = Color.FromArgb(25, 25, 28); t.SidebarHover = Color.FromArgb(45, 45, 50);
                t.BgLight = Color.FromArgb(30, 30, 30); t.ContentBg = Color.FromArgb(45, 45, 48);
                t.TextColor = Color.White; t.ControlBg = Color.FromArgb(55, 55, 58);
                t.ActiveColor = Color.FromArgb(0, 122, 204);
            }
            else if (themeName == "Industrial")
            {
                t.SidebarColor = Color.Black; t.SidebarHover = Color.FromArgb(35, 35, 35);
                t.BgLight = Color.FromArgb(40, 40, 40); t.ContentBg = Color.FromArgb(50, 50, 50);
                t.TextColor = Color.White; t.ControlBg = Color.FromArgb(70, 70, 70);
                t.ActiveColor = Color.DarkOrange;
            }
            else if (themeName == "Modern Windows")
            {
                t.SidebarColor = Color.FromArgb(240, 240, 240); t.SidebarHover = Color.FromArgb(220, 220, 220);
                t.BgLight = Color.FromArgb(250, 250, 250); t.ContentBg = Color.White;
                t.TextColor = Color.Black; t.ControlBg = Color.White;
                t.ActiveColor = Color.FromArgb(0, 99, 177);
            }
            else if (themeName == "NPPL Corporate")
            {
                t.SidebarColor = Color.FromArgb(10, 35, 66); // Deep Navy Blue
                t.SidebarHover = Color.FromArgb(20, 55, 96);
                t.BgLight = Color.FromArgb(245, 246, 248);
                t.ContentBg = Color.White;
                t.TextColor = Color.Black;
                t.ControlBg = Color.White;
                t.ActiveColor = Color.FromArgb(0, 166, 81); // Safety Green
            }
            else if (themeName == "Dracula Dark")
            {
                t.SidebarColor = Color.FromArgb(40, 42, 54);
                t.SidebarHover = Color.FromArgb(68, 71, 90);
                t.BgLight = Color.FromArgb(40, 42, 54);
                t.ContentBg = Color.FromArgb(68, 71, 90);
                t.TextColor = Color.FromArgb(248, 248, 242);
                t.ControlBg = Color.FromArgb(40, 42, 54);
                t.ActiveColor = Color.FromArgb(255, 121, 198); // Dracula Pink
            }
            else if (themeName == "Discord Theme")
            {
                t.SidebarColor = Color.FromArgb(30, 31, 34);
                t.SidebarHover = Color.FromArgb(43, 45, 49);
                t.BgLight = Color.FromArgb(49, 51, 56);
                t.ContentBg = Color.FromArgb(43, 45, 49);
                t.TextColor = Color.FromArgb(219, 222, 225);
                t.ControlBg = Color.FromArgb(30, 31, 34);
                t.ActiveColor = Color.FromArgb(88, 101, 242); // Discord Blurple
            }
            else if (themeName == "GitHub Light")
            {
                t.SidebarColor = Color.FromArgb(246, 248, 250);
                t.SidebarHover = Color.FromArgb(234, 238, 242);
                t.BgLight = Color.FromArgb(255, 255, 255);
                t.ContentBg = Color.FromArgb(246, 248, 250);
                t.TextColor = Color.FromArgb(36, 41, 47);
                t.ControlBg = Color.FromArgb(255, 255, 255);
                t.ActiveColor = Color.FromArgb(9, 105, 218); // GitHub Blue
            }
            else // BarTender Classic
            {
                t.SidebarColor = Color.FromArgb(30, 30, 35); t.SidebarHover = Color.FromArgb(50, 50, 55);
                t.BgLight = Color.FromArgb(245, 246, 248); t.ContentBg = Color.White;
                t.TextColor = Color.Black; t.ControlBg = Color.White;
                t.ActiveColor = Color.FromArgb(0, 122, 204);
            }
            return t;
        }

        public static void ApplyColorsToControls(Control.ControlCollection controls, ThemeColors t)
        {
            foreach (Control c in controls)
            {
                if (c is Label lbl) { lbl.ForeColor = t.TextColor; lbl.BackColor = Color.Transparent; }
                else if (c is CheckBox chk) { chk.ForeColor = t.TextColor; chk.BackColor = Color.Transparent; }
                else if (c is GroupBox grp) { grp.ForeColor = t.TextColor; grp.BackColor = Color.Transparent; ApplyColorsToControls(grp.Controls, t); }
                else if (c is TextBox txt && !txt.ReadOnly) { txt.BackColor = t.ControlBg; txt.ForeColor = t.TextColor; }
                else if (c is TextBox txtR && txtR.ReadOnly) { txtR.BackColor = (t.ContentBg.R < 100) ? Color.FromArgb(t.ControlBg.R - 10, t.ControlBg.G - 10, t.ControlBg.B - 10) : Color.WhiteSmoke; txtR.ForeColor = t.TextColor; }
                else if (c is ComboBox cmb) { cmb.BackColor = t.ControlBg; cmb.ForeColor = t.TextColor; }
                else if (c is NumericUpDown num) { num.BackColor = t.ControlBg; num.ForeColor = t.TextColor; }
                else if (c is FlowLayoutPanel flp) { flp.BackColor = t.ControlBg; ApplyColorsToControls(flp.Controls, t); }
                else if (c is Panel pnl) { ApplyColorsToControls(pnl.Controls, t); }

                if (c is Button btn && btn.Text != "▶ Extract Images" && btn.Text != "▶ Format Images" && btn.Text != "▶ Generate Basic Job Card" && btn.Text != "🗑️ Clear All Images" && btn.Text != "Pick Color")
                {
                    btn.BackColor = t.ControlBg;
                    btn.ForeColor = t.TextColor;
                    btn.FlatAppearance.BorderColor = (t.ContentBg.R < 100) ? Color.Gray : Color.LightGray;
                }
            }
        }
    }
}