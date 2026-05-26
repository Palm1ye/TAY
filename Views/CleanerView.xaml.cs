using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System.Linq;
using TAY.ViewModels;

namespace TAY.Views
{
    public sealed partial class CleanerView : Page
    {
        public CleanerViewModel ViewModel { get; }

        public CleanerView()
        {
            ViewModel = new CleanerViewModel();
            this.InitializeComponent();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var folder in ViewModel.Folders)
            {
                folder.IsSelected = true;
            }
            ViewModel.RefreshSelectionSummary();
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var folder in ViewModel.Folders)
            {
                folder.IsSelected = false;
            }
            ViewModel.RefreshSelectionSummary();
        }

        private async void CleanSelected_Click(object sender, RoutedEventArgs e)
        {
            var selectedCount = ViewModel.Folders.Count(f => f.IsSelected);
            if (selectedCount == 0)
            {
                var emptyDialog = new ContentDialog
                {
                    XamlRoot = this.XamlRoot,
                    Title = "No cleanup targets selected",
                    Content = "Select at least one cleanup target before running the cleaner.",
                    CloseButtonText = "OK"
                };
                await emptyDialog.ShowAsync();
                return;
            }

            var dialog = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Title = "Confirm cleanup",
                Content = $"TAY will delete temporary/cache contents from {selectedCount} selected target(s). Locked files are skipped, but deleted files cannot be restored from TAY.",
                PrimaryButtonText = "Clean selected",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && ViewModel.CleanCommand.CanExecute(null))
            {
                ViewModel.CleanCommand.Execute(null);
            }
        }

        private async void Preview_Click(object sender, RoutedEventArgs e)
        {
            var selected = ViewModel.Folders.Where(f => f.IsSelected).ToList();
            var preview = selected.Count == 0
                ? "No cleanup targets are selected."
                : string.Join("\n", selected.Select(f => $"{f.Name} - {f.SizeStr}"));

            var dialog = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Title = "Cleanup preview",
                Content = preview,
                CloseButtonText = "OK"
            };

            await dialog.ShowAsync();
        }
    }
}
