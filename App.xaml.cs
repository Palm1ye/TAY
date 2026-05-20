using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;
using TAY.Services;
using TAY.ViewModels;
using TAY.Views;

namespace TAY
{
    public partial class App : Application
    {
        private Window? m_window;
        private TrayFlyoutWindow? _flyoutWindow;
        private static System.Threading.Mutex? _appMutex;

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private const int SW_RESTORE = 9;

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            const string mutexName = "TAY_System_Optimizer_Mutex";
            _appMutex = new System.Threading.Mutex(true, mutexName, out bool createdNew);
            if (!createdNew)
            {
                // App is already running in background. Terminate duplicate.
                System.Diagnostics.Process.GetCurrentProcess().Kill();
                return;
            }

            m_window = new MainWindow();
            m_window.Closed += (s, e) => { ExitApplication(); };
            m_window.Activate();

            SettingsViewModel.Instance.BeginAutoCheck();

            try
            {
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(m_window);
                TrayIconHelper.Initialize(
                    hWnd,
                    "TAY System Optimizer",
                    () => ShowTrayFlyout(),
                    () => ShowMainWindow(),
                    () => ExitApplication()
                );
            }
            catch { }
        }

        private void ShowTrayFlyout()
        {
            if (_flyoutWindow != null)
            {
                try { _flyoutWindow.Close(); } catch { }
                _flyoutWindow = null;
                return;
            }

            _flyoutWindow = new TrayFlyoutWindow();
            _flyoutWindow.Closed += (s, e) => { _flyoutWindow = null; };
            _flyoutWindow.Activate();
        }

        public void ShowMainWindow()
        {
            if (m_window != null)
            {
                m_window.Activate();
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(m_window);
                ShowWindow(hWnd, SW_RESTORE);
                SetForegroundWindow(hWnd);
            }
        }

        public void ExitApplication()
        {
            try { _flyoutWindow?.Close(); } catch { }
            try { TrayIconHelper.Shutdown(); } catch { }
            try { m_window?.Close(); } catch { }
            try { this.Exit(); } catch { }
        }
    }
}
