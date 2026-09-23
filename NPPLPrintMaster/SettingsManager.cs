using System;
using System.IO;
using System.Windows.Forms;

namespace NPPLPrintMaster
{
    public class AppSettings
    {
        public string Theme { get; set; } = "Forest Graphite";
        public string TypographyStyle { get; set; } = AppearanceManager.UseThemeDefault;
        public string LayoutStyle { get; set; } = AppearanceManager.UseThemeDefault;
        public int DefaultDpi { get; set; } = 800;
        public string DefaultExport1 { get; set; } = "";
        public string DefaultExport2 { get; set; } = "";

        // Quick Job Card memory
        public string LastProductFolder { get; set; } = "";
        public string LastCartonFolder { get; set; } = "";
        public string LastOtherFolder { get; set; } = "";
        public string LastJobCardSaveFolder { get; set; } = "";

        // Smart Image Finder
        public string SmartProductImageFolder { get; set; } = "";
        public string SmartCartonImageFolder { get; set; } = "";

        // BTW Image Library
        public string BtwLibraryIncludeFolders { get; set; } = "";
        public string BtwLibraryExcludeFolders { get; set; } = "";

        // Quick Job Card workflow
        public bool OpenJobCardDirectlyInFreeform { get; set; } = true;
    }

    public static class SettingsManager
    {
        private static string settingsFile = Path.Combine(Application.StartupPath, "settings.ini");

        public static AppSettings Load()
        {
            AppSettings s = new AppSettings();
            if (File.Exists(settingsFile))
            {
                string[] lines = File.ReadAllLines(settingsFile);
                foreach (string line in lines)
                {
                    var parts = line.Split(new[] { '=' }, 2);
                    if (parts.Length == 2)
                    {
                        if (parts[0] == "Theme") s.Theme = parts[1];
                        if (parts[0] == "TypographyStyle") s.TypographyStyle = parts[1];
                        if (parts[0] == "LayoutStyle") s.LayoutStyle = parts[1];
                        if (parts[0] == "DefaultDpi" && int.TryParse(parts[1], out int dpi)) s.DefaultDpi = dpi;
                        if (parts[0] == "DefaultExport1") s.DefaultExport1 = parts[1];
                        if (parts[0] == "DefaultExport2") s.DefaultExport2 = parts[1];
                        if (parts[0] == "LastProductFolder") s.LastProductFolder = parts[1];
                        if (parts[0] == "LastCartonFolder") s.LastCartonFolder = parts[1];
                        if (parts[0] == "LastOtherFolder") s.LastOtherFolder = parts[1];
                        if (parts[0] == "LastJobCardSaveFolder") s.LastJobCardSaveFolder = parts[1];
                        if (parts[0] == "SmartProductImageFolder") s.SmartProductImageFolder = parts[1];
                        if (parts[0] == "SmartCartonImageFolder") s.SmartCartonImageFolder = parts[1];
                        if (parts[0] == "BtwLibraryIncludeFolders") s.BtwLibraryIncludeFolders = parts[1];
                        if (parts[0] == "BtwLibraryExcludeFolders") s.BtwLibraryExcludeFolders = parts[1];
                        if (parts[0] == "OpenJobCardDirectlyInFreeform" &&
                            bool.TryParse(parts[1], out bool directFreeform))
                            s.OpenJobCardDirectlyInFreeform = directFreeform;
                    }
                }
            }
            s.Theme = AppearanceManager.NormalizeTheme(s.Theme);
            s.TypographyStyle = AppearanceManager.NormalizeTypographyChoice(s.TypographyStyle);
            s.LayoutStyle = AppearanceManager.NormalizeLayoutChoice(s.LayoutStyle);
            return s;
        }

        public static void Save(AppSettings s)
        {
            using (StreamWriter sw = new StreamWriter(settingsFile))
            {
                sw.WriteLine($"Theme={s.Theme}");
                sw.WriteLine($"TypographyStyle={s.TypographyStyle}");
                sw.WriteLine($"LayoutStyle={s.LayoutStyle}");
                sw.WriteLine($"DefaultDpi={s.DefaultDpi}");
                sw.WriteLine($"DefaultExport1={s.DefaultExport1}");
                sw.WriteLine($"DefaultExport2={s.DefaultExport2}");
                sw.WriteLine($"LastProductFolder={s.LastProductFolder}");
                sw.WriteLine($"LastCartonFolder={s.LastCartonFolder}");
                sw.WriteLine($"LastOtherFolder={s.LastOtherFolder}");
                sw.WriteLine($"LastJobCardSaveFolder={s.LastJobCardSaveFolder}");
                sw.WriteLine($"SmartProductImageFolder={s.SmartProductImageFolder}");
                sw.WriteLine($"SmartCartonImageFolder={s.SmartCartonImageFolder}");
                sw.WriteLine($"BtwLibraryIncludeFolders={s.BtwLibraryIncludeFolders}");
                sw.WriteLine($"BtwLibraryExcludeFolders={s.BtwLibraryExcludeFolders}");
                sw.WriteLine($"OpenJobCardDirectlyInFreeform={s.OpenJobCardDirectlyInFreeform}");
            }
        }
    }
}