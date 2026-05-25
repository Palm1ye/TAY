using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Threading.Tasks;

namespace TAY.Services
{
    public class DnsOption
    {
        public string Name { get; set; } = "";
        public string PrimaryIp { get; set; } = "";
        public string SecondaryIp { get; set; } = "";
        public long LatencyMs { get; set; } = -1;
    }

    public class AdapterBackup
    {
        public string SettingId { get; set; } = "";
        public string Description { get; set; } = "";
        public string[]? OriginalDns { get; set; }
        public bool DHCPEnabled { get; set; } = true;
    }

    public class NetworkService
    {
        private static readonly string BackupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TAY");
        private static readonly string BackupPath = Path.Combine(BackupDir, "dns_backup.json");

        public static async Task<bool> FlushDnsAsync()
        {
            return await Task.Run(() =>
            {
                RealTimeLogService.Instance.Log("Executing DNS cache flush (ipconfig /flushdns)...");
                try
                {
                    var psi = new ProcessStartInfo("ipconfig", "/flushdns")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true
                    };
                    using var proc = Process.Start(psi);
                    proc?.WaitForExit(5000);
                    RealTimeLogService.Instance.Log("[SUCCESS] System DNS resolver cache flushed successfully.");
                    return true;
                }
                catch (Exception ex)
                {
                    RealTimeLogService.Instance.Log($"[ERROR] DNS flush failed: {ex.Message}");
                    return false;
                }
            });
        }

