using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace NPPLPrintMaster
{
    internal sealed class AppRelease
    {
        public Version Version;
        public string Url, Digest, Notes;
        public long Size;
    }

    internal sealed class AppUpdateService
    {
        internal const string Repository = "https://github.com/yateshr/NPPLPrintMaster";
        internal static readonly Version CurrentVersion = Assembly.GetExecutingAssembly().GetName().Version;
        private sealed class ReleaseJson
        {
            public string tag_name { get; set; }
            public string body { get; set; }
            public bool draft { get; set; }
            public bool prerelease { get; set; }
            public AssetJson[] assets { get; set; }
        }
        private sealed class AssetJson
        {
            public string name { get; set; }
            public string browser_download_url { get; set; }
            public string digest { get; set; }
            public long size { get; set; }
        }
        internal static AppRelease Parse(string json)
        {
            var release = new JavaScriptSerializer().Deserialize<ReleaseJson>(json);
            Version version;
            if (release == null || release.draft || release.prerelease ||
                !Version.TryParse((release.tag_name ?? "").TrimStart('v', 'V'), out version))
                throw new InvalidDataException("The release version is not supported.");
            if (Normalize(version) <= Normalize(CurrentVersion)) return null;
            string expected = "NPPLPrintMaster-Setup-" + version.ToString() + ".exe";
            var asset = (release.assets ?? new AssetJson[0]).FirstOrDefault(a => a.name == expected);
            Uri url;
            if (asset == null || !Uri.TryCreate(asset.browser_download_url, UriKind.Absolute, out url) ||
                url.Scheme != "https" || url.Host != "github.com" ||
                !url.AbsolutePath.StartsWith("/yateshr/NPPLPrintMaster/releases/download/", StringComparison.OrdinalIgnoreCase) ||
                asset.size <= 0 || asset.size > 1024L * 1024 * 1024 ||
                asset.digest == null || !System.Text.RegularExpressions.Regex.IsMatch(asset.digest, "^sha256:[a-fA-F0-9]{64}$"))
                throw new InvalidDataException("The latest release does not have a verified Windows installer yet.");
            return new AppRelease { Version = version, Url = url.AbsoluteUri, Digest = asset.digest.Substring(7), Size = asset.size, Notes = release.body ?? "" };
        }
        internal static Version Normalize(Version version)
        { return new Version(version.Major, version.Minor, Math.Max(0, version.Build), Math.Max(0, version.Revision)); }
        private static HttpClient Client(TimeSpan timeout)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var client = new HttpClient { Timeout = timeout };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("NPPLPrintMaster/" + CurrentVersion);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return client;
        }
        internal async Task<AppRelease> CheckAsync(CancellationToken token)
        {
            using (var client = Client(TimeSpan.FromSeconds(20)))
            using (var response = await client.GetAsync("https://api.github.com/repos/yateshr/NPPLPrintMaster/releases/latest", token))
            {
                if (response.StatusCode == HttpStatusCode.NotFound) throw new InvalidDataException("No public update release is available yet.");
                response.EnsureSuccessStatusCode();
                return Parse(await response.Content.ReadAsStringAsync());
            }
        }
        internal async Task<string> DownloadAsync(AppRelease release, IProgress<int> progress, CancellationToken token)
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NPPLPrintMaster", "Updates", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, "NPPLPrintMaster-Setup-" + release.Version + ".exe");
            try
            {
                using (var client = Client(TimeSpan.FromMinutes(15)))
                using (var response = await client.GetAsync(release.Url, HttpCompletionOption.ResponseHeadersRead, token))
                {
                    response.EnsureSuccessStatusCode();
                    using (var source = await response.Content.ReadAsStreamAsync())
                    using (var target = new FileStream(file + ".part", FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                    {
                        byte[] buffer = new byte[81920]; long total = 0; int count;
                        while ((count = await source.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                        {
                            total += count;
                            if (total > release.Size) throw new InvalidDataException("The installer size does not match the release.");
                            await target.WriteAsync(buffer, 0, count, token);
                            progress.Report((int)(total * 100 / release.Size));
                        }
                        if (total != release.Size) throw new InvalidDataException("The download is incomplete.");
                    }
                }
                VerifyFile(file + ".part", release.Digest);
                File.Move(file + ".part", file);
                return file;
            }
            catch { if (File.Exists(file + ".part")) File.Delete(file + ".part"); throw; }
        }
        internal static void VerifyFile(string file, string digest)
        {
            using (var stream = File.OpenRead(file))
            using (var sha = SHA256.Create())
                if (!string.Equals(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""), digest, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Installer verification failed. Please download the update again.");
        }
    }
}
