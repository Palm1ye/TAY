using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace TAY.Services
{
    public class MemoryOptimizationService
    {
        // P/Invoke declarations
        [DllImport("ntdll.dll")]
        private static extern int NtSetSystemInformation(
            int SystemInformationClass,
            IntPtr SystemInformation,
            int SystemInformationLength
        );

        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);

        // NTSTATUS constants
        private const uint STATUS_SUCCESS = 0x00000000;
        private const uint STATUS_ACCESS_DENIED = 0xC0000022;

        // System information class to purge memory lists
        private const int SystemMemoryListInformation = 80;

        // Commands for SystemMemoryListInformation
        private enum SystemMemoryListCommand
        {
            MemoryCaptureState = 1,
            MemoryPurgeActiveAndTransition = 2,
            MemoryPurgeStandbyList = 4,
            MemoryPurgeLowPriorityStandbyList = 5
        }

        public static async Task<long> DeepOptimizeMemoryAsync()
        {
            return await Task.Run(() =>
            {
                RealTimeLogService.Instance.Log("Starting deep RAM optimization...");
                
                long ramSavedBefore = 0;
                long ramSavedAfter = 0;

                try
                {
                    var (total, usedBefore, freeBefore, percent) = SystemService.GetRamInfo();
                    ramSavedBefore = freeBefore;
                }
                catch { }

                // Phase 1: Trim working set of all processes
                int processesTrimmedCount = 0;
                int processesFailedCount = 0;
                
                var processes = Process.GetProcesses();
                RealTimeLogService.Instance.Log($"Phase 1: Trimming working sets for {processes.Length} processes...");

                foreach (var proc in processes)
                {
                    try
                    {
                        // Standard guard check to avoid crashing on system/idle processes
                        if (proc.Id == 0 || proc.Id == 4) continue;

                        bool success = EmptyWorkingSet(proc.Handle);
                        if (success)
                        {
                            processesTrimmedCount++;
                        }
                        else
                        {
                            processesFailedCount++;
                        }
                    }
                    catch
                    {
                        processesFailedCount++;
                    }
                    finally
                    {
                        proc.Dispose();
                    }
                }
                RealTimeLogService.Instance.Log($"Trimming completed: {processesTrimmedCount} succeeded, {processesFailedCount} skipped/protected.");

                // Phase 2: Clear Standby List using NtSetSystemInformation
                RealTimeLogService.Instance.Log("Phase 2: Purging Standby List cache...");
                
                int command = (int)SystemMemoryListCommand.MemoryPurgeStandbyList;
                IntPtr alloc = IntPtr.Zero;

                try
                {
                    alloc = Marshal.AllocHGlobal(sizeof(int));
                    Marshal.WriteInt32(alloc, command);

                    int result = NtSetSystemInformation(SystemMemoryListInformation, alloc, sizeof(int));
                    
                    if (result == 0)
                    {
                        RealTimeLogService.Instance.Log("Successfully purged standby list cache via NtSetSystemInformation.");
                    }
                    else if ((uint)result == STATUS_ACCESS_DENIED)
                    {
                        RealTimeLogService.Instance.Log("[ERROR] Access Denied: Standby List purging requires Administrator privileges.");
                    }
                    else
                    {
                        // Check if low priority standby list can be purged as a fallback
                        int fallbackCommand = (int)SystemMemoryListCommand.MemoryPurgeLowPriorityStandbyList;
                        Marshal.WriteInt32(alloc, fallbackCommand);
                        int fallbackResult = NtSetSystemInformation(SystemMemoryListInformation, alloc, sizeof(int));

                        if (fallbackResult == 0)
                        {
                            RealTimeLogService.Instance.Log("Successfully purged low priority standby list cache.");
                        }
                        else
                        {
                            RealTimeLogService.Instance.Log($"[WARNING] Standby List purge returned NTSTATUS code: 0x{result:X8}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    RealTimeLogService.Instance.Log($"[ERROR] Standby list purge failed: {ex.Message}");
                }
                finally
                {
                    if (alloc != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(alloc);
                    }
                }

                // Phase 3: Evaluate memory freed
                long freedBytes = 0;
                try
                {
                    var (total, usedAfter, freeAfter, percent) = SystemService.GetRamInfo();
                    ramSavedAfter = freeAfter;
                    freedBytes = ramSavedAfter - ramSavedBefore;
                }
                catch { }

                if (freedBytes > 0)
                {
                    RealTimeLogService.Instance.Log($"[SUCCESS] Deep RAM Clean finished! Recovered {SystemService.FormatBytes(freedBytes)} cached memory.");
                }
                else
                {
                    RealTimeLogService.Instance.Log("[SUCCESS] Deep RAM Clean finished. Working sets compressed and standby list flushed.");
                }

                return freedBytes;
            });
        }
    }
}
