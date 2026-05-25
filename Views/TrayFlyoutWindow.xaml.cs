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
        private Button? _boostBtn;

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
            // Organic Theme Palette Matching App.xaml
            var accentMain = new SolidColorBrush(Color.FromArgb(255, 74, 234, 220)); // #4AEADC (Teal-Cyan)
            var accentWarning = new SolidColorBrush(Color.FromArgb(255, 242, 198, 109)); // #F2C66D (Sunset Gold)
            var textMain = new SolidColorBrush(Color.FromArgb(255, 232, 237, 243)); // #E8EDF3
            var textDim = new SolidColorBrush(Color.FromArgb(255, 136, 153, 170)); // #8899AA

            var grid = new Grid { Background = new SolidColorBrush(Colors.Transparent) };

            // Rounded Space-Blue Outer Card Border (Whole body is now draggable)
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(255, 13, 27, 42)), // #0D1B2A Navy Blue
                CornerRadius = new CornerRadius(16),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(50, 74, 234, 220)), // Subtle cyan border
                Padding = new Thickness(18),
                Margin = new Thickness(2)
            };
            border.PointerPressed += Border_PointerPressed;
            border.PointerMoved += Border_PointerMoved;
            border.PointerReleased += Border_PointerReleased;

            var mainStack = new StackPanel { Spacing = 14 };

            // --- Header Segment ---
            var headerGrid = new Grid
            {
                Background = new SolidColorBrush(Colors.Transparent)
            };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var logoStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            
            var iconBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(25, 74, 234, 220)),
                CornerRadius = new CornerRadius(6),
                Width = 20,
                Height = 20
            };
            
            var appLogo = new Image
            {
                Width = 18,
                Height = 18,
                VerticalAlignment = VerticalAlignment.Center,
                Source = new Microsoft.UI.Xaml.Media.Imaging.SvgImageSource(new Uri("ms-appx:///Assets/tay.svg"))
            };
            iconBorder.Child = appLogo;

            var titleText = new TextBlock
            {
                Text = "TAY STATUS",
                FontFamily = new FontFamily("Consolas"),
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                FontSize = 11,
                Foreground = accentMain,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            logoStack.Children.Add(iconBorder);
            logoStack.Children.Add(titleText);

            // Sleek Close Button
            var closeBtn = new Button
            {
                Content = new FontIcon { Glyph = "\uE711", FontSize = 9, Foreground = textDim },
                Width = 20,
                Height = 20,
                Padding = new Thickness(0),
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(4),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            closeBtn.Click += (s, e) => this.Close();

            Grid.SetColumn(logoStack, 0);
            Grid.SetColumn(closeBtn, 1);
            
            headerGrid.Children.Add(logoStack);
            headerGrid.Children.Add(closeBtn);

            // --- CPU, RAM, and GPU Live Progress Gauges ---
            var metricsStack = new StackPanel { Spacing = 10 };

            // CPU Gauge
            var cpuStack = new StackPanel { Spacing = 3 };
            var cpuHeader = new Grid();
            var cpuLabel = new TextBlock { Text = "CPU Usage", FontFamily = new FontFamily("Segoe UI"), FontSize = 12, Foreground = textMain };
            _cpuText = new TextBlock { Text = "0%", FontFamily = new FontFamily("Consolas"), FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = accentMain, HorizontalAlignment = HorizontalAlignment.Right };
            cpuHeader.Children.Add(cpuLabel);
            cpuHeader.Children.Add(_cpuText);

            _cpuProgress = new ProgressBar
            {
                Value = 0,
                Minimum = 0,
                Maximum = 100,
                Height = 4,
                CornerRadius = new CornerRadius(2),
                Foreground = accentMain,
                Background = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255))
            };
            cpuStack.Children.Add(cpuHeader);
            cpuStack.Children.Add(_cpuProgress);

            // Memory Gauge
            var ramStack = new StackPanel { Spacing = 3 };
            var ramHeader = new Grid();
            var ramLabel = new TextBlock { Text = "Memory Usage", FontFamily = new FontFamily("Segoe UI"), FontSize = 12, Foreground = textMain };
            _ramText = new TextBlock { Text = "0%", FontFamily = new FontFamily("Consolas"), FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = accentWarning, HorizontalAlignment = HorizontalAlignment.Right };
            ramHeader.Children.Add(ramLabel);
            ramHeader.Children.Add(_ramText);

            _ramProgress = new ProgressBar
            {
                Value = 0,
                Minimum = 0,
                Maximum = 100,
                Height = 4,
                CornerRadius = new CornerRadius(2),
                Foreground = accentWarning,
                Background = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255))
            };
            ramStack.Children.Add(ramHeader);
            ramStack.Children.Add(_ramProgress);

            // GPU Gauge
            var accentInfo = new SolidColorBrush(Color.FromArgb(255, 59, 159, 255)); // Bright blue
            var gpuStack = new StackPanel { Spacing = 3 };
            var gpuHeader = new Grid();
            var gpuLabel = new TextBlock { Text = "GPU Usage", FontFamily = new FontFamily("Segoe UI"), FontSize = 12, Foreground = textMain };
            _gpuText = new TextBlock { Text = "0%", FontFamily = new FontFamily("Consolas"), FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = accentInfo, HorizontalAlignment = HorizontalAlignment.Right };
            gpuHeader.Children.Add(gpuLabel);
            gpuHeader.Children.Add(_gpuText);

            _gpuProgress = new ProgressBar
            {
                Value = 0,
                Minimum = 0,
                Maximum = 100,
                Height = 4,
                CornerRadius = new CornerRadius(2),
                Foreground = accentInfo,
                Background = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255))
            };
            gpuStack.Children.Add(gpuHeader);
            gpuStack.Children.Add(_gpuProgress);

            metricsStack.Children.Add(cpuStack);
            metricsStack.Children.Add(ramStack);
            metricsStack.Children.Add(gpuStack);

            // --- PC Uptime & Glowing Status Row ---
            var detailsGrid = new Grid();
            _uptimeText = new TextBlock
            {
                Text = "Uptime: 00m",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                Foreground = textDim,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            var statusStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, HorizontalAlignment = HorizontalAlignment.Right };
            
            var statusDot = new Border
            {
                Width = 6,
                Height = 6,
                CornerRadius = new CornerRadius(3),
                Background = accentMain,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            _statusText = new TextBlock
            {
                Text = "Optimized",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 10,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = accentMain,
                VerticalAlignment = VerticalAlignment.Center
            };
            statusStack.Children.Add(statusDot);
            statusStack.Children.Add(_statusText);

            detailsGrid.Children.Add(_uptimeText);
            detailsGrid.Children.Add(statusStack);

            // --- Action Buttons (Quick Boost & Dashboard) ---
            var buttonsGrid = new Grid();
            buttonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5.5, GridUnitType.Star) });
            buttonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4.5, GridUnitType.Star) });
            buttonsGrid.ColumnSpacing = 8;

            _boostBtn = new Button
            {
                Content = "Quick Boost",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Height = 30,
                Padding = new Thickness(0, 4, 0, 4),
                Background = new SolidColorBrush(Color.FromArgb(30, 74, 234, 220)),
                Foreground = accentMain,
                CornerRadius = new CornerRadius(15),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(70, 74, 234, 220))
            };
            _boostBtn.Click += Boost_Click;

            var openBtn = new Button
            {
                Content = "Dashboard",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                FontSize = 11,
                Height = 30,
                Padding = new Thickness(0, 4, 0, 4),
                Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
                Foreground = textMain,
                CornerRadius = new CornerRadius(15),
                BorderThickness = new Thickness(0)
            };
            openBtn.Click += OpenDashboard_Click;

            Grid.SetColumn(_boostBtn, 0);
            Grid.SetColumn(openBtn, 1);
            buttonsGrid.Children.Add(_boostBtn);
            buttonsGrid.Children.Add(openBtn);

            mainStack.Children.Add(headerGrid);
            mainStack.Children.Add(metricsStack);
            mainStack.Children.Add(detailsGrid);
            mainStack.Children.Add(buttonsGrid);

            border.Child = mainStack;
            grid.Children.Add(border);

            this.Content = grid;
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

                int width = 295;
                int height = 275;
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
            _boostBtn.Content = "Boosting...";

            System.Threading.Tasks.Task.Run(() =>
            {
                long clearedBytes = OptimizeMemory();
                double clearedMB = clearedBytes / (1024.0 * 1024);

                _dispatcher?.TryEnqueue(() =>
                {
                    UpdateStats();
                    if (clearedMB > 10)
                    {
                        _boostBtn.Content = $"Cleared {clearedMB:F0} MB";
                    }
                    else
                    {
                        _boostBtn.Content = "System Peak";
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
                            _boostBtn.Content = "Quick Boost";
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
