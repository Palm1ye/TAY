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
                "CHANNEL: stable"
            });

            var data = new DataPackage();
            data.SetText(text);
            Clipboard.SetContent(data);
        }
    }
}
