using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;
using TAY.ViewModels;

namespace TAY.Views
{
    public sealed partial class ProcessView : Page
    {
        public ProcessViewModel ViewModel { get; }

        public ProcessView()
        {
            ViewModel = new ProcessViewModel();
            this.InitializeComponent();
        }

        private void CopyProcessList_Click(object sender, RoutedEventArgs e)
        {
            var lines = ViewModel.Processes
                .Select(p => $"{p.Id}\t{p.Name}\t{p.RamStr}");

            var data = new DataPackage();
            data.SetText(string.Join(Environment.NewLine, lines));
            Clipboard.SetContent(data);
        }

        private async void EndTask_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.DataContext is not ProcessItemVM process)
            {
                return;
            }

            var dialog = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Title = "End process?",
                Content = $"This will force {process.Name} (PID {process.Id}) to exit. Unsaved work in that process may be lost.",
                PrimaryButtonText = "End task",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await ViewModel.KillAsync(process);
            }
        }
    }
}
