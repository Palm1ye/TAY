using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System;
using Windows.ApplicationModel.DataTransfer;
using TAY.ViewModels;

namespace TAY.Views
{
    public sealed partial class SettingsView : Page
    {
        public SettingsViewModel ViewModel { get; }

        public SettingsView()
        {
            ViewModel = SettingsViewModel.Instance;
            this.InitializeComponent();
        }

        private void CopyInfo_Click(object sender, RoutedEventArgs e)
        {
            var text = string.Join(Environment.NewLine, new[]
            {
                "TAY APP INFO",
                $"NAME: {ViewModel.AppName}",
                $"VERSION: {ViewModel.AppVersion}",
                $"CHANNEL: {ViewModel.AppChannel}",
                $"PRIVILEGES: {ViewModel.ElevationStatus}"
            });

            var data = new DataPackage();
            data.SetText(text);
            Clipboard.SetContent(data);
        }

        private void CopyDiagnostics_Click(object sender, RoutedEventArgs e)
        {
            var text = string.Join(Environment.NewLine, new[]
            {
                "TAY DIAGNOSTICS",
                $"APP: {ViewModel.AppName} {ViewModel.AppVersion} ({ViewModel.AppChannel})",
                $"WINDOWS: {ViewModel.WindowsInfo}",
                $"RUNTIME: {ViewModel.RuntimeInfo}",
                $"ARCHITECTURE: {ViewModel.ArchitectureInfo}",
                $"PRIVILEGES: {ViewModel.ElevationStatus}",
                $"HARDWARE: {ViewModel.HardwareSummary}",
                $"APP DATA: {ViewModel.AppDataPath}",
                $"BACKUPS: {ViewModel.BackupFiles}",
                $"UPDATE STATUS: {ViewModel.UpdateStatus}"
            });

            var data = new DataPackage();
            data.SetText(text);
            Clipboard.SetContent(data);
        }

        private async void ClearBackups_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Title = "Clear local restore backups?",
                Content = "This removes TAY's local DNS, telemetry, and game booster restore backup files. Only use this when you no longer need to revert those changes from the saved state.",
                PrimaryButtonText = "Clear backups",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && ViewModel.ClearLocalBackupsCommand.CanExecute(null))
            {
                ViewModel.ClearLocalBackupsCommand.Execute(null);
            }
        }
    }
}
