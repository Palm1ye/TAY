using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using TAY.Services;

namespace TAY
{
    public sealed partial class MainWindow : Window
    {
        public bool AllowClose { get; set; }

        private readonly Dictionary<string, Type> _routes = new()
        {
            ["Optimize"] = typeof(Views.DashboardView),
            ["Boost"] = typeof(Views.BoostView),
            ["Clean"] = typeof(Views.CleanerView),
            ["Startup"] = typeof(Views.StartupView),
            ["Processes"] = typeof(Views.ProcessView),
            ["Storage"] = typeof(Views.DiskView),
            ["Hardware"] = typeof(Views.HardwareView),
            ["Settings"] = typeof(Views.SettingsView)
        };
        private bool _isSidebarCompact;

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        public ObservableCollection<string> LogLines => RealTimeLogService.Instance.LogLines;

        public MainWindow()
        {
            this.InitializeComponent();
            Title = "TAY System Optimizer";
            this.SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();

            ConfigureWindow();
            RootFrame.Navigated += (_, _) => SyncSelectedTabWithCurrentPage();
            UpdateTabVisuals(TabOptimize);
            Navigate("Optimize");

            // Initialize the central logging dispatcher on the primary thread context
            RealTimeLogService.Instance.Initialize(this.DispatcherQueue);
            LogLines.CollectionChanged += LogLines_CollectionChanged;
        }

        private void ConfigureWindow()
        {
            try
            {
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
                var appWindow = AppWindow.GetFromWindowId(windowId);

                appWindow.Resize(new SizeInt32(1220, 760));
                appWindow.SetIcon("Assets\\tay.ico");
                appWindow.Closing += AppWindow_Closing;

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

        private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
        {
            if (AllowClose) return;

            args.Cancel = true;
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            ShowWindow(hWnd, 0);
            RealTimeLogService.Instance.Log("TAY is still running in the system tray. Use tray menu > Exit to close it.");
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
            TabBoost.Style = inactiveStyle;
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
            SidebarColumn.Width = new GridLength(_isSidebarCompact ? 56 : 224);
            SidebarButtonsPanel.Orientation = _isSidebarCompact ? Orientation.Vertical : Orientation.Horizontal;

            var labelVisibility = _isSidebarCompact ? Visibility.Collapsed : Visibility.Visible;
            AppTitleText.Visibility = labelVisibility;
            DashboardLabel.Visibility = labelVisibility;
            BoostLabel.Visibility = labelVisibility;
            HardwareLabel.Visibility = labelVisibility;
            StartupLabel.Visibility = labelVisibility;
            CleanerLabel.Visibility = labelVisibility;
            DiskLabel.Visibility = labelVisibility;
            ProcessesLabel.Visibility = labelVisibility;
            SettingsLabel.Visibility = labelVisibility;

            // Adjust button padding and centering when compact to prevent clipping
            var buttonPadding = _isSidebarCompact ? new Thickness(0) : new Thickness(12, 0, 12, 0);
            var contentAlignment = _isSidebarCompact ? HorizontalAlignment.Center : HorizontalAlignment.Left;
            double iconSpacing = _isSidebarCompact ? 0 : 12;

            var buttons = new List<Button> { TabOptimize, TabBoost, TabHardware, TabStartup, TabClean, TabStorage, TabProcesses, TabSettings };
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

            // Handle compact/expanded transitions for Sidebar Terminal
            if (_isSidebarCompact)
            {
                SidebarTerminalExpanded.Visibility = Visibility.Collapsed;
                SidebarTerminalCollapsed.Visibility = Visibility.Visible;
                SidebarTerminalPanel.Padding = new Thickness(0);
                SidebarTerminalPanel.Margin = new Thickness(6, 0, 6, 8);
                SidebarTerminalPanel.BorderThickness = new Thickness(0);
                SidebarTerminalPanel.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            }
            else
            {
                SidebarTerminalExpanded.Visibility = Visibility.Visible;
                SidebarTerminalCollapsed.Visibility = Visibility.Collapsed;
                SidebarTerminalPanel.Padding = new Thickness(10, 8, 10, 8);
                SidebarTerminalPanel.Margin = new Thickness(8, 0, 8, 8);
                SidebarTerminalPanel.BorderThickness = new Thickness(1);
                SidebarTerminalPanel.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 7, 15, 26)); // matches #070F1A
            }
        }

        private void ToggleSidebarFromTerminal_Click(object sender, RoutedEventArgs e)
        {
            if (_isSidebarCompact)
            {
                ToggleSidebarButton_Click(sender, e);
            }
        }

        private void SyncSelectedTabWithCurrentPage()
        {
            var pageType = RootFrame.CurrentSourcePageType;
            if (pageType == typeof(Views.DashboardView)) UpdateTabVisuals(TabOptimize);
            else if (pageType == typeof(Views.BoostView)) UpdateTabVisuals(TabBoost);
            else if (pageType == typeof(Views.CleanerView)) UpdateTabVisuals(TabClean);
            else if (pageType == typeof(Views.StartupView)) UpdateTabVisuals(TabStartup);
            else if (pageType == typeof(Views.ProcessView)) UpdateTabVisuals(TabProcesses);
            else if (pageType == typeof(Views.DiskView)) UpdateTabVisuals(TabStorage);
            else if (pageType == typeof(Views.HardwareView)) UpdateTabVisuals(TabHardware);
            else if (pageType == typeof(Views.SettingsView)) UpdateTabVisuals(TabSettings);
        }

        private void LogLines_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                if (TerminalListView.Items.Count > 0)
                {
                    TerminalListView.ScrollIntoView(TerminalListView.Items[TerminalListView.Items.Count - 1]);
                }
            });
        }

        private void ClearTerminal_Click(object sender, RoutedEventArgs e)
        {
            RealTimeLogService.Instance.Clear();
        }
    }
}
