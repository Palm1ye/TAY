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
        [NotifyPropertyChangedFor(nameof(IsNotScanning))]
        private bool isScanning = false;

        public bool IsNotScanning => !IsScanning;

        [ObservableProperty]
        private bool isCleaning = false;

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
                    Folders.Add(new FolderItemVM(f));
                    total += f.Size;
                }

                TotalSize = SystemService.FormatBytes(total);
                IsScanning = false;
            });
        }

        [RelayCommand]
        private async Task CleanAsync()
        {
            IsCleaning = true;
            var toClean = Folders.Where(f => f.IsSelected).Select(f => f.Path).ToList();
            await Task.Run(() => SystemService.CleanFolders(toClean));
            IsCleaning = false;
            await ScanAsync();
        }
    }

    public partial class FolderItemVM : ObservableObject
    {
        public string Name { get; }
        public string Path { get; }
        public string SizeStr { get; }

        [ObservableProperty]
        private bool isSelected = true;

        public FolderItemVM(FolderInfo f)
        {
            Name = f.Name;
            Path = f.Path;
            SizeStr = SystemService.FormatBytes(f.Size);
        }
    }
}
