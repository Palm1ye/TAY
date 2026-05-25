using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using TAY.Services;

namespace TAY.ViewModels
{
    public partial class StartupViewModel : ObservableObject
    {
        public ObservableCollection<StartupAppVM> Apps { get; } = new();
        public string[] SortOptions { get; } =
        {
            "Recommended",
            "Name A-Z",
            "Impact High-Low",
            "Enabled First",
            "Disabled First"
        };

        [ObservableProperty]
        private string selectedSort = "Recommended";

        [ObservableProperty]
        private string guidance = "Start with high-impact enabled apps. Disable only items you recognize.";

        private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcher;

        public StartupViewModel()
        {
            _dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            _ = LoadAppsAsync();
        }

        private async Task LoadAppsAsync()
        {
            var apps = await Task.Run(() => SystemService.GetStartupApps());
            _dispatcher?.TryEnqueue(() =>
            {
                Apps.Clear();
                foreach (var a in apps)
                {
                    var vm = new StartupAppVM(a);
                    vm.PropertyChanged += App_PropertyChanged;
                    Apps.Add(vm);
                }
                ApplySort();
            });
        }

        partial void OnSelectedSortChanged(string value)
        {
            ApplySort();
        }

        private void App_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(StartupAppVM.IsEnabled))
            {
                ApplySort();
            }
        }

        private void ApplySort()
        {
            _dispatcher?.TryEnqueue(() =>
            {
                var sorted = SelectedSort switch
                {
                    "Name A-Z" => Apps.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList(),
                    "Impact High-Low" => Apps.OrderByDescending(a => a.ImpactRank).ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList(),
                    "Enabled First" => Apps.OrderByDescending(a => a.IsEnabled).ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList(),
                    "Disabled First" => Apps.OrderBy(a => a.IsEnabled).ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList(),
                    _ => Apps.OrderByDescending(a => a.IsEnabled).ThenByDescending(a => a.ImpactRank).ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList()
                };

                Guidance = SelectedSort switch
                {
                    "Impact High-Low" => "Highest impact items are listed first. These usually matter most for boot time.",
                    "Disabled First" => "Disabled entries are grouped first so you can quickly restore something you need.",
                    "Enabled First" => "Enabled entries are grouped first so you can review what currently launches with Windows.",
                    "Name A-Z" => "Alphabetical view is useful when you are looking for a specific app.",
                    _ => "Recommended view prioritizes enabled and high-impact entries."
                };

                for (int targetIndex = 0; targetIndex < sorted.Count; targetIndex++)
                {
                    var item = sorted[targetIndex];
                    int currentIndex = Apps.IndexOf(item);
                    if (currentIndex >= 0 && currentIndex != targetIndex)
                    {
                        Apps.Move(currentIndex, targetIndex);
                    }
                }
            });
        }

        [RelayCommand]
        private void SortByName()
        {
            SelectedSort = "Name A-Z";
        }

        [RelayCommand]
        private void SortByImpact()
        {
            SelectedSort = "Impact High-Low";
        }

        [RelayCommand]
        private void SortByEnabled()
        {
            SelectedSort = SelectedSort == "Enabled First" ? "Disabled First" : "Enabled First";
        }
    }

    public partial class StartupAppVM : ObservableObject
    {
        private readonly StartupApp _app;

        public StartupAppVM(StartupApp app)
        {
            _app = app;
            Name = app.Name;
            Command = app.Command;
            IsEnabled = app.Enabled;
            Impact = app.Impact.ToUpper();
            ImpactRank = GetImpactRank(app.Impact);
        }

        public string Name { get; }
        public string Command { get; }
        public string Impact { get; }
        public int ImpactRank { get; }

        internal static int GetImpactRank(string impact)
        {
            return impact?.ToLowerInvariant() switch
            {
                "high" => 3,
                "medium" => 2,
                "low" => 1,
                _ => 0
            };
        }

        [ObservableProperty]
        private bool isEnabled;

        partial void OnIsEnabledChanged(bool value)
        {
            Task.Run(() => SystemService.ToggleStartupApp(Name, value));
        }
    }
}
