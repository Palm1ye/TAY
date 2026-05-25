using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Windows.ApplicationModel.DataTransfer;
using TAY.ViewModels;

namespace TAY.Views
{
    public sealed partial class DiskView : Page
    {
        public DiskViewModel ViewModel { get; }

        public DiskView()
        {
            ViewModel = new DiskViewModel();
            this.InitializeComponent();
        }

        private void RescanLast_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.RescanLast();
        }

        private void CopyReport_Click(object sender, RoutedEventArgs e)
        {
            var lines = new List<string>
            {
                "TAY DISK REPORT",
                $"STATUS: {ViewModel.ScanStatus}",
                "",
                "DRIVES:"
            };

            foreach (var drive in ViewModel.Drives)
            {
                lines.Add($"{drive.Letter} {drive.Label} - {drive.CapacityStr}");
            }

            lines.Add("");
            lines.Add("LARGE FILES:");
            foreach (var file in ViewModel.LargeFiles)
            {
                lines.Add($"{file.SizeStr}\t{file.Name}\t{file.Path}");
            }

            var data = new DataPackage();
            data.SetText(string.Join(Environment.NewLine, lines));
            Clipboard.SetContent(data);
        }

        private void OpenFileLocation_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.DataContext is not LargeFileVM file)
            {
                return;
            }

            try
            {
                if (File.Exists(file.Path))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        ArgumentList = { "/select,", file.Path },
                        UseShellExecute = true
                    });
                    return;
                }

                if (Directory.Exists(file.FolderPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = file.FolderPath,
                        UseShellExecute = true
                    });
                }
            }
            catch
            {
            }
        }

        private void CopyFilePath_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.DataContext is not LargeFileVM file)
            {
                return;
            }

            var data = new DataPackage();
            data.SetText(file.Path);
            Clipboard.SetContent(data);
        }
    }
}
