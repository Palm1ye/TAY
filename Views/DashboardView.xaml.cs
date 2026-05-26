using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using TAY.ViewModels;

namespace TAY.Views
{
    public sealed partial class DashboardView : Page
    {
        public DashboardViewModel ViewModel { get; }

        public DashboardView()
        {
            ViewModel = new DashboardViewModel();
            this.DataContext = ViewModel;
            this.InitializeComponent();
        }

        private void CopySnapshot_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var lines = new[]
            {
                "TAY SYSTEM SNAPSHOT",
                $"CPU: {ViewModel.CpuUsage}",
                $"RAM: {ViewModel.RamUsage}",
                $"GPU: {ViewModel.GpuUsage}",
                $"DISK: {ViewModel.DiskUsage} ({ViewModel.DiskFree})",
                $"PROCESSES: {ViewModel.ProcessCount}",
                $"UPTIME: {ViewModel.Uptime}",
                $"DIAGNOSIS: {ViewModel.DiagnosisTitle}",
                $"SIGNAL: {ViewModel.DiagnosisSignal}"
            };

            var data = new DataPackage();
            data.SetText(string.Join(Environment.NewLine, lines));
            Clipboard.SetContent(data);
        }

        private async void Refresh_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            await ViewModel.RefreshAsync();
        }

        private void OpenBoost_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Frame.Navigate(typeof(BoostView));
        }

        private void OpenHardware_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Frame.Navigate(typeof(HardwareView));
        }

        private void OpenDiagnosis_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            switch (ViewModel.DiagnosisRoute)
            {
                case "Boost":
                    OpenBoost_Click(sender, e);
                    break;
                case "Storage":
                    OpenStorage_Click(sender, e);
                    break;
                case "Processes":
                    OpenProcesses_Click(sender, e);
                    break;
                default:
                    break;
            }
        }

        private void OpenClean_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Frame.Navigate(typeof(CleanerView));
        }

        private void OpenStorage_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Frame.Navigate(typeof(DiskView));
        }

        private void OpenProcesses_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Frame.Navigate(typeof(ProcessView));
        }

        private void OpenStartup_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Frame.Navigate(typeof(StartupView));
        }

    }
}
