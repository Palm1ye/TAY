using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using TAY.Services;
using TAY.ViewModels;
using Windows.System;

namespace TAY
{
    public sealed partial class MainWindow : Window
    {
        public bool AllowClose { get; set; }

        private readonly Dictionary<string, Type> _routes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Dashboard"] = typeof(Views.DashboardView),
            ["Boost"] = typeof(Views.BoostView),
            ["Hardware"] = typeof(Views.HardwareView),
            ["Startup"] = typeof(Views.StartupView),
            ["Cleaner"] = typeof(Views.CleanerView),
            ["Disk"] = typeof(Views.DiskView),
            ["Processes"] = typeof(Views.ProcessView),
            ["Network"] = typeof(Views.NetworkView),
            ["Activity"] = typeof(Views.ActivityView),
            ["Settings"] = typeof(Views.SettingsView)
        };

        private readonly DispatcherTimer _statusTimer = new();
        private bool _isPinned;

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_TOPMOST = new(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new(-2);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;

        public ObservableCollection<string> LogLines => RealTimeLogService.Instance.LogLines;

        public MainWindow()
        {
            InitializeComponent();

            Title = "TAY Optimizer";
            SystemBackdrop = new MicaBackdrop();

            RegisterKeyboardShortcuts();
            RealTimeLogService.Instance.Initialize(DispatcherQueue);
            ConfigureWindow();
            SettingsViewModel.Instance.PinWindowOnTopChanged += SetPinned;

            RootFrame.Navigated += (_, _) => SyncSelectedTabWithCurrentPage();
            Navigate("Dashboard");

            _statusTimer.Interval = TimeSpan.FromSeconds(3);
            _statusTimer.Tick += (_, _) => UpdateFooterMetrics();
            _statusTimer.Start();
            UpdateFooterMetrics();
        }

        private void RegisterKeyboardShortcuts()
        {
            var searchAccelerator = new KeyboardAccelerator
            {
                Key = VirtualKey.K,
                Modifiers = VirtualKeyModifiers.Control
            };
            searchAccelerator.Invoked += SearchAccelerator_Invoked;
            RootGrid.KeyboardAccelerators.Add(searchAccelerator);
        }

        private void ConfigureWindow()
        {
            try
            {
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
                var appWindow = AppWindow.GetFromWindowId(windowId);

                appWindow.Resize(new Windows.Graphics.SizeInt32(500, 680));
                appWindow.SetIcon("Assets\\tay.ico");
                appWindow.Closing += AppWindow_Closing;

                ExtendsContentIntoTitleBar = true;
                SetTitleBar(AppTitleBar);

                if (AppWindowTitleBar.IsCustomizationSupported())
                {
                    var titleBar = appWindow.TitleBar;
                    titleBar.ExtendsContentIntoTitleBar = true;

                    var shell = Windows.UI.Color.FromArgb(255, 32, 32, 32);
                    var text = Windows.UI.Color.FromArgb(255, 243, 243, 243);
                    var dim = Windows.UI.Color.FromArgb(255, 160, 160, 160);
                    var accent = Windows.UI.Color.FromArgb(255, 73, 199, 247);

                    titleBar.BackgroundColor = shell;
                    titleBar.InactiveBackgroundColor = shell;
                    titleBar.ForegroundColor = text;
                    titleBar.InactiveForegroundColor = dim;
                    titleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
                    titleBar.ButtonInactiveBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
                    titleBar.ButtonForegroundColor = text;
                    titleBar.ButtonInactiveForegroundColor = dim;
                    titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(255, 48, 48, 48);
                    titleBar.ButtonHoverForegroundColor = accent;
                    titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(255, 58, 58, 58);
                    titleBar.ButtonPressedForegroundColor = accent;
                }

                if (appWindow.Presenter is OverlappedPresenter presenter)
                {
                    presenter.IsResizable = true;
                    presenter.IsMaximizable = true;
                    presenter.IsMinimizable = true;
                }
            }
            catch
            {
                // Window chrome customization is best-effort across Windows builds.
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

            foreach (var button in GetTabButtons())
            {
                button.Style = ReferenceEquals(button, activeButton) ? activeStyle : inactiveStyle;
            }
        }

        private IEnumerable<Button> GetTabButtons()
        {
            yield return TabDashboard;
            yield return TabBoost;
            yield return TabHardware;
            yield return TabStartup;
            yield return TabCleaner;
            yield return TabDisk;
            yield return TabProcesses;
            yield return TabNetwork;
            yield return TabActivity;
            yield return TabSettings;
        }

        private void SyncSelectedTabWithCurrentPage()
        {
            var pageType = RootFrame.CurrentSourcePageType;

            if (pageType == typeof(Views.DashboardView)) UpdateTabVisuals(TabDashboard);
            else if (pageType == typeof(Views.BoostView)) UpdateTabVisuals(TabBoost);
            else if (pageType == typeof(Views.HardwareView)) UpdateTabVisuals(TabHardware);
            else if (pageType == typeof(Views.StartupView)) UpdateTabVisuals(TabStartup);
            else if (pageType == typeof(Views.CleanerView)) UpdateTabVisuals(TabCleaner);
            else if (pageType == typeof(Views.DiskView)) UpdateTabVisuals(TabDisk);
            else if (pageType == typeof(Views.ProcessView)) UpdateTabVisuals(TabProcesses);
            else if (pageType == typeof(Views.NetworkView)) UpdateTabVisuals(TabNetwork);
            else if (pageType == typeof(Views.ActivityView)) UpdateTabVisuals(TabActivity);
            else if (pageType == typeof(Views.SettingsView)) UpdateTabVisuals(TabSettings);
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            NavigateFromSearch(SearchBox.Text);
        }

        private void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                NavigateFromSearch(SearchBox.Text, force: true);
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Escape)
            {
                SearchBox.Text = "";
                RootFrame.Focus(FocusState.Programmatic);
                e.Handled = true;
            }
        }

