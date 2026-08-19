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

        // AUDIT C-4: _username / _displayName are written by the UI thread
        // (UpdateIdentity on sign-in and user switch) and read by two timer
        // callbacks and the receive loop on ThreadPool threads. Reference
        // assignment is atomic, but without `volatile` the JIT is free to
        // hoist the read out of the heartbeat loop, so a worker could keep
        // broadcasting the OLD identity after a user switch. Marking them
        // volatile makes the publication visible to the other threads.
        private volatile string _username = "";
        private volatile string _displayName = "";

        // Read by the receive loop on a background thread while Dispose
        // writes it from the UI thread — must not be cached in a register.
        private volatile bool _disposed;

        // Guards Start/Dispose against each other, so a Dispose racing a
        // Start cannot leave a live socket behind with no owner.
        private readonly object _lifecycleLock = new();

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
            lock (_lifecycleLock)
            {
                // A Start after Dispose would create sockets and timers
                // nothing will ever clean up.
                if (_disposed) return;

                StartCore(username, displayName);
            }
        }

        private void StartCore(string username, string displayName)
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
            // A timer callback already in flight when Dispose ran would
            // otherwise resurrect the sender socket it just closed.
            if (_disposed) return;

            if (string.IsNullOrWhiteSpace(_username))
                return;

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
                    // Re-check inside the lock: Dispose may have run
                    // between the check above and here.
                    if (_disposed) return;

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
                    // Dispose closed the socket — this is the exit signal.
                    break;
                }
                catch (SocketException)
                {
                    // Transient (cable pulled, adapter reset). Back off so a
                    // persistently failing socket cannot spin this loop at
                    // 100% CPU, and re-check _disposed on the next pass.
                    if (_disposed) break;
                    await Task.Delay(500).ConfigureAwait(false);
                }
                catch
                {
                    if (_disposed) break;
                    await Task.Delay(500).ConfigureAwait(false);
                }
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  AUDIT S-10 — THIS INPUT IS UNAUTHENTICATED AND UNSOLICITED
        //
        //  Every field below arrives in a broadcast UDP datagram from
        //  anyone on the LAN. There is no handshake and no signature, so
        //  the contents are attacker-controlled by definition. Two limits
        //  that were missing:
        //
        //  1. UNBOUNDED PEER TABLE. The dictionary key is built from the
        //     sender's own user + machine strings, so a single host
        //     spoofing a new identity per packet added an unbounded number
        //     of entries. They are only removed after a 45s silence, so a
        //     trivial flood grew the table (and the bound Users list) until
        //     the process ran out of memory. Now capped.
        //
        //  2. UNBOUNDED STRING LENGTHS. A datagram could carry megabyte
        //     display names straight into a WPF ItemsControl. Now clamped
        //     to lengths a real username can never exceed.
        //
        //  Presence is cosmetic — it decorates a "who's online" list — so
        //  discarding hostile input is always the right trade.
        // ══════════════════════════════════════════════════════════════

        /// <summary>Hard cap on tracked peers (a LAN office, not a crowd).</summary>
        private const int MaxPeers = 256;

        /// <summary>Longest accepted value for any peer-supplied string.</summary>
        private const int MaxFieldLength = 64;

        private static string Clamp(string? value) =>
            string.IsNullOrEmpty(value)
                ? ""
                : (value.Length <= MaxFieldLength
                    ? value
                    : value[..MaxFieldLength]);

        private void HandleDatagram(byte[] buffer)
        {
            try
            {
                // Reject oversized datagrams before paying to parse them.
                if (buffer == null || buffer.Length == 0 || buffer.Length > 4096)
                    return;

                var json = Encoding.UTF8.GetString(buffer);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.ValueKind != JsonValueKind.Object) return;

                if (!root.TryGetProperty("magic", out var m) ||
                    m.ValueKind != JsonValueKind.String ||
                    m.GetString() != Magic)
                    return;

                var user = Clamp(root.TryGetProperty("user", out var u)
                    ? u.GetString() ?? "" : "");

                if (user.Length == 0 || user == _username)
                    return;   // never list ourselves as a peer

                var machine = Clamp(root.TryGetProperty("machine", out var mc)
                    ? mc.GetString() ?? "" : "");

                lock (_peers)
                {
                    var key = $"{user}@{machine}";
                    var isNew = !_peers.ContainsKey(key);

                    // Refuse NEW identities once full; known peers may keep
                    // refreshing their heartbeat so real colleagues are
                    // never evicted by a flood.
                    if (isNew && _peers.Count >= MaxPeers)
                        return;

                    _peers[key] = new NetworkPeer
                    {
                        Username = user,
                        DisplayName = Clamp(
                            root.TryGetProperty("display", out var d)
                                ? d.GetString() ?? user : user),
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
            if (_disposed) return;

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
            // Serialised against Start so the two cannot interleave and
            // strand a socket that nothing owns.
            lock (_lifecycleLock)
            {
                if (_disposed) return;

                // Set FIRST: the receive loop and both timer callbacks
                // check this flag, so flipping it before tearing anything
                // down is what stops them re-entering a closing socket.
                _disposed = true;

                _heartbeat?.Dispose();
                _heartbeat = null;

                _sweeper?.Dispose();
                _sweeper = null;

                // Dispose (not just Close) the listener: this both unblocks
                // the pending ReceiveAsync — it completes with
                // ObjectDisposedException, which the loop treats as its
                // exit signal — and releases the bound UDP port. Closing
                // without disposing left the socket handle to the finalizer,
                // so a restart could still hit "address already in use".
                try { _listener?.Dispose(); } catch { }
                _listener = null;

                lock (_senderLock)
                {
                    try { _sender?.Dispose(); } catch { }
                    _sender = null;
                }
            }

            GC.SuppressFinalize(this);
        }
    }
}
