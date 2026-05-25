using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using TAY.Services;

namespace TAY.ViewModels
{
    public partial class ProcessViewModel : ObservableObject
    {
        public ObservableCollection<ProcessItemVM> Processes { get; } = new();
        public string[] SortOptions { get; } =
        {
            "Memory High-Low",
            "Memory Low-High",
            "Name A-Z",
            "Name Z-A"
        };

        private System.Collections.Generic.List<ProcessItemVM> _allProcesses = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotRefreshing))]
        private bool isRefreshing = false;

        public bool IsNotRefreshing => !IsRefreshing;

        [ObservableProperty]
        private string searchQuery = "";

        [ObservableProperty]
        private string operationStatus = "Review active processes before ending a task.";

        [ObservableProperty]
        private string selectedSort = "Memory High-Low";

        partial void OnSearchQueryChanged(string value)
        {
            FilterProcesses();
        }

        partial void OnSelectedSortChanged(string value)
        {
            FilterProcesses();
        }

        private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcher;
        public ProcessViewModel()
        {
            _dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            _ = LoadProcessesAsync();
        }

        [RelayCommand]
        private async Task LoadProcessesAsync()
        {
            IsRefreshing = true;
            _allProcesses.Clear();
            var procs = await Task.Run(() => SystemService.GetTopProcesses(100)); // Load top 100
            foreach (var p in procs)
            {
                _allProcesses.Add(new ProcessItemVM(p, this));
            }
            FilterProcesses();
            IsRefreshing = false;
        }

        private void FilterProcesses()
        {
            _dispatcher?.TryEnqueue(() =>
            {
                Processes.Clear();
                var filtered = string.IsNullOrWhiteSpace(SearchQuery) 
                    ? _allProcesses 
                    : _allProcesses.Where(p => p.Name.Contains(SearchQuery, System.StringComparison.OrdinalIgnoreCase)).ToList();

                filtered = SelectedSort switch
                {
                    "Memory Low-High" => filtered.OrderBy(p => p.RamBytes).ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList(),
                    "Name A-Z" => filtered.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ThenByDescending(p => p.RamBytes).ToList(),
                    "Name Z-A" => filtered.OrderByDescending(p => p.Name, StringComparer.OrdinalIgnoreCase).ThenByDescending(p => p.RamBytes).ToList(),
                    _ => filtered.OrderByDescending(p => p.RamBytes).ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList()
                };
                    
                foreach (var p in filtered)
                {
                    Processes.Add(p);
                }
            });
        }

        public async Task<bool> KillAsync(ProcessItemVM p)
        {
            if (p.IsProtected)
            {
                OperationStatus = $"{p.Name} is protected and was not ended.";
                return false;
            }

            OperationStatus = $"Ending {p.Name} (PID {p.Id})...";
            return await Task.Run(() =>
            {
                return SystemService.KillProcess(p.Id);
            }).ContinueWith(task =>
            {
                var success = task.Result;
                _dispatcher?.TryEnqueue(() => 
                {
                    if (success)
                    {
                        _allProcesses.Remove(p);
                        Processes.Remove(p);
                        OperationStatus = $"{p.Name} was ended.";
                    }
                    else
                    {
                        OperationStatus = $"Could not end {p.Name}. It may require system protection or already exited.";
                    }
                });
                return success;
            });
        }
    }

    public partial class ProcessItemVM : ObservableObject
    {
        private readonly ProcessViewModel _parent;
        public int Id { get; }
        public string Name { get; }
        public string RamStr { get; }
        public long RamBytes { get; }
        public bool IsProtected { get; }
        public bool CanEndTask => !IsProtected;
        public string ProtectionLabel => IsProtected ? "protected" : "user task";

        public ProcessItemVM(ProcessInfo p, ProcessViewModel parent)
        {
            _parent = parent;
            Id = p.Id;
            Name = p.Name;
            RamBytes = p.Ram;
            RamStr = SystemService.FormatBytes(p.Ram);
            IsProtected = IsProtectedProcess(Name, Id);
        }

        [RelayCommand]
        private async Task EndTaskAsync()
        {
            await _parent.KillAsync(this);
        }

        private static bool IsProtectedProcess(string name, int id)
        {
            if (id <= 4) return true;

            var protectedNames = new[]
            {
                "system",
                "registry",
                "smss",
                "csrss",
                "wininit",
                "winlogon",
                "services",
                "lsass",
                "fontdrvhost",
                "dwm"
            };

            return protectedNames.Any(p => string.Equals(p, name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
