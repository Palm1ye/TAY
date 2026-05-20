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
    }
}
