using System;
using System.IO;
using System.Windows.Forms;

namespace NPPLPrintMaster
{
    public class AppSettings
    {
        public string Theme { get; set; } = "NPPL Corporate";
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
                    var parts = line.Split('=');
                    if (parts.Length == 2)
                    {
                        if (parts[0] == "Theme") s.Theme = parts[1];
                        if (parts[0] == "DefaultDpi" && int.TryParse(parts[1], out int dpi)) s.DefaultDpi = dpi;
                        if (parts[0] == "DefaultExport1") s.DefaultExport1 = parts[1];
                        if (parts[0] == "DefaultExport2") s.DefaultExport2 = parts[1];
                        if (parts[0] == "LastProductFolder") s.LastProductFolder = parts[1];
                        if (parts[0] == "LastCartonFolder") s.LastCartonFolder = parts[1];
                        if (parts[0] == "LastOtherFolder") s.LastOtherFolder = parts[1];
                        if (parts[0] == "LastJobCardSaveFolder") s.LastJobCardSaveFolder = parts[1];
                        if (parts[0] == "SmartProductImageFolder") s.SmartProductImageFolder = parts[1];
                        if (parts[0] == "SmartCartonImageFolder") s.SmartCartonImageFolder = parts[1];
                    }
                }
            }
            return s;
        }

        public static void Save(AppSettings s)
        {
            using (StreamWriter sw = new StreamWriter(settingsFile))
            {
                sw.WriteLine($"Theme={s.Theme}");
                sw.WriteLine($"DefaultDpi={s.DefaultDpi}");
                sw.WriteLine($"DefaultExport1={s.DefaultExport1}");
                sw.WriteLine($"DefaultExport2={s.DefaultExport2}");
                sw.WriteLine($"LastProductFolder={s.LastProductFolder}");
                sw.WriteLine($"LastCartonFolder={s.LastCartonFolder}");
                sw.WriteLine($"LastOtherFolder={s.LastOtherFolder}");
                sw.WriteLine($"LastJobCardSaveFolder={s.LastJobCardSaveFolder}");
                sw.WriteLine($"SmartProductImageFolder={s.SmartProductImageFolder}");
                sw.WriteLine($"SmartCartonImageFolder={s.SmartCartonImageFolder}");
            }
        }
    }
}