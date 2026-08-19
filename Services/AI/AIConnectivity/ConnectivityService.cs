// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/AI/AIConnectivity/ConnectivityService.cs
// PROJECT: ProGlassAutomation
// MODULE: AI Connectivity
//
// Detects online/offline state. Polls on a timer and reacts to OS-level
// network events, marshalling every state change onto the UI dispatcher.
//
// PREVIOUS FIX (retained): NetworkChange.NetworkAvailabilityChanged fires
// on a ThreadPool thread. Setting IsOnline directly raised
// ConnectivityChanged off the UI thread, which crashed any bound UI with
// a cross-thread exception. All changes now go through the Dispatcher.
//
// AUDIT M-3 + T-2 — WHAT THIS REVISION FIXES
// ──────────────────────────────────────────
//  1. LEAKED OS EVENT SUBSCRIPTION.
//     The constructor attached a lambda to the STATIC event
//     NetworkChange.NetworkAvailabilityChanged and nothing ever detached
//     it. Because the handler captured `this`, the service — and the
//     DispatcherTimer it owns — was rooted for the life of the process by
//     a CLR-owned static. StopMonitoring() only stopped the timer, so a
//     "stopped" service still woke up on every network transition and
//     still raised ConnectivityChanged to its subscribers. The service is
//     now IDisposable and detaches properly.
//
//  2. STOPPED SERVICE KEPT REPORTING.
//     CheckStatus ran regardless of state, so a service that had been
//     stopped (or disposed) continued to publish connectivity changes.
//     Both paths are now gated on the running/disposed flags.
//
//  3. TIMER CREATED ON THE WRONG THREAD.
//     A DispatcherTimer binds to the Dispatcher of the thread that
//     CONSTRUCTS it. This service is constructed during startup, so that
//     is normally the UI thread — but nothing guaranteed it, and if it is
//     ever created from a background thread the timer silently never
//     fires (no exception, connectivity simply freezes at its initial
//     value). It is now explicitly bound to the application dispatcher.
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
    /// marshaled to the UI dispatcher. Dispose to release the OS hook.
    /// </summary>
    public class ConnectivityService : IConnectivityService, IDisposable
    {
        private readonly DispatcherTimer _timer;
        private readonly NetworkAvailabilityChangedEventHandler _networkHandler;

        private bool _isOnline;
        private bool _isMonitoring;
        private bool _disposed;

        public bool IsOnline
        {
            get => _isOnline;
            private set
            {
                if (_isOnline == value) return;

                _isOnline = value;
                ConnectivityChanged?.Invoke(this, _isOnline);
            }
        }

        public event EventHandler<bool> ConnectivityChanged;

        public ConnectivityService()
        {
            // Bind explicitly to the application dispatcher rather than
            // relying on the constructing thread being the UI thread.
            var dispatcher =
                Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

            _timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _timer.Tick += OnTimerTick;

            // Named handler so it can actually be detached in Dispose.
            _networkHandler = OnNetworkAvailabilityChanged;
            NetworkChange.NetworkAvailabilityChanged += _networkHandler;
        }

        public void StartMonitoring()
        {
            if (_disposed) return;

            _isMonitoring = true;
            CheckStatus();
            _timer.Start();
        }

        public void StopMonitoring()
        {
            _isMonitoring = false;
            _timer.Stop();
        }

        private void OnTimerTick(object? sender, EventArgs e) => CheckStatus();

        private void OnNetworkAvailabilityChanged(
            object? sender, NetworkAvailabilityEventArgs e)
        {
            if (_disposed || !_isMonitoring) return;

            // Fires on a ThreadPool thread — never touch state directly.
            Application.Current?.Dispatcher?.BeginInvoke(
                new Action(CheckStatus),
                DispatcherPriority.Background);
        }

        private void CheckStatus()
        {
            if (_disposed || !_isMonitoring) return;

            try
            {
                IsOnline = NetworkInterface.GetIsNetworkAvailable();
            }
            catch
            {
                // A transient failure inside the OS network stack must not
                // propagate out of a background poll. Treat as unchanged.
            }
        }

        /// <summary>
        /// Stops polling and detaches the OS-level network hook, which is
        /// the reference that would otherwise keep this instance alive for
        /// the whole process.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            StopMonitoring();

            _timer.Tick -= OnTimerTick;

            try
            {
                NetworkChange.NetworkAvailabilityChanged -= _networkHandler;
            }
            catch
            {
                // Detaching during process teardown can throw if the
                // network stack is already gone. Nothing to recover.
            }

            GC.SuppressFinalize(this);
        }
    }
}
