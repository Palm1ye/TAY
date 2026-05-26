using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using TAY.Services;

namespace TAY.ViewModels
{
    public partial class NetworkViewModel : ObservableObject
    {
        private readonly Microsoft.UI.Xaml.DispatcherTimer _timer = new();
        private NetworkInterface? _adapter;
        private long _lastBytesReceived;
        private long _lastBytesSent;
        private DateTime _lastSample = DateTime.UtcNow;
        private bool _isInternalAdapterChange;
        private DateTime _lastInternetCheck = DateTime.MinValue;
        private bool _hasInternet = true;
        private string _adapterDetailBase = "";

        [ObservableProperty] private string adapterName = "No active adapter";
        [ObservableProperty] private string adapterDetails = "Offline";
        [ObservableProperty] private string adapterStatus = "Offline";
        [ObservableProperty] private string downloadRate = "0 KB/s";
        [ObservableProperty] private string uploadRate = "0 KB/s";
        [ObservableProperty] private double downloadValue;
        [ObservableProperty] private double uploadValue;
        [ObservableProperty] private string latencyLabel = "Speed test pending";
        [ObservableProperty] private string speedTestDownload = "-- Mbps";
        [ObservableProperty] private string speedTestUpload = "-- Mbps";
        [ObservableProperty] private string speedTestLatency = "-- ms";
        [ObservableProperty] private string speedTestProvider = "Cloudflare quick test";
        [ObservableProperty] private bool isTesting;
        [ObservableProperty] private NetworkAdapterVM? selectedAdapter;

        public ObservableCollection<NetworkAdapterVM> Adapters { get; } = new();
        public ObservableCollection<NetworkConsumerVM> Consumers { get; } = new();

        public NetworkViewModel()
        {
            RefreshAdapters();
            RefreshConsumers();

            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += (_, _) => UpdateRates();
            _timer.Start();
        }

        partial void OnSelectedAdapterChanged(NetworkAdapterVM? value)
        {
            if (_isInternalAdapterChange) return;
            SelectAdapter(value?.Id);
        }

        [RelayCommand]
        private async Task RunSpeedTestAsync()
        {
            if (IsTesting) return;
            IsTesting = true;
            LatencyLabel = "Running in-app speed test...";
            SpeedTestDownload = "Testing...";
            SpeedTestUpload = "Testing...";
            SpeedTestLatency = "Testing...";

            try
            {
                var result = await NetworkService.RunQuickSpeedTestAsync();
                SpeedTestDownload = $"{result.DownloadMbps:0.0} Mbps";
                SpeedTestUpload = $"{result.UploadMbps:0.0} Mbps";
                SpeedTestLatency = $"{result.LatencyMs} ms";
                SpeedTestProvider = $"{result.Provider} quick test";
                LatencyLabel = $"Speed test complete - {SpeedTestDownload} down / {SpeedTestUpload} up";
            }
            catch (Exception ex)
            {
                SpeedTestDownload = "-- Mbps";
                SpeedTestUpload = "-- Mbps";
                SpeedTestLatency = "-- ms";
                LatencyLabel = "Speed test failed";
                RealTimeLogService.Instance.Log($"[ERROR] Speed test failed: {ex.Message}");
            }
            finally
            {
                IsTesting = false;
            }
        }

        [RelayCommand]
        private void Refresh()
        {
            RefreshAdapters();
            RefreshConsumers();
            UpdateRates();
        }

        private void RefreshAdapters()
        {
            var previousId = SelectedAdapter?.Id ?? _adapter?.Id;
            var adapters = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Select(CreateAdapterVM)
                .OrderByDescending(a => a.Score)
                .ThenBy(a => a.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _isInternalAdapterChange = true;
            Adapters.Clear();
            foreach (var adapter in adapters)
            {
                Adapters.Add(adapter);
            }

            SelectedAdapter = adapters.FirstOrDefault(a => a.Id == previousId)
                              ?? adapters.FirstOrDefault(a => a.IsPreferred)
                              ?? adapters.FirstOrDefault();
            _isInternalAdapterChange = false;

            SelectAdapter(SelectedAdapter?.Id);
        }

        private void SelectAdapter(string? adapterId)
        {
            try
            {
                _adapter = string.IsNullOrWhiteSpace(adapterId)
                    ? null
                    : NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n => n.Id == adapterId);

                if (_adapter == null)
                {
                    AdapterName = "No active adapter";
                    AdapterDetails = "Offline";
                    AdapterStatus = "Offline";
                    DownloadRate = "0 KB/s";
                    UploadRate = "0 KB/s";
                    DownloadValue = 0;
                    UploadValue = 0;
                    return;
                }

                var props = _adapter.GetIPProperties();
                var ip = props.UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                    ?.Address
                    .ToString() ?? "No IPv4";
                var mac = string.Join(":", _adapter.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("X2")));
                var link = FormatLinkSpeed(_adapter.Speed);
                var isUp = _adapter.OperationalStatus == OperationalStatus.Up;

                AdapterName = _adapter.Name;
                _adapterDetailBase = $"{ip} - MAC {mac} - {link}";
                _hasInternet = isUp && CheckInternet();
                _lastInternetCheck = DateTime.UtcNow;
                AdapterDetails = _hasInternet ? _adapterDetailBase : $"{_adapterDetailBase} - down";
                AdapterStatus = isUp ? (_hasInternet ? "Online" : "Down") : _adapter.OperationalStatus.ToString();

                var stats = _adapter.GetIPv4Statistics();
                _lastBytesReceived = stats.BytesReceived;
                _lastBytesSent = stats.BytesSent;
                _lastSample = DateTime.UtcNow;
                UpdateRates();
            }
            catch
            {
                AdapterName = "Network unavailable";
                AdapterDetails = "Adapter query failed";
                AdapterStatus = "Offline";
            }
        }

