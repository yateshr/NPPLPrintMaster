using System;
using System.Drawing;
using System.Windows.Forms;

namespace NPPLPrintMaster
{
    public class ThemeColors
    {
        public Color SidebarColor;
        public Color SidebarHover;
        public Color ActiveColor;
        public Color BgLight;
        public Color ContentBg;
        public Color TextColor;
        public Color ControlBg;

        public Color MutedTextColor;
        public Color BorderColor;
        public Color ReadOnlyBg;
        public Color ButtonHover;
    }

    public static class ThemeManager
    {
        // Deliberately small list: each theme has a genuinely different
        // visual identity instead of many near-duplicates.
        public static readonly object[] ThemeNames =
        {
            "Midnight Dark",
            "Soft Slate",
            "Forest Graphite",
            "BarTender Blue",
            "BarTender 10 Classic",
            "SAP Classic"
        };

        public static ThemeColors GetTheme(string themeName)
        {
            switch (themeName)
            {
                // ------------------------------------------------
                // MIDNIGHT DARK
                // The existing dark theme the user preferred.
                // ------------------------------------------------
                case "Midnight Dark":
                    return new ThemeColors
                    {
                        SidebarColor = Color.FromArgb(10, 17, 29),
                        SidebarHover = Color.FromArgb(22, 34, 52),
                        ActiveColor = Color.FromArgb(48, 145, 255),
                        BgLight = Color.FromArgb(15, 23, 36),
                        ContentBg = Color.FromArgb(22, 31, 46),
                        TextColor = Color.FromArgb(232, 239, 248),
                        ControlBg = Color.FromArgb(31, 43, 61),
                        MutedTextColor = Color.FromArgb(153, 170, 192),
                        BorderColor = Color.FromArgb(58, 77, 101),
                        ReadOnlyBg = Color.FromArgb(25, 35, 50),
                        ButtonHover = Color.FromArgb(42, 58, 80)
                    };

                // ------------------------------------------------
                // SOFT SLATE
                // Dark mode without being extremely dark.
                // ------------------------------------------------
                case "Soft Slate":
                    return new ThemeColors
                    {
                        SidebarColor = Color.FromArgb(38, 45, 52),
                        SidebarHover = Color.FromArgb(52, 61, 70),
                        ActiveColor = Color.FromArgb(73, 151, 208),
                        BgLight = Color.FromArgb(43, 50, 57),
                        ContentBg = Color.FromArgb(50, 58, 66),
                        TextColor = Color.FromArgb(239, 243, 246),
                        ControlBg = Color.FromArgb(62, 72, 81),
                        MutedTextColor = Color.FromArgb(184, 194, 202),
                        BorderColor = Color.FromArgb(88, 101, 112),
                        ReadOnlyBg = Color.FromArgb(46, 54, 61),
                        ButtonHover = Color.FromArgb(76, 88, 99)
                    };

                // ------------------------------------------------
                // FOREST GRAPHITE
                // Built around the requested colors:
                // RGB(31,36,33) == #1F2421
                // #0A210F       == RGB(10,33,15)
                // ------------------------------------------------
                case "Forest Graphite":
                    return new ThemeColors
                    {
                        SidebarColor = Color.FromArgb(10, 33, 15),   // #0A210F
                        SidebarHover = Color.FromArgb(22, 54, 31),
                        ActiveColor = Color.FromArgb(72, 181, 108),
                        BgLight = Color.FromArgb(31, 36, 33),       // #1F2421
                        ContentBg = Color.FromArgb(40, 49, 43),
                        TextColor = Color.FromArgb(235, 242, 237),
                        ControlBg = Color.FromArgb(53, 66, 58),
                        MutedTextColor = Color.FromArgb(171, 190, 178),
                        BorderColor = Color.FromArgb(77, 96, 84),
                        ReadOnlyBg = Color.FromArgb(34, 42, 37),
                        ButtonHover = Color.FromArgb(65, 82, 71)
                    };

                // ------------------------------------------------
                // BARTENDER BLUE
                // A blue/gray enterprise desktop palette inspired by the
                // familiar BarTender-style production environment.
                // ------------------------------------------------
                case "BarTender Blue":
                    return new ThemeColors
                    {
                        SidebarColor = Color.FromArgb(18, 72, 116),
                        SidebarHover = Color.FromArgb(24, 94, 148),
                        ActiveColor = Color.FromArgb(0, 120, 212),
                        BgLight = Color.FromArgb(229, 238, 246),
                        ContentBg = Color.FromArgb(247, 250, 253),
                        TextColor = Color.FromArgb(26, 43, 57),
                        ControlBg = Color.FromArgb(255, 255, 255),
                        MutedTextColor = Color.FromArgb(91, 111, 128),
                        BorderColor = Color.FromArgb(171, 192, 210),
                        ReadOnlyBg = Color.FromArgb(232, 239, 245),
                        ButtonHover = Color.FromArgb(218, 232, 243)
                    };

                // ------------------------------------------------
                // BARTENDER 10 CLASSIC
                // Inspired by the BarTender 10.0 SR1 / Windows-classic
                // production UI: pale blue chrome, blue-gray workspace,
                // white controls, thin cool-gray borders and dark text.
                // ------------------------------------------------
                case "BarTender 10 Classic":
                    return new ThemeColors
                    {
                        // BarTender 10 SR1-inspired profile:
                        // pale classic chrome + darker desaturated editor/workspace.
                        SidebarColor = Color.FromArgb(82, 112, 149),
                        SidebarHover = Color.FromArgb(98, 132, 171),
                        ActiveColor = Color.FromArgb(62, 126, 190),
                        BgLight = Color.FromArgb(151, 176, 207),
                        ContentBg = Color.FromArgb(216, 230, 245),
                        TextColor = Color.FromArgb(18, 18, 18),
                        ControlBg = Color.FromArgb(248, 250, 252),
                        MutedTextColor = Color.FromArgb(70, 87, 108),
                        BorderColor = Color.FromArgb(118, 146, 180),
                        ReadOnlyBg = Color.FromArgb(231, 238, 246),
                        ButtonHover = Color.FromArgb(219, 230, 242)
                    };

                // ------------------------------------------------
                // SAP CLASSIC
                // Classic enterprise blue-gray / steel workspace.
                // ------------------------------------------------
                case "SAP Classic":
                    return new ThemeColors
                    {
                        SidebarColor = Color.FromArgb(47, 72, 88),
                        SidebarHover = Color.FromArgb(61, 93, 112),
                        ActiveColor = Color.FromArgb(10, 110, 209),
                        BgLight = Color.FromArgb(220, 227, 232),
                        ContentBg = Color.FromArgb(242, 246, 248),
                        TextColor = Color.FromArgb(29, 45, 62),
                        ControlBg = Color.FromArgb(255, 255, 255),
                        MutedTextColor = Color.FromArgb(91, 106, 119),
                        BorderColor = Color.FromArgb(164, 178, 188),
                        ReadOnlyBg = Color.FromArgb(226, 232, 236),
                        ButtonHover = Color.FromArgb(214, 226, 235)
                    };

                default:
                    // Old saved theme names automatically fall back safely.
                    return GetTheme("Forest Graphite");
            }
        }

