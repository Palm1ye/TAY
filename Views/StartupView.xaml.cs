using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using TAY.ViewModels;

namespace TAY.Views
{
    public sealed partial class StartupView : Page
    {
        public StartupViewModel ViewModel { get; }

        public StartupView()
        {
            ViewModel = new StartupViewModel();
            this.InitializeComponent();
        }

        private void EnableAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var app in ViewModel.Apps)
            {
                app.IsEnabled = true;
            }
        }

        private void DisableAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var app in ViewModel.Apps)
            {
                app.IsEnabled = false;
            }
        }
    }
}
