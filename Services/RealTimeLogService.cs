using Microsoft.UI.Dispatching;
using System;
using System.Collections.ObjectModel;

namespace TAY.Services
{
    public class RealTimeLogService
    {
        private static readonly Lazy<RealTimeLogService> _instance = new(() => new RealTimeLogService());
        public static RealTimeLogService Instance => _instance.Value;

        private DispatcherQueue? _dispatcherQueue;

        public ObservableCollection<string> LogLines { get; } = new();

        private RealTimeLogService()
        {
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        }

        public void Initialize(DispatcherQueue dispatcherQueue)
        {
            _dispatcherQueue = dispatcherQueue;
        }

        public void Log(string message)
        {
            string formatted = $"[{DateTime.Now:HH:mm:ss}] {message}";
            System.Diagnostics.Debug.WriteLine(formatted);

            if (_dispatcherQueue != null)
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    LogLines.Add(formatted);
                    if (LogLines.Count > 100) // Keep standard sliding log size
                    {
                        LogLines.RemoveAt(0);
                    }
                });
            }
            else
            {
                // Fallback if dispatcher is not ready yet
                LogLines.Add(formatted);
            }
        }

        public void Clear()
        {
            if (_dispatcherQueue != null)
            {
                _dispatcherQueue.TryEnqueue(() => LogLines.Clear());
            }
            else
            {
                LogLines.Clear();
            }
        }
    }
}