        public static void ApplyColorsToControls(
            Control.ControlCollection controls,
            ThemeColors t)
        {
            if (controls == null || t == null)
                return;

            foreach (Control c in controls)
            {
                if (c is Label lbl)
                {
                    lbl.ForeColor = t.TextColor;
                    lbl.BackColor = Color.Transparent;
                }
                else if (c is CheckBox chk)
                {
                    chk.ForeColor = t.TextColor;
                    chk.BackColor = Color.Transparent;
                }
                else if (c is RadioButton radio)
                {
                    radio.ForeColor = t.TextColor;
                    radio.BackColor = Color.Transparent;
                }
                else if (c is GroupBox grp)
                {
                    grp.ForeColor = t.TextColor;
                    grp.BackColor = Color.Transparent;
                    ApplyColorsToControls(grp.Controls, t);
                }
                else if (c is RichTextBox rtb)
                {
                    // Preserve deliberately black live execution consoles.
                    bool looksLikeConsole =
                        rtb.BackColor.R < 25 &&
                        rtb.BackColor.G < 25 &&
                        rtb.BackColor.B < 25;

                    if (!looksLikeConsole)
                    {
                        rtb.BackColor =
                            rtb.ReadOnly ? t.ReadOnlyBg : t.ControlBg;
                        rtb.ForeColor = t.TextColor;
                    }
                }
                else if (c is TextBox txt)
                {
                    txt.BackColor =
                        txt.ReadOnly ? t.ReadOnlyBg : t.ControlBg;
                    txt.ForeColor = t.TextColor;
                }
                else if (c is ComboBox cmb)
                {
                    cmb.BackColor = t.ControlBg;
                    cmb.ForeColor = t.TextColor;
                }
                else if (c is NumericUpDown num)
                {
                    num.BackColor = t.ControlBg;
                    num.ForeColor = t.TextColor;
                }
                else if (c is FlowLayoutPanel flp)
                {
                    flp.BackColor = t.ControlBg;
                    ApplyColorsToControls(flp.Controls, t);
                }
                else if (c is Panel pnl)
                {
                    ApplyColorsToControls(pnl.Controls, t);
                }

                if (c is Button btn)
                {
                    ApplyButtonTheme(btn, t);
                }
            }
        }

