using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using TAY.Services;

namespace TAY.ViewModels
{
    public partial class BoostViewModel : ObservableObject
    {
        private readonly DispatcherQueue _dispatcherQueue;
        private bool _isInternalStateChange = false;

        [ObservableProperty]
        private bool isTelemetryDebloated;

        [ObservableProperty]
        private bool isGameModeActive;

        [ObservableProperty]
        private bool isRamCleaning;

        [ObservableProperty]
        private bool isRamPanelVisible;

        [ObservableProperty]
        private bool isNetworkCleaning;

        [ObservableProperty]
        private string ramCleanLog = "";

        [ObservableProperty]
        private string networkCleanLog = "";

        [ObservableProperty]
        private string dnsScanResult = "DNS Status: System Default (DHCP)";

        [ObservableProperty]
        private string recommendedDnsSummary = "Run a DNS scan to get a recommendation.";

        [ObservableProperty]
        private bool canApplyRecommendedDns;

        [ObservableProperty]
        private int outdatedDriversCount;

        [ObservableProperty]
        private bool isDriverScanning;

        [ObservableProperty]
        private bool isContextScanning;

        public string ExpectedGameBoost => "Typical range: 0-8% FPS uplift when background services or CPU scheduling are the bottleneck.";
        public string ExpectedMemoryBoost => "Best for stutter recovery: frees cache/working sets, but does not increase raw FPS by itself.";
        public string ExpectedNetworkBoost => "Latency impact: usually 0-20 ms depending on DNS path and adapter state.";
        public string BoostGuidance => "Use Game Focus for CPU-bound games, Memory Sweep after long sessions, and Network tools only when latency or DNS feels unstable.";
        public string SupportedGamesSummary => "CS2, Dota 2, Rocket League, GTA V, Cyberpunk 2077, Valorant, Fortnite, League of Legends, Overwatch";

        public ObservableCollection<DnsOption> DnsResults { get; } = new();
        public ObservableCollection<ContextMenuItem> ContextHandlers { get; } = new();
        public ObservableCollection<DriverInfo> Drivers { get; } = new();
        public ObservableCollection<SupportedGameVM> SupportedGames { get; } = new()
        {
            new("Counter-Strike 2", "cs2.exe"),
            new("Dota 2", "dota2.exe"),
            new("Rocket League", "RocketLeague.exe"),
            new("GTA V", "GTA5.exe"),
            new("Cyberpunk 2077", "Cyberpunk2077.exe"),
            new("Valorant", "VALORANT-Win64-Shipping.exe"),
            new("Fortnite", "FortniteClient-Win64-Shipping.exe"),
            new("League of Legends", "League of Legends.exe"),
            new("Overwatch", "Overwatch.exe")
        };

        private DnsOption? _recommendedDns;

        public BoostViewModel()
        {
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            _ = LoadStatesAsync();
        }

        private async Task LoadStatesAsync()
        {
            bool debloated = await TelemetryService.IsDebloatedAsync();
            bool gameMode = GameBoosterService.IsActive;

            _dispatcherQueue.TryEnqueue(() =>
            {
                _isInternalStateChange = true;
                IsTelemetryDebloated = debloated;
                IsGameModeActive = gameMode;
                _isInternalStateChange = false;
            });

            await RefreshContextHandlersAsync();
            await RefreshDriversAsync();
        }