        public static async Task<bool> ResetTcpIpAsync()
        {
            return await Task.Run(() =>
            {
                RealTimeLogService.Instance.Log("Executing TCP/IP stack reset...");
                try
                {
                    // 1. Reset TCP/IP
                    var psi = new ProcessStartInfo("netsh", "int ip reset")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true
                    };
                    using var proc = Process.Start(psi);
                    proc?.WaitForExit(5000);

                    // 2. Reset Winsock
                    var psiWinsock = new ProcessStartInfo("netsh", "winsock reset")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true
                    };
                    using var procWinsock = Process.Start(psiWinsock);
                    procWinsock?.WaitForExit(5000);

                    RealTimeLogService.Instance.Log("[SUCCESS] TCP/IP protocol stack and Winsock catalog reset completed. A reboot is highly recommended.");
                    return true;
                }
                catch (Exception ex)
                {
                    RealTimeLogService.Instance.Log($"[ERROR] TCP/IP reset failed: {ex.Message}");
                    return false;
                }
            });
        }

        public static async Task<List<DnsOption>> PingDnsServersAsync()
        {
            return await Task.Run(() =>
            {
                RealTimeLogService.Instance.Log("Pinging public DNS servers to determine lowest latency...");
                var options = new List<DnsOption>
                {
                    new DnsOption { Name = "Cloudflare DNS", PrimaryIp = "1.1.1.1", SecondaryIp = "1.0.0.1" },
                    new DnsOption { Name = "Google Public DNS", PrimaryIp = "8.8.8.8", SecondaryIp = "8.8.4.4" },
                    new DnsOption { Name = "Quad9 Secure DNS", PrimaryIp = "9.9.9.9", SecondaryIp = "149.112.112.112" },
                    new DnsOption { Name = "AdGuard DNS", PrimaryIp = "94.140.14.14", SecondaryIp = "94.140.15.15" }
                };

                using var ping = new Ping();
                foreach (var opt in options)
                {
                    try
                    {
                        long totalLatency = 0;
                        int successfulPings = 0;

                        // Ping primary 3 times to get a stable average
                        for (int i = 0; i < 3; i++)
                        {
                            var reply = ping.Send(opt.PrimaryIp, 400);
                            if (reply.Status == IPStatus.Success)
                            {
                                totalLatency += reply.RoundtripTime;
                                successfulPings++;
                            }
                        }

                        if (successfulPings > 0)
                        {
                            opt.LatencyMs = totalLatency / successfulPings;
                            RealTimeLogService.Instance.Log($"DNS Result: {opt.Name} ({opt.PrimaryIp}) latency = {opt.LatencyMs}ms");
                        }
                        else
                        {
                            opt.LatencyMs = 999;
                            RealTimeLogService.Instance.Log($"DNS Warning: {opt.Name} ({opt.PrimaryIp}) was unreachable.");
                        }
                    }
                    catch (Exception ex)
                    {
                        opt.LatencyMs = 999;
                        System.Diagnostics.Debug.WriteLine($"Failed to ping {opt.Name}: {ex.Message}");
                    }
                }

                return options;
            });
        }

        public static async Task<bool> ApplyBestDnsAsync(DnsOption bestDns)
        {
            return await Task.Run(() =>
            {
                RealTimeLogService.Instance.Log($"Applying best DNS Configuration: {bestDns.Name} ({bestDns.PrimaryIp}, {bestDns.SecondaryIp})...");
                
                try
                {
                    // 1. Back up all existing configurations before altering
                    BackupCurrentDns();

                    // 2. Query and apply DNS to all IPEnabled adapters using WMI
                    using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = True");
                    int adaptersModified = 0;

                    foreach (ManagementObject obj in searcher.Get())
                    {
                        try
                        {
                            string desc = obj["Description"]?.ToString() ?? "Unknown Adapter";
                            string[] dnsServers = { bestDns.PrimaryIp, bestDns.SecondaryIp };

                            using var managementParams = obj.GetMethodParameters("SetDNSServerSearchOrder");
                            managementParams["DNSServerSearchOrder"] = dnsServers;

                            using var outParams = obj.InvokeMethod("SetDNSServerSearchOrder", managementParams, null);
                            uint returnVal = (uint)outParams["ReturnValue"];

                            if (returnVal == 0)
                            {
                                RealTimeLogService.Instance.Log($"Configured DNS on: {desc}");
                                adaptersModified++;
                            }
                            else
                            {
                                RealTimeLogService.Instance.Log($"[WARNING] Failed to configure DNS on {desc}. WMI Error: {returnVal}");
                            }
                        }
                        catch (Exception ex)
                        {
                            RealTimeLogService.Instance.Log($"WMI adapter write error: {ex.Message}");
                        }
                    }

                    if (adaptersModified > 0)
                    {
                        RealTimeLogService.Instance.Log($"[SUCCESS] Applied DNS configuration successfully to {adaptersModified} adapters.");
                        return true;
                    }
                    else
                    {
                        RealTimeLogService.Instance.Log("[ERROR] No active network adapters found to configure.");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    RealTimeLogService.Instance.Log($"[ERROR] Applying DNS failed: {ex.Message}");
                    return false;
                }
            });
        }

        public static async Task<bool> RestoreDefaultDnsAsync()
        {
            return await Task.Run(() =>
            {
                RealTimeLogService.Instance.Log("Restoring default DNS / DHCP configuration...");
                try
                {
                    var backupList = new List<AdapterBackup>();
                    if (File.Exists(BackupPath))
                    {
                        try
                        {
                            string json = File.ReadAllText(BackupPath);
                            backupList = JsonSerializer.Deserialize<List<AdapterBackup>>(json) ?? new List<AdapterBackup>();
                        }
                        catch { }
                    }

                    using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = True");
                    int restoredCount = 0;

                    foreach (ManagementObject obj in searcher.Get())
                    {
                        try
                        {
                            string settingId = obj["SettingID"]?.ToString() ?? "";
                            string desc = obj["Description"]?.ToString() ?? "Unknown Adapter";

                            var backup = backupList.Find(b => b.SettingId.Equals(settingId, StringComparison.OrdinalIgnoreCase));
                            
                            using var managementParams = obj.GetMethodParameters("SetDNSServerSearchOrder");

                            if (backup != null)
                            {
                                if (backup.DHCPEnabled || backup.OriginalDns == null || backup.OriginalDns.Length == 0)
                                {
                                    // Reset to DHCP
                                    managementParams["DNSServerSearchOrder"] = null;
                                    RealTimeLogService.Instance.Log($"Reset to automatic (DHCP) DNS for: {desc}");
                                }
                                else
                                {
                                    // Restore static original DNS list
                                    managementParams["DNSServerSearchOrder"] = backup.OriginalDns;
                                    RealTimeLogService.Instance.Log($"Restored original static DNS ({string.Join(", ", backup.OriginalDns)}) for: {desc}");
                                }
                            }
                            else
                            {
                                // Default fallback: DHCP
                                managementParams["DNSServerSearchOrder"] = null;
                                RealTimeLogService.Instance.Log($"Fallback: Set automatic (DHCP) DNS for: {desc}");
                            }

                            using var outParams = obj.InvokeMethod("SetDNSServerSearchOrder", managementParams, null);
                            uint returnVal = (uint)outParams["ReturnValue"];

                            if (returnVal == 0)
                            {
                                restoredCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            RealTimeLogService.Instance.Log($"WMI adapter restore error: {ex.Message}");
                        }
                    }

                    // Clean backup file
                    try { File.Delete(BackupPath); } catch { }

                    RealTimeLogService.Instance.Log($"[SUCCESS] Restored DNS configurations successfully on {restoredCount} network adapters.");
                    return true;
                }
                catch (Exception ex)
                {
                    RealTimeLogService.Instance.Log($"[ERROR] Restore DNS failed: {ex.Message}");
                    return false;
                }
            });
        }

        private static void BackupCurrentDns()
        {
            try
            {
                if (File.Exists(BackupPath)) return; // Backup already exists, don't overwrite it to protect original state!

                var backupList = new List<AdapterBackup>();
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = True");
                
                foreach (ManagementObject obj in searcher.Get())
                {
                    try
                    {
                        var backup = new AdapterBackup
                        {
                            SettingId = obj["SettingID"]?.ToString() ?? "",
                            Description = obj["Description"]?.ToString() ?? "Unknown Adapter",
                            OriginalDns = (string[])obj["DNSServerSearchOrder"],
                            DHCPEnabled = (bool)obj["DHCPEnabled"]
                        };
                        backupList.Add(backup);
                    }
                    catch { }
                }

                if (!Directory.Exists(BackupDir)) Directory.CreateDirectory(BackupDir);
                string json = JsonSerializer.Serialize(backupList);
                File.WriteAllText(BackupPath, json);
                RealTimeLogService.Instance.Log("Original DNS configuration backed up to LocalAppData.");
            }
            catch (Exception ex)
            {
                RealTimeLogService.Instance.Log($"Warning: DNS backup generation failed: {ex.Message}");
            }
        }
    }
}
