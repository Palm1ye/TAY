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
        private string cpuStatus = "Waiting for sample";

        [ObservableProperty]
        private string ramStatus = "Waiting for sample";

        [ObservableProperty]
        private string storageStatus = "Waiting for sample";

        [ObservableProperty]
        private string processStatus = "Waiting for sample";

        [ObservableProperty]
        private string diagnosisTitle = "Waiting for telemetry";

        [ObservableProperty]
        private string diagnosisDetail = "Live CPU, memory, storage, and process data will select the most useful tool here.";

        [ObservableProperty]
        private string diagnosisSignal = "Thresholds: RAM 85%, storage 88%, CPU 80%, processes 220+";

        [ObservableProperty]
        private string diagnosisActionText = "Open Dashboard";

        [ObservableProperty]
        private string diagnosisRoute = "Dashboard";

        [ObservableProperty]
        private string memoryPressureStatus = "Pending";

        [ObservableProperty]
        private string storagePressureStatus = "Pending";

        [ObservableProperty]
        private string backgroundLoadStatus = "Pending";

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

                    UpdateDashboardSignals(cpu, ram.percent, storage.percent, processes);

                    OnPropertyChanged(nameof(CpuSeries));
                });
            }
            catch { }
            finally
            {
                _isUpdating = false;
            }
        }

        private void UpdateDashboardSignals(double cpu, double ram, double storage, int processes)
        {
            CpuStatus = cpu >= 80 ? "High load" : cpu >= 60 ? "Moderate" : "Normal";
            RamStatus = ram >= 85 ? "Action needed" : ram >= 70 ? "Watch" : "Normal";
            StorageStatus = storage >= 88 ? "Cleanup advised" : storage >= 80 ? "Review soon" : "Enough free space";
            ProcessStatus = processes >= 220 ? "Review processes" : processes >= 170 ? "Busy" : "Normal";

            MemoryPressureStatus = ram >= 85
                ? "High pressure"
                : ram >= 70
                    ? "Elevated"
                    : "Healthy";

            StoragePressureStatus = storage >= 90
                ? "Critical capacity"
                : storage >= 80
                    ? "Review storage"
                    : "Enough headroom";

            BackgroundLoadStatus = processes >= 220
                ? "Busy process table"
                : processes >= 170
                    ? "Moderate background load"
                    : "Normal";

            if (ram >= 85)
            {
                DiagnosisTitle = "Memory pressure detected";
                DiagnosisDetail = $"RAM is at {ram:0}%. Free standby cache or inspect memory-heavy apps before launching another workload.";
                DiagnosisSignal = "Triggered by RAM >= 85%";
                DiagnosisActionText = "Open Boost";
                DiagnosisRoute = "Boost";
            }
            else if (storage >= 88)
            {
                DiagnosisTitle = "Storage capacity is tight";
                DiagnosisDetail = $"Disk usage is at {storage:0}%. Map large files first, then clean selected targets.";
                DiagnosisSignal = "Triggered by storage >= 88%";
                DiagnosisActionText = "Open Disk";
                DiagnosisRoute = "Storage";
            }
            else if (processes >= 220)
            {
                DiagnosisTitle = "Background process load is high";
                DiagnosisDetail = $"{processes:N0} processes are running. Review the list before ending anything.";
                DiagnosisSignal = "Triggered by processes >= 220";
                DiagnosisActionText = "Review Processes";
                DiagnosisRoute = "Processes";
            }
            else if (cpu >= 80)
            {
                DiagnosisTitle = "CPU load spike";
                DiagnosisDetail = $"CPU is at {cpu:0}%. Check active processes if this stays high.";
                DiagnosisSignal = "Triggered by CPU >= 80%";
                DiagnosisActionText = "Review Processes";
                DiagnosisRoute = "Processes";
            }
            else
            {
                DiagnosisTitle = "No urgent bottleneck";
                DiagnosisDetail = "Live metrics are inside normal thresholds. Use Boost Tuning only when memory, latency, or game focus needs attention.";
                DiagnosisSignal = "All monitored thresholds are currently clear";
                DiagnosisActionText = "Open Boost";
                DiagnosisRoute = "Boost";
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
