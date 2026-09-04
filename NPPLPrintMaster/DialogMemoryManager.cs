using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace NPPLPrintMaster
{
    /// <summary>
    /// Gives every file/folder picker its own independent remembered location.
    /// Stored separately from general application settings so adding new picker
    /// memories does not make Form1 or SettingsManager larger.
    /// </summary>
    public static class DialogMemoryManager
    {
        private static readonly object syncRoot = new object();

        private static readonly string memoryFolder =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NPPLPrintMaster");

        private static readonly string memoryFile =
            Path.Combine(memoryFolder, "dialog-memory.ini");

        private static Dictionary<string, string> memory =
            LoadMemory();

        public static string GetFolder(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return "";

            lock (syncRoot)
            {
                string folder;

                if (!memory.TryGetValue(key, out folder))
                    return "";

                return Directory.Exists(folder)
                    ? folder
                    : "";
            }
        }

        public static void RememberFolder(string key, string folder)
        {
            if (string.IsNullOrWhiteSpace(key) ||
                string.IsNullOrWhiteSpace(folder) ||
                !Directory.Exists(folder))
            {
                return;
            }

            lock (syncRoot)
            {
                memory[key] = folder;
                SaveMemory();
            }
        }

        public static void RememberFileFolder(string key, string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            string folder = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrWhiteSpace(folder))
                RememberFolder(key, folder);
        }

        public static DialogResult ShowOpenDialog(
            OpenFileDialog dialog,
            string key)
        {
            if (dialog == null)
                throw new ArgumentNullException("dialog");

            ApplyInitialDirectory(dialog, key);

            DialogResult result = dialog.ShowDialog();

            if (result == DialogResult.OK)
            {
                string selectedFile =
                    dialog.FileNames != null && dialog.FileNames.Length > 0
                        ? dialog.FileNames[0]
                        : dialog.FileName;

                RememberFileFolder(key, selectedFile);
            }

            return result;
        }

        public static DialogResult ShowSaveDialog(
            SaveFileDialog dialog,
            string key)
        {
            if (dialog == null)
                throw new ArgumentNullException("dialog");

            string folder = GetFolder(key);

            if (!string.IsNullOrWhiteSpace(folder))
                dialog.InitialDirectory = folder;

            DialogResult result = dialog.ShowDialog();

            if (result == DialogResult.OK)
                RememberFileFolder(key, dialog.FileName);

            return result;
        }

        public static string SelectFolder(string key)
        {
            using (
                OpenFileDialog dialog =
                    new OpenFileDialog
                    {
                        ValidateNames = false,
                        CheckFileExists = false,
                        CheckPathExists = true,
                        FileName = "Folder Selection"
                    })
            {
                string remembered = GetFolder(key);

                if (!string.IsNullOrWhiteSpace(remembered))
                    dialog.InitialDirectory = remembered;

                if (dialog.ShowDialog() != DialogResult.OK)
                    return "";

                string folder = Path.GetDirectoryName(dialog.FileName);

                if (!string.IsNullOrWhiteSpace(folder) &&
                    Directory.Exists(folder))
                {
                    RememberFolder(key, folder);
                    return folder;
                }
            }

            return "";
        }

        private static void ApplyInitialDirectory(
            OpenFileDialog dialog,
            string key)
        {
            string folder = GetFolder(key);

            if (!string.IsNullOrWhiteSpace(folder))
                dialog.InitialDirectory = folder;
        }

        private static Dictionary<string, string> LoadMemory()
        {
            Dictionary<string, string> result =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);

            try
            {
                if (!File.Exists(memoryFile))
                    return result;

                foreach (string line in File.ReadAllLines(memoryFile))
                {
                    if (string.IsNullOrWhiteSpace(line) ||
                        line.StartsWith("#", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string[] parts =
                        line.Split(new[] { '=' }, 2);

                    if (parts.Length != 2 ||
                        string.IsNullOrWhiteSpace(parts[0]))
                    {
                        continue;
                    }

                    result[parts[0].Trim()] = parts[1];
                }
            }
            catch
            {
                // Picker memory is convenience-only. Never block the app.
            }

            return result;
        }

        private static void SaveMemory()
        {
            try
            {
                Directory.CreateDirectory(memoryFolder);

                List<string> lines = new List<string>();

                foreach (KeyValuePair<string, string> pair in memory)
                    lines.Add(pair.Key + "=" + pair.Value);

                lines.Sort(StringComparer.OrdinalIgnoreCase);

                File.WriteAllLines(memoryFile, lines.ToArray());
            }
            catch
            {
                // Picker memory is convenience-only. Never block the app.
            }
        }
    }
}
