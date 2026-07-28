using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using WrightLogs.ViewModels;

namespace WrightLogs.Services;

/// <summary>
/// Checks a remote download to see whether a newer version of the file is
/// available since dimwit last looked.  All HTTP work is delegated to
/// <see cref="RemoteDownloadInstanceHelper"/> so every web call in the project
/// is configured and handled the same way.
/// </summary>
public class SelfUpdater
{
    // The shared download link to the latest published file.  We only need its
    private static HttpClient client = new HttpClient();
    
    // headers (Last-Modified) to decide whether something new is available, so
    // we never pull the whole payload just to perform a check.
    private const string NextCloudBaseUrlForWebDav =
        "https://whqnextcloud.decisions.com/public.php/webdav/";

    /// <summary>
    /// Checks the remote download for a newer version of the file.  The time of
    /// this check is always recorded on <see cref="Config.LastUpdateCheck"/> so a
    /// subsequent run can tell whether the file has changed since we last looked.
    ///
    /// Here's the curl command exemplar ---
    /// curl -u "igqey8e7RZd6fma:" -X PROPFIND -H "X-Requested-With: XMLHttpRequest" https://whqnextcloud.decisions.com/public.php/webdav/
    /// The username here is the last part of the URL path from a public share.  This will return XML
    /// </summary>
    /// <returns>True when a newer version appears to be available.</returns>
    private static async Task<(string, string)> CheckForUpdateAsync(Version currentVersion)
    {
        // Download the file list from NextCloud Server
        XDocument doc = GetFileListFromNextCloudFolder();

        // Find nodes based on OS
        // Environment.
        string os = "win";
        if (OperatingSystem.IsMacOS())
        {
            os = "mac";
        }

        XmlNamespaceManager nsmgr = new XmlNamespaceManager(new NameTable());
        nsmgr.AddNamespace("d", "DAV:");
        Version highestVersion = currentVersion;
        string lastestVersionHref = string.Empty;
        
        // Now I have a bunch of XML nodes that are this tool for this OS.
        XNamespace d = "DAV:";
        var nodes = doc.Element(d + "multistatus").Elements(d + "response").Elements(d + "href").Where(
            href => href.Value.Contains($"wrightlogs.{os}"));
        foreach (XElement el in nodes)
        {
            // Get Version from string.
            string optionalVersion = el.Value;
            if (string.IsNullOrEmpty(optionalVersion) == false)
            {
                string ver = optionalVersion.Substring(34).Replace(".zip", string.Empty);
                Version possible =  new Version(ver);
                if (possible > highestVersion)
                {
                    highestVersion = possible;
                    lastestVersionHref = el.Value; // has public.php and everything included.
                }
            }
        }

        if (highestVersion == currentVersion)
        {
            Console.Out.WriteLine("No upgrade needed! Let's sleep it off.");
            return (string.Empty, string.Empty);
        }
        
        // return updateAvailable;
        return (highestVersion.ToString(),  lastestVersionHref);
    }

