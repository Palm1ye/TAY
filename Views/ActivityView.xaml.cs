using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using TAY.Services;

namespace TAY.Views
{
    public sealed partial class ActivityView : Page
    {
        private static ActivityWindow? _detachedWindow;

        public ObservableCollection<string> LogLines => RealTimeLogService.Instance.LogLines;

        public ActivityView()
        {
            InitializeComponent();
            LogLines.CollectionChanged += LogLines_CollectionChanged;
            Unloaded += (_, _) => LogLines.CollectionChanged -= LogLines_CollectionChanged;
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            RealTimeLogService.Instance.Clear();
        }

        private void Filter_Click(object sender, RoutedEventArgs e)
        {
            if (ActivityList.Items.Count > 0)
            {
                ActivityList.ScrollIntoView(ActivityList.Items[ActivityList.Items.Count - 1]);
            }
        }

        private void OpenDetached_Click(object sender, RoutedEventArgs e)
        {
            if (_detachedWindow == null)
            {
                _detachedWindow = new ActivityWindow();
                _detachedWindow.Closed += (_, _) => _detachedWindow = null;
            }

            _detachedWindow.Activate();
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
