using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using TAY.Services;

namespace TAY.ViewModels
{
    public partial class HardwareViewModel : ObservableObject
    {
        [ObservableProperty] private string cpuName = "Loading...";
        [ObservableProperty] private string cpuCores = "Loading...";
        [ObservableProperty] private string cpuThreads = "Loading...";
        [ObservableProperty] private string cpuClockSpeed = "Loading...";

        [ObservableProperty] private string gpuName = "Loading...";
        [ObservableProperty] private string dedicatedVram = "Loading...";
        [ObservableProperty] private string sharedVram = "Loading...";
        [ObservableProperty] private string totalVram = "Loading...";

        [ObservableProperty] private string ramCapacity = "Loading...";
        [ObservableProperty] private string ramType = "Loading...";
        [ObservableProperty] private string motherboardName = "Loading...";
        [ObservableProperty] private string osName = "Loading...";
        [ObservableProperty] private string osBuildInfo = "Loading...";

        public ObservableCollection<HardwareCategoryVM> HardwareCategories { get; } = new();

        private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcher;

        public HardwareViewModel()
        {
            _dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            _ = LoadHardwareAsync();
        }

        public void Refresh()
        {
            _ = LoadHardwareAsync();
        }

        private async Task LoadHardwareAsync()
        {
            var snapshot = await Task.Run(BuildSnapshot);
            _dispatcher?.TryEnqueue(() =>
            {
                var info = snapshot.Info;

                CpuName = info.ContainsKey("CPU") ? info["CPU"].ToString() ?? "" : "Unknown";
                CpuCores = info.ContainsKey("Cores") ? $"{info["Cores"]} Cores" : "Unknown";
                CpuThreads = info.ContainsKey("Threads") ? $"{info["Threads"]} Threads" : "Unknown";
                CpuClockSpeed = info.ContainsKey("ClockSpeed") ? $"{info["ClockSpeed"]} GHz" : "Unknown";

                GpuName = info.ContainsKey("GPU") ? info["GPU"].ToString() ?? "" : "Unknown";
                DedicatedVram = info.ContainsKey("DedicatedVRAM") ? info["DedicatedVRAM"].ToString() ?? "" : "0 MB";
                SharedVram = info.ContainsKey("SharedVRAM") ? info["SharedVRAM"].ToString() ?? "" : "0 MB";
                TotalVram = info.ContainsKey("TotalVRAM") ? info["TotalVRAM"].ToString() ?? "" : "0 MB";

                RamCapacity = info.ContainsKey("RAM") ? $"{info["RAM"]} GB" : "Unknown";
                RamType = info.ContainsKey("RAMType") ? $"{info["RAMType"]} System Memory" : "DDR System Memory";
                MotherboardName = info.ContainsKey("Motherboard") ? info["Motherboard"].ToString() ?? "" : "Unknown";
                
                string caption = info.ContainsKey("OS") ? info["OS"].ToString() ?? "" : "Windows";
                string build = info.ContainsKey("OSBuild") ? info["OSBuild"].ToString() ?? "" : "";
                OsName = caption;
                OsBuildInfo = $"Build {build}";

                HardwareCategories.Clear();
                foreach (var category in snapshot.Categories)
                {
                    HardwareCategories.Add(category);
                }
            });
        }

