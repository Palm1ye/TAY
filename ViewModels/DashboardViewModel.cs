using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Timers;
using TAY.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace TAY.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly System.Timers.Timer _timer;
        private readonly DispatcherQueue _dispatcherQueue;
        private readonly List<double> _cpuHistory = new();
        private readonly List<double> _ramHistory = new();
        private readonly List<double> _gpuHistory = new();

        [ObservableProperty]
        private string cpuUsage = "0%";

        [ObservableProperty]
        private double cpuValue = 0;

        [ObservableProperty]
        private string ramUsage = "0%";

        [ObservableProperty]
        private double ramValue = 0;

        [ObservableProperty]
        private string gpuUsage = "0%";

        [ObservableProperty]
        private double gpuValue = 0;

        [ObservableProperty]
        private string diskUsage = "0%";

        [ObservableProperty]
        private double diskValue = 0;

        [ObservableProperty]
        private string diskFree = "...";

        [ObservableProperty]
        private string processCount = "...";

        [ObservableProperty]
        private string uptime = "...";

        [ObservableProperty]
        private string cpuModel = "...";

        [ObservableProperty]
        private string gpuModel = "...";

        [ObservableProperty]
        private string ramAmount = "...";

        [ObservableProperty]
        private string motherboardModel = "...";

        [ObservableProperty]
        private ISeries[] cpuSeries = Array.Empty<ISeries>();

        public Axis[] XAxes { get; set; } = new Axis[]
        {
            new Axis
            {
                LabelsPaint = null,
                SeparatorsPaint = null
            }
        };

        public Axis[] YAxes { get; set; } = new Axis[]
        {
            new Axis
            {
                LabelsPaint = null,
                SeparatorsPaint = null,
                MinLimit = 0,
                MaxLimit = 100
            }
        };

        public ObservableCollection<PowerPlan> PowerPlans { get; } = new();

        public ObservableCollection<string> LogLines => RealTimeLogService.Instance.LogLines;

        [ObservableProperty]
        private PowerPlan? selectedPlan;

        public DashboardViewModel()
        {
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            RealTimeLogService.Instance.Initialize(_dispatcherQueue);
            RealTimeLogService.Instance.Log("Tay System Optimizer Dashboard initialized.");
            
            for (int i = 0; i < 20; i++)
            {
                _cpuHistory.Add(0);
                _ramHistory.Add(0);
                _gpuHistory.Add(0);
            }

            CpuSeries = new ISeries[]
            {
                new LineSeries<double>
                {
                    Values = _cpuHistory,
                    GeometryFill = null,
                    GeometryStroke = null,
                    Stroke = new SolidColorPaint(SKColors.SpringGreen, 1.5f),
                    Fill = new SolidColorPaint(new SKColor(80, 250, 123, 18)),
                    LineSmoothness = 0.5
                },
                new LineSeries<double>
                {
                    Values = _ramHistory,
                    GeometryFill = null,
                    GeometryStroke = null,
                    Stroke = new SolidColorPaint(new SKColor(242, 198, 109), 1.5f),
                    Fill = null,
                    LineSmoothness = 0.5
                },
                new LineSeries<double>
                {
                    Values = _gpuHistory,
                    GeometryFill = null,
                    GeometryStroke = null,
                    Stroke = new SolidColorPaint(new SKColor(121, 168, 255), 1.5f),
                    Fill = null,
                    LineSmoothness = 0.5
                }
            };

            _timer = new System.Timers.Timer(3000);
            _timer.Elapsed += OnTimerElapsed;
            _timer.Start();
            
            LoadSpecs();
            LoadPowerPlans();
            _ = UpdateTelemetryAsync();
        }

        private void LoadSpecs()
        {
            try
            {
                var info = SystemService.GetHardwareInfo();
                if (info.TryGetValue("CPU", out var cpu)) CpuModel = cpu?.ToString() ?? "...";
                if (info.TryGetValue("GPU", out var gpu)) GpuModel = gpu?.ToString() ?? "...";
                if (info.TryGetValue("RAM", out var ram)) RamAmount = $"{ram} GB";
                if (info.TryGetValue("Motherboard", out var mb)) MotherboardModel = mb?.ToString() ?? "...";
            }
            catch { }
        }

        private void LoadPowerPlans()
        {
            try
            {
                var plans = SystemService.GetPowerPlans();
                PowerPlans.Clear();
                foreach (var p in plans)
                {
                    PowerPlans.Add(p);
                    if (p.Active)
                    {
#pragma warning disable MVVMTK0034
                        selectedPlan = p;
#pragma warning restore MVVMTK0034
                    }
                }
                OnPropertyChanged(nameof(SelectedPlan));
            }
            catch { }
        }

        partial void OnSelectedPlanChanged(PowerPlan? value)
        {
            if (value != null)
            {
                Task.Run(() => SystemService.SetPowerPlan(value.Id));
            }
        }

        private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            _ = UpdateTelemetryAsync();
        }

        private bool _isUpdating = false;
        private async Task UpdateTelemetryAsync()
        {
            if (_isUpdating) return;
            _isUpdating = true;
            try
            {
                var cpuTask = Task.Run(() => SystemService.GetCpuUsage());
                var ramTask = Task.Run(() => SystemService.GetRamInfo());
                var gpuTask = Task.Run(() => SystemService.GetGpuUsage());
                var storageTask = Task.Run(() => SystemService.GetStorageInfo());
                var processCountTask = Task.Run(() => SystemService.GetProcessCount());

                await Task.WhenAll(cpuTask, ramTask, gpuTask, storageTask, processCountTask);

                var cpu = cpuTask.Result;
                var ram = ramTask.Result;
                var gpu = gpuTask.Result;
                var storage = storageTask.Result;
                var processes = processCountTask.Result;
                var utime = SystemService.GetUptime();

                _dispatcherQueue.TryEnqueue(() =>
                {
                    CpuValue = cpu;
                    CpuUsage = $"{cpu}%";
                    
                    RamValue = ram.percent;
                    RamUsage = $"{ram.percent}%";

                    GpuValue = gpu;
                    GpuUsage = $"{gpu}%";

                    DiskValue = storage.percent;
                    DiskUsage = $"{storage.percent}%";
                    DiskFree = $"{SystemService.FormatBytes(storage.free)} free";

                    ProcessCount = processes.ToString("N0");
                    
                    Uptime = SystemService.FormatUptime(utime);

                    PushTelemetryValue(_cpuHistory, cpu);
                    PushTelemetryValue(_ramHistory, ram.percent);
                    PushTelemetryValue(_gpuHistory, gpu);

                    CpuSeries[0].Values = _cpuHistory;
                    CpuSeries[1].Values = _ramHistory;
                    CpuSeries[2].Values = _gpuHistory;

                    OnPropertyChanged(nameof(CpuSeries));
                });
            }
            catch { }
            finally
            {
                _isUpdating = false;
            }
        }

        private static void PushTelemetryValue(List<double> values, double value)
        {
            values.Add(value);
            if (values.Count > 20) values.RemoveAt(0);
        }

        public void Cleanup()
        {
            _timer?.Stop();
            _timer?.Dispose();
        }
    }
}
