// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Network/UserPresenceService.cs
// PROJECT: ProGlassAutomation
// MODULE: Core — same-network user presence
//
// Lets colleagues on the same LAN see who is logged into ProGlass.
// Every instance broadcasts a tiny UDP heartbeat (username + machine);
// listeners collect peers and drop them after they go quiet.
//
// SAFETY:
//   • Connectionless UDP broadcast — no server, no ports forwarded,
//     no credentials ever transmitted.
//   • Every network call is wrapped; failures are silent and logged.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ProGlassAutomation.Services.Network
{
    /// <summary>One ProGlass user visible on the local network.</summary>
    public class NetworkPeer
    {
        public string Username { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string MachineName { get; set; } = "";
        public DateTime LastSeenUtc { get; set; }
    }

    /// <summary>
    /// UDP broadcast presence. Start once after login, stop on exit.
    /// </summary>
    public class UserPresenceService : IDisposable
    {
        private const int Port = 47319;
        private const string Magic = "PROGLASS2";
        private static readonly TimeSpan PeerTimeout = TimeSpan.FromSeconds(45);

        private readonly Dictionary<string, NetworkPeer> _peers = new();
        private UdpClient? _listener;
        private Timer? _heartbeat;
        private Timer? _sweeper;

        // PERFORMANCE: one reusable broadcast sender. The previous
        // implementation allocated + disposed a UdpClient on every
        // 10-second heartbeat (socket churn + allocations for nothing).
        private UdpClient? _sender;
        private readonly object _senderLock = new();
        private string _username = "";
        private string _displayName = "";
        private bool _disposed;

        /// <summary>Raised on the UI thread whenever the peer list changes.</summary>
        public event EventHandler? PeersChanged;

        /// <summary>Snapshot of currently visible peers.</summary>
        public List<NetworkPeer> GetPeers()
        {
            lock (_peers)
                return _peers.Values
                    .OrderBy(p => p.DisplayName)
                    .ToList();
        }

        /// <summary>Switches the broadcast identity (user switching).</summary>
        public void UpdateIdentity(string username, string displayName)
        {
            _username = username ?? "";
            _displayName = displayName ?? username ?? "";
        }

        public void Start(string username, string displayName)
        {
            UpdateIdentity(username, displayName);

            try
            {
                _listener = new UdpClient(AddressFamily.InterNetwork)
                {
                    EnableBroadcast = true
                };
                _listener.Client.SetSocketOption(
                    SocketOptionLevel.Socket,
                    SocketOptionName.ReuseAddress, true);
                _listener.Client.Bind(new IPEndPoint(IPAddress.Any, Port));

                _ = ReceiveLoopAsync();
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Presence listener could not start: {ex.Message}",
                    new LogContext { Module = "Network", Operation = "Presence.Start" });
            }

            _heartbeat = new Timer(
                _ => SendHeartbeat(), null,
                TimeSpan.Zero, TimeSpan.FromSeconds(10));

            _sweeper = new Timer(
                _ => SweepExpired(), null,
                TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));
        }

        private void SendHeartbeat()
        {
            try
            {
                var payload = JsonSerializer.Serialize(new
                {
                    magic = Magic,
                    user = _username,
                    display = _displayName,
                    machine = Environment.MachineName
                });

                var bytes = Encoding.UTF8.GetBytes(payload);

                // Reuse a single broadcast socket across heartbeats.
                UdpClient sender;
                lock (_senderLock)
                {
                    if (_sender == null)
                        _sender = new UdpClient { EnableBroadcast = true };
                    sender = _sender;
                }

                sender.Send(bytes, bytes.Length,
                    new IPEndPoint(IPAddress.Broadcast, Port));
            }
            catch
            {
                // Broadcast can briefly fail on network switches — ignore.
            }
        }

        private async Task ReceiveLoopAsync()
        {
            while (_listener != null && !_disposed)
            {
                try
                {
                    var result = await _listener.ReceiveAsync();
                    HandleDatagram(result.Buffer);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch
                {
                    await Task.Delay(500);
                }
            }
        }

        private void HandleDatagram(byte[] buffer)
        {
            try
            {
                var json = Encoding.UTF8.GetString(buffer);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("magic", out var m) ||
                    m.GetString() != Magic)
                    return;

                var user = root.TryGetProperty("user", out var u)
                    ? u.GetString() ?? "" : "";

                if (user.Length == 0 || user == _username)
                    return;   // never list ourselves as a peer

                var machine = root.TryGetProperty("machine", out var mc)
                    ? mc.GetString() ?? "" : "";

                lock (_peers)
                {
                    var key = $"{user}@{machine}";
                    var isNew = !_peers.ContainsKey(key);

                    _peers[key] = new NetworkPeer
                    {
                        Username = user,
                        DisplayName = root.TryGetProperty("display", out var d)
                            ? d.GetString() ?? user : user,
                        MachineName = machine,
                        LastSeenUtc = DateTime.UtcNow
                    };

                    if (isNew) RaisePeersChanged();
                }
            }
            catch
            {
                // Foreign traffic on the port — ignore silently.
            }
        }

        private void SweepExpired()
        {
            bool changed = false;

            lock (_peers)
            {
                var cutoff = DateTime.UtcNow - PeerTimeout;
                var expired = _peers
                    .Where(kv => kv.Value.LastSeenUtc < cutoff)
                    .Select(kv => kv.Key)
                    .ToList();

                foreach (var key in expired)
                {
                    _peers.Remove(key);
                    changed = true;
                }
            }

            if (changed) RaisePeersChanged();
        }

        private void RaisePeersChanged()
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            if (dispatcher.CheckAccess())
                PeersChanged?.Invoke(this, EventArgs.Empty);
            else
                dispatcher.InvokeAsync(
                    () => PeersChanged?.Invoke(this, EventArgs.Empty));
        }

        public void Dispose()
        {
            _disposed = true;
            _heartbeat?.Dispose();
            _sweeper?.Dispose();
            try { _listener?.Close(); } catch { }

            lock (_senderLock)
            {
                try { _sender?.Close(); } catch { }
                _sender = null;
            }
        }
    }
}