        private void UpdateRates()
        {
            if (_adapter == null)
            {
                SelectAdapter(SelectedAdapter?.Id);
                return;
            }

            try
            {
                var stats = _adapter.GetIPv4Statistics();
                var now = DateTime.UtcNow;
                var seconds = Math.Max(0.2, (now - _lastSample).TotalSeconds);

                var down = Math.Max(0, (stats.BytesReceived - _lastBytesReceived) / seconds);
                var up = Math.Max(0, (stats.BytesSent - _lastBytesSent) / seconds);

                _lastBytesReceived = stats.BytesReceived;
                _lastBytesSent = stats.BytesSent;
                _lastSample = now;

                DownloadRate = FormatRate(down);
                UploadRate = FormatRate(up);
                DownloadValue = Math.Clamp(down / (1024.0 * 1024.0 * 2.0) * 100.0, 0, 100);
                UploadValue = Math.Clamp(up / (1024.0 * 1024.0) * 100.0, 0, 100);

                if (_adapter.OperationalStatus == OperationalStatus.Up && (now - _lastInternetCheck).TotalSeconds >= 10)
                {
                    _hasInternet = CheckInternet();
                    _lastInternetCheck = now;
                    AdapterDetails = _hasInternet ? _adapterDetailBase : $"{_adapterDetailBase} - down";
                    AdapterStatus = _hasInternet ? "Online" : "Down";
                }
            }
            catch
            {
                RefreshAdapters();
            }
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

        private void RefreshConsumers()
        {
            Consumers.Clear();
            try
            {
                var names = new[] { "steam", "chrome", "msedge", "discord", "onedrive", "spotify", "epicgameslauncher" };
                var processes = Process.GetProcesses()
                    .Where(p =>
                    {
                        try { return names.Any(n => p.ProcessName.Contains(n, StringComparison.OrdinalIgnoreCase)); }
                        catch { return false; }
                    })
                    .GroupBy(p => p.ProcessName, StringComparer.OrdinalIgnoreCase)
                    .Select(g => new NetworkConsumerVM(g.Key, "active", $"{g.Count()} proc"))
                    .Take(6)
                    .ToList();

                if (processes.Count == 0)
                {
                    Consumers.Add(new NetworkConsumerVM("No common network apps", "idle", "-"));
                    return;
                }

                foreach (var item in processes)
                {
                    Consumers.Add(item);
                }
            }
            catch
            {
                Consumers.Add(new NetworkConsumerVM("Process scan unavailable", "idle", "-"));
            }
        }

        private static NetworkAdapterVM CreateAdapterVM(NetworkInterface adapter)
        {
            var hasGateway = false;
            var hasIpv4 = false;
            try
            {
                var props = adapter.GetIPProperties();
                hasGateway = props.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork);
                hasIpv4 = props.UnicastAddresses.Any(a => a.Address.AddressFamily == AddressFamily.InterNetwork);
            }
            catch { }

            var isUp = adapter.OperationalStatus == OperationalStatus.Up;
            var score = 0;
            if (isUp) score += 1000;
            if (hasGateway) score += 700;
            if (hasIpv4) score += 300;
            if (adapter.NetworkInterfaceType is NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.Ethernet) score += 500;
            if (adapter.NetworkInterfaceType is NetworkInterfaceType.Tunnel or NetworkInterfaceType.Ppp or NetworkInterfaceType.Unknown) score -= 550;
            if (IsLikelyVirtual(adapter)) score -= 450;

            return new NetworkAdapterVM(
                adapter.Id,
                adapter.Name,
                $"{adapter.NetworkInterfaceType} - {FormatLinkSpeed(adapter.Speed)}",
                isUp ? "Online" : adapter.OperationalStatus.ToString(),
                score,
                isUp && hasGateway && (adapter.NetworkInterfaceType is NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.Ethernet));
        }

        private static bool IsLikelyVirtual(NetworkInterface adapter)
        {
            var text = $"{adapter.Name} {adapter.Description}".ToLowerInvariant();
            return text.Contains("tailscale") ||
                   text.Contains("wireguard") ||
                   text.Contains("vpn") ||
                   text.Contains("virtual") ||
                   text.Contains("hyper-v") ||
                   text.Contains("vmware") ||
                   text.Contains("bluetooth") ||
                   text.Contains("loopback");
        }

        private static string FormatLinkSpeed(long bitsPerSecond)
        {
            if (bitsPerSecond <= 0) return "Unknown speed";
            if (bitsPerSecond >= 1_000_000_000) return $"{bitsPerSecond / 1_000_000_000.0:0.#} Gbps";
            if (bitsPerSecond >= 1_000_000) return $"{bitsPerSecond / 1_000_000.0:0.#} Mbps";
            return $"{bitsPerSecond / 1_000.0:0.#} Kbps";
        }

        private static string FormatRate(double bytesPerSecond)
        {
            if (bytesPerSecond >= 1024 * 1024)
            {
                return $"{bytesPerSecond / (1024 * 1024):0.0} MB/s";
            }

            return $"{bytesPerSecond / 1024:0.0} KB/s";
        }

        public void Cleanup()
        {
            _timer.Stop();
        }
    }

    public record NetworkAdapterVM(
        string Id,
        string DisplayName,
        string Details,
        string Status,
        int Score,
        bool IsPreferred);

    public record NetworkConsumerVM(string Name, string Download, string Upload);
}
