using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Markdig;

namespace NPPLPrintMaster
{
    public static class HelpViewer
    {
        public static void ShowDocument(string title, string fileName)
        {
            // Point directly to the Documents folder inside your project directory
            string folderPath = Path.Combine(Application.StartupPath, "Documents");
            string filePath = Path.Combine(folderPath, fileName);

            // If it's not in a Documents subfolder, look right next to the executable
            if (!File.Exists(filePath))
            {
                filePath = Path.Combine(Application.StartupPath, fileName);
            }

            // (Keep the rest of your conversion and Form opening code below this)

            string htmlContent = "<h1>Document Not Found</h1><p>Could not locate: " + fileName + "</p>";

            if (File.Exists(filePath))
            {
                // 2. Read the raw markdown text
                string markdownText = File.ReadAllText(filePath);

                // 3. Convert Markdown to HTML using Markdig
                string bodyHtml = Markdown.ToHtml(markdownText);

                // 4. Style it with a dark theme to match your NPPL PrintMaster design
                htmlContent = $@"
                <!DOCTYPE html>
                <html>
                <head>
                <style>
                    body {{
                        font-family: 'Segoe UI', Arial, sans-serif;
                        background-color: #26282e;
                        color: #ffffff;
                        padding: 30px;
                        line-height: 1.6;
                    }}
                    h1, h2, h3 {{
                        color: #fcd535; /* Your app's accent yellow */
                        border-bottom: 1px solid #444;
                        padding-bottom: 8px;
                    }}
                    code {{
                        background-color: #32353b;
                        padding: 2px 6px;
                        border-radius: 4px;
                        font-family: Consolas, monospace;
                        color: #38bdf8;
                    }}
                    pre {{
                        background-color: #1e1f23;
                        padding: 15px;
                        border-radius: 6px;
                        overflow-x: auto;
                    }}
                    ul, ol {{
                        padding-left: 20px;
                    }}
                    li {{
                        margin-bottom: 8px;
                    }}
                </style>
                </head>
                <body>
                    {bodyHtml}
                </body>
                </html>";
            }

            // 5. Open a popup window to display the styled Markdown
            Form docForm = new Form
            {
                Text = title,
                Size = new Size(800, 600),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Color.FromArgb(38, 40, 46)
            };

            WebBrowser browser = new WebBrowser
            {
                Dock = DockStyle.Fill
            };

            browser.DocumentText = htmlContent;
            docForm.Controls.Add(browser);
            docForm.ShowDialog();
        }
    }
}