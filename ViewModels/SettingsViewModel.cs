using CommunityToolkit.Mvvm.ComponentModel;

namespace TAY.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        public string AppVersion => "0.1.0";
        public string AppName => "TAY Optimizer";
    }
}
