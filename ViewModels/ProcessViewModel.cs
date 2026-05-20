using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using TAY.Services;

namespace TAY.ViewModels
{
    public partial class ProcessViewModel : ObservableObject
    {
        public ObservableCollection<ProcessItemVM> Processes { get; } = new();
        private System.Collections.Generic.List<ProcessItemVM> _allProcesses = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotRefreshing))]
        private bool isRefreshing = false;

        public bool IsNotRefreshing => !IsRefreshing;

        [ObservableProperty]
        private string searchQuery = "";

        partial void OnSearchQueryChanged(string value)
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
                    
                foreach (var p in filtered)
                {
                    Processes.Add(p);
                }
            });
        }

        public void Kill(ProcessItemVM p)
        {
            Task.Run(() => 
            {
                SystemService.KillProcess(p.Id);
                _dispatcher?.TryEnqueue(() => 
                {
                    _allProcesses.Remove(p);
                    Processes.Remove(p);
                });
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

        public ProcessItemVM(ProcessInfo p, ProcessViewModel parent)
        {
            _parent = parent;
            Id = p.Id;
            Name = p.Name;
            RamBytes = p.Ram;
            RamStr = SystemService.FormatBytes(p.Ram);
        }

        [RelayCommand]
        private void EndTask()
        {
            _parent.Kill(this);
        }
    }
}
