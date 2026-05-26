using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using TAY.Services;

namespace TAY.Views
{
    public sealed partial class ActivityWindow : Window
    {
        public ObservableCollection<string> LogLines => RealTimeLogService.Instance.LogLines;

        public ActivityWindow()
        {
            InitializeComponent();
            SystemBackdrop = new MicaBackdrop();

            try
            {
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
                var appWindow = AppWindow.GetFromWindowId(windowId);
                appWindow.Resize(new Windows.Graphics.SizeInt32(430, 560));
                appWindow.SetIcon("Assets\\tay.ico");
            }
            catch { }

            LogLines.CollectionChanged += LogLines_CollectionChanged;
            Closed += (_, _) => LogLines.CollectionChanged -= LogLines_CollectionChanged;
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            RealTimeLogService.Instance.Clear();
        }

        private void LogLines_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (ActivityList.Items.Count > 0)
            {
                ActivityList.ScrollIntoView(ActivityList.Items[ActivityList.Items.Count - 1]);
            }
        }
    }
}
