using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
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
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var folder in ViewModel.Folders)
            {
                folder.IsSelected = false;
            }
        }
    }
}
