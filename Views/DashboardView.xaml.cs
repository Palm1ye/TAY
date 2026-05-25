using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using TAY.ViewModels;

namespace TAY.Views
{
    public sealed partial class DashboardView : Page
    {
        public DashboardViewModel ViewModel { get; }
        private bool _quickBoostRunning = false;

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

        private async void QuickBoost_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (_quickBoostRunning) return;
            _quickBoostRunning = true;

            QuickBoostBtn.IsEnabled = false;
            QuickBoostBtn.Content = "Boosting...";

            QuickBoostDialog.XamlRoot = this.XamlRoot;
            QuickBoostDialog.IsPrimaryButtonEnabled = false;
            QuickBoostRing.IsActive = true;
            QuickBoostStatusText.Text = "Boosting...";
            QuickBoostSummaryText.Text = "";
            QuickBoostStep1.Text = "Preparing managed memory sweep";
            QuickBoostStep2.Text = "Waiting to trim working sets";
            QuickBoostStep3.Text = "Waiting to finalize report";
            _ = QuickBoostDialog.ShowAsync();

            QuickBoostReport report;
            try
            {
                report = await Task.Run(() =>
                {
                    var localReport = new QuickBoostReport();
                    try
                    {
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        localReport.ManagedSweep = true;

                        this.DispatcherQueue.TryEnqueue(() =>
                        {
                            QuickBoostStep1.Text = "Managed memory sweep complete";
                            QuickBoostStep2.Text = "Trimming process working sets";
                        });

                        var processes = System.Diagnostics.Process.GetProcesses();
                        localReport.ProcessesScanned = processes.Length;

                        foreach (var process in processes)
                        {
                            try
                            {
                                long wsBefore = process.WorkingSet64;
                                bool success = EmptyWorkingSet(process.Handle);
                                long wsAfter = process.WorkingSet64;
                                if (success && wsBefore > wsAfter)
                                {
                                    localReport.ProcessesTrimmed++;
                                    localReport.ClearedBytes += (wsBefore - wsAfter);
                                }
                            }
                            catch { }
                        }

                        this.DispatcherQueue.TryEnqueue(() =>
                        {
                            QuickBoostStep2.Text = "Working set trim complete";
                            QuickBoostStep3.Text = "Finalizing report";
                        });
                    }
                    catch { }

                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        QuickBoostStep3.Text = "Report finalized";
                    });

                    return localReport;
                });
            }
            catch
            {
                QuickBoostBtn.Content = "Quick Boost";
                QuickBoostBtn.IsEnabled = true;
                _quickBoostRunning = false;
                return;
            }

            double clearedMB = report.ClearedBytes / (1024.0 * 1024);

            this.DispatcherQueue.TryEnqueue(() =>
            {
                QuickBoostRing.IsActive = false;
                QuickBoostStatusText.Text = "Boost complete";
                QuickBoostSummaryText.Text = $"Cleared {clearedMB:F0} MB from {report.ProcessesTrimmed}/{report.ProcessesScanned} processes.";
                QuickBoostDialog.IsPrimaryButtonEnabled = true;
            });

            await Task.Delay(1200);
            try { QuickBoostDialog.Hide(); } catch { }

            this.DispatcherQueue.TryEnqueue(() =>
            {
                if (clearedMB > 10)
                {
                    QuickBoostBtn.Content = $"Cleared {clearedMB:F0} MB";
                }
                else
                {
                    QuickBoostBtn.Content = "System Peak";
                }

                var resetTimer = new Microsoft.UI.Xaml.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(3)
                };
                resetTimer.Tick += (s, ev) =>
                {
                    resetTimer.Stop();
                    QuickBoostBtn.Content = "Quick Boost";
                    QuickBoostBtn.IsEnabled = true;
                    _quickBoostRunning = false;
                };
                resetTimer.Start();
            });
        }

        private sealed class QuickBoostReport
        {
            public bool ManagedSweep { get; set; }
            public long ClearedBytes { get; set; }
            public int ProcessesScanned { get; set; }
            public int ProcessesTrimmed { get; set; }
        }
    }
}
