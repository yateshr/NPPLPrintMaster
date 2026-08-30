using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace NPPLPrintMaster
{
    // 1. THE DATA TRANSFER OBJECT (DTO)
    // This safely carries data between the UI and the Hard Drive
    public class WorkspaceData
    {
        public List<string> Lane1Files = new List<string>();
        public List<string> Lane2Files = new List<string>();
        public List<string> Lane3Files = new List<string>();
        public string Text1 = "";
        public string Text2 = "";
        public bool ShowText1 = true;
        public bool ShowText2 = true;
        public Font Font1 = new Font("Calibri", 14, FontStyle.Bold);
        public Font Font2 = new Font("Calibri", 14, FontStyle.Bold);
        public List<string> LayoutMemory = new List<string>();
    }

    // 2. THE ENGINE
    public static class WorkspaceEngine
    {
        public static void SaveToFile(string filePath, WorkspaceData data)
        {
            using (StreamWriter sw = new StreamWriter(filePath))
            {
                sw.WriteLine("[LANE1]"); foreach (string f in data.Lane1Files) sw.WriteLine(f);
                sw.WriteLine("[LANE2]"); foreach (string f in data.Lane2Files) sw.WriteLine(f);
                sw.WriteLine("[LANE3]"); foreach (string f in data.Lane3Files) sw.WriteLine(f);

                sw.WriteLine("[TEXT]");
                sw.WriteLine(data.Text1);
                sw.WriteLine(data.Text2);
                sw.WriteLine(data.ShowText1.ToString());
                sw.WriteLine(data.ShowText2.ToString());
                sw.WriteLine($"{data.Font1.FontFamily.Name}|{data.Font1.Size}|{(int)data.Font1.Style}");
                sw.WriteLine($"{data.Font2.FontFamily.Name}|{data.Font2.Size}|{(int)data.Font2.Style}");

                sw.WriteLine("[LAYOUT]");
                foreach (string layoutData in data.LayoutMemory) sw.WriteLine(layoutData);
            }
        }

        public static WorkspaceData LoadFromFile(string filePath)
        {
            WorkspaceData data = new WorkspaceData();
            string[] lines = File.ReadAllLines(filePath);
            int mode = 0;
            List<string> textData = new List<string>();

            foreach (string line in lines)
            {
                if (line == "[LANE1]") { mode = 1; continue; }
                if (line == "[LANE2]") { mode = 2; continue; }
                if (line == "[LANE3]") { mode = 3; continue; }
                if (line == "[TEXT]") { mode = 4; continue; }
                if (line == "[LAYOUT]") { mode = 5; continue; }

                if (mode == 1 && File.Exists(line)) data.Lane1Files.Add(line);
                if (mode == 2 && File.Exists(line)) data.Lane2Files.Add(line);
                if (mode == 3 && File.Exists(line)) data.Lane3Files.Add(line);
                if (mode == 4) textData.Add(line);
                if (mode == 5) data.LayoutMemory.Add(line);
            }

            if (textData.Count >= 6)
            {
                data.Text1 = textData[0];
                data.Text2 = textData[1];
                data.ShowText1 = bool.Parse(textData[2]);
                data.ShowText2 = bool.Parse(textData[3]);
                var f1 = textData[4].Split('|'); data.Font1 = new Font(f1[0], float.Parse(f1[1]), (FontStyle)int.Parse(f1[2]));
                var f2 = textData[5].Split('|'); data.Font2 = new Font(f2[0], float.Parse(f2[1]), (FontStyle)int.Parse(f2[2]));
            }

            return data;
        }
    }
}