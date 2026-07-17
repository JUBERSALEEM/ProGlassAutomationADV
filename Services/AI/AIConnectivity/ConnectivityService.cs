using System;
using System.Net.NetworkInformation;
using System.Windows.Threading;

namespace ProGlassAutomation.Services.AI.AIConnectivity
{
    /// <summary>
    /// Detects online/offline state. Polls every 5 seconds and reacts
    /// to OS-level network events. Essential for AI (needs internet).
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
            NetworkChange.NetworkAvailabilityChanged += (_, __) => CheckStatus();
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