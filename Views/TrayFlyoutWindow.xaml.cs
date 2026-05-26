using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Windowing;
using Microsoft.UI;
using System;
using System.Runtime.InteropServices;
using System.Timers;
using TAY.Services;
using Windows.UI;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace TAY.Views
{
    public sealed partial class TrayFlyoutWindow : Window
    {
        private readonly System.Timers.Timer _timer;
        private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcher;

        private TextBlock? _cpuText;
        private ProgressBar? _cpuProgress;
        private TextBlock? _ramText;
        private ProgressBar? _ramProgress;
        private TextBlock? _gpuText;
        private ProgressBar? _gpuProgress;
        private TextBlock? _uptimeText;
        private TextBlock? _statusText;
        private Border? _rootBorder;
        private Button? _boostBtn;
        private Button? _gameModeBtn;
        private Button? _dnsBtn;
        private Button? _transparentBtn;
        private bool _isTransparentMode;
        private readonly Brush _solidFlyoutBackground = new SolidColorBrush(Color.FromArgb(232, 17, 21, 28));
        private readonly Brush _transparentFlyoutBackground = new SolidColorBrush(Colors.Transparent);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowPos", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        private const int GWL_STYLE = -16;
        private const int WS_CAPTION = 0x00C00000;
        private const int WS_THICKFRAME = 0x00040000; // Resize border
        private const int WS_MINIMIZEBOX = 0x00020000;
        private const int WS_MAXIMIZEBOX = 0x00010000;
        private const int WS_SYSMENU = 0x00080000;
        private const int WS_BORDER = 0x00800000;
        private const int WS_DLGFRAME = 0x00400000;

        private const uint SWP_FRAMECHANGED = 0x0020;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        private bool _isDragging = false;
        private POINT _dragStartMousePos;
        private Windows.Graphics.PointInt32 _dragStartWindowPos;

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool GetMonitorInfoW(IntPtr hMonitor, ref MONITORINFO lpmi);

        private const uint MONITOR_DEFAULTTOPRIMARY = 1;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }

        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);

        public TrayFlyoutWindow()
        {
            _dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            
            BuildUI();
            ConfigureBorderless();

            this.Closed += (s, e) => Cleanup();

            _timer = new System.Timers.Timer(2000);
            _timer.Elapsed += OnTimerElapsed;
            _timer.Start();

            UpdateStats();
        }

        private void BuildUI()
        {
            var accentMain = new SolidColorBrush(Color.FromArgb(255, 88, 213, 201));
            var accentInfo = new SolidColorBrush(Color.FromArgb(255, 122, 167, 255));
            var accentWarning = new SolidColorBrush(Color.FromArgb(255, 242, 198, 109));
            var accentDanger = new SolidColorBrush(Color.FromArgb(255, 244, 124, 124));
            var textMain = new SolidColorBrush(Color.FromArgb(255, 244, 244, 245));
            var textDim = new SolidColorBrush(Color.FromArgb(255, 161, 161, 170));

            var root = new Grid { Background = new SolidColorBrush(Colors.Transparent) };
            var border = new Border
            {
                Background = _solidFlyoutBackground,
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(58, 255, 255, 255)),
                Padding = new Thickness(14),
                Margin = new Thickness(2)
            };
            _rootBorder = border;
            border.PointerPressed += Border_PointerPressed;
            border.PointerMoved += Border_PointerMoved;
            border.PointerReleased += Border_PointerReleased;

            var main = new StackPanel { Spacing = 10 };

            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var iconFrame = new Border
            {
                Width = 34,
                Height = 34,
                CornerRadius = new CornerRadius(7),
                BorderBrush = new SolidColorBrush(Color.FromArgb(90, 122, 167, 255)),
                BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(Color.FromArgb(48, 122, 167, 255))
            };
            iconFrame.Child = new Image
            {
                Width = 25,
                Height = 25,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Source = new Microsoft.UI.Xaml.Media.Imaging.SvgImageSource(new Uri("ms-appx:///Assets/tay.svg"))
            };

            var title = new TextBlock
            {
                Text = "TAY Control Mini",
                FontSize = 20,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = textMain,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0)
            };

            var closeBtn = new Button
            {
                Content = new FontIcon { Glyph = "\uE711", FontSize = 16, Foreground = textDim },
                Width = 30,
                Height = 30,
                Padding = new Thickness(0),
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(6)
            };
            closeBtn.Click += (s, e) => this.Close();

            Grid.SetColumn(iconFrame, 0);
            Grid.SetColumn(title, 1);
            Grid.SetColumn(closeBtn, 2);
            header.Children.Add(iconFrame);
            header.Children.Add(title);
            header.Children.Add(closeBtn);

            var metricsShell = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255)),
                Padding = new Thickness(10)
            };
            var metricsGrid = new Grid();
            metricsShell.Child = metricsGrid;
            for (var i = 0; i < 4; i++)
            {
                metricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            _cpuText = new TextBlock();
            _cpuProgress = new ProgressBar();
            var cpuCard = CreateMetricColumn("\uE950", "CPU", _cpuText, _cpuProgress, accentMain, "Load monitor", "Core sample");

            _ramText = new TextBlock();
            _ramProgress = new ProgressBar();
            var ramCard = CreateMetricColumn("\uE8B7", "RAM", _ramText, _ramProgress, accentWarning, "Memory use", "Sweep ready");

            _gpuText = new TextBlock();
            _gpuProgress = new ProgressBar();
            var gpuCard = CreateMetricColumn("\uE7F4", "GPU", _gpuText, _gpuProgress, accentInfo, "Engine load", "VRAM view");

            var netValue = new TextBlock
            {
                Text = "Live",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 19,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = textMain
            };
            var netProgress = new ProgressBar { Value = 42, Minimum = 0, Maximum = 100, Height = 6, CornerRadius = new CornerRadius(3), Foreground = accentMain, Background = new SolidColorBrush(Color.FromArgb(36, 255, 255, 255)) };
            var netCard = CreateMetricColumn("\uE839", "NET", netValue, netProgress, accentMain, "DNS tools", "Tray actions");

            Grid.SetColumn(cpuCard, 0);
            Grid.SetColumn(ramCard, 1);
            Grid.SetColumn(gpuCard, 2);
            Grid.SetColumn(netCard, 3);
            metricsGrid.Children.Add(cpuCard);
            metricsGrid.Children.Add(ramCard);
            metricsGrid.Children.Add(gpuCard);
            metricsGrid.Children.Add(netCard);

            var infoGrid = new Grid { ColumnSpacing = 10 };
            infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _uptimeText = new TextBlock
            {
                Text = "Uptime: 00m",
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = textMain
            };
            var bootText = new TextBlock
            {
                Text = $"Last Boot: {DateTime.Now.Subtract(SystemService.GetUptime()):MMM d, HH:mm}",
                FontSize = 12,
                Foreground = textDim
            };
            var uptimeStack = new StackPanel { Spacing = 2 };
            uptimeStack.Children.Add(_uptimeText);
            uptimeStack.Children.Add(bootText);

            var alert = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(76, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 8, 10, 8),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            var alertGrid = new Grid { ColumnSpacing = 12 };
            alertGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            alertGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var alertStack = new StackPanel();
            _statusText = new TextBlock
            {
                Text = "System Ready",
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = textMain
            };
            alertStack.Children.Add(_statusText);
            alertStack.Children.Add(new TextBlock { Text = "Live monitor active", FontSize = 11, Foreground = textDim });
            var analyzeBtn = CreatePlainButton("Processes", textMain, new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)));
            analyzeBtn.Click += OpenDashboard_Click;
            Grid.SetColumn(alertStack, 0);
            Grid.SetColumn(analyzeBtn, 1);
            alertGrid.Children.Add(alertStack);
            alertGrid.Children.Add(analyzeBtn);
            alert.Child = alertGrid;

            Grid.SetColumn(uptimeStack, 0);
            Grid.SetColumn(alert, 1);
            infoGrid.Children.Add(uptimeStack);
            infoGrid.Children.Add(alert);

            var actions = new Grid { ColumnSpacing = 10, RowSpacing = 10 };
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            actions.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            actions.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _boostBtn = CreateActionTile("\uE945", "Boost", "Memory sweep", accentMain, new SolidColorBrush(Color.FromArgb(96, 28, 205, 140)));
            _boostBtn.Click += Boost_Click;
            _gameModeBtn = CreateActionTile("\uE7FC", "Game", GameBoosterService.IsActive ? "Focus active" : "Focus ready", accentInfo, new SolidColorBrush(Color.FromArgb(74, 45, 96, 210)));
            _gameModeBtn.Click += GameMode_Click;
            _dnsBtn = CreateActionTile("\uE839", "DNS", "Flush cache", accentWarning, new SolidColorBrush(Color.FromArgb(78, 172, 111, 45)));
            _dnsBtn.Click += DnsFlush_Click;
            _transparentBtn = CreateActionTile("\uE8A1", "Overlay", "Transparent", textMain, new SolidColorBrush(Color.FromArgb(54, 255, 255, 255)));
            _transparentBtn.Click += Transparent_Click;

            Grid.SetColumn(_boostBtn, 0);
            Grid.SetColumn(_gameModeBtn, 1);
            Grid.SetRow(_dnsBtn, 1);
            Grid.SetColumn(_dnsBtn, 0);
            Grid.SetRow(_transparentBtn, 1);
            Grid.SetColumn(_transparentBtn, 1);
            actions.Children.Add(_boostBtn);
            actions.Children.Add(_gameModeBtn);
            actions.Children.Add(_dnsBtn);
            actions.Children.Add(_transparentBtn);

            var dashboardButton = CreatePlainButton("Dashboard", textMain, new SolidColorBrush(Color.FromArgb(88, 255, 255, 255)));
            dashboardButton.Height = 46;
            dashboardButton.FontSize = 18;
            dashboardButton.Click += OpenDashboard_Click;

            var quickModes = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            quickModes.Children.Add(new TextBlock { Text = "Mode", FontSize = 12, Foreground = textDim, VerticalAlignment = VerticalAlignment.Center });
            quickModes.Children.Add(CreateModePill("Eco Mode", false));
            quickModes.Children.Add(CreateModePill("Balanced Mode", false));
            quickModes.Children.Add(CreateModePill(GameBoosterService.IsActive ? "Game Focus On" : "Game Focus Off", GameBoosterService.IsActive));

            main.Children.Add(header);
            main.Children.Add(metricsShell);
            main.Children.Add(infoGrid);
            main.Children.Add(actions);
            main.Children.Add(dashboardButton);
            main.Children.Add(quickModes);

            border.Child = main;
            root.Children.Add(border);
            this.Content = root;
        }

        private static Border CreateMetricColumn(string glyph, string label, TextBlock valueText, ProgressBar progress, Brush accent, string line1, string line2)
        {
            valueText.Text = "0%";
            valueText.FontFamily = new FontFamily("Consolas");
            valueText.FontSize = 20;
            valueText.FontWeight = Microsoft.UI.Text.FontWeights.Bold;
            valueText.Foreground = new SolidColorBrush(Color.FromArgb(255, 244, 244, 245));

            progress.Minimum = 0;
            progress.Maximum = 100;
            progress.Height = 5;
            progress.CornerRadius = new CornerRadius(3);
            progress.Foreground = accent;
            progress.Background = new SolidColorBrush(Color.FromArgb(36, 255, 255, 255));

            var stack = new StackPanel { Spacing = 5, Margin = new Thickness(0, 0, 10, 0) };
            var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            header.Children.Add(new FontIcon { Glyph = glyph, FontSize = 18, Foreground = accent });
            header.Children.Add(valueText);
            stack.Children.Add(header);
            stack.Children.Add(progress);
            stack.Children.Add(new TextBlock { Text = label, FontSize = 10, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = accent });
            stack.Children.Add(new TextBlock { Text = line1, FontSize = 11, Foreground = new SolidColorBrush(Color.FromArgb(220, 244, 244, 245)) });
            stack.Children.Add(new TextBlock { Text = line2, FontSize = 10, Foreground = new SolidColorBrush(Color.FromArgb(190, 161, 161, 170)) });

            return new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromArgb(34, 255, 255, 255)),
                BorderThickness = new Thickness(0, 0, 1, 0),
                Child = stack
            };
        }

        private static Button CreateActionTile(string glyph, string title, string subtitle, Brush foreground, Brush background)
        {
            return new Button
            {
                Content = CreateActionContent(glyph, title, subtitle, foreground),
                Height = 60,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(12, 8, 12, 8),
                Background = background,
                BorderBrush = new SolidColorBrush(Color.FromArgb(55, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8)
            };
        }

        private static Grid CreateActionContent(string glyph, string title, string subtitle, Brush foreground)
        {
            var grid = new Grid { ColumnSpacing = 10 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.Children.Add(new FontIcon { Glyph = glyph, FontSize = 24, Foreground = foreground, VerticalAlignment = VerticalAlignment.Center });
            var text = new StackPanel();
            text.Children.Add(new TextBlock { Text = title, FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = foreground });
            text.Children.Add(new TextBlock { Text = subtitle, FontSize = 11, Foreground = new SolidColorBrush(Color.FromArgb(220, 244, 244, 245)) });
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);
            return grid;
        }

        private static Button CreatePlainButton(string text, Brush foreground, Brush background)
        {
            return new Button
            {
                Content = text,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = foreground,
                Background = background,
                BorderBrush = new SolidColorBrush(Color.FromArgb(58, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 7, 12, 7)
            };
        }

        private static Border CreateModePill(string text, bool active)
        {
            return new Border
            {
                Background = new SolidColorBrush(active ? Color.FromArgb(86, 242, 198, 109) : Color.FromArgb(42, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(active ? Color.FromArgb(120, 242, 198, 109) : Color.FromArgb(50, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(9, 5, 9, 5),
                Child = new TextBlock
                {
                    Text = text,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromArgb(230, 244, 244, 245))
                }
            };
        }

        private void Border_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var properties = e.GetCurrentPoint(sender as UIElement).Properties;
            if (properties.IsLeftButtonPressed)
            {
                _isDragging = true;
                if (GetCursorPos(out POINT currentMousePos))
                {
                    _dragStartMousePos = currentMousePos;
                    
                    var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                    var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
                    var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                    if (appWindow != null)
                    {
                        _dragStartWindowPos = appWindow.Position;
                    }
                }
                (sender as UIElement)?.CapturePointer(e.Pointer);
                e.Handled = true;
            }
        }

        private void Border_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (_isDragging)
            {
                if (GetCursorPos(out POINT currentMousePos))
                {
                    int deltaX = currentMousePos.X - _dragStartMousePos.X;
                    int deltaY = currentMousePos.Y - _dragStartMousePos.Y;

                    var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                    var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
                    var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                    if (appWindow != null)
                    {
                        appWindow.Move(new Windows.Graphics.PointInt32(
                            _dragStartWindowPos.X + deltaX,
                            _dragStartWindowPos.Y + deltaY
                        ));
                    }
                }
                e.Handled = true;
            }
        }

        private void Border_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                (sender as UIElement)?.ReleasePointerCapture(e.Pointer);
                e.Handled = true;
            }
        }

        private void ConfigureBorderless()
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            
            // Explicitly strip caption, resizing, sysmenu, and border via Win32 styles
            int style = GetWindowLong(hWnd, GWL_STYLE);
            style &= ~WS_CAPTION;
            style &= ~WS_THICKFRAME;
            style &= ~WS_MINIMIZEBOX;
            style &= ~WS_MAXIMIZEBOX;
            style &= ~WS_SYSMENU;
            style &= ~WS_BORDER;
            style &= ~WS_DLGFRAME;
            SetWindowLong(hWnd, GWL_STYLE, style);

            // Force style changes to apply immediately
            SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0, 
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED | SWP_NOACTIVATE);

            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            
            if (appWindow != null)
            {
                var presenter = appWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
                if (presenter != null)
                {
                    presenter.IsAlwaysOnTop = true;
                    // Completely disable resizing, minimizing, and maximizing in app window presenter as backup
                    presenter.IsResizable = false;
                    presenter.IsMaximizable = false;
                    presenter.IsMinimizable = false;
                    presenter.SetBorderAndTitleBar(false, false);
                }

                // Paint titlebar to match background to prevent white strip glitches
                if (AppWindowTitleBar.IsCustomizationSupported())
                {
                    var titleBar = appWindow.TitleBar;
                    var darkColor = Windows.UI.Color.FromArgb(255, 13, 27, 42); // #0D1B2A (BgSidebar)
                    titleBar.BackgroundColor = darkColor;
                    titleBar.InactiveBackgroundColor = darkColor;
                    titleBar.ButtonBackgroundColor = darkColor;
                    titleBar.ButtonInactiveBackgroundColor = darkColor;
                }

                // Crucial WinUI 3 fix: DO NOT extend content into titlebar when border is false. 
                // That avoids creating empty white title bar placeholders in borderless windows!
                this.ExtendsContentIntoTitleBar = false;

                int width = 680;
                int height = 418;
                appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));

                // Position beautifully above the bottom-right system tray working area using native Win32 calls
                var monitor = MonitorFromWindow(hWnd, MONITOR_DEFAULTTOPRIMARY);
                var monitorInfo = new MONITORINFO();
                monitorInfo.cbSize = Marshal.SizeOf<MONITORINFO>();
                GetMonitorInfoW(monitor, ref monitorInfo);

                int x = monitorInfo.rcWork.Right - width - 12;
                int y = monitorInfo.rcWork.Bottom - height - 12;
                appWindow.Move(new Windows.Graphics.PointInt32(x, y));
            }

            int preference = 3; // DWMWCP_ROUND (Rounded Windows Corners)
            DwmSetWindowAttribute(hWnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
        }

        private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            UpdateStats();
        }

        private void UpdateStats()
        {
            try
            {
                var cpu = SystemService.GetCpuUsage();
                var ram = SystemService.GetRamInfo();
                var gpu = SystemService.GetGpuUsage();
                var uptime = SystemService.GetUptime();

                _dispatcher?.TryEnqueue(() =>
                {
                    if (_cpuText != null) _cpuText.Text = $"{cpu}%";
                    if (_cpuProgress != null) _cpuProgress.Value = cpu;

                    if (_ramText != null) _ramText.Text = $"{ram.percent}%";
                    if (_ramProgress != null) _ramProgress.Value = ram.percent;

                    if (_gpuText != null) _gpuText.Text = $"{gpu}%";
                    if (_gpuProgress != null) _gpuProgress.Value = gpu;

                    if (_uptimeText != null)
                    {
                        _uptimeText.Text = $"Uptime: {SystemService.FormatUptime(uptime)}";
                    }

                    if (_statusText != null)
                    {
                        if (ram.percent > 75)
                        {
                            _statusText.Text = "Heavy Load";
                            _statusText.Foreground = new SolidColorBrush(Color.FromArgb(255, 244, 76, 76)); // Danger Red
                        }
                        else
                        {
                            _statusText.Text = "Optimized";
                            _statusText.Foreground = new SolidColorBrush(Color.FromArgb(255, 74, 234, 220)); // Teal-Cyan
                        }
                    }
                });
            }
            catch { }
        }

        private void Boost_Click(object sender, RoutedEventArgs e)
        {
            if (_boostBtn == null) return;

            _boostBtn.IsEnabled = false;
            _boostBtn.Content = CreateActionContent("\uE945", "Boosting", "Memory sweep", new SolidColorBrush(Color.FromArgb(255, 88, 213, 201)));

            System.Threading.Tasks.Task.Run(() =>
            {
                long clearedBytes = OptimizeMemory();
                double clearedMB = clearedBytes / (1024.0 * 1024);

                _dispatcher?.TryEnqueue(() =>
                {
                    UpdateStats();
                    if (clearedMB > 10)
                    {
                        _boostBtn.Content = CreateActionContent("\uE945", "Boost", $"Cleared {clearedMB:F0} MB", new SolidColorBrush(Color.FromArgb(255, 88, 213, 201)));
                    }
                    else
                    {
                        _boostBtn.Content = CreateActionContent("\uE945", "Boost", "Already lean", new SolidColorBrush(Color.FromArgb(255, 88, 213, 201)));
                    }

                    // Reset after 3 seconds
                    var resetTimer = new Microsoft.UI.Xaml.DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(3)
                    };
                    resetTimer.Tick += (s, ev) =>
                    {
                        resetTimer.Stop();
                        if (_boostBtn != null)
                        {
                            _boostBtn.Content = CreateActionContent("\uE945", "Boost", "Memory sweep", new SolidColorBrush(Color.FromArgb(255, 88, 213, 201)));
                            _boostBtn.IsEnabled = true;
                        }
                    };
                    resetTimer.Start();
                });
            });
        }

        private static long OptimizeMemory()
        {
            long bytesCleared = 0;
            try
            {
                // Force Garbage Collection
                GC.Collect();
                GC.WaitForPendingFinalizers();

                // Empty working set of all processes
                foreach (var process in Process.GetProcesses())
                {
                    try
                    {
                        long wsBefore = process.WorkingSet64;
                        bool success = EmptyWorkingSet(process.Handle);
                        long wsAfter = process.WorkingSet64;
                        if (success && wsBefore > wsAfter)
                        {
                            bytesCleared += (wsBefore - wsAfter);
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return bytesCleared;
        }

        private async void GameMode_Click(object sender, RoutedEventArgs e)
        {
            if (_gameModeBtn == null) return;

            _gameModeBtn.IsEnabled = false;
            _gameModeBtn.Content = CreateActionContent("\uE7FC", "Game", GameBoosterService.IsActive ? "Stopping focus" : "Starting focus", new SolidColorBrush(Color.FromArgb(255, 122, 167, 255)));

            try
            {
                if (GameBoosterService.IsActive)
                {
                    await Task.Run(GameBoosterService.DeactivateGameMode);
                }
                else
                {
                    await GameBoosterService.ActivateGameModeAsync();
                }
            }
            finally
            {
                _gameModeBtn.Content = CreateActionContent("\uE7FC", "Game", GameBoosterService.IsActive ? "Focus active" : "Focus ready", new SolidColorBrush(Color.FromArgb(255, 122, 167, 255)));
                _gameModeBtn.IsEnabled = true;
            }
        }

        private async void DnsFlush_Click(object sender, RoutedEventArgs e)
        {
            if (_dnsBtn == null) return;

            _dnsBtn.IsEnabled = false;
            _dnsBtn.Content = CreateActionContent("\uE839", "DNS", "Flushing cache", new SolidColorBrush(Color.FromArgb(255, 242, 198, 109)));

            try
            {
                var ok = await NetworkService.FlushDnsAsync();
                _dnsBtn.Content = CreateActionContent("\uE839", "DNS", ok ? "Cache cleared" : "Flush failed", new SolidColorBrush(Color.FromArgb(255, 242, 198, 109)));
            }
            catch
            {
                _dnsBtn.Content = CreateActionContent("\uE839", "DNS", "Flush failed", new SolidColorBrush(Color.FromArgb(255, 242, 198, 109)));
            }

            var resetTimer = new Microsoft.UI.Xaml.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            resetTimer.Tick += (s, ev) =>
            {
                resetTimer.Stop();
                if (_dnsBtn != null)
                {
                    _dnsBtn.Content = CreateActionContent("\uE839", "DNS", "Flush cache", new SolidColorBrush(Color.FromArgb(255, 242, 198, 109)));
                    _dnsBtn.IsEnabled = true;
                }
            };
            resetTimer.Start();
        }

        private void Transparent_Click(object sender, RoutedEventArgs e)
        {
            _isTransparentMode = !_isTransparentMode;

            if (_rootBorder != null)
            {
                _rootBorder.Background = _isTransparentMode
                    ? _transparentFlyoutBackground
                    : _solidFlyoutBackground;
                _rootBorder.BorderBrush = new SolidColorBrush(_isTransparentMode
                    ? Color.FromArgb(72, 255, 255, 255)
                    : Color.FromArgb(58, 255, 255, 255));
            }

            if (_transparentBtn != null)
            {
                _transparentBtn.Content = CreateActionContent("\uE8A1", _isTransparentMode ? "Solid" : "Overlay", _isTransparentMode ? "Restore panel" : "Transparent", new SolidColorBrush(Color.FromArgb(255, 244, 244, 245)));
            }
        }

        private void OpenDashboard_Click(object sender, RoutedEventArgs e)
        {
            _dispatcher?.TryEnqueue(() =>
            {
                if (App.Current is App appInstance)
                {
                    appInstance.ShowMainWindow();
                }
                this.Close();
            });
        }

        public void Cleanup()
        {
            _timer?.Stop();
            _timer?.Dispose();
        }
    }
}