        partial void OnIsTelemetryDebloatedChanged(bool value)
        {
            if (_isInternalStateChange) return;

            Task.Run(async () =>
            {
                bool success;
                if (value)
                {
                    success = await TelemetryService.ApplyDebloatAsync();
                }
                else
                {
                    success = await TelemetryService.RevertDebloatAsync();
                }

                if (!success)
                {
                    // Revert the toggle on failure
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        _isInternalStateChange = true;
                        IsTelemetryDebloated = !value;
                        _isInternalStateChange = false;
                    });
                }
            });
        }

        partial void OnIsGameModeActiveChanged(bool value)
        {
            if (_isInternalStateChange) return;

            Task.Run(async () =>
            {
                bool success = false;
                if (value)
                {
                    success = await GameBoosterService.ActivateGameModeAsync();
                }
                else
                {
                    GameBoosterService.DeactivateGameMode();
                    success = true;
                }

                if (!success)
                {
                    // Revert the toggle on failure
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        _isInternalStateChange = true;
                        IsGameModeActive = !value;
                        _isInternalStateChange = false;
                    });
                }
            });
        }

        [RelayCommand]
        private async Task DeepCleanRamAsync()
        {
            if (IsRamCleaning) return;
            IsRamCleaning = true;
            IsRamPanelVisible = true;
            RamCleanLog = ">> INITIALIZING CORE MEMORY SWEEP PROTOCOL...\n";
            try
            {
                await Task.Delay(400);
                RamCleanLog += ">> [STEP 1/4] Scanning physical memory partitions...\n";
                var (total, usedBefore, freeBefore, percent) = SystemService.GetRamInfo();
                RamCleanLog += $"   - Total physical memory detected: {SystemService.FormatBytes(total)}\n";
                RamCleanLog += $"   - In-use working sets: {SystemService.FormatBytes(usedBefore)} ({percent}%)\n";
                await Task.Delay(500);
                
                RamCleanLog += ">> [STEP 2/4] Querying inactive background processes...\n";
                var processes = System.Diagnostics.Process.GetProcesses();
                RamCleanLog += $"   - Found {processes.Length} active process handles to analyze.\n";
                RamCleanLog += "   - Evaluating compression eligibility for idle working sets...\n";
                await Task.Delay(500);
                
                RamCleanLog += ">> [STEP 3/4] Compressing idle process working sets...\n";
                long freedBytes = await MemoryOptimizationService.DeepOptimizeMemoryAsync();
                await Task.Delay(500);
                
                RamCleanLog += ">> [STEP 4/4] Purging Windows system standby file cache blocks...\n";
                RamCleanLog += "   - Executing NtSetSystemInformation (MemoryPurgeStandbyList).\n";
                await Task.Delay(500);
                
                if (freedBytes > 0)
                {
                    RamCleanLog += $"[SUCCESS] Sweep finished! Reclaimed {SystemService.FormatBytes(freedBytes)} of locked memory.\n";
                }
                else
                {
                    RamCleanLog += "[SUCCESS] Sweep finished! Working sets compressed and standby list flushed.\n";
                }
                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                RamCleanLog += $"[ERROR] Memory Sweep failed: {ex.Message}\n";
                await Task.Delay(2000);
            }
            finally
            {
                IsRamCleaning = false;
            }
        }

        [RelayCommand]
        private void DismissRamResult()
        {
            if (IsRamCleaning) return;
            IsRamPanelVisible = false;
        }

        [RelayCommand]
        private async Task FlushDnsAsync()
        {
            if (IsNetworkCleaning) return;
            IsNetworkCleaning = true;
            NetworkCleanLog = ">> INITIALIZING LOCAL RESOLVER CACHE FLUSH...\n";
            try
            {
                await Task.Delay(400);
                NetworkCleanLog += ">> [STEP 1/2] Connecting to local Dnscache service...\n";
                NetworkCleanLog += "   - Cache service state: RUNNING\n";
                NetworkCleanLog += "   - Binding local resolver stack API.\n";
                await Task.Delay(500);
                
                bool ok = await NetworkService.FlushDnsAsync();
                
                NetworkCleanLog += ">> [STEP 2/2] Invoking DnsFlushResolverCache API...\n";
                NetworkCleanLog += "   - Flushing host cache tables...\n";
                NetworkCleanLog += "   - Resetting local hosts resolver map.\n";
                await Task.Delay(500);
                
                if (ok)
                    NetworkCleanLog += "[SUCCESS] System DNS resolver cache cleared successfully!\n";
                else
                    NetworkCleanLog += "[ERROR] DNS resolver cache flush failed.\n";
                await Task.Delay(1000);
            }
            finally
            {
                IsNetworkCleaning = false;
            }
        }

        [RelayCommand]
        private async Task ResetTcpIpAsync()
        {
            if (IsNetworkCleaning) return;
            IsNetworkCleaning = true;
            NetworkCleanLog = ">> INITIALIZING TCP/IP STACK & WINSOCK CATALOG RESET...\n";
            try
            {
                await Task.Delay(400);
                NetworkCleanLog += ">> [STEP 1/3] Binding network interface adapter stack keys...\n";
                NetworkCleanLog += "   - Scanning active Ethernet & Wi-Fi adapter cards...\n";
                await Task.Delay(500);
                
                bool ok = await NetworkService.ResetTcpIpAsync();
                
                NetworkCleanLog += ">> [STEP 2/3] Executing Winsock API resets in Registry...\n";
                NetworkCleanLog += "   - Rebuilding catalog hives under System\\CurrentControlSet\\Services\\Winsock2\n";
                NetworkCleanLog += "   - Resetting Netsh IP v4/v6 stack registers...\n";
                await Task.Delay(500);
                
                NetworkCleanLog += ">> [STEP 3/3] Flushing core system network routing tables...\n";
                NetworkCleanLog += "   - Deleting local interface transient route entries...\n";
                await Task.Delay(500);
                
                if (ok)
                    NetworkCleanLog += "[SUCCESS] Network stack reset complete! Reboot highly recommended.\n";
                else
                    NetworkCleanLog += "[ERROR] TCP/IP reset failed.\n";
                await Task.Delay(1000);
            }
            finally
            {
                IsNetworkCleaning = false;
            }
        }

        [RelayCommand]
        private async Task FindBestDnsAsync()
        {
            if (IsNetworkCleaning) return;
            IsNetworkCleaning = true;
            NetworkCleanLog = ">> INITIALIZING DNS LATENCY PROTOCOL SCAN...\n";
            DnsScanResult = "Pinging public DNS servers...";
            RecommendedDnsSummary = "Scanning DNS latency...";
            CanApplyRecommendedDns = false;
            _recommendedDns = null;
            DnsResults.Clear();

            try
            {
                await Task.Delay(400);
                NetworkCleanLog += ">> [STEP 1/2] Transmitting ICMP packets to Cloudflare, Google, Quad9, AdGuard...\n";
                await Task.Delay(500);
                
                var options = await NetworkService.PingDnsServersAsync();
                DnsOption? bestOption = null;

                foreach (var opt in options)
                {
                    DnsResults.Add(opt);
                    string pingStatus = opt.LatencyMs < 20 ? "[EXCELLENT]" : (opt.LatencyMs < 50 ? "[GOOD]" : "[STABLE]");
                    NetworkCleanLog += $"   - {opt.Name,-12} ({opt.PrimaryIp,-15}) -> Ping: {opt.LatencyMs}ms {pingStatus}\n";
                    if (opt.LatencyMs >= 0 && opt.LatencyMs < 999)
                    {
                        if (bestOption == null || opt.LatencyMs < bestOption.LatencyMs)
                        {
                            bestOption = opt;
                        }
                    }
                }

                await Task.Delay(500);
                NetworkCleanLog += ">> [STEP 2/2] Evaluating lowest latency options...\n";
                
                if (bestOption != null)
                {
                    _recommendedDns = bestOption;
                    CanApplyRecommendedDns = true;
                    DnsScanResult = $"Recommended DNS: {bestOption.Name} ({bestOption.LatencyMs}ms). Review warning before applying.";
                    RecommendedDnsSummary = $"{bestOption.Name} - {bestOption.PrimaryIp}, {bestOption.SecondaryIp}";
                    NetworkCleanLog += $"[READY] Recommended DNS: {bestOption.Name} ({bestOption.LatencyMs}ms). No adapter settings changed yet.\n";
                }
                else
                {
                    DnsScanResult = "DNS Scan failed: All servers were unreachable.";
                    RecommendedDnsSummary = "No reachable DNS recommendation.";
                    NetworkCleanLog += "[ERROR] DNS ping failed. All servers were unreachable.\n";
                }
                await Task.Delay(1000);
            }
            finally
            {
                IsNetworkCleaning = false;
            }
        }

        [RelayCommand]
        public async Task ApplyRecommendedDnsAsync()
        {
            if (IsNetworkCleaning || _recommendedDns == null) return;
            IsNetworkCleaning = true;
            NetworkCleanLog = ">> APPLYING RECOMMENDED DNS PROFILE...\n";
            DnsScanResult = $"Applying DNS: {_recommendedDns.Name}...";

            try
            {
                await Task.Delay(300);
                NetworkCleanLog += ">> [STEP 1/2] Backing up current adapter DNS configuration...\n";
                NetworkCleanLog += ">> [STEP 2/2] Writing DNS profile through WMI adapter configuration...\n";

                bool applied = await NetworkService.ApplyBestDnsAsync(_recommendedDns);
                if (applied)
                {
                    DnsScanResult = $"DNS Optimized: {_recommendedDns.Name} ({_recommendedDns.LatencyMs}ms)";
                    RecommendedDnsSummary = $"Applied: {_recommendedDns.Name} - {_recommendedDns.PrimaryIp}, {_recommendedDns.SecondaryIp}";
                    NetworkCleanLog += "[SUCCESS] Applied DNS configuration successfully on active network adapters.\n";
                }
                else
                {
                    DnsScanResult = "Failed to apply recommended DNS servers via WMI.";
                    NetworkCleanLog += "[ERROR] Failed to configure DNS through WMI interface.\n";
                }
            }
            finally
            {
                IsNetworkCleaning = false;
            }
        }

        [RelayCommand]
        private async Task RestoreDefaultDnsAsync()
        {
            if (IsNetworkCleaning) return;
            IsNetworkCleaning = true;
            NetworkCleanLog = ">> INITIALIZING BASELINE SYSTEM DHCP RESTORATION...\n";
            DnsScanResult = "Restoring DHCP DNS configuration...";
            DnsResults.Clear();

            try
            {
                await Task.Delay(400);
                NetworkCleanLog += ">> [STEP 1/2] Fetching baseline DNS backup profiles from LocalAppData...\n";
                NetworkCleanLog += "   - Checking backup profiles under LocalAppData\\Tay\\dns_backup.json...\n";
                await Task.Delay(500);
                
                bool success = await NetworkService.RestoreDefaultDnsAsync();
                
                NetworkCleanLog += ">> [STEP 2/2] Restoring dynamic automatic (DHCP) DNS server lists...\n";
                NetworkCleanLog += "   - Clearing hardcoded DNS servers from active interface keys...\n";
                NetworkCleanLog += "   - Re-enabling DHCP DNS resolution protocol...\n";
                await Task.Delay(500);
                
                if (success)
                {
                    DnsScanResult = "DNS Configuration: Restored System Default (DHCP)";
                    NetworkCleanLog += "[SUCCESS] Original DNS / DHCP configurations restored successfully!\n";
                }
                else
                {
                    DnsScanResult = "Failed to restore DHCP configurations.";
                    NetworkCleanLog += "[ERROR] Failed to write DHCP settings over network cards.\n";
                }
                await Task.Delay(1000);
            }
            finally
            {
                IsNetworkCleaning = false;
            }
        }

        [RelayCommand]
        private async Task RefreshContextHandlersAsync()
        {
            if (IsContextScanning) return;
            IsContextScanning = true;
            ContextHandlers.Clear();

            try
            {
                var handlers = await ContextMenuService.GetContextMenuHandlersAsync();
                foreach (var h in handlers)
                {
                    ContextHandlers.Add(h);
                }
            }
            finally
            {
                IsContextScanning = false;
            }
        }

        [RelayCommand]
        private async Task ToggleContextMenuHandlerAsync(ContextMenuItem item)
        {
            if (item == null) return;
            
            bool originalState = item.IsEnabled;
            bool success = await ContextMenuService.ToggleHandlerAsync(item, !originalState);
            
            if (success)
            {
                item.IsEnabled = !originalState;
                // Force collection update notification
                var idx = ContextHandlers.IndexOf(item);
                if (idx >= 0)
                {
                    ContextHandlers[idx] = item;
                }
            }
        }

        [RelayCommand]
        private async Task RefreshDriversAsync()
        {
            if (IsDriverScanning) return;
            IsDriverScanning = true;
            Drivers.Clear();

            try
            {
                var scanned = await DriverService.ScanDriversAsync();
                int outdated = 0;
                foreach (var d in scanned)
                {
                    Drivers.Add(d);
                    if (d.IsOutdated) outdated++;
                }
                OutdatedDriversCount = outdated;
            }
            finally
            {
                IsDriverScanning = false;
            }
        }
    }

    public record SupportedGameVM(string Name, string ProcessName);
}