    private static XDocument GetFileListFromNextCloudFolder()
    {
        // Example curl command: 
        // curl -u "igqey8e7RZd6fma:" -X PROPFIND -H "X-Requested-With: XMLHttpRequest" https://whqnextcloud.decisions.com/public.php/webdav/
        using var request = new HttpRequestMessage(new HttpMethod("PROPFIND"), NextCloudBaseUrlForWebDav);

        // Tell the server we expect an XML response
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
        
        // Add your custom headers
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        // request.Headers.Authorization = new AuthenticationHeaderValue("Basic", "igqey8e7RZd6fma:");
        request.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes("igqey8e7RZd6fma:")));

        
        using var response = client.Send(request);
        response.EnsureSuccessStatusCode();

        // Parse the response as XML
        using var stream = response.Content.ReadAsStream();
        return XDocument.Load(stream);
        
    }

    /// <summary>
    /// Downloads the latest published file and unpacks it over the directory dimwit
    /// is currently running from.  This is self-contained (it uses its own HttpClient)
    /// but mirrors the streaming + progress-dot pattern used by the remote deployment
    /// downloader so the experience is consistent.
    /// </summary>
    public static async Task ExecuteUpdateToVersion(MainWindowViewModel vm)
    {
        if (vm == null)
        {
            throw new ArgumentNullException(nameof(vm), "Update cannot be called without the new version saved in the main window data model.");
        }

        // The directory the running dimwit executable lives in.
        string targetDir = AppContext.BaseDirectory;
        string filePart = vm.UpdateVersionUrl;
        // Since NextCloud URL for WebDav ENDS WITH /public.php/webdav
        // and the fetched urls begin with it, omit it from the version URL
        filePart = filePart.Substring(19);
        string downloadUrl = $"{NextCloudBaseUrlForWebDav}/{filePart}";
        string zipPath = Path.Combine(Path.GetTempPath(), "wrightLogs.zip");

        
        // download.....
        using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);

        // request.Headers.Authorization = new AuthenticationHeaderValue("Basic", "igqey8e7RZd6fma:");
        request.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes("igqey8e7RZd6fma:")));
       
        // ResponseHeadersRead ensures it returns as soon as headers are read, 
        // rather than waiting to buffer the entire payload into memory.
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
    
        // Throw an exception if the status code is not 200 OK or similar success codes
        response.EnsureSuccessStatusCode();
        // Stream content to local disk
        using (var downloadStream = await response.Content.ReadAsStreamAsync())
        {
            using (var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {

                vm.UpdateVersionText = "Downloading update...";
                await downloadStream.CopyToAsync(fileStream);

                vm.UpdateVersionText = "Extracting new version...";
                fileStream.Flush();
            }
        }
        // Flush the bytes or something to ensure file lock is not active.
        
        // Console.Out.WriteLine($"Download complete. Unpacking into {targetDir}", ConsoleColor.Cyan);
        ExtractOver(vm, zipPath, targetDir);

        try { File.Delete(zipPath); } catch { /* best-effort cleanup of the temp archive */ }

        vm.UpdateVersionText = "Update Complete, Restart.";
    }

    /// <summary>
    /// Extracts the archive into <paramref name="targetDir"/>, overwriting existing files.
    /// Files that are locked because they belong to the running process (dimwit's own exe
    /// and dlls) are moved aside to a ".old" copy so the new version can take their place.
    /// The user's dimwit.json is never overwritten.
    /// </summary>
    private static void ExtractOver(MainWindowViewModel mwvm, string zipPath, string targetDir)
    {
        string root = Path.GetFullPath(targetDir);
        mwvm.UpdateVersionText = $"Unpacking {zipPath} to: {root}";
        using var archive = ZipFile.OpenRead(zipPath);
        int total = archive.Entries.Count;
        int done = 0;

        foreach (var entry in archive.Entries)
        {
            done++;
            string destPath = Path.GetFullPath(Path.Combine(root, entry.FullName));

            mwvm.UpdateVersionText = $"Extracting: {entry.FullName}";
            // Guard against path traversal ("zip slip").
            if (!destPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                continue;

            // Directory entry (no file name) — just ensure it exists.
            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

            // Never clobber the user's existing config.
            // Hold over from dimwit, do not remove in case I reuse this newer code back
            // on dimwit.
            if (Path.GetFileName(destPath).Equals("dimwit.json", StringComparison.OrdinalIgnoreCase)
                && File.Exists(destPath))
                continue;

            ReplaceFile(entry, destPath);
            mwvm.UpdateVersionText = $"Extracted {done} of {total}";
            Console.Write($"\r   Extracted {done}/{total} files...    ");
        }
        mwvm.UpdateVersionText = "Extracting complete."; 
    }
    
    private static void ReplaceFile(ZipArchiveEntry entry, string destPath)
    {
        try
        {
            entry.ExtractToFile(destPath, overwrite: true);
        }
        catch (IOException)
        {
            // The file is probably locked because it is part of the running process.
            // Move it out of the way so the new version can take its place; the ".old"
            // copy gets cleaned up on a later run (or can be removed by the user).
            string backup = destPath + ".old";
            try { if (File.Exists(backup)) File.Delete(backup); } catch { /* leave stale backup */ }
            File.Move(destPath, backup);
            entry.ExtractToFile(destPath, overwrite: true);
        }
    }

    public static async void CheckForUpdates(object? dataContext)
    {
        try
        {
            // Don't check if you have already checked today.
            // maybe a .lastCheck file? 

            // Now scan for updates.
            Version assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;

            (string,string) newVersion = await CheckForUpdateAsync(assemblyVersion);
            if (string.IsNullOrEmpty(newVersion.Item1))
            {
                // Do no update!
            }
            else
            {
                if (dataContext is MainWindowViewModel mainModel)
                {
                    mainModel.UpdateVersionUrl = newVersion.Item2; // URL part saved off!
                    mainModel.UpdateVersionText = $"{newVersion.Item1} Available. Install Me!";
                }
            }
        }
        catch
        {
            // Ignore any exceptions and just know that there's no update needed here.
        }
    }
}
    