using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TAY.ViewModels;
using TAY.Services;

namespace TAY.Views
{
    public sealed partial class BoostView : Page
    {
        public BoostViewModel ViewModel { get; } = new();

        public BoostView()
        {
            this.InitializeComponent();
            this.DataContext = ViewModel;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            Unloaded += (_, _) => ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(BoostViewModel.RamCleanLog) or nameof(BoostViewModel.IsRamPanelVisible) or nameof(BoostViewModel.IsRamCleaning))
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    BoostScrollViewer.UpdateLayout();
                    BoostScrollViewer.ChangeView(null, BoostScrollViewer.ScrollableHeight, null, true);
                });
            }
        }

        private void OnContextMenuToggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch ts && ts.DataContext is ContextMenuItem item)
            {
                // Verify toggled state is actually changing from its currently recorded state
                // to prevent circular trigger cascades.
                if (ts.IsOn != item.IsEnabled)
                {
                    _ = ViewModel.ToggleContextMenuHandlerCommand.ExecuteAsync(item);
                }
            }
        }

        private async void ApplyRecommendedDns_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Title = "Apply recommended DNS?",
                Content = "Do not continue if you use a DNS service to bypass website blocks, filtering, parental controls, ad blocking, or custom routing. Applying this profile will overwrite DNS server settings on active adapters. You can restore DHCP later from this screen.",
                PrimaryButtonText = "Apply DNS",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && ViewModel.ApplyRecommendedDnsCommand.CanExecute(null))
            {
                await ViewModel.ApplyRecommendedDnsCommand.ExecuteAsync(null);
            }
        }
    }
}
