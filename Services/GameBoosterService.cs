using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TAY.Services
{
    public class GameBoosterService
    {
        private static readonly string BackupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TAY");
        private static readonly string BackupPath = Path.Combine(BackupDir, "gamebooster_backup.json");

        private static readonly string[] OptimizeServices = { "wuauserv", "SysMain", "WerSvc" };
        public static readonly string[] MonitorGames = {
            "cs2", "dota2", "RocketLeague", "GTA5", "Cyberpunk2077", 
            "VALORANT-Win64-Shipping", "FortniteClient-Win64-Shipping", 
            "League of Legends", "Overwatch"
        };

        private static List<string> _pausedServices = new();
        private static HashSet<int> _boostedProcessIds = new();
        private static Timer? _monitorTimer;
        private static bool _isGameModeActive = false;
        private static readonly object _lock = new();

        public static bool IsActive => _isGameModeActive;

        public static async Task<bool> ActivateGameModeAsync()
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (_isGameModeActive) return true;
                    _isGameModeActive = true;
                }

                RealTimeLogService.Instance.Log("Activating Game Booster Mode...");

                try
                {
                    // 1. Identify and stop active target services
                    _pausedServices.Clear();
                    foreach (var svc in OptimizeServices)
                    {
                        if (IsServiceRunning(svc))
                        {
                            RealTimeLogService.Instance.Log($"Service {svc} is running. Pausing...");
                            StopService(svc);
                            _pausedServices.Add(svc);
                        }
                    }

                    // 2. Persist paused services list for crash recovery
                    try
                    {
                        if (!Directory.Exists(BackupDir)) Directory.CreateDirectory(BackupDir);
                        File.WriteAllText(BackupPath, JsonSerializer.Serialize(_pausedServices));
                    }
                    catch (Exception ex)
                    {
                        RealTimeLogService.Instance.Log($"Warning: Failed to save booster state: {ex.Message}");
                    }

                    // 3. Start process priority monitoring
                    _boostedProcessIds.Clear();
                    _monitorTimer = new Timer(MonitorTick, null, 1000, 3000);

                    RealTimeLogService.Instance.Log("[SUCCESS] Game Mode activated. Non-essential services paused.");
                    return true;
                }
                catch (Exception ex)
                {
                    RealTimeLogService.Instance.Log($"[ERROR] Game Mode activation failed: {ex.Message}");
                    DeactivateGameMode();
                    return false;
                }
            });
        }

        public static void DeactivateGameMode()
        {
            lock (_lock)
            {
                if (!_isGameModeActive) return;
                _isGameModeActive = false;
            }

            RealTimeLogService.Instance.Log("Deactivating Game Booster Mode...");

            try
            {
                // 1. Stop timer
                _monitorTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                _monitorTimer?.Dispose();
                _monitorTimer = null;

                // 2. Restore process priorities
                RestorePriorities();

                // 3. Restart paused services
                RestoreServices();

                // 4. Clean up backup
                if (File.Exists(BackupPath))
                {
                    try { File.Delete(BackupPath); } catch { }
                }

                RealTimeLogService.Instance.Log("[SUCCESS] Game Mode deactivated. All states restored.");
            }
            catch (Exception ex)
            {
                RealTimeLogService.Instance.Log($"[ERROR] Game Mode deactivation failed: {ex.Message}");
            }
        }

        private static void MonitorTick(object? state)
        {
            if (!_isGameModeActive) return;

            try
            {
                var currentGames = new List<Process>();
                foreach (var gameName in MonitorGames)
                {
                    try
                    {
                        var procs = Process.GetProcessesByName(gameName);
                        currentGames.AddRange(procs);
                    }
                    catch { }
                }

                // Check for games that exited
                var activeIds = currentGames.Select(p => p.Id).ToHashSet();
                var exitedIds = new List<int>();

                lock (_lock)
                {
                    foreach (var pid in _boostedProcessIds)
                    {
                        if (!activeIds.Contains(pid))
                        {
                            exitedIds.Add(pid);
                        }
                    }

                    foreach (var pid in exitedIds)
                    {
                        _boostedProcessIds.Remove(pid);
                        RealTimeLogService.Instance.Log($"Detected game/app exit (PID: {pid}). Resuming background services...");
                        
                        // Automatically deactivate Game Mode or trigger automatic service restore on game exit!
                        Task.Run(() => DeactivateGameMode());
                        return; // Deactivate will clean everything up, no need to proceed
                    }

                    // Boost new game processes
                    foreach (var proc in currentGames)
                    {
                        if (!_boostedProcessIds.Contains(proc.Id))
                        {
                            try
                            {
                                proc.PriorityClass = ProcessPriorityClass.High;
                                _boostedProcessIds.Add(proc.Id);
                                RealTimeLogService.Instance.Log($"[BOOST] Elevated {proc.ProcessName} (PID: {proc.Id}) to HIGH PRIORITY.");
                            }
                            catch (Exception ex)
                            {
                                RealTimeLogService.Instance.Log($"Failed to elevate priority for {proc.ProcessName}: {ex.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Booster monitoring error: {ex.Message}");
            }
        }

        private static void RestorePriorities()
        {
            lock (_lock)
            {
                foreach (var pid in _boostedProcessIds)
                {
                    try
                    {
                        var proc = Process.GetProcessById(pid);
                        proc.PriorityClass = ProcessPriorityClass.Normal;
                        RealTimeLogService.Instance.Log($"Restored priority of {proc.ProcessName} to Normal.");
                    }
                    catch { }
                }
                _boostedProcessIds.Clear();
            }
        }

        private static void RestoreServices()
        {
            // Load from file if empty (e.g. app restarted while game booster was active)
            if (_pausedServices.Count == 0 && File.Exists(BackupPath))
            {
                try
                {
                    string json = File.ReadAllText(BackupPath);
                    _pausedServices = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                }
                catch { }
            }

            foreach (var svc in _pausedServices)
            {
                RealTimeLogService.Instance.Log($"Restoring background service: {svc}");
                StartService(svc);
            }
            _pausedServices.Clear();
        }

        private static bool IsServiceRunning(string name)
        {
            try
            {
                var psi = new ProcessStartInfo("sc", $"query {name}")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                string output = proc?.StandardOutput.ReadToEnd() ?? "";
                proc?.WaitForExit();
                return output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static void StopService(string name)
        {
            try
            {
                var psi = new ProcessStartInfo("sc", $"stop {name}")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                var proc = Process.Start(psi);
                proc?.WaitForExit(4000);
            }
            catch { }
        }

        private static void StartService(string name)
        {
            try
            {
                var psi = new ProcessStartInfo("sc", $"start {name}")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                var proc = Process.Start(psi);
                proc?.WaitForExit(4000);
            }
            catch { }
        }
    }
}
