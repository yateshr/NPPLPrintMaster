using System;
using System.IO;

namespace NPPLPrintMaster
{
    public static class Logger
    {
        // Creates a hidden print log in your Documents folder
        private static string logFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "NPPLPrintMaster", "PrintLog.txt");

        public static void LogAction(string action, string details)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(logFile));
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                File.AppendAllText(logFile, $"[{timestamp}] {action} | {details}{Environment.NewLine}");
            }
            catch { } // Fails silently so it never interrupts the production floor
        }
    }
}