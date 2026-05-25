using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using TAY.Services;

namespace TAY.ViewModels
{
    public partial class CleanerViewModel : ObservableObject
    {
        public ObservableCollection<FolderItemVM> Folders { get; } = new();

        [ObservableProperty]
        private string totalSize = "0 MB";

        [ObservableProperty]
        private string selectedSize = "0 MB selected";

        [ObservableProperty]
        private string selectedCount = "0 targets selected";

        [ObservableProperty]
        private string cleanupStatus = "Scan the system, review the selected targets, then reclaim space.";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotBusy))]
        [NotifyPropertyChangedFor(nameof(ScanButtonText))]
        private bool isScanning = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotBusy))]
        [NotifyPropertyChangedFor(nameof(CleanButtonText))]
        private bool isCleaning = false;

        public bool IsNotBusy => !IsScanning && !IsCleaning;

        public string ScanButtonText => IsScanning ? "Scanning..." : "Scan System";
        public string CleanButtonText => IsCleaning ? "Cleaning..." : "Clean Selected";

        private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcher;
        public CleanerViewModel()
        {
            _dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            _ = ScanAsync();
        }

        [RelayCommand]
        private async Task ScanAsync()
        {
            IsScanning = true;
            Folders.Clear();
            TotalSize = "Scanning...";

            var folders = await Task.Run(() => SystemService.GetCleanerFolders());
            _dispatcher?.TryEnqueue(() =>
            {
                long total = 0;
                foreach (var f in folders)
                {
                    Folders.Add(new FolderItemVM(f, this));
                    total += f.Size;
                }

                TotalSize = SystemService.FormatBytes(total);
                RefreshSelectionSummary();
                CleanupStatus = "Scan complete. Review the cleanup targets before deleting files.";
                IsScanning = false;
            });
        }

        [RelayCommand]
        private async Task CleanAsync()
        {
            IsCleaning = true;
            var toClean = Folders.Where(f => f.IsSelected).Select(f => f.Path).ToList();
            CleanupStatus = $"Cleaning {toClean.Count} selected target(s)...";
            await Task.Run(() => SystemService.CleanFolders(toClean));
            IsCleaning = false;
            CleanupStatus = $"Cleanup finished for {toClean.Count} target(s). Locked files may have been skipped.";
            await ScanAsync();
        }

        public void RefreshSelectionSummary()
        {
            var selected = Folders.Where(f => f.IsSelected).ToList();
            var totalBytes = selected.Sum(f => f.SizeBytes);
            SelectedSize = $"{SystemService.FormatBytes(totalBytes)} selected";
            SelectedCount = selected.Count == 1 ? "1 target selected" : $"{selected.Count} targets selected";
        }
    }

    public partial class FolderItemVM : ObservableObject
    {
        private readonly CleanerViewModel _parent;

        public string Name { get; }
        public string Path { get; }
        public string SizeStr { get; }
        public long SizeBytes { get; }

        [ObservableProperty]
        private bool isSelected = true;

        public FolderItemVM(FolderInfo f, CleanerViewModel parent)
        {
            _parent = parent;
            Name = f.Name;
            Path = f.Path;
            SizeBytes = f.Size;
            SizeStr = SystemService.FormatBytes(f.Size);
        }

        partial void OnIsSelectedChanged(bool value)
        {
            _parent.RefreshSelectionSummary();
        }
    }
}