        private static HardwareSnapshot BuildSnapshot()
        {
            var info = SystemService.GetHardwareInfo();
            var categories = new ObservableCollection<HardwareCategoryVM>();

            string cpuName = Value(info, "CPU");
            categories.Add(new HardwareCategoryVM
            {
                IconGlyph = "\uE950",
                Title = "CPU",
                Summary = $"{Value(info, "Cores")} cores / {Value(info, "Threads")} threads",
                Rows =
                {
                    new("Model", cpuName),
                    new("Physical cores", Value(info, "Cores")),
                    new("Logical processors", Value(info, "Threads")),
                    new("Max clock", $"{Value(info, "ClockSpeed")} GHz")
                }
            });

            foreach (var row in QueryFirst("Win32_Processor", "Manufacturer", "Architecture", "SocketDesignation", "L2CacheSize", "L3CacheSize"))
            {
                AddIfPresent(categories.Last(), row.Key, row.Value);
            }

            var gpuCategory = new HardwareCategoryVM
            {
                IconGlyph = "\uE7F4",
                Title = "GPU",
                Summary = Value(info, "GPU"),
                Rows =
                {
                    new("Adapter", Value(info, "GPU")),
                    new("Dedicated VRAM", Value(info, "DedicatedVRAM")),
                    new("Shared memory", Value(info, "SharedVRAM")),
                    new("Total graphics memory", Value(info, "TotalVRAM"))
                }
            };
            foreach (var row in QueryFirst("Win32_VideoController", "VideoProcessor", "DriverVersion", "CurrentHorizontalResolution", "CurrentVerticalResolution", "CurrentRefreshRate"))
            {
                AddIfPresent(gpuCategory, row.Key, row.Value);
            }
            categories.Add(gpuCategory);

            var memoryCategory = new HardwareCategoryVM
            {
                IconGlyph = "\uE8B7",
                Title = "Memory",
                Summary = $"{Value(info, "RAM")} GB {Value(info, "RAMType")}",
                Rows =
                {
                    new("Total capacity", $"{Value(info, "RAM")} GB"),
                    new("Detected type", Value(info, "RAMType"))
                }
            };
            AddMemoryModules(memoryCategory);
            categories.Add(memoryCategory);

            var boardCategory = new HardwareCategoryVM
            {
                IconGlyph = "\uE964",
                Title = "Motherboard and BIOS",
                Summary = Value(info, "Motherboard"),
                Rows =
                {
                    new("Baseboard", Value(info, "Motherboard"))
                }
            };
            foreach (var row in QueryFirst("Win32_BaseBoard", "Manufacturer", "Product", "Version", "SerialNumber"))
            {
                AddIfPresent(boardCategory, row.Key, row.Value);
            }
            foreach (var row in QueryFirst("Win32_BIOS", "Manufacturer", "SMBIOSBIOSVersion", "ReleaseDate", "SerialNumber"))
            {
                AddIfPresent(boardCategory, $"BIOS {row.Key}", NormalizeWmiValue(row.Key, row.Value));
            }
            categories.Add(boardCategory);

            var storageCategory = new HardwareCategoryVM
            {
                IconGlyph = "\uEDA2",
                Title = "Storage volumes",
                Summary = "Fixed disks and visible Windows volumes"
            };
            foreach (var drive in SystemService.GetDrives())
            {
                storageCategory.Rows.Add(new HardwareDetailVM(
                    $"{drive.Letter}: {drive.Label}",
                    $"{SystemService.FormatBytes(drive.Used)} used / {SystemService.FormatBytes(drive.Total)} total / {SystemService.FormatBytes(drive.Free)} free"));
            }
            if (storageCategory.Rows.Count == 0)
            {
                storageCategory.Rows.Add(new("Volumes", "No ready fixed disks detected"));
            }
            categories.Add(storageCategory);

            var diskCategory = new HardwareCategoryVM
            {
                IconGlyph = "\uE8B7",
                Title = "Physical disks",
                Summary = "Disk models reported by Windows"
            };
            foreach (var disk in QueryMany("Win32_DiskDrive", "Model", "InterfaceType", "MediaType", "Size"))
            {
                var model = disk.GetValueOrDefault("Model", "Disk");
                var details = $"{disk.GetValueOrDefault("InterfaceType", "Unknown")} / {NormalizeBytes(disk.GetValueOrDefault("Size", ""))}";
                diskCategory.Rows.Add(new(model, details));
            }
            if (diskCategory.Rows.Count == 0)
            {
                diskCategory.Rows.Add(new("Physical disks", "No disk model details reported"));
            }
            categories.Add(diskCategory);

            var networkCategory = new HardwareCategoryVM
            {
                IconGlyph = "\uE839",
                Title = "Network adapters",
                Summary = "Active and available interfaces"
            };
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces()
                         .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                         .OrderByDescending(n => n.OperationalStatus == OperationalStatus.Up)
                         .Take(8))
            {
                networkCategory.Rows.Add(new(adapter.Name, $"{adapter.NetworkInterfaceType} / {adapter.OperationalStatus} / {FormatSpeed(adapter.Speed)}"));
            }
            if (networkCategory.Rows.Count == 0)
            {
                networkCategory.Rows.Add(new("Network", "No adapters detected"));
            }
            categories.Add(networkCategory);

