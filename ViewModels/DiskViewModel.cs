using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TAY.Services;

namespace TAY.ViewModels
{
    public partial class DiskViewModel : ObservableObject
    {
        public ObservableCollection<DriveItemVM> Drives { get; } = new();
        public ObservableCollection<LargeFileVM> LargeFiles { get; } = new();

        [ObservableProperty]
        private bool isScanning = false;

        [ObservableProperty]
        private string scanStatus = "";

        [ObservableProperty]
        private string lastDrive = "";

        [ObservableProperty]
        private string mapStatus = "Select a drive to map storage.";

        [ObservableProperty]
        private bool hasLargeFiles = false;

        [ObservableProperty]
        private string systemFilesSize = "0 MB";

        [ObservableProperty]
        private string applicationsSize = "0 MB";

        [ObservableProperty]
        private string userMediaSize = "0 MB";

        [ObservableProperty]
        private string cacheTempSize = "0 MB";

        [ObservableProperty]
        private string otherFilesSize = "0 MB";

        [ObservableProperty]
        private string scannedTotalSize = "0 MB scanned";

        [ObservableProperty]
        private double systemPercent = 0;

        [ObservableProperty]
        private double appsPercent = 0;

        [ObservableProperty]
        private double mediaPercent = 0;

        [ObservableProperty]
        private double cachePercent = 0;

        [ObservableProperty]
        private double otherPercent = 0;

        [ObservableProperty]
        private string systemPercentStr = "0.0%";

        [ObservableProperty]
        private string appsPercentStr = "0.0%";

        [ObservableProperty]
        private string mediaPercentStr = "0.0%";

        [ObservableProperty]
        private string cachePercentStr = "0.0%";

        [ObservableProperty]
        private string otherPercentStr = "0.0%";

        private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcher;

        public DiskViewModel()
        {
            _dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            _ = LoadDrivesAsync();
        }

        private async Task LoadDrivesAsync()
        {
            var drives = await Task.Run(() => SystemService.GetDrives());
            foreach (var d in drives)
            {
                Drives.Add(new DriveItemVM(d, this));
            }
        }

