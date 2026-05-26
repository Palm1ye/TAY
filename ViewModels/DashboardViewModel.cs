using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Timers;
using TAY.Services;

namespace TAY.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly System.Timers.Timer _timer;
        private readonly DispatcherQueue _dispatcherQueue;
        private readonly List<double> _cpuHistory = new();
        private readonly List<double> _ramHistory = new();
        private readonly List<double> _gpuHistory = new();
        private readonly List<double> _networkHistory = new();
        private NetworkInterface? _networkAdapter;
        private long _lastNetworkBytesReceived;
        private long _lastNetworkBytesSent;
        private DateTime _lastNetworkSample = DateTime.UtcNow;
    private DateTime _lastInternetCheck = DateTime.MinValue;
    private bool _hasInternet = true;

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
        private string networkUsage = "0 KB/s";

        [ObservableProperty]
        private double networkValue = 0;

        [ObservableProperty]
        private string networkStatus = "No active link";

        [ObservableProperty]
        private string cpuDetail = "Temp unavailable";

        [ObservableProperty]
        private string ramDetail = "Memory pending";

        [ObservableProperty]
        private string gpuDetail = "Temp unavailable";

        [ObservableProperty]
        private string networkDetail = "Adapter pending";

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
    private string diagnosisIconGlyph = "\uE80F";

    [ObservableProperty]
    private string memoryPressureStatus = "Pending";

        [ObservableProperty]
        private string storagePressureStatus = "Pending";

        [ObservableProperty]
        private string backgroundLoadStatus = "Pending";

        [ObservableProperty]
        private IReadOnlyList<double> cpuSparklineValues = Array.Empty<double>();

        [ObservableProperty]
        private IReadOnlyList<double> ramSparklineValues = Array.Empty<double>();

        [ObservableProperty]
        private IReadOnlyList<double> gpuSparklineValues = Array.Empty<double>();

        [ObservableProperty]
        private IReadOnlyList<double> networkSparklineValues = Array.Empty<double>();

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
                _networkHistory.Add(0);
            }

            CpuSparklineValues = _cpuHistory.ToArray();
            RamSparklineValues = _ramHistory.ToArray();
            GpuSparklineValues = _gpuHistory.ToArray();
            NetworkSparklineValues = _networkHistory.ToArray();

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
                var networkTask = Task.Run(GetNetworkSnapshot);
                var cpuTempTask = Task.Run(SystemService.GetCpuTemperatureC);
                var gpuTempTask = Task.Run(SystemService.GetGpuTemperatureC);

                await Task.WhenAll(cpuTask, ramTask, gpuTask, storageTask, processCountTask, networkTask, cpuTempTask, gpuTempTask);

                var cpu = cpuTask.Result;
                var ram = ramTask.Result;
                var gpu = gpuTask.Result;
                var storage = storageTask.Result;
                var processes = processCountTask.Result;
                var network = networkTask.Result;
                var cpuTemp = cpuTempTask.Result;
                var gpuTemp = gpuTempTask.Result;
                var utime = SystemService.GetUptime();

                _dispatcherQueue.TryEnqueue(() =>
                {
                    CpuValue = cpu;
                    CpuUsage = $"{cpu}%";
                    
                    RamValue = ram.percent;
                    RamUsage = $"{ram.percent}%";
                    RamDetail = $"{SystemService.FormatBytes(ram.used)} / {SystemService.FormatBytes(ram.total)}";

                    GpuValue = gpu;
                    GpuUsage = $"{gpu}%";

                    DiskValue = storage.percent;
                    DiskUsage = $"{storage.percent}%";
                    DiskFree = $"{SystemService.FormatBytes(storage.free)} free";

                    NetworkUsage = network.value;
                    NetworkValue = network.percent;
                    NetworkStatus = network.status;
                    NetworkDetail = network.detail;

                    CpuDetail = FormatTemperature(cpuTemp);
                    GpuDetail = FormatTemperature(gpuTemp);

                    ProcessCount = processes.ToString("N0");
                    
                    Uptime = SystemService.FormatUptime(utime);

                    PushTelemetryValue(_cpuHistory, cpu);
                    PushTelemetryValue(_ramHistory, ram.percent);
                    PushTelemetryValue(_gpuHistory, gpu);
                    PushTelemetryValue(_networkHistory, network.percent);

                    CpuSparklineValues = _cpuHistory.ToArray();
                    RamSparklineValues = _ramHistory.ToArray();
                    GpuSparklineValues = _gpuHistory.ToArray();
                    NetworkSparklineValues = _networkHistory.ToArray();

                    UpdateDashboardSignals(cpu, ram.percent, storage.percent, processes);
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
                DiagnosisIconGlyph = "\uE8B7";
            }
            else if (storage >= 88)
            {
                DiagnosisTitle = "Storage capacity is tight";
                DiagnosisDetail = $"Disk usage is at {storage:0}%. Map large files first, then clean selected targets.";
                DiagnosisSignal = "Triggered by storage >= 88%";
                DiagnosisActionText = "Open Disk";
                DiagnosisRoute = "Storage";
                DiagnosisIconGlyph = "\uEDA2";
            }
            else if (processes >= 220)
            {
                DiagnosisTitle = "Background process load is high";
                DiagnosisDetail = $"{processes:N0} processes are running. Review the list before ending anything.";
                DiagnosisSignal = "Triggered by processes >= 220";
                DiagnosisActionText = "Review Processes";
                DiagnosisRoute = "Processes";
                DiagnosisIconGlyph = "\uE9D2";
            }
            else if (cpu >= 80)
            {
                DiagnosisTitle = "CPU load spike";
                DiagnosisDetail = $"CPU is at {cpu:0}%. Check active processes if this stays high.";
                DiagnosisSignal = "Triggered by CPU >= 80%";
                DiagnosisActionText = "Review Processes";
                DiagnosisRoute = "Processes";
                DiagnosisIconGlyph = "\uE950";
            }
            else
            {
                DiagnosisTitle = "No urgent bottleneck";
                DiagnosisDetail = "Live metrics are inside normal thresholds. Use Boost Tuning only when memory, latency, or game focus needs attention.";
                DiagnosisSignal = "All monitored thresholds are currently clear";
                DiagnosisActionText = "Open Boost";
                DiagnosisRoute = "Boost";
                DiagnosisIconGlyph = "\uE80F";
            }
        }

        private static void PushTelemetryValue(List<double> values, double value)
        {
            if (values.Count > 0 && value > 0 && values.All(v => Math.Abs(v) < 0.001))
            {
                for (var i = 0; i < values.Count; i++)
                {
                    values[i] = value;
                }

                return;
            }

            values.Add(value);
            if (values.Count > 20) values.RemoveAt(0);
        }

        private (string value, double percent, string status, string detail) GetNetworkSnapshot()
        {
            try
            {
                if (_networkAdapter == null || _networkAdapter.OperationalStatus != OperationalStatus.Up)
                {
                    _networkAdapter = PickPreferredNetworkAdapter();
                    if (_networkAdapter == null)
                    {
                        _hasInternet = false;
                        return ("0 KB/s", 0, "Offline", "No active adapter");
                    }

                    var initialStats = _networkAdapter.GetIPv4Statistics();
                    _lastNetworkBytesReceived = initialStats.BytesReceived;
                    _lastNetworkBytesSent = initialStats.BytesSent;
                    _lastNetworkSample = DateTime.UtcNow;
                    _hasInternet = CheckInternet();
                    _lastInternetCheck = _lastNetworkSample;
                    var initialDetail = _hasInternet ? _networkAdapter.Name : $"{_networkAdapter.Name} - down";
                    return ("0 KB/s", 0, $"{_networkAdapter.NetworkInterfaceType} - active", initialDetail);
                }

                var stats = _networkAdapter.GetIPv4Statistics();
                var now = DateTime.UtcNow;
                var seconds = Math.Max(0.2, (now - _lastNetworkSample).TotalSeconds);
                var down = Math.Max(0, (stats.BytesReceived - _lastNetworkBytesReceived) / seconds);
                var up = Math.Max(0, (stats.BytesSent - _lastNetworkBytesSent) / seconds);
                var total = down + up;

                _lastNetworkBytesReceived = stats.BytesReceived;
                _lastNetworkBytesSent = stats.BytesSent;
                _lastNetworkSample = now;

                if ((now - _lastInternetCheck).TotalSeconds >= 10)
                {
                    _hasInternet = CheckInternet();
                    _lastInternetCheck = now;
                }

                var detail = _hasInternet ? _networkAdapter.Name : $"{_networkAdapter.Name} - down";
                return (FormatRate(total), Math.Clamp(total / (1024.0 * 1024.0 * 2.0) * 100.0, 0, 100), $"{_networkAdapter.NetworkInterfaceType} - active", detail);
            }
            catch
            {
                _networkAdapter = null;
                _hasInternet = false;
                return ("0 KB/s", 0, "Unavailable", "Adapter unavailable");
            }
        }

        private static string FormatTemperature(double? temperature)
        {
            return temperature.HasValue ? $"{temperature.Value:0} C" : "Temp unavailable";
        }

        private static bool CheckInternet()
        {
            try
            {
                if (!NetworkInterface.GetIsNetworkAvailable()) return false;
                using var ping = new Ping();
                var reply = ping.Send("1.1.1.1", 400);
                return reply.Status == IPStatus.Success;
            }
            catch
            {
                return false;
            }
        }

        private static NetworkInterface? PickPreferredNetworkAdapter()
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                            n.OperationalStatus == OperationalStatus.Up)
                .OrderByDescending(ScoreNetworkAdapter)
                .FirstOrDefault();
        }

        private static int ScoreNetworkAdapter(NetworkInterface adapter)
        {
            var score = 0;
            try
            {
                var props = adapter.GetIPProperties();
                if (props.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork)) score += 700;
                if (props.UnicastAddresses.Any(a => a.Address.AddressFamily == AddressFamily.InterNetwork)) score += 250;
            }
            catch { }

            if (adapter.NetworkInterfaceType is NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.Ethernet) score += 500;
            if (adapter.NetworkInterfaceType is NetworkInterfaceType.Tunnel or NetworkInterfaceType.Ppp or NetworkInterfaceType.Unknown) score -= 550;

            var text = $"{adapter.Name} {adapter.Description}".ToLowerInvariant();
            if (text.Contains("tailscale") ||
                text.Contains("wireguard") ||
                text.Contains("vpn") ||
                text.Contains("virtual") ||
                text.Contains("hyper-v") ||
                text.Contains("vmware") ||
                text.Contains("bluetooth"))
            {
                score -= 450;
            }

            return score;
        }

        private static string FormatRate(double bytesPerSecond)
        {
            if (bytesPerSecond >= 1024 * 1024)
            {
                return $"{bytesPerSecond / (1024 * 1024):0.0} MB/s";
            }

            return $"{bytesPerSecond / 1024:0.0} KB/s";
        }

        public Task RefreshAsync()
        {
            return UpdateTelemetryAsync();
        }

        public void Cleanup()
        {
            _timer?.Stop();
            _timer?.Dispose();
        }
    }
}