            categories.Add(new HardwareCategoryVM
            {
                IconGlyph = "\uE7C3",
                Title = "Operating system",
                Summary = $"{Value(info, "OS")} - build {Value(info, "OSBuild")}",
                Rows =
                {
                    new("Edition", Value(info, "OS")),
                    new("Build", Value(info, "OSBuild")),
                    new("Version", Value(info, "OSVersion")),
                    new("Machine", Environment.MachineName),
                    new("Architecture", System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString())
                }
            });

            return new HardwareSnapshot(info, categories);
        }

        private static string Value(System.Collections.Generic.Dictionary<string, object> info, string key)
        {
            return info.TryGetValue(key, out var value) ? value?.ToString() ?? "Unknown" : "Unknown";
        }

        private static System.Collections.Generic.Dictionary<string, string> QueryFirst(string className, params string[] properties)
        {
            return QueryMany(className, properties).FirstOrDefault() ?? new System.Collections.Generic.Dictionary<string, string>();
        }

        private static System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, string>> QueryMany(string className, params string[] properties)
        {
            var rows = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, string>>();
            try
            {
                using var searcher = new ManagementObjectSearcher($"SELECT {string.Join(", ", properties)} FROM {className}");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var row = new System.Collections.Generic.Dictionary<string, string>();
                    foreach (var prop in properties)
                    {
                        row[prop] = obj[prop]?.ToString() ?? "";
                    }
                    rows.Add(row);
                }
            }
            catch { }
            return rows;
        }

        private static void AddMemoryModules(HardwareCategoryVM category)
        {
            int slot = 1;
            foreach (var module in QueryMany("Win32_PhysicalMemory", "BankLabel", "Capacity", "Speed", "Manufacturer", "PartNumber"))
            {
                var bank = module.GetValueOrDefault("BankLabel", $"Slot {slot}");
                var capacity = NormalizeBytes(module.GetValueOrDefault("Capacity", ""));
                var speed = module.GetValueOrDefault("Speed", "");
                var maker = module.GetValueOrDefault("Manufacturer", "");
                var part = module.GetValueOrDefault("PartNumber", "").Trim();
                category.Rows.Add(new($"{bank}", $"{capacity} / {speed} MHz / {maker} {part}".Trim()));
                slot++;
            }
        }

        private static void AddIfPresent(HardwareCategoryVM category, string label, string value)
        {
            value = NormalizeWmiValue(label, value);
            if (!string.IsNullOrWhiteSpace(value))
            {
                category.Rows.Add(new HardwareDetailVM(label, value));
            }
        }

        private static string NormalizeWmiValue(string label, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            if (label.Contains("ReleaseDate", StringComparison.OrdinalIgnoreCase) && value.Length >= 8)
            {
                return $"{value[..4]}-{value.Substring(4, 2)}-{value.Substring(6, 2)}";
            }
            return value.Trim();
        }

        private static string NormalizeBytes(string value)
        {
            return long.TryParse(value, out var bytes) ? SystemService.FormatBytes(bytes) : value;
        }

        private static string FormatSpeed(long bitsPerSecond)
        {
            if (bitsPerSecond <= 0) return "Unknown speed";
            if (bitsPerSecond >= 1_000_000_000) return $"{bitsPerSecond / 1_000_000_000.0:F1} Gbps";
            if (bitsPerSecond >= 1_000_000) return $"{bitsPerSecond / 1_000_000.0:F0} Mbps";
            return $"{bitsPerSecond / 1_000.0:F0} Kbps";
        }
    }

    public partial class HardwareCategoryVM : ObservableObject
    {
        public string IconGlyph { get; set; } = "\uE950";
        public string Title { get; set; } = "";
        public string Summary { get; set; } = "";
        public ObservableCollection<HardwareDetailVM> Rows { get; } = new();
    }

    public record HardwareDetailVM(string Label, string Value);

    internal record HardwareSnapshot(System.Collections.Generic.Dictionary<string, object> Info, ObservableCollection<HardwareCategoryVM> Categories);
}
