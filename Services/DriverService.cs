using System;
using System.Collections.Generic;
using System.Management;
using System.Threading.Tasks;

namespace TAY.Services
{
    public class DriverInfo
    {
        public string DeviceName { get; set; } = "";
        public string DeviceClass { get; set; } = ""; // DISPLAY, SYSTEM, etc.
        public string DriverVersion { get; set; } = "";
        public DateTime? DriverDate { get; set; }
        public string Provider { get; set; } = "";
        public bool IsOutdated { get; set; } = false;
        public string Suggestion { get; set; } = "";
        public string StatusLabel => IsOutdated ? "Needs review" : "Current";
        public string DateLabel => DriverDate.HasValue ? DriverDate.Value.ToString("yyyy-MM-dd") : "Unknown date";
    }

    public class DriverService
    {

        public static async Task<List<DriverInfo>> ScanDriversAsync()
        {
            return await Task.Run(() =>
            {
                var list = new List<DriverInfo>();
                RealTimeLogService.Instance.Log("Initiating hardware driver analysis via WMI...");

                try
                {
                    // Scan critical classes: DISPLAY and SYSTEM
                    using var searcher = new ManagementObjectSearcher(
                        "SELECT DeviceClass, DeviceName, DriverDate, DriverVersion, InfName, Signer FROM Win32_PnPSignedDriver"
                    );

                    int totalScanned = 0;
                    int criticalCount = 0;

                    foreach (ManagementObject obj in searcher.Get())
                    {
                        totalScanned++;
                        try
                        {
                            string deviceClass = obj["DeviceClass"]?.ToString() ?? "";
                            string deviceName = obj["DeviceName"]?.ToString() ?? "";

                            bool isDisplay = deviceClass.Equals("DISPLAY", StringComparison.OrdinalIgnoreCase);
                            bool isSystem = deviceClass.Equals("SYSTEM", StringComparison.OrdinalIgnoreCase);

                            // Keep list focused on Display and critical Chipset/system devices
                            if (!isDisplay && !isSystem) continue;

                            string lowerName = deviceName.ToLowerInvariant();

                            // Filter system list to actual motherboard chipsets/bridges/controllers to avoid flooding
                            if (isSystem && 
                                !lowerName.Contains("chipset") && 
                                !lowerName.Contains("pcie") && 
                                !lowerName.Contains("pci express") && 
                                !lowerName.Contains("lpc") && 
                                !lowerName.Contains("smbus") && 
                                !lowerName.Contains("sata ahci") && 
                                !lowerName.Contains("management engine"))
                            {
                                continue;
                            }

                            criticalCount++;

                            string driverVersion = obj["DriverVersion"]?.ToString() ?? "Unknown";
                            string provider = obj["Signer"]?.ToString() ?? "Unknown Provider";
                            string dateString = obj["DriverDate"]?.ToString() ?? "";

                            DateTime? driverDate = ParseDmtfDate(dateString);
                            bool isOutdated = false;
                            string suggestion = "Driver is up to date.";

                            if (driverDate.HasValue)
                            {
                                var age = DateTime.Now - driverDate.Value;
                                if (age.TotalDays > 365) // Outdated if older than 1 year
                                {
                                    isOutdated = true;
                                    
                                    if (isDisplay)
                                    {
                                        if (lowerName.Contains("nvidia"))
                                        {
                                            suggestion = "Critical display driver outdated. Update via NVIDIA GeForce Experience or official NVIDIA portal.";
                                        }
                                        else if (lowerName.Contains("amd") || lowerName.Contains("radeon"))
                                        {
                                            suggestion = "Display driver outdated. Download AMD Software: Adrenalin Edition or update from AMD support.";
                                        }
                                        else
                                        {
                                            suggestion = "Graphics adapter outdated. Upgrade via Intel Driver & Support Assistant or Device Manager.";
                                        }
                                    }
                                    else
                                    {
                                        suggestion = "Motherboard chipset interface driver is old. Recommended to check OEM manufacturer support page for chipset upgrades.";
                                    }
                                }
                            }

                            list.Add(new DriverInfo
                            {
                                DeviceName = deviceName,
                                DeviceClass = isDisplay ? "Display Adapter" : "System Chipset",
                                DriverVersion = driverVersion,
                                DriverDate = driverDate,
                                Provider = provider,
                                IsOutdated = isOutdated,
                                Suggestion = suggestion
                            });
                        }
                        catch { }
                    }

                    RealTimeLogService.Instance.Log($"Driver scan finished. Scanned {totalScanned} system drivers. Evaluated {criticalCount} critical devices.");
                }
                catch (Exception ex)
                {
                    RealTimeLogService.Instance.Log($"[ERROR] Hardware driver scanning failed: {ex.Message}");
                }

                return list;
            });
        }

        private static DateTime? ParseDmtfDate(string dmtfDate)
        {
            if (string.IsNullOrEmpty(dmtfDate) || dmtfDate.Length < 8) return null;
            try
            {
                int year = int.Parse(dmtfDate.Substring(0, 4));
                int month = int.Parse(dmtfDate.Substring(4, 2));
                int day = int.Parse(dmtfDate.Substring(6, 2));
                return new DateTime(year, month, day);
            }
            catch
            {
                return null;
            }
        }
    }
}