        private void SearchAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            SearchBox.Focus(FocusState.Programmatic);
            SearchBox.SelectAll();
            args.Handled = true;
        }

        private bool NavigateFromSearch(string text, bool force = false)
        {
            var query = text.Trim().ToLowerInvariant();
            if (!force && query.Length < 2) return false;
            if (query.Length == 0) return false;

            var route = query switch
            {
                var q when Matches(q, "dashboard", "home", "status", "system") => "Dashboard",
                var q when Matches(q, "boost", "boost tuning", "game", "ram", "memory", "dns") => "Boost",
                var q when Matches(q, "hardware", "driver", "drivers", "device", "cpu", "gpu") => "Hardware",
                var q when Matches(q, "startup", "boot", "sign in", "launch") => "Startup",
                var q when Matches(q, "cleaner", "clean", "cache", "temp", "trash") => "Cleaner",
                var q when Matches(q, "disk", "storage", "drive", "volume") => "Disk",
                var q when Matches(q, "processes", "process", "task", "cpu") => "Processes",
                var q when Matches(q, "network", "wifi", "wi-fi", "adapter", "tailscale", "internet") => "Network",
                var q when Matches(q, "activity", "log", "logs", "events") => "Activity",
                var q when Matches(q, "settings", "setting", "ayar", "ayarlar", "update") => "Settings",
                _ => null
            };

            if (route != null)
            {
                Navigate(route);
                return true;
            }

            return false;
        }

        private static bool Matches(string query, params string[] aliases)
        {
            foreach (var alias in aliases)
            {
                if (alias.StartsWith(query, StringComparison.OrdinalIgnoreCase) ||
                    query.Contains(alias, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void PinButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsViewModel.Instance.PinWindowOnTop = !_isPinned;
        }

        private void SetPinned(bool value)
        {
            if (_isPinned == value)
            {
                return;
            }

            _isPinned = value;
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            SetWindowPos(
                hWnd,
                _isPinned ? HWND_TOPMOST : HWND_NOTOPMOST,
                0,
                0,
                0,
                0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

            PinButton.Foreground = (Brush)Application.Current.Resources[_isPinned ? "AccentMain" : "TextDim"];
            RealTimeLogService.Instance.Log(_isPinned ? "Window pinned on top." : "Window unpinned.");
        }

        private void UpdateFooterMetrics()
        {
            Task.Run(() =>
            {
                var cpu = SystemService.GetCpuUsage();
                var ram = SystemService.GetRamInfo().percent;
                var gpu = SystemService.GetGpuUsage();

                DispatcherQueue.TryEnqueue(() =>
                {
                    FooterCpuText.Text = $"C {cpu}%";
                    FooterMemoryText.Text = $"M {ram}%";
                    FooterGpuText.Text = $"G {gpu}%";
                });
            });
        }
    }
}