        private static void ApplyButtonTheme(Button btn, ThemeColors t)
        {
            if (btn == null)
                return;

            string text = btn.Text ?? string.Empty;

            bool primaryAction =
                text.Contains("Extract Images") ||
                text.Contains("Find & Extract") ||
                text.Contains("Format Images") ||
                text.Contains("Generate Collage") ||
                text.Contains("Generate Job Card") ||
                text.Contains("Generate Basic Job Card") ||
                text.Contains("Save Settings");

            bool destructiveAction =
                text.Contains("Clear All");

            btn.FlatStyle = FlatStyle.Flat;

            if (primaryAction)
            {
                btn.BackColor = t.ActiveColor;
                btn.ForeColor = GetContrastTextColor(t.ActiveColor);
                btn.FlatAppearance.BorderColor = t.ActiveColor;
                btn.FlatAppearance.MouseOverBackColor =
                    Blend(t.ActiveColor, Color.White, IsLight(t.ActiveColor) ? -0.10f : 0.12f);
                btn.FlatAppearance.MouseDownBackColor =
                    Blend(t.ActiveColor, Color.Black, 0.16f);
            }
            else if (destructiveAction)
            {
                btn.BackColor = t.ControlBg;
                btn.ForeColor =
                    IsLight(t.ControlBg)
                        ? Color.FromArgb(176, 45, 45)
                        : Color.FromArgb(255, 130, 130);

                btn.FlatAppearance.BorderColor = btn.ForeColor;
                btn.FlatAppearance.MouseOverBackColor = t.ButtonHover;
            }
            else
            {
                btn.BackColor = t.ControlBg;
                btn.ForeColor = t.TextColor;
                btn.FlatAppearance.BorderColor = t.BorderColor;
                btn.FlatAppearance.MouseOverBackColor = t.ButtonHover;
                btn.FlatAppearance.MouseDownBackColor =
                    Blend(t.ButtonHover, t.ActiveColor, 0.15f);
            }
        }

        public static bool IsBarTender10Classic(string themeName)
        {
            return string.Equals(
                themeName,
                "BarTender 10 Classic",
                StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsLight(Color color)
        {
            double luminance =
                (color.R * 0.299) +
                (color.G * 0.587) +
                (color.B * 0.114);

            return luminance >= 155;
        }

        public static Color GetContrastTextColor(Color background)
        {
            return IsLight(background)
                ? Color.FromArgb(20, 25, 30)
                : Color.White;
        }

        private static Color Blend(Color a, Color b, float amount)
        {
            // Allow a small negative amount by reversing the blend direction.
            if (amount < 0f)
            {
                amount = Math.Abs(amount);
                b = Color.Black;
            }

            amount = Math.Max(0f, Math.Min(1f, amount));

            int r = (int)Math.Round(a.R + (b.R - a.R) * amount);
            int g = (int)Math.Round(a.G + (b.G - a.G) * amount);
            int bl = (int)Math.Round(a.B + (b.B - a.B) * amount);

            return Color.FromArgb(
                ClampColor(r),
                ClampColor(g),
                ClampColor(bl));
        }

        private static int ClampColor(int value)
        {
            return Math.Max(0, Math.Min(255, value));
        }
    }
}
