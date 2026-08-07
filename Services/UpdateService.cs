using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using ProGlassAutomation.Models;

namespace ProGlassAutomation.Services
{
    public class UpdateService
    {
        private readonly HttpClient _httpClient;
        private const string UpdateUrl = "https://raw.githubusercontent.com/JUBERSALEEM/ProGlassAutomationADV/new/update.json";

        public UpdateService()
        {
            _httpClient = new HttpClient();
            // Standard timeout and headers
            _httpClient.Timeout = TimeSpan.FromSeconds(15);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "ProGlassAutomation-UpdateService");
        }

        public async Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            try
            {
                string json = await _httpClient.GetStringAsync(UpdateUrl);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<UpdateInfo>(json, options);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Update] Connection failed: {ex.Message}");
                return null;
            }
        }

        public bool IsNewVersionAvailable(string latestVersion)
        {
            try
            {
                var current = new Version(VersionService.CurrentVersion);
                var latest = new Version(latestVersion);
                return latest > current;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string?> DownloadUpdateAsync(string downloadUrl, Action<int> progressCallback)
        {
            try
            {
                string tempFile = Path.Combine(Path.GetTempPath(), "ProGlassAutomation-Setup.exe");

                using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    long? totalBytes = response.Content.Headers.ContentLength;

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        var buffer = new byte[8192];
                        long totalReadBytes = 0;
                        int readBytes;

                        while ((readBytes = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, readBytes);
                            totalReadBytes += readBytes;

                            if (totalBytes.HasValue)
                            {
                                int progress = (int)((double)totalReadBytes / totalBytes.Value * 100);
                                progressCallback(progress);
                            }
                        }
                    }
                }
                return tempFile;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Update] Download failed: {ex.Message}");
                return null;
            }
        }

        public void PerformBackupSafety()
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ProGlassAutomation",
                    "Backup",
                    timestamp);

                if (!Directory.Exists(backupFolder))
                {
                    Directory.CreateDirectory(backupFolder);
                }

                // 1) Backup AppData directory (Modular databases + Configuration file)
                string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ProGlassAutomation");
                if (Directory.Exists(appDataFolder))
                {
                    foreach (string dbFile in Directory.GetFiles(appDataFolder, "*.db"))
                    {
                        string dest = Path.Combine(backupFolder, Path.GetFileName(dbFile));
                        File.Copy(dbFile, dest, true);
                    }

                    string configPath = Path.Combine(appDataFolder, "config.json");
                    if (File.Exists(configPath))
                    {
                        File.Copy(configPath, Path.Combine(backupFolder, "config.json"), true);
                    }
                }

                // 2) Backup Common ApplicationData directory (Primary database)
                string commonFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ProGlassAutomation", "Database");
                if (Directory.Exists(commonFolder))
                {
                    string commonBackupDir = Path.Combine(backupFolder, "Common");
                    Directory.CreateDirectory(commonBackupDir);

                    foreach (string dbFile in Directory.GetFiles(commonFolder, "*.db"))
                    {
                        string dest = Path.Combine(commonBackupDir, Path.GetFileName(dbFile));
                        File.Copy(dbFile, dest, true);
                    }
                }

                Debug.WriteLine($"[Backup] Professional backup successfully created at: {backupFolder}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Backup] Safety backup failed: {ex.Message}");
            }
        }

        public void LaunchInstallerAndExit(string installerPath)
        {
            try
            {
                // Run safety backup before switching to the installer
                PerformBackupSafety();

                Process.Start(new ProcessStartInfo
                {
                    FileName = installerPath,
                    UseShellExecute = true
                });

                // Shutdown current application context safely
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    System.Windows.Application.Current.Shutdown();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Update] Failed to handoff to installer: {ex.Message}");
            }
        }
    }
}
