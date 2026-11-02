using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Markdig;

namespace NPPLPrintMaster
{
    public static class HelpViewer
    {
        internal static string LoadMarkdown(string fileName, string baseDirectory = null)
        {
            if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName) return null;
            string root = baseDirectory ?? Application.StartupPath;
            foreach (string path in new[] { Path.Combine(root, "Documents", fileName), Path.Combine(root, fileName) })
                if (File.Exists(path)) return File.ReadAllText(path);
            using (var stream = typeof(HelpViewer).Assembly.GetManifestResourceStream("NPPLPrintMaster.Documents." + fileName))
            {
                if (stream == null) return null;
                using (var reader = new StreamReader(stream)) return reader.ReadToEnd();
            }
        }

        public static void ShowDocument(string title, string fileName)
        {
            string markdownText = LoadMarkdown(fileName);
            string htmlContent = "<h1>Document Not Found</h1><p>Could not locate: " + System.Net.WebUtility.HtmlEncode(fileName) + "</p>";

            if (markdownText != null)
            {
                // 3. Convert Markdown to HTML using Markdig
                string bodyHtml = Markdown.ToHtml(markdownText,
                    new MarkdownPipelineBuilder().UsePipeTables().Build());

                // Consistent reading styles for the bundled Help documents.
                htmlContent = $@"
                <!DOCTYPE html>
                <html>
                <head>
                <meta charset='utf-8'>
                <meta http-equiv='X-UA-Compatible' content='IE=edge'>
                <style>
                    body {{
                        font-family: 'Segoe UI', Arial, sans-serif;
                        background-color: #26282e;
                        color: #ffffff;
                        padding: 30px;
                        line-height: 1.6;
                        max-width: 960px;
                        margin: 0 auto;
                    }}
                    h1, h2, h3 {{
                        color: #f97316;
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
                    table {{
                        width: 100%;
                        border-collapse: collapse;
                        margin: 18px 0;
                    }}
                    th, td {{
                        border: 1px solid #49505a;
                        padding: 10px 12px;
                        text-align: left;
                        vertical-align: top;
                    }}
                    th {{ background-color: #32353b; color: #ffb17b; }}
                    hr {{ border: 0; border-top: 1px solid #49505a; margin: 24px 0; }}
                </style>
                </head>
                <body>
                    {bodyHtml}
                </body>
                </html>";
            }

            // 5. Open a popup window to display the styled Markdown
            Form docForm = new BrandedForm
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
            using (docForm) docForm.ShowDialog();
        }
    }
}
