using Microsoft.UI.Xaml.Controls;
using TAY.ViewModels;

namespace TAY.Views
{
    public sealed partial class NetworkView : Page
    {
        public NetworkViewModel ViewModel { get; } = new();

        public NetworkView()
        {
            InitializeComponent();
        }

        private void Page_Unloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            ViewModel.Cleanup();
        }
    }
}
