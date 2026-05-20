using CommunityToolkit.Mvvm.ComponentModel;
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
                var sorted = apps
                    .OrderByDescending(a => a.Enabled)
                    .ThenByDescending(a => StartupAppVM.GetImpactRank(a.Impact))
                    .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                Apps.Clear();
                foreach (var a in sorted)
                {
                    var vm = new StartupAppVM(a);
                    vm.PropertyChanged += App_PropertyChanged;
                    Apps.Add(vm);
                }
            });
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
                var sorted = Apps
                    .OrderByDescending(a => a.IsEnabled)
                    .ThenByDescending(a => a.ImpactRank)
                    .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

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
