using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using System;
using System.Collections.Generic;

namespace TAY
{
    public sealed partial class MainWindow : Window
    {
        private readonly Dictionary<string, Type> _routes = new()
        {
            ["Optimize"] = typeof(Views.DashboardView),
            ["Clean"] = typeof(Views.CleanerView),
            ["Startup"] = typeof(Views.StartupView),
            ["Processes"] = typeof(Views.ProcessView),
            ["Storage"] = typeof(Views.DiskView),
            ["Hardware"] = typeof(Views.HardwareView),
            ["Settings"] = typeof(Views.SettingsView)
        };
        private bool _isSidebarCompact;

        public MainWindow()
        {
            this.InitializeComponent();
            Title = "TAY System Optimizer";
            this.SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();

            ConfigureWindow();
            UpdateTabVisuals(TabOptimize);
            Navigate("Optimize");
        }

        private void ConfigureWindow()
        {
            try
            {
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
                var appWindow = AppWindow.GetFromWindowId(windowId);

                appWindow.Resize(new SizeInt32(1180, 760));
                appWindow.SetIcon("Assets\\tay.ico");

                this.ExtendsContentIntoTitleBar = true;
                this.SetTitleBar(AppTitleBar);

                if (AppWindowTitleBar.IsCustomizationSupported())
                {
                    var titleBar = appWindow.TitleBar;
                    titleBar.ExtendsContentIntoTitleBar = true;

                    var bgSidebarColor = Windows.UI.Color.FromArgb(255, 13, 27, 42); // #0D1B2A (BgSidebar)
                    
                    titleBar.BackgroundColor = bgSidebarColor;
                    titleBar.InactiveBackgroundColor = bgSidebarColor;

                    // Button backgrounds transparent
                    titleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
                    titleBar.ButtonInactiveBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);

                    // Foreground Colors (White / Dim)
                    var textColor = Windows.UI.Color.FromArgb(255, 232, 237, 243); // #E8EDF3 (TextMain)
                    var textDimColor = Windows.UI.Color.FromArgb(255, 136, 153, 170); // #8899AA (TextDim)
                    var tealCyan = Windows.UI.Color.FromArgb(255, 74, 234, 220); // #4AEADC (AccentMain)

                    titleBar.ForegroundColor = textColor;
                    titleBar.InactiveForegroundColor = textDimColor;

                    titleBar.ButtonForegroundColor = textColor;
                    titleBar.ButtonInactiveForegroundColor = textDimColor;

                    // Hover States
                    titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(20, 255, 255, 255);
                    titleBar.ButtonHoverForegroundColor = tealCyan;

                    // Pressed States
                    titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(40, 255, 255, 255);
                    titleBar.ButtonPressedForegroundColor = tealCyan;
                }

                if (appWindow.Presenter is OverlappedPresenter presenter)
                {
                    presenter.Minimize();
                    presenter.Restore();
                    presenter.IsResizable = true;
                    presenter.IsMaximizable = true;
                    presenter.IsMinimizable = true;
                }
            }
            catch
            {
                // Window customization is best-effort on older Windows builds.
            }
        }

        private void Navigate(string route)
        {
            if (_routes.TryGetValue(route, out var pageType) && RootFrame.CurrentSourcePageType != pageType)
            {
                RootFrame.Navigate(pageType);
            }
        }

        private void OnTabClicked(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string route)
            {
                UpdateTabVisuals(button);
                Navigate(route);
            }
        }

        private void UpdateTabVisuals(Button activeButton)
        {
            var activeStyle = (Style)RootGrid.Resources["ActiveTabStyle"];
            var inactiveStyle = (Style)RootGrid.Resources["InactiveTabStyle"];

            TabOptimize.Style = inactiveStyle;
            TabClean.Style = inactiveStyle;
            TabStartup.Style = inactiveStyle;
            TabProcesses.Style = inactiveStyle;
            TabStorage.Style = inactiveStyle;
            TabHardware.Style = inactiveStyle;
            TabSettings.Style = inactiveStyle;

            activeButton.Style = activeStyle;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (RootFrame.CanGoBack)
            {
                RootFrame.GoBack();
                SyncSelectedTabWithCurrentPage();
            }
        }

        private void ToggleSidebarButton_Click(object sender, RoutedEventArgs e)
        {
            _isSidebarCompact = !_isSidebarCompact;
            SidebarColumn.Width = new GridLength(_isSidebarCompact ? 56 : 250);
            SidebarButtonsPanel.Orientation = _isSidebarCompact ? Orientation.Vertical : Orientation.Horizontal;

            var labelVisibility = _isSidebarCompact ? Visibility.Collapsed : Visibility.Visible;
            AppTitleText.Visibility = labelVisibility;
            DashboardLabel.Visibility = labelVisibility;
            HardwareLabel.Visibility = labelVisibility;
            StartupLabel.Visibility = labelVisibility;
            CleanerLabel.Visibility = labelVisibility;
            DiskLabel.Visibility = labelVisibility;
            ProcessesLabel.Visibility = labelVisibility;
            SettingsLabel.Visibility = labelVisibility;

            // Adjust button padding and centering when compact to prevent clipping
            var buttonPadding = _isSidebarCompact ? new Thickness(0) : new Thickness(14, 0, 14, 0);
            var contentAlignment = _isSidebarCompact ? HorizontalAlignment.Center : HorizontalAlignment.Left;
            double iconSpacing = _isSidebarCompact ? 0 : 14;

            var buttons = new List<Button> { TabOptimize, TabHardware, TabStartup, TabClean, TabStorage, TabProcesses, TabSettings };
            foreach (var btn in buttons)
            {
                btn.Padding = buttonPadding;
                btn.HorizontalContentAlignment = contentAlignment;
                if (btn.Content is StackPanel sp)
                {
                    sp.Spacing = iconSpacing;
                }
            }

            // Dynamically center the App Logo inside the collapsed column
            AppLogoPanel.Margin = _isSidebarCompact ? new Thickness(18, 0, 0, 0) : new Thickness(14, 0, 0, 0);
        }

        private void SyncSelectedTabWithCurrentPage()
        {
            var pageType = RootFrame.CurrentSourcePageType;
            if (pageType == typeof(Views.DashboardView)) UpdateTabVisuals(TabOptimize);
            else if (pageType == typeof(Views.CleanerView)) UpdateTabVisuals(TabClean);
            else if (pageType == typeof(Views.StartupView)) UpdateTabVisuals(TabStartup);
            else if (pageType == typeof(Views.ProcessView)) UpdateTabVisuals(TabProcesses);
            else if (pageType == typeof(Views.DiskView)) UpdateTabVisuals(TabStorage);
            else if (pageType == typeof(Views.HardwareView)) UpdateTabVisuals(TabHardware);
            else if (pageType == typeof(Views.SettingsView)) UpdateTabVisuals(TabSettings);
        }
    }
}