        public void Analyze(string driveLetter)
        {
            if (IsScanning) return;
            IsScanning = true;
            LastDrive = driveLetter;
            LargeFiles.Clear();
            HasLargeFiles = false;
            ScanStatus = $"Scanning drive {driveLetter}:\\ ...";

            Task.Run(() =>
            {
                var filesList = new System.Collections.Generic.List<FileInfo>();
                long systemFiles = 0;
                long applications = 0;
                long userMedia = 0;
                long cacheTemp = 0;
                long otherFiles = 0;
                var stack = new System.Collections.Generic.Stack<(string path, int depth)>();
                stack.Push(($"{driveLetter}:\\", 0));

                int processedDirectories = 0;
                int maxFilesLimit = 120000;
                int maxDepth = 8;

                while (stack.Count > 0 && filesList.Count < maxFilesLimit)
                {
                    var (currentPath, depth) = stack.Pop();
                    if (depth > maxDepth) continue;

                    try
                    {
                        var dir = new DirectoryInfo(currentPath);
                        if ((dir.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                            continue; // Skip junction points/symbolic links to prevent circular loops

                        string dirName = dir.Name.ToLowerInvariant();
                        if (dirName == "system volume information" || dirName == "$winreagent")
                            continue;

                        // Enumerate files
                        foreach (var f in dir.EnumerateFiles())
                        {
                            try
                            {
                                filesList.Add(f);
                                switch (ClassifyStorageFile(f))
                                {
                                    case StorageCategory.System:
                                        systemFiles += f.Length;
                                        break;
                                    case StorageCategory.Applications:
                                        applications += f.Length;
                                        break;
                                    case StorageCategory.UserMedia:
                                        userMedia += f.Length;
                                        break;
                                    case StorageCategory.CacheTemp:
                                        cacheTemp += f.Length;
                                        break;
                                    case StorageCategory.Other:
                                        otherFiles += f.Length;
                                        break;
                                }
                            }
                            catch { }
                        }

                        foreach (var d in dir.EnumerateDirectories())
                        {
                            try
                            {
                                if ((d.Attributes & FileAttributes.ReparsePoint) != FileAttributes.ReparsePoint &&
                                    ShouldEnterDirectory(d.FullName, depth + 1, maxDepth))
                                {
                                    stack.Push((d.FullName, depth + 1));
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }

                    processedDirectories++;
                    if (processedDirectories % 150 == 0)
                    {
                        string status = $"Scanning... mapped {processedDirectories} folders, found {filesList.Count} files.";
                        long currentSystem = systemFiles;
                        long currentApplications = applications;
                        long currentUserMedia = userMedia;
                        long currentCacheTemp = cacheTemp;
                        long currentOther = otherFiles;
                        _dispatcher?.TryEnqueue(() => ScanStatus = status);
                        _dispatcher?.TryEnqueue(() =>
                        {
                            SystemFilesSize = SystemService.FormatBytes(currentSystem);
                            ApplicationsSize = SystemService.FormatBytes(currentApplications);
                            UserMediaSize = SystemService.FormatBytes(currentUserMedia);
                            CacheTempSize = SystemService.FormatBytes(currentCacheTemp);
                            OtherFilesSize = SystemService.FormatBytes(currentOther);
                            ScannedTotalSize = $"{SystemService.FormatBytes(currentSystem + currentApplications + currentUserMedia + currentCacheTemp + currentOther)} scanned";
                            MapStatus = $"Live map for {driveLetter}:\\";
                        });
                        UpdateWeights(currentSystem, currentApplications, currentUserMedia, currentCacheTemp, currentOther);
                    }
                }

                var topFiles = filesList.OrderByDescending(f => f.Length).Take(50).ToList();

                _dispatcher?.TryEnqueue(() =>
                {
                    foreach (var f in topFiles)
                        LargeFiles.Add(new LargeFileVM(f));
                    HasLargeFiles = LargeFiles.Count > 0;
                    ScanStatus = filesList.Count >= maxFilesLimit
                        ? $"Scan capped at {maxFilesLimit:N0} files after {processedDirectories:N0} folders. Showing top 50 largest files."
                        : $"Scan complete. Processed {processedDirectories:N0} folders. Showing top 50 largest files.";
                    SystemFilesSize = SystemService.FormatBytes(systemFiles);
                    ApplicationsSize = SystemService.FormatBytes(applications);
                    UserMediaSize = SystemService.FormatBytes(userMedia);
                    CacheTempSize = SystemService.FormatBytes(cacheTemp);
                    OtherFilesSize = SystemService.FormatBytes(otherFiles);
                    ScannedTotalSize = $"{SystemService.FormatBytes(systemFiles + applications + userMedia + cacheTemp + otherFiles)} scanned";
                    MapStatus = $"Storage map for {driveLetter}:\\ based on scanned files.";
                    IsScanning = false;
                });
                UpdateWeights(systemFiles, applications, userMedia, cacheTemp, otherFiles);
            });
        }

        private enum StorageCategory
        {
            System,
            Applications,
            UserMedia,
            CacheTemp,
            Other
        }

        private static bool ShouldEnterDirectory(string path, int depth, int maxDepth)
        {
            var lower = path.ToLowerInvariant();
            if (depth > maxDepth) return false;

            if (lower.Contains("\\windows\\winsxs\\") ||
                lower.Contains("\\windows\\servicing\\") ||
                lower.Contains("\\windows\\system32\\driverstore\\filerepository\\") ||
                lower.Contains("\\appdata\\local\\packages\\") ||
                lower.Contains("\\node_modules\\") ||
                lower.Contains("\\.git\\"))
            {
                return false;
            }

            return true;
        }

        private static StorageCategory ClassifyStorageFile(FileInfo file)
        {
            var path = file.FullName.ToLowerInvariant();
            var extension = file.Extension.ToLowerInvariant();

            if (path.Contains("\\temp\\") ||
                path.Contains("\\cache\\") ||
                path.Contains("\\prefetch\\") ||
                path.Contains("\\$recycle.bin\\") ||
                path.Contains("\\logs\\"))
            {
                return StorageCategory.CacheTemp;
            }

            if (path.Contains("\\program files") ||
                path.Contains("\\programdata\\") ||
                path.Contains("\\steam\\") ||
                path.Contains("\\epic games\\") ||
                path.Contains("\\xboxgames\\") ||
                path.Contains("\\windowsapps\\"))
            {
                return StorageCategory.Applications;
            }

            if (extension is ".mp4" or ".mkv" or ".mov" or ".avi" or ".mp3" or ".wav" or ".flac" or ".jpg" or ".jpeg" or ".png" or ".webp" or ".zip" or ".rar" or ".7z" or ".iso")
            {
                return StorageCategory.UserMedia;
            }

            if (path.Contains("\\windows\\") ||
                path.Contains("\\drivers\\") ||
                extension is ".sys" or ".dll" or ".drv")
            {
                return StorageCategory.System;
            }

            return StorageCategory.Other;
        }

        public void RescanLast()
        {
            if (!string.IsNullOrWhiteSpace(LastDrive))
            {
                Analyze(LastDrive);
            }
        }

        private void UpdateWeights(long system, long apps, long media, long cache, long other)
        {
            double total = system + apps + media + cache + other;
            if (total == 0)
            {
                _dispatcher?.TryEnqueue(() =>
                {
                    SystemPercent = 0;
                    AppsPercent = 0;
                    MediaPercent = 0;
                    CachePercent = 0;
                    OtherPercent = 0;
                    SystemPercentStr = "0.0%";
                    AppsPercentStr = "0.0%";
                    MediaPercentStr = "0.0%";
                    CachePercentStr = "0.0%";
                    OtherPercentStr = "0.0%";
                });
                return;
            }

            _dispatcher?.TryEnqueue(() =>
            {
                SystemPercent = (double)system / total * 100;
                AppsPercent = (double)apps / total * 100;
                MediaPercent = (double)media / total * 100;
                CachePercent = (double)cache / total * 100;
                OtherPercent = (double)other / total * 100;

                SystemPercentStr = $"{SystemPercent:F1}%";
                AppsPercentStr = $"{AppsPercent:F1}%";
                MediaPercentStr = $"{MediaPercent:F1}%";
                CachePercentStr = $"{CachePercent:F1}%";
                OtherPercentStr = $"{OtherPercent:F1}%";
            });
        }
    }

    public partial class DriveItemVM : ObservableObject
    {
        private readonly DiskViewModel _parent;
        public string Letter { get; }
        public string Label { get; }
        public string CapacityStr { get; }
        public double UsedPercent { get; }
        public double UsedWidth { get; }

        public DriveItemVM(DriveData d, DiskViewModel parent)
        {
            _parent = parent;
            Letter = d.Letter;
            Label = d.Label;
            CapacityStr = $"{SystemService.FormatBytes(d.Free)} free of {SystemService.FormatBytes(d.Total)}";
            UsedPercent = (double)d.Used / d.Total * 100;
            UsedWidth = (UsedPercent / 100.0) * 210;
        }

        [RelayCommand]
        private void Analyze()
        {
            _parent.Analyze(Letter);
        }
    }

    public partial class LargeFileVM : ObservableObject
    {
        public string Name { get; }
        public string Path { get; }
        public string SizeStr { get; }
        public string FolderPath { get; }

        public LargeFileVM(FileInfo f)
        {
            Name = f.Name;
            Path = f.FullName;
            SizeStr = SystemService.FormatBytes(f.Length);
            FolderPath = f.DirectoryName ?? "";
        }
    }
}
