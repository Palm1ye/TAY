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

        private async void EnableAll_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Title = "Enable all startup entries?",
                Content = "This will mark every listed startup entry as enabled. Boot time may increase.",
                PrimaryButtonText = "Enable all",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            foreach (var app in ViewModel.Apps)
            {
                app.IsEnabled = true;
            }
        }

        private async void DisableAll_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Title = "Disable all startup entries?",
                Content = "This will disable every listed startup entry that TAY can control. Security, driver, or sync utilities may stop launching automatically.",
                PrimaryButtonText = "Disable all",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            foreach (var app in ViewModel.Apps)
            {
                app.IsEnabled = false;
            }
        }

        private async void DisableHighImpact_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Title = "Disable high-impact startup apps?",
                Content = "Only enabled entries marked HIGH will be disabled. You can re-enable them from this list.",
                PrimaryButtonText = "Disable",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            foreach (var app in ViewModel.Apps)
            {
                if (app.IsEnabled && app.Impact.Equals("HIGH", System.StringComparison.OrdinalIgnoreCase))
                {
                    app.IsEnabled = false;
                }
            }
        }
    }
}
