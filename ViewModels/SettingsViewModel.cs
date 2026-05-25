using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using TAY.Services;

namespace TAY.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        public static SettingsViewModel Instance { get; } = new SettingsViewModel();

        private const string ReleaseApiUrl = "https://api.github.com/repos/Palm1ye/TAY/releases/latest";
        private static readonly string AppDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TAY");
        private static readonly string[] ManagedBackupFiles =
        {
            "dns_backup.json",
            "telemetry_backup.json",
            "gamebooster_backup.json"
        };

        private static readonly HttpClient Http = CreateHttpClient();

        private bool _autoCheckStarted;
        private bool _updateNotified;

        public string AppVersion { get; } = $"v{GetAppVersion()}";
        public string AppName => "TAY Optimizer";
        public string AppChannel => "stable";
        public string AppDataPath => AppDataDir;
        public string RuntimeInfo => $".NET {Environment.Version} / {RuntimeInformationLabel}";
        public string ElevationStatus => IsRunningAsAdministrator() ? "Administrator session active" : "Standard user session";
        public string ArchitectureInfo => $"{RuntimeInformationLabel}, {Environment.ProcessorCount} logical processors";

        private SettingsViewModel()
        {
            RefreshSystemSummary();
            RefreshMaintenanceState();
        }

        public void BeginAutoCheck()
        {
            if (_autoCheckStarted) return;
            _autoCheckStarted = true;
            _ = CheckUpdatesAsync();
        }

        private string _latestVersion = "-";
        private string _updateStatus = "idle";
        private bool _isChecking;
        private bool _isUpdateAvailable;
        private bool _isDownloading;
        private string? _downloadUrl;
        private string? _releasePageUrl = "https://github.com/Palm1ye/TAY/releases/latest";
        private string _windowsInfo = "Windows";
        private string _hardwareSummary = "Hardware telemetry pending";
        private string _backupStatus = "No managed backups found";
        private string _backupFiles = "-";
        private string _maintenanceStatus = "Ready";

        public string LatestVersion
        {
            get => _latestVersion;
            set => SetProperty(ref _latestVersion, value);
        }

        public string UpdateStatus
        {
            get => _updateStatus;
            set => SetProperty(ref _updateStatus, value);
        }

        public bool IsChecking
        {
            get => _isChecking;
            set
            {
                if (SetProperty(ref _isChecking, value))
                {
                    OnPropertyChanged(nameof(IsNotChecking));
                    OnPropertyChanged(nameof(CanDownloadUpdate));
                }
            }
        }

        public bool IsUpdateAvailable
        {
            get => _isUpdateAvailable;
            set
            {
                if (SetProperty(ref _isUpdateAvailable, value))
                {
                    OnPropertyChanged(nameof(CanDownloadUpdate));
                }
            }
        }

        public bool IsDownloading
        {
            get => _isDownloading;
            set
            {
                if (SetProperty(ref _isDownloading, value))
                {
                    OnPropertyChanged(nameof(IsNotDownloading));
                    OnPropertyChanged(nameof(CanDownloadUpdate));
                }
            }
        }

        public string? DownloadUrl
        {
            get => _downloadUrl;
            set => SetProperty(ref _downloadUrl, value);
        }

        public string? ReleasePageUrl
        {
            get => _releasePageUrl;
            set => SetProperty(ref _releasePageUrl, value);
        }

        public string WindowsInfo
        {
            get => _windowsInfo;
            set => SetProperty(ref _windowsInfo, value);
        }

        public string HardwareSummary
        {
            get => _hardwareSummary;
            set => SetProperty(ref _hardwareSummary, value);
        }

        public string BackupStatus
        {
            get => _backupStatus;
            set => SetProperty(ref _backupStatus, value);
        }

        public string BackupFiles
        {
            get => _backupFiles;
            set => SetProperty(ref _backupFiles, value);
        }

        public string MaintenanceStatus
        {
            get => _maintenanceStatus;
            set => SetProperty(ref _maintenanceStatus, value);
        }

        public bool IsNotChecking => !IsChecking;
        public bool IsNotDownloading => !IsDownloading;
        public bool CanDownloadUpdate => IsUpdateAvailable && !IsDownloading;

        [RelayCommand]
        private void RefreshSystemSummary()
        {
            try
            {
                var info = SystemService.GetHardwareInfo();
                var os = info.TryGetValue("OS", out var osValue) ? osValue?.ToString() : "Windows";
                var build = info.TryGetValue("OSBuild", out var buildValue) ? buildValue?.ToString() : "";
                WindowsInfo = string.IsNullOrWhiteSpace(build) ? os ?? "Windows" : $"{os} build {build}";

                var cpu = info.TryGetValue("CPU", out var cpuValue) ? cpuValue?.ToString() : "Unknown CPU";
                var ram = info.TryGetValue("RAM", out var ramValue) ? $"{ramValue} GB RAM" : "RAM unknown";
                var gpu = info.TryGetValue("GPU", out var gpuValue) ? gpuValue?.ToString() : "GPU unknown";
                HardwareSummary = $"{cpu} / {ram} / {gpu}";
                MaintenanceStatus = "System summary refreshed.";
            }
            catch (Exception ex)
            {
                MaintenanceStatus = $"System summary failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private void RefreshMaintenanceState()
        {
            try
            {
                var files = new System.Collections.Generic.List<string>();
                foreach (var name in ManagedBackupFiles)
                {
                    var path = Path.Combine(AppDataDir, name);
                    if (!File.Exists(path)) continue;
                    files.Add(name);
                }

                BackupFiles = files.Count == 0 ? "-" : string.Join(", ", files);
                BackupStatus = files.Count == 0
                    ? "No managed backups found"
                    : $"{files.Count} restore backup file(s) available";
            }
            catch (Exception ex)
            {
                BackupStatus = "No restore backups found";
                BackupFiles = "-";
                MaintenanceStatus = $"Backup state could not be refreshed: {ex.Message}";
            }
        }

        [RelayCommand]
        private void OpenAppDataFolder()
        {
            try
            {
                Directory.CreateDirectory(AppDataDir);
                Process.Start(new ProcessStartInfo
                {
                    FileName = AppDataDir,
                    UseShellExecute = true
                });
                MaintenanceStatus = "Opened the local TAY data folder.";
            }
            catch (Exception ex)
            {
                MaintenanceStatus = $"Could not open app data: {ex.Message}";
            }
        }

        [RelayCommand]
        private void ClearLocalBackups()
        {
            try
            {
                var removed = 0;
                foreach (var name in ManagedBackupFiles)
                {
                    var path = Path.Combine(AppDataDir, name);
                    if (!File.Exists(path)) continue;
                    File.Delete(path);
                    removed++;
                }

                MaintenanceStatus = removed == 0
                    ? "No local restore backups needed cleanup."
                    : $"Removed {removed} local restore backup file(s).";
                RefreshMaintenanceState();
            }
            catch (Exception ex)
            {
                MaintenanceStatus = $"Backup cleanup failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task CheckUpdatesAsync()
        {
            if (IsChecking) return;

            IsChecking = true;
            UpdateStatus = "checking...";
            LatestVersion = "-";
            IsUpdateAvailable = false;
            DownloadUrl = null;
            ReleasePageUrl = null;

            try
            {
                using var response = await Http.GetAsync(ReleaseApiUrl);
                if (!response.IsSuccessStatusCode)
                {
                    UpdateStatus = $"update check failed ({(int)response.StatusCode})";
                    return;
                }

                var json = await response.Content.ReadAsStringAsync();
                var release = JsonSerializer.Deserialize<GitHubRelease>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                var latestTag = release?.TagName ?? "";
                var normalizedLatest = NormalizeVersion(latestTag);
                LatestVersion = string.IsNullOrWhiteSpace(normalizedLatest) ? latestTag : normalizedLatest;
                ReleasePageUrl = release?.HtmlUrl;

                var currentVersion = ParseVersion(AppVersion);
                var latestVersionParsed = ParseVersion(LatestVersion);
                if (currentVersion == null || latestVersionParsed == null)
                {
                    UpdateStatus = "update check failed";
                    return;
                }

                IsUpdateAvailable = latestVersionParsed > currentVersion;
                DownloadUrl = release?.Assets?.FirstOrDefault(a =>
                    string.Equals(a.Name, "TAY_Setup.exe", StringComparison.OrdinalIgnoreCase))?.DownloadUrl;

                if (IsUpdateAvailable)
                {
                    UpdateStatus = string.IsNullOrWhiteSpace(DownloadUrl)
                        ? "update available (installer missing)"
                        : "update available";

                    if (!_updateNotified)
                    {
                        _updateNotified = true;
                        TrayIconHelper.ShowBalloon(
                            "TAY Update Available",
                            $"A new version is available: {LatestVersion}. Open Settings > Update Control to download it."
                        );
                    }
                }
                else
                {
                    UpdateStatus = "up to date";
                }
            }
            catch
            {
                UpdateStatus = "update check failed";
            }
            finally
            {
                IsChecking = false;
            }
        }

        [RelayCommand]
        private async Task DownloadUpdateAsync()
        {
            if (!IsUpdateAvailable || IsDownloading) return;
            if (string.IsNullOrWhiteSpace(DownloadUrl))
            {
                UpdateStatus = "installer missing";
                return;
            }

            IsDownloading = true;
            UpdateStatus = "downloading...";

            try
            {
                var safeVersion = string.IsNullOrWhiteSpace(LatestVersion) ? "latest" : LatestVersion.Replace("/", "-");
                var targetPath = Path.Combine(
                    Path.GetTempPath(),
                    $"TAY_Setup_{safeVersion}_{DateTime.UtcNow:yyyyMMddHHmmss}.exe");

                using var response = await Http.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode)
                {
                    UpdateStatus = $"download failed ({(int)response.StatusCode})";
                    return;
                }

                await using var input = await response.Content.ReadAsStreamAsync();
                await using var output = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await input.CopyToAsync(output);

                UpdateStatus = "launching installer...";
                Process.Start(new ProcessStartInfo
                {
                    FileName = targetPath,
                    UseShellExecute = true
                });
            }
            catch (HttpRequestException ex)
            {
                UpdateStatus = ex.StatusCode.HasValue
                    ? $"download failed ({(int)ex.StatusCode.Value})"
                    : $"download failed: {ex.Message}";
            }
            catch (IOException ex)
            {
                UpdateStatus = $"download failed: {ex.Message}";
            }
            catch (UnauthorizedAccessException ex)
            {
                UpdateStatus = $"download failed: {ex.Message}";
            }
            catch (Exception ex)
            {
                UpdateStatus = $"download failed: {ex.Message}";
            }
            finally
            {
                IsDownloading = false;
            }
        }

        [RelayCommand]
        private void OpenReleasePage()
        {
            if (string.IsNullOrWhiteSpace(ReleasePageUrl)) return;
            Process.Start(new ProcessStartInfo
            {
                FileName = ReleasePageUrl,
                UseShellExecute = true
            });
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("TAY-Updater/1.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return client;
        }

        private static string RuntimeInformationLabel =>
            $"{System.Runtime.InteropServices.RuntimeInformation.OSArchitecture} / {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}";

        private static bool IsRunningAsAdministrator()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private static string GetAppVersion()
        {
            var version = typeof(SettingsViewModel).Assembly.GetName().Version;
            if (version == null) return "0.0.0";
            return version.Revision > 0 ? version.ToString() : version.ToString(3);
        }

        private static string NormalizeVersion(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return "";
            var trimmed = tag.Trim();
            if (trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed.Substring(1);
            }
            return trimmed;
        }

        private static Version? ParseVersion(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var normalized = NormalizeVersion(value);
            return Version.TryParse(normalized, out var version) ? version : null;
        }

        private sealed class GitHubRelease
        {
            [JsonPropertyName("tag_name")]
            public string? TagName { get; set; }

            [JsonPropertyName("html_url")]
            public string? HtmlUrl { get; set; }

            [JsonPropertyName("assets")]
            public GitHubAsset[]? Assets { get; set; }
        }

        private sealed class GitHubAsset
        {
            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("browser_download_url")]
            public string? DownloadUrl { get; set; }
        }
    }
}
