using System;
using System.Collections.Generic;

namespace NPPLPrintMaster
{
    /// <summary>
    /// Separates appearance into three independent layers:
    /// 1. Color Theme
    /// 2. Typography / Text Style
    /// 3. Layout Style
    ///
    /// Forest Graphite + NPPL Original text/layout is the application's
    /// main/default NPPLPrintMaster identity.
    /// </summary>
    public static class AppearanceManager
    {
        public const string UseThemeDefault = "Use Theme Default";

        public const string NpplOriginalTypography = "NPPL Original";
        public const string BarTender10Typography = "BarTender 10 Classic Text";

        public const string NpplOriginalLayout = "NPPL Original";
        public const string BarTender10Layout = "BarTender 10 Classic Layout";

        public static readonly object[] TypographyNames =
        {
            UseThemeDefault,
            NpplOriginalTypography,
            BarTender10Typography
        };

        public static readonly object[] LayoutNames =
        {
            UseThemeDefault,
            NpplOriginalLayout,
            BarTender10Layout
        };

        public static string NormalizeTheme(string themeName)
        {
            if (string.IsNullOrWhiteSpace(themeName))
                return "Forest Graphite";

            foreach (object item in ThemeManager.ThemeNames)
            {
                if (string.Equals(
                    Convert.ToString(item),
                    themeName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return Convert.ToString(item);
                }
            }

            return "Forest Graphite";
        }

        public static string NormalizeTypographyChoice(string choice)
        {
            if (string.IsNullOrWhiteSpace(choice))
                return UseThemeDefault;

            foreach (object item in TypographyNames)
            {
                if (string.Equals(
                    Convert.ToString(item),
                    choice,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return Convert.ToString(item);
                }
            }

            return UseThemeDefault;
        }

        public static string NormalizeLayoutChoice(string choice)
        {
            if (string.IsNullOrWhiteSpace(choice))
                return UseThemeDefault;

            foreach (object item in LayoutNames)
            {
                if (string.Equals(
                    Convert.ToString(item),
                    choice,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return Convert.ToString(item);
                }
            }

            return UseThemeDefault;
        }

        public static string ResolveTypography(
            string themeName,
            string selectedTypography)
        {
            string normalizedChoice =
                NormalizeTypographyChoice(selectedTypography);

            if (!string.Equals(
                normalizedChoice,
                UseThemeDefault,
                StringComparison.OrdinalIgnoreCase))
            {
                return normalizedChoice;
            }

            string normalizedTheme = NormalizeTheme(themeName);

            if (string.Equals(
                normalizedTheme,
                "BarTender 10 Classic",
                StringComparison.OrdinalIgnoreCase))
            {
                return BarTender10Typography;
            }

            // Forest Graphite is the canonical NPPL baseline.
            // All existing non-BarTender themes keep the NPPL text structure.
            return NpplOriginalTypography;
        }

        public static string ResolveLayout(
            string themeName,
            string selectedLayout)
        {
            string normalizedChoice =
                NormalizeLayoutChoice(selectedLayout);

            if (!string.Equals(
                normalizedChoice,
                UseThemeDefault,
                StringComparison.OrdinalIgnoreCase))
            {
                return normalizedChoice;
            }

            string normalizedTheme = NormalizeTheme(themeName);

            if (string.Equals(
                normalizedTheme,
                "BarTender 10 Classic",
                StringComparison.OrdinalIgnoreCase))
            {
                return BarTender10Layout;
            }

            // Forest Graphite and the other existing themes keep the
            // original NPPLPrintMaster layout by default.
            return NpplOriginalLayout;
        }

        public static string GetResolvedSummary(
            string themeName,
            string typographyChoice,
            string layoutChoice)
        {
            return
                "Color: " + NormalizeTheme(themeName) +
                "  |  Text: " + ResolveTypography(themeName, typographyChoice) +
                "  |  Layout: " + ResolveLayout(themeName, layoutChoice);
        }
    }
}
