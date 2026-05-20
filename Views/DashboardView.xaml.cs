using Microsoft.UI.Xaml.Controls;
using System;
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

        private void Page_Unloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            ViewModel.Cleanup();
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
                $"CPU MODEL: {ViewModel.CpuModel}",
                $"GPU MODEL: {ViewModel.GpuModel}",
                $"RAM TOTAL: {ViewModel.RamAmount}",
                $"MOTHERBOARD: {ViewModel.MotherboardModel}"
            };

            var data = new DataPackage();
            data.SetText(string.Join(Environment.NewLine, lines));
            Clipboard.SetContent(data);
        }

        [System.Runtime.InteropServices.DllImport("psapi.dll", SetLastError = true)]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);

        private void QuickBoost_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            QuickBoostBtn.IsEnabled = false;
            QuickBoostBtn.Content = "[ boosting... ]";

            System.Threading.Tasks.Task.Run(() =>
            {
                long clearedBytes = 0;
                try
                {
                    // Force Garbage Collection
                    GC.Collect();
                    GC.WaitForPendingFinalizers();

                    // Empty working set of all processes
                    foreach (var process in System.Diagnostics.Process.GetProcesses())
                    {
                        try
                        {
                            long wsBefore = process.WorkingSet64;
                            bool success = EmptyWorkingSet(process.Handle);
                            long wsAfter = process.WorkingSet64;
                            if (success && wsBefore > wsAfter)
                            {
                                clearedBytes += (wsBefore - wsAfter);
                            }
                        }
                        catch { }
                    }
                }
                catch { }

                double clearedMB = clearedBytes / (1024.0 * 1024);

                this.DispatcherQueue.TryEnqueue(() =>
                {
                    if (clearedMB > 10)
                    {
                        QuickBoostBtn.Content = $"[ cleared {clearedMB:F0} MB! ]";
                    }
                    else
                    {
                        QuickBoostBtn.Content = "[ system peak! ]";
                    }

                    // Reset after 3 seconds
                    var resetTimer = new Microsoft.UI.Xaml.DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(3)
                    };
                    resetTimer.Tick += (s, ev) =>
                    {
                        resetTimer.Stop();
                        QuickBoostBtn.Content = "[ quick_boost ]";
                        QuickBoostBtn.IsEnabled = true;
                    };
                    resetTimer.Start();
                });
            });
        }
    }
}
