// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/AI/AIConnectivity/ConnectivityService.cs
// PROJECT: ProGlassAutomation
// MODULE: AI Connectivity (Chat 25 v1.0 — thread-safe fix)
//
// CHANGES vs previous:
//   ✓ FIX: NetworkChange.NetworkAvailabilityChanged fires on ThreadPool
//     thread, not UI thread. Previous code set IsOnline directly which
//     fired ConnectivityChanged event — any UI binding would crash with
//     cross-thread exception. Now marshals to Dispatcher.
//
// REGRESSION RISK: ZERO — same interface, same behavior, thread-safe.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Threading;

namespace ProGlassAutomation.Services.AI.AIConnectivity
{
    /// <summary>
    /// Detects online/offline state. Polls every 5 seconds and reacts
    /// to OS-level network events. Thread-safe — all state changes
    /// marshaled to the UI dispatcher.
    /// </summary>
    public class ConnectivityService : IConnectivityService
    {
        private readonly DispatcherTimer _timer;
        private bool _isOnline;

        public bool IsOnline
        {
            get => _isOnline;
            private set
            {
                if (_isOnline != value)
                {
                    _isOnline = value;
                    ConnectivityChanged?.Invoke(this, _isOnline);
                }
            }
        }

        public event EventHandler<bool> ConnectivityChanged;

        public ConnectivityService()
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _timer.Tick += (_, __) => CheckStatus();

            // NetworkAvailabilityChanged fires on ThreadPool thread.
            // Must marshal to Dispatcher to avoid cross-thread UI exceptions.
            NetworkChange.NetworkAvailabilityChanged += (_, __) =>
            {
                Application.Current?.Dispatcher?.BeginInvoke(
                    new Action(CheckStatus),
                    DispatcherPriority.Background);
            };
        }

        public void StartMonitoring()
        {
            CheckStatus();
            _timer.Start();
        }

        public void StopMonitoring() => _timer.Stop();

        private void CheckStatus()
        {
            IsOnline = NetworkInterface.GetIsNetworkAvailable();
        }
    }
}