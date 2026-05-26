using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System;
using Windows.ApplicationModel.DataTransfer;
using TAY.ViewModels;

namespace TAY.Views
{
    public sealed partial class HardwareView : Page
    {
        public HardwareViewModel ViewModel { get; }

        public HardwareView()
        {
            ViewModel = new HardwareViewModel();
            this.InitializeComponent();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Refresh();
        }

        private void CopySpecs_Click(object sender, RoutedEventArgs e)
        {
            var lines = new System.Collections.Generic.List<string>
            {
                "TAY HARDWARE SPECS",
                $"CPU: {ViewModel.CpuName}",
                $"CORES: {ViewModel.CpuCores}",
                $"THREADS: {ViewModel.CpuThreads}",
                $"BASE_FREQ: {ViewModel.CpuClockSpeed}",
                $"GPU: {ViewModel.GpuName}",
                $"DEDICATED_VRAM: {ViewModel.DedicatedVram}",
                $"SHARED_VRAM: {ViewModel.SharedVram}",
                $"TOTAL_VRAM: {ViewModel.TotalVram}",
                $"RAM: {ViewModel.RamCapacity}",
                $"MOTHERBOARD: {ViewModel.MotherboardName}",
                $"OS: {ViewModel.OsName}",
                $"OS_BUILD: {ViewModel.OsBuildInfo}"
            };

            foreach (var category in ViewModel.HardwareCategories)
            {
                lines.Add("");
                lines.Add($"[{category.Title}]");
                foreach (var row in category.Rows)
                {
                    lines.Add($"{row.Label}: {row.Value}");
                }
            }

            var data = new DataPackage();
            data.SetText(string.Join(Environment.NewLine, lines));
            Clipboard.SetContent(data);
        }

        private void ReviewDrivers_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(BoostView));
        }
    }
}
