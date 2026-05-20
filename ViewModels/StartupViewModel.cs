using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
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
                Apps.Clear();
                foreach (var a in apps)
                {
                    Apps.Add(new StartupAppVM(a));
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
        }

        public string Name { get; }
        public string Command { get; }
        public string Impact { get; }

        [ObservableProperty]
        private bool isEnabled;

        partial void OnIsEnabledChanged(bool value)
        {
            Task.Run(() => SystemService.ToggleStartupApp(Name, value));
        }
    }
}
