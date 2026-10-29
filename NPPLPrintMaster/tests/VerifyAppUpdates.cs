using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using NPPLPrintMaster;
[assembly: AssemblyVersion("1.0.0.0")]
class VerifyAppUpdates
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static string Json(string version, string url = "https://github.com/yateshr/NPPLPrintMaster/releases/download/v2.0.0.0/setup.exe", string digest = null)
    { return "{\"tag_name\":\"" + version + "\",\"assets\":[{\"name\":\"NPPLPrintMaster-Setup-" + version.TrimStart('v') + ".exe\",\"browser_download_url\":\"" + url + "\",\"size\":123,\"digest\":\"sha256:" + (digest ?? new string('a', 64)) + "\"}]}"; }
    static void Reject(string json)
    { try { AppUpdateService.Parse(json); throw new Exception("Unsafe release accepted"); } catch (InvalidDataException) { } }
    static void Main()
    {
        Check(AppUpdateService.Parse(Json("v1.0.0")) == null, "Three-part equal version");
        Check(AppUpdateService.Parse(Json("v0.9.0.0")) == null, "Downgrade");
        Check(AppUpdateService.Parse(Json("v2.0.0.0")).Version.Major == 2, "New stable release");
        Reject(Json("v2.0.0.0", "https://evil.example/setup.exe"));
        Reject(Json("v2.0.0.0", "http://github.com/yateshr/NPPLPrintMaster/releases/download/v2/setup.exe"));
        Reject(Json("v2.0.0.0", digest: "bad"));
        Reject("{\"tag_name\":\"v2.0.0.0\",\"assets\":[]}");
        Reject(Json("v2.0.0.0").Replace("\"assets\"", "\"prerelease\":true,\"assets\""));
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "installer test"); string digest;
            using (var sha = SHA256.Create()) digest = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
            AppUpdateService.VerifyFile(path, digest);
            try { AppUpdateService.VerifyFile(path, new string('0', 64)); throw new Exception("Corrupt installer accepted"); } catch (InvalidDataException) { }
        }
        finally { File.Delete(path); }
        Console.WriteLine("PASS: version comparison, downgrade prevention, release filtering, installer selection, HTTPS/source validation and checksum verification.");
    }
}
