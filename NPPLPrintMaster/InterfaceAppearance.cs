using System;
using System.Drawing;

namespace NPPLPrintMaster
{
    public static class InterfaceAppearance
    {
        public const string Current = "Current UI";
        public const string Modern = "Modern UI (Preview)";

        public static string Normalize(string choice)
        {
            return string.Equals(choice, Modern, StringComparison.OrdinalIgnoreCase) ? Modern : Current;
        }

        public static bool IsModern(string choice) { return Normalize(choice) == Modern; }

        public static ThemeColors ModernTheme()
        {
            return new ThemeColors
            {
                SidebarColor = Color.FromArgb(24, 33, 38), SidebarHover = Color.FromArgb(36, 50, 55),
                ActiveColor = Color.FromArgb(0, 135, 90), BgLight = Color.FromArgb(245, 247, 250),
                ContentBg = Color.White, TextColor = Color.FromArgb(27, 39, 51),
                ControlBg = Color.White, MutedTextColor = Color.FromArgb(100, 116, 133),
                BorderColor = Color.FromArgb(218, 226, 234), ReadOnlyBg = Color.FromArgb(246, 248, 250),
                ButtonHover = Color.FromArgb(236, 243, 240)
            };
        }
    }
}
