using CommunityToolkit.Mvvm.ComponentModel;
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
            var info = await Task.Run(() => SystemService.GetHardwareInfo());
            _dispatcher?.TryEnqueue(() =>
            {
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
            });
        }
    }
}
