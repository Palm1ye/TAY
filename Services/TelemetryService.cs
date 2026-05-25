using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace TAY.Services
{
    public class TelemetryService
    {
        private static readonly string BackupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TAY");
        private static readonly string BackupPath = Path.Combine(BackupDir, "telemetry_backup.json");

        private class TelemetryBackup
        {
            public int? OriginalAllowCortana { get; set; }
            public int? OriginalAllowTelemetry { get; set; }
            public int OriginalDiagTrackStart { get; set; } = 2;
            public int OriginalDmwappushserviceStart { get; set; } = 3;
            public bool IsBackupCreated { get; set; } = false;
        }

        public static async Task<bool> IsDebloatedAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var diagTrackStart = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\DiagTrack", "Start", -1);
                    var dmwapStart = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\dmwappushservice", "Start", -1);
                    
                    // If either service is disabled, we consider telemetry partially or fully managed
                    return (diagTrackStart is int d && d == 4) && (dmwapStart is int w && w == 4);
                }
                catch
                {
                    return false;
                }
            });
        }

        public static async Task<bool> ApplyDebloatAsync()
        {
            return await Task.Run(() =>
            {
                RealTimeLogService.Instance.Log("Initiating Telemetry Debloat routine...");
                try
                {
                    // 1. Create Backup if not already exists
                    TelemetryBackup backup;
                    if (File.Exists(BackupPath))
                    {
                        try
                        {
                            string json = File.ReadAllText(BackupPath);
                            backup = JsonSerializer.Deserialize<TelemetryBackup>(json) ?? new TelemetryBackup();
                        }
                        catch
                        {
                            backup = new TelemetryBackup();
                        }
                    }
                    else
                    {
                        backup = new TelemetryBackup();
                    }

                    if (!backup.IsBackupCreated)
                    {
                        RealTimeLogService.Instance.Log("Backing up current telemetry & Cortana states...");
                        
                        // Read Cortana
                        var cortanaVal = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana", null);
                        backup.OriginalAllowCortana = cortanaVal is int c ? c : null;

                        // Read Telemetry
                        var telemetryVal = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", null);
                        backup.OriginalAllowTelemetry = telemetryVal is int t ? t : null;

                        // Read Services
                        backup.OriginalDiagTrackStart = (int)(Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\DiagTrack", "Start", 2) ?? 2);
                        backup.OriginalDmwappushserviceStart = (int)(Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\dmwappushservice", "Start", 3) ?? 3);

                        backup.IsBackupCreated = true;

                        if (!Directory.Exists(BackupDir))
                        {
                            Directory.CreateDirectory(BackupDir);
                        }
                        string json = JsonSerializer.Serialize(backup);
                        File.WriteAllText(BackupPath, json);
                        RealTimeLogService.Instance.Log("Telemetry backup persisted successfully.");
                    }

                    // 2. Disable Cortana in Registry
                    try
                    {
                        using var searchKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search", true);
                        searchKey.SetValue("AllowCortana", 0, RegistryValueKind.DWord);
                        RealTimeLogService.Instance.Log("Registry: Cortana marked disabled.");
                    }
                    catch (Exception ex)
                    {
                        RealTimeLogService.Instance.Log($"Registry Warning (Cortana): {ex.Message}");
                    }

                    // 3. Disable Telemetry in Registry
                    try
                    {
                        using var dataKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", true);
                        dataKey.SetValue("AllowTelemetry", 0, RegistryValueKind.DWord);
                        RealTimeLogService.Instance.Log("Registry: Telemetry collection set to 0.");
                    }
                    catch (Exception ex)
                    {
                        RealTimeLogService.Instance.Log($"Registry Warning (Telemetry): {ex.Message}");
                    }

                    // 4. Disable Xbox background Cortana/Telemetry tasks & services
                    try
                    {
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\DiagTrack", "Start", 4, RegistryValueKind.DWord);
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\dmwappushservice", "Start", 4, RegistryValueKind.DWord);
                        RealTimeLogService.Instance.Log("Services: Telemetry startup configuration set to Disabled.");
                    }
                    catch (Exception ex)
                    {
                        RealTimeLogService.Instance.Log($"Service Config Warning: {ex.Message}");
                    }

                    // 5. Stop the running telemetry services
                    StopServiceSilently("DiagTrack");
                    StopServiceSilently("dmwappushservice");

                    RealTimeLogService.Instance.Log("[SUCCESS] Windows Telemetry & Cortana disabled.");
                    return true;
                }
                catch (Exception ex)
                {
                    RealTimeLogService.Instance.Log($"[ERROR] Debloat failed: {ex.Message}");
                    return false;
                }
            });
        }

        public static async Task<bool> RevertDebloatAsync()
        {
            return await Task.Run(() =>
            {
                RealTimeLogService.Instance.Log("Initiating Telemetry Restore routine...");
                try
                {
                    if (!File.Exists(BackupPath))
                    {
                        RealTimeLogService.Instance.Log("Restore aborted: No backup found. Reverting to default Windows states...");
                        RestoreDefaults();
                        return true;
                    }

                    string json = File.ReadAllText(BackupPath);
                    var backup = JsonSerializer.Deserialize<TelemetryBackup>(json);
                    if (backup == null || !backup.IsBackupCreated)
                    {
                        RealTimeLogService.Instance.Log("Restore warning: Invalid backup format. Restoring standard defaults...");
                        RestoreDefaults();
                        return true;
                    }

                    // 1. Restore Cortana
                    try
                    {
                        if (backup.OriginalAllowCortana.HasValue)
                        {
                            using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search", true);
                            key.SetValue("AllowCortana", backup.OriginalAllowCortana.Value, RegistryValueKind.DWord);
                        }
                        else
                        {
                            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search", true);
                            key?.DeleteValue("AllowCortana", false);
                        }
                        RealTimeLogService.Instance.Log("Registry: Restored Cortana configuration.");
                    }
                    catch (Exception ex)
                    {
                        RealTimeLogService.Instance.Log($"Registry Revert Warning (Cortana): {ex.Message}");
                    }

                    // 2. Restore Telemetry
                    try
                    {
                        if (backup.OriginalAllowTelemetry.HasValue)
                        {
                            using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", true);
                            key.SetValue("AllowTelemetry", backup.OriginalAllowTelemetry.Value, RegistryValueKind.DWord);
                        }
                        else
                        {
                            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", true);
                            key?.DeleteValue("AllowTelemetry", false);
                        }
                        RealTimeLogService.Instance.Log("Registry: Restored telemetry configuration.");
                    }
                    catch (Exception ex)
                    {
                        RealTimeLogService.Instance.Log($"Registry Revert Warning (Telemetry): {ex.Message}");
                    }

                    // 3. Restore Services
                    try
                    {
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\DiagTrack", "Start", backup.OriginalDiagTrackStart, RegistryValueKind.DWord);
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\dmwappushservice", "Start", backup.OriginalDmwappushserviceStart, RegistryValueKind.DWord);
                        RealTimeLogService.Instance.Log("Services: Restored startup configuration.");
                    }
                    catch (Exception ex)
                    {
                        RealTimeLogService.Instance.Log($"Service Revert Warning: {ex.Message}");
                    }

                    // 4. Start services again if they were automatic/manual
                    if (backup.OriginalDiagTrackStart != 4) StartServiceSilently("DiagTrack");
                    if (backup.OriginalDmwappushserviceStart != 4) StartServiceSilently("dmwappushservice");

                    // 5. Clean up backup file
                    try
                    {
                        File.Delete(BackupPath);
                    }
                    catch { }

                    RealTimeLogService.Instance.Log("[SUCCESS] Telemetry configurations restored successfully.");
                    return true;
                }
                catch (Exception ex)
                {
                    RealTimeLogService.Instance.Log($"[ERROR] Restore failed: {ex.Message}");
                    return false;
                }
            });
        }

        private static void RestoreDefaults()
        {
            try
            {
                using var searchKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search", true);
                searchKey?.DeleteValue("AllowCortana", false);
            }
            catch { }

            try
            {
                using var dataKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", true);
                dataKey?.DeleteValue("AllowTelemetry", false);
            }
            catch { }

            try
            {
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\DiagTrack", "Start", 2, RegistryValueKind.DWord); // Automatic
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\dmwappushservice", "Start", 3, RegistryValueKind.DWord); // Manual
                StartServiceSilently("DiagTrack");
            }
            catch { }
        }

        private static void StopServiceSilently(string name)
        {
            try
            {
                var psi = new ProcessStartInfo("sc", $"stop {name}")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                var proc = Process.Start(psi);
                proc?.WaitForExit(3000);
                RealTimeLogService.Instance.Log($"Stopped telemetry service: {name}");
            }
            catch { }
        }

        private static void StartServiceSilently(string name)
        {
            try
            {
                var psi = new ProcessStartInfo("sc", $"start {name}")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                var proc = Process.Start(psi);
                proc?.WaitForExit(3000);
                RealTimeLogService.Instance.Log($"Started service: {name}");
            }
            catch { }
        }
    }
}
