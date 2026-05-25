using System.Diagnostics;
using System.IO;
using System.Management;
using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace TAY.Services;

public class SystemService
{
    private static List<PerformanceCounter>? _gpuCounters;
    private static DateTime _lastGpuCounterRefresh = DateTime.MinValue;

    public static (long total, long used, long free, int percent) GetRamInfo()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
            foreach (var obj in searcher.Get())
            {
                long totalKB = Convert.ToInt64(obj["TotalVisibleMemorySize"]);
                long freeKB = Convert.ToInt64(obj["FreePhysicalMemory"]);
                long total = totalKB * 1024;
                long free = freeKB * 1024;
                long used = total - free;
                int percent = (int)((double)used / total * 100);
                return (total, used, free, percent);
            }
        }
        catch { }
        return (0, 0, 0, 0);
    }

    private static FILETIME _prevIdleTime;
    private static FILETIME _prevKernelTime;
    private static FILETIME _prevUserTime;
    private static bool _hasPrevTimes = false;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FILETIME lpIdleTime, out FILETIME lpKernelTime, out FILETIME lpUserTime);

    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public uint dwLowDateTime;
        public uint dwHighDateTime;

        public ulong ToULong()
        {
            return ((ulong)dwHighDateTime << 32) | dwLowDateTime;
        }
    }

    public static int GetCpuUsage()
    {
        try
        {
            if (!GetSystemTimes(out var idleTime, out var kernelTime, out var userTime))
            {
                return 0;
            }

            if (!_hasPrevTimes)
            {
                _prevIdleTime = idleTime;
                _prevKernelTime = kernelTime;
                _prevUserTime = userTime;
                _hasPrevTimes = true;
                return 0;
            }

            ulong prevIdle = _prevIdleTime.ToULong();
            ulong prevKernel = _prevKernelTime.ToULong();
            ulong prevUser = _prevUserTime.ToULong();

            ulong currIdle = idleTime.ToULong();
            ulong currKernel = kernelTime.ToULong();
            ulong currUser = userTime.ToULong();

            ulong idleDiff = currIdle - prevIdle;
            ulong kernelDiff = currKernel - prevKernel;
            ulong userDiff = currUser - prevUser;

            ulong totalSysDiff = kernelDiff + userDiff;

            _prevIdleTime = idleTime;
            _prevKernelTime = kernelTime;
            _prevUserTime = userTime;

            if (totalSysDiff == 0) return 0;

            double cpu = 100.0 * (1.0 - ((double)idleDiff / totalSysDiff));
            if (cpu < 0) cpu = 0;
            if (cpu > 100) cpu = 100;

            return (int)Math.Round(cpu);
        }
        catch { return 0; }
    }

    public static TimeSpan GetUptime()
    {
        return TimeSpan.FromMilliseconds(Environment.TickCount64);
    }

    public static string FormatUptime(TimeSpan ts)
    {
        if (ts.Days > 0) return $"{ts.Days}d {ts.Hours}h {ts.Minutes}m";
        if (ts.Hours > 0) return $"{ts.Hours}h {ts.Minutes}m";
        return $"{ts.Minutes}m";
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 MB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F0} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
    }

    public static int GetGpuUsage()
    {
        try
        {
            if (_gpuCounters == null || DateTime.UtcNow - _lastGpuCounterRefresh > TimeSpan.FromMinutes(2))
            {
                _gpuCounters?.ForEach(c => c.Dispose());
                _gpuCounters = new List<PerformanceCounter>();

                var category = new PerformanceCounterCategory("GPU Engine");
                foreach (var instance in category.GetInstanceNames())
                {
                    if (!instance.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase) &&
                        !instance.Contains("engtype_Compute", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    try
                    {
                        var counter = new PerformanceCounter("GPU Engine", "Utilization Percentage", instance, true);
                        counter.NextValue();
                        _gpuCounters.Add(counter);
                    }
                    catch { }
                }

                _lastGpuCounterRefresh = DateTime.UtcNow;
            }

            if (_gpuCounters.Count == 0) return 0;
            var total = _gpuCounters.Sum(counter =>
            {
                try { return counter.NextValue(); }
                catch { return 0; }
            });

            return Math.Clamp((int)Math.Round(total), 0, 100);
        }
        catch
        {
            return 0;
        }
    }

    public static (long total, long used, long free, int percent) GetStorageInfo()
    {
        try
        {
            long total = 0;
            long free = 0;
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady || drive.DriveType != DriveType.Fixed) continue;
                total += drive.TotalSize;
                free += drive.TotalFreeSpace;
            }

            var used = total - free;
            var percent = total > 0 ? (int)Math.Round((double)used / total * 100) : 0;
            return (total, used, free, percent);
        }
        catch
        {
            return (0, 0, 0, 0);
        }
    }

    public static int GetProcessCount()
    {
        try { return Process.GetProcesses().Length; }
        catch { return 0; }
    }

    // --- Hardware Info ---
    public static Dictionary<string, object> GetHardwareInfo()
    {
        var info = new Dictionary<string, object>();
        try
        {
            using var cpuSearcher = new ManagementObjectSearcher("SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor");
            foreach (var cpu in cpuSearcher.Get())
            {
                info["CPU"] = cpu["Name"]?.ToString()?.Trim() ?? "Unknown";
                info["Cores"] = Convert.ToInt32(cpu["NumberOfCores"]);
                info["Threads"] = Convert.ToInt32(cpu["NumberOfLogicalProcessors"]);
                info["ClockSpeed"] = Math.Round(Convert.ToDouble(cpu["MaxClockSpeed"]) / 1000, 1);
                break;
            }

            using var ramSearcher = new ManagementObjectSearcher("SELECT Capacity, Speed, SMBIOSMemoryType, MemoryType FROM Win32_PhysicalMemory");
            long totalRam = 0;
            string ramType = "DDR";
            foreach (var ram in ramSearcher.Get())
            {
                totalRam += Convert.ToInt64(ram["Capacity"]);
                try
                {
                    uint typeVal = 0;
                    if (ram["SMBIOSMemoryType"] != null)
                    {
                        typeVal = Convert.ToUInt32(ram["SMBIOSMemoryType"]);
                    }
                    else if (ram["MemoryType"] != null)
                    {
                        typeVal = Convert.ToUInt32(ram["MemoryType"]);
                    }

                    if (typeVal > 0)
                    {
                        ramType = typeVal switch
                        {
                            20 => "DDR",
                            21 => "DDR2",
                            22 => "DDR2 FB-DIMM",
                            24 => "DDR3",
                            26 => "DDR4",
                            30 => "LPDDR5",
                            31 => "LPDDR3",
                            32 => "LPDDR4",
                            33 => "DDR5",
                            34 => "DDR5",
                            _ => ramType
                        };
                    }
                    else if (ram["Speed"] != null)
                    {
                        uint speed = Convert.ToUInt32(ram["Speed"]);
                        if (speed >= 4800) ramType = "DDR5";
                        else if (speed >= 2133) ramType = "DDR4";
                        else if (speed >= 800) ramType = "DDR3";
                    }
                }
                catch { }
            }
            info["RAM"] = Math.Round(totalRam / (1024.0 * 1024 * 1024), 2);
            info["RAMType"] = ramType;

            using var gpuSearcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController");
            var gpuNames = new List<string>();
            long maxDedicatedVram = 0;
            foreach (var gpu in gpuSearcher.Get())
            {
                var gpuName = gpu["Name"]?.ToString() ?? "Unknown";
                if (!IsVirtualDisplayAdapter(gpuName))
                {
                    gpuNames.Add(gpuName);
                }
                try
                {
                    long ramVal = Convert.ToInt64(gpu["AdapterRAM"]);
                    if (ramVal > maxDedicatedVram) maxDedicatedVram = ramVal;
                }
                catch { }
            }
            var registryVram = GetDedicatedVramFromDisplayRegistry();
            if (registryVram > maxDedicatedVram)
            {
                maxDedicatedVram = registryVram;
            }

            info["GPU"] = gpuNames.Count > 0 ? string.Join(" / ", gpuNames) : "Integrated / Virtual Display";
            
            // WDDM specification sets Shared System Memory as 50% of total Physical RAM
            long sharedSystemMemory = totalRam / 2;
            long totalGraphicsMemory = maxDedicatedVram + sharedSystemMemory;

            info["DedicatedVRAM"] = FormatBytes(maxDedicatedVram);
            info["SharedVRAM"] = FormatBytes(sharedSystemMemory);
            info["TotalVRAM"] = FormatBytes(totalGraphicsMemory);

            using var boardSearcher = new ManagementObjectSearcher("SELECT Manufacturer, Product FROM Win32_BaseBoard");
            foreach (var board in boardSearcher.Get())
            {
                info["Motherboard"] = $"{board["Manufacturer"]} {board["Product"]}";
                break;
            }

            using var osSearcher = new ManagementObjectSearcher("SELECT Caption, BuildNumber, Version FROM Win32_OperatingSystem");
            foreach (var os in osSearcher.Get())
            {
                info["OS"] = os["Caption"]?.ToString() ?? "Windows";
                info["OSBuild"] = os["BuildNumber"]?.ToString() ?? "";
                info["OSVersion"] = os["Version"]?.ToString() ?? "";
                break;
            }
        }
        catch { }
        return info;
    }

    private static bool IsVirtualDisplayAdapter(string name)
    {
        var lower = name.ToLowerInvariant();
        return lower.Contains("virtual") ||
               lower.Contains("parsec") ||
               lower.Contains("meta") ||
               lower.Contains("remote") ||
               lower.Contains("basic render");
    }

    private static long GetDedicatedVramFromDisplayRegistry()
    {
        long best = 0;
        try
        {
            using var videoRoot = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Video");
            if (videoRoot == null) return 0;

            foreach (var adapterKeyName in videoRoot.GetSubKeyNames())
            {
                using var adapterKey = videoRoot.OpenSubKey(adapterKeyName);
                if (adapterKey == null) continue;

                foreach (var childName in adapterKey.GetSubKeyNames())
                {
                    using var childKey = adapterKey.OpenSubKey(childName);
                    if (childKey == null) continue;

                    var adapterString = childKey.GetValue("HardwareInformation.AdapterString")?.ToString() ?? "";
                    if (IsVirtualDisplayAdapter(adapterString)) continue;

                    var memory = childKey.GetValue("HardwareInformation.qwMemorySize");
                    if (memory is long longMemory && longMemory > best)
                    {
                        best = longMemory;
                    }
                    else if (memory is byte[] bytes && bytes.Length >= 8)
                    {
                        var byteMemory = BitConverter.ToInt64(bytes, 0);
                        if (byteMemory > best) best = byteMemory;
                    }
                }
            }
        }
        catch { }

        return best;
    }

    // --- Drive Info ---
    public static List<DriveData> GetDrives()
    {
        var drives = new List<DriveData>();
        foreach (var d in DriveInfo.GetDrives())
        {
            if (!d.IsReady || d.DriveType != DriveType.Fixed) continue;
            drives.Add(new DriveData
            {
                Letter = d.Name[0].ToString(),
                Label = string.IsNullOrEmpty(d.VolumeLabel) ? "Local Disk" : d.VolumeLabel,
                Total = d.TotalSize,
                Free = d.TotalFreeSpace,
                Used = d.TotalSize - d.TotalFreeSpace
            });
        }
        return drives;
    }

    // --- Startup Apps ---
    public static List<StartupApp> GetStartupApps()
    {
        var apps = new List<StartupApp>();
        try
        {
            var highImpact = new[] { "steam", "discord", "spotify", "chrome", "onedrive", "teams", "edge", "msedge", "epicgames", "opera", "brave" };
            var lowImpact = new[] { "securityhealth", "rtkaud", "vanguard" };

            var approved = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            string[] approvedPaths = {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"
            };
            foreach (var path in approvedPaths)
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(path);
                    if (key != null)
                    {
                        foreach (var name in key.GetValueNames())
                        {
                            var val = key.GetValue(name) as byte[];
                            if (val != null && val.Length > 0)
                                approved[name] = (val[0] % 2) == 0;
                        }
                    }
                }
                catch { }
            }

            string[] runPaths = {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"
            };
            RegistryKey[] roots = { Registry.CurrentUser, Registry.LocalMachine };
            int id = 0;
            foreach (var root in roots)
            {
                foreach (var path in runPaths)
                {
                    try
                    {
                        using var key = root.OpenSubKey(path);
                        if (key != null)
                        {
                            foreach (var name in key.GetValueNames())
                            {
                                var val = key.GetValue(name)?.ToString() ?? "";
                                bool enabled = approved.ContainsKey(name) ? approved[name] : true;
                                string nl = name.ToLower();
                                string impact = highImpact.Any(h => nl.Contains(h)) ? "high"
                                              : lowImpact.Any(l => nl.Contains(l)) ? "low"
                                              : "medium";
                                apps.Add(new StartupApp { Id = id++, Name = name, Command = val, Enabled = enabled, Impact = impact });
                            }
                        }
                    }
                    catch { }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetStartupApps Global Error: {ex.Message}");
        }
        return apps;
    }

    public static void ToggleStartupApp(string name, bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run", true);
            if (key != null)
            {
                var val = key.GetValue(name) as byte[];
                if (val == null || val.Length == 0)
                {
                    val = new byte[] { 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
                }
                
                if (enable)
                    val[0] = (byte)(val[0] % 2 == 0 ? val[0] : val[0] - 1); // Make it even
                else
                    val[0] = (byte)(val[0] % 2 == 0 ? val[0] + 1 : val[0]); // Make it odd
                    
                key.SetValue(name, val, RegistryValueKind.Binary);
            }
        }
        catch { }
    }

    // --- Temp Size ---
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct SHQUERYRBINFO
    {
        public int cbSize;
        public long i64Size;
        public long i64NumItems;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

    public static List<FolderInfo> GetCleanerFolders()
    {
        var list = new List<FolderInfo>();
        var folders = new Dictionary<string, string>
        {
            { "Windows Temp", @"C:\Windows\Temp" },
            { "User Temp", Path.GetTempPath() },
            { "Chrome Cache", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\User Data\Default\Cache") },
            { "Edge Cache", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Edge\User Data\Default\Cache") },
            { "Windows Prefetch", @"C:\Windows\Prefetch" }
        };

        long GetDirectorySizeSafely(string folderPath)
        {
            long size = 0;
            try
            {
                var dirInfo = new DirectoryInfo(folderPath);
                if (!dirInfo.Exists) return 0;
                
                foreach (var file in dirInfo.EnumerateFiles())
                {
                    try { size += file.Length; } catch { }
                }
                
                foreach (var dir in dirInfo.EnumerateDirectories())
                {
                    try { size += GetDirectorySizeSafely(dir.FullName); } catch { }
                }
            }
            catch { }
            return size;
        }

        foreach (var f in folders)
        {
            long size = GetDirectorySizeSafely(f.Value);
            list.Add(new FolderInfo { Name = f.Key, Path = f.Value, Size = size });
        }

        // Query Recycle Bin size
        long recycleBinSize = 0;
        try
        {
            var rbInfo = new SHQUERYRBINFO();
            rbInfo.cbSize = Marshal.SizeOf(typeof(SHQUERYRBINFO));
            int hr = SHQueryRecycleBin(null, ref rbInfo);
            if (hr == 0)
            {
                recycleBinSize = rbInfo.i64Size;
            }
        }
        catch { }
        list.Add(new FolderInfo { Name = "Recycle Bin", Path = "RECYCLE_BIN", Size = recycleBinSize });

        return list;
    }

    private static void DeleteDirectoryContentsSafely(string dirPath)
    {
        try
        {
            if (!Directory.Exists(dirPath)) return;

            foreach (var file in Directory.EnumerateFiles(dirPath))
            {
                try
                {
                    File.Delete(file);
                }
                catch { }
            }

            foreach (var subDir in Directory.EnumerateDirectories(dirPath))
            {
                try
                {
                    DeleteDirectoryContentsSafely(subDir);
                    Directory.Delete(subDir, false);
                }
                catch { }
            }
        }
        catch { }
    }

    public static void CleanFolders(List<string> pathsToClean)
    {
        foreach (var p in pathsToClean)
        {
            if (p == "RECYCLE_BIN")
            {
                try
                {
                    SHEmptyRecycleBin(IntPtr.Zero, null, 7); // SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND
                }
                catch { }
                continue;
            }

            try
            {
                DeleteDirectoryContentsSafely(p);
            }
            catch { }
        }
    }

    // --- Power Plans ---
    public static List<PowerPlan> GetPowerPlans()
    {
        var plans = new List<PowerPlan>();
        try
        {
            var psi = new ProcessStartInfo("powercfg", "/list")
            {
                RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            string output = proc?.StandardOutput.ReadToEnd() ?? "";
            proc?.WaitForExit();
            foreach (var line in output.Split('\n'))
            {
                var match = System.Text.RegularExpressions.Regex.Match(line, @"GUID: ([0-9a-f\-]+)\s+\((.+?)\)(\s*\*)?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success)
                    plans.Add(new PowerPlan { Id = match.Groups[1].Value, Name = match.Groups[2].Value.Trim(), Active = match.Groups[3].Value.Contains("*") });
            }
        }
        catch { }
        return plans;
    }

    public static void SetPowerPlan(string guid)
    {
        try
        {
            var psi = new ProcessStartInfo("powercfg", $"/setactive {guid}")
            { UseShellExecute = false, CreateNoWindow = true };
            Process.Start(psi)?.WaitForExit();
        }
        catch { }
    }

    // --- Top Processes ---
    public static bool KillProcess(int id)
    {
        try
        {
            var process = Process.GetProcessById(id);
            process.Kill();
            process.WaitForExit(3000);
            return true;
        }
        catch
        {
            return false;
        }
    }
    public static List<ProcessInfo> GetTopProcesses(int count = 5)
    {
        return Process.GetProcesses()
            .OrderByDescending(p => { try { return p.WorkingSet64; } catch { return 0; } })
            .Take(count)
            .Select(p => new ProcessInfo { Id = p.Id, Name = p.ProcessName, Ram = p.WorkingSet64 })
            .ToList();
    }
}

public class DriveData { public string Letter { get; set; } = ""; public string Label { get; set; } = ""; public long Total { get; set; } public long Free { get; set; } public long Used { get; set; } }
public class StartupApp { public int Id { get; set; } public string Name { get; set; } = ""; public string Command { get; set; } = ""; public bool Enabled { get; set; } public string Impact { get; set; } = "medium"; }
public class PowerPlan { public string Id { get; set; } = ""; public string Name { get; set; } = ""; public bool Active { get; set; } }
public class ProcessInfo { public int Id { get; set; } public string Name { get; set; } = ""; public long Ram { get; set; } }
public class FolderInfo { public string Name { get; set; } = ""; public string Path { get; set; } = ""; public long Size { get; set; } }
