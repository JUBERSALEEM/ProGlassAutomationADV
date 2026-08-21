// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/ThemeWeakEventManager.cs
// PROJECT: ProGlassAutomation
// MODULE: Theme — Weak Event Manager (Phase 4: Event Lifetime)
// VERSION: Phase 4 — 2026-08-21
//
// PURPOSE:
//   Static events (ThemeManager.ThemeChanged, AccentChanged, UiScaleChanged)
//   strongly root every subscriber for process lifetime. A ViewModel that
//   subscribes with a lambda capturing 'this' can never be collected —
//   even after navigation away.
//
//   WeakEventManager holds weak references to subscribers, so they can be
//   GC'd even if they forget to unsubscribe. This is the WPF-recommended
//   pattern for static events that outlive their subscribers.
//
//   Usage:
//     ThemeWeakEventManager.AddListener(ThemeManager, "ThemeChanged", OnThemeChanged);
//     ThemeWeakEventManager.RemoveListener(ThemeManager, "ThemeChanged", OnThemeChanged);
//
//   Or use the typed helpers:
//     ThemeWeakEventManager.SubscribeThemeChanged(OnThemeChanged);
//     ThemeWeakEventManager.UnsubscribeThemeChanged(OnThemeChanged);
//
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Windows;

namespace ProGlassAutomation.Services.Theme
{
    /// <summary>
    /// Weak event manager for ThemeManager static events — Phase 4.
    /// Prevents memory leaks from undetached static event subscriptions.
    /// </summary>
    public static class ThemeWeakEventManager
    {
        // We use WPF's WeakEventManager<TEventSource, TEventArgs> pattern
        // but for static events we need custom implementation with weak refs

        private static readonly Dictionary<string, List<WeakReference<EventHandler>>> _themeChangedListeners = new();
        private static readonly Dictionary<string, List<WeakReference<EventHandler>>> _accentChangedListeners = new();
        private static readonly Dictionary<string, List<WeakReference<EventHandler>>> _uiScaleChangedListeners = new();
        private static readonly object _gate = new();

        private static bool _isWired = false;

        private static void EnsureWired()
        {
            if (_isWired) return;
            _isWired = true;

            // Wire to actual ThemeManager static events once
            ThemeManager.ThemeChanged += OnThemeManagerThemeChanged;
            ThemeManager.AccentChanged += OnThemeManagerAccentChanged;
            ThemeManager.UiScaleChanged += OnThemeManagerUiScaleChanged;
        }

        // ── Public API — typed helpers ───────────────────────────────────

        public static void SubscribeThemeChanged(EventHandler handler)
        {
            EnsureWired();
            lock (_gate)
            {
                AddWeak(_themeChangedListeners, "ThemeChanged", handler);
            }
        }

        public static void UnsubscribeThemeChanged(EventHandler handler)
        {
            lock (_gate)
            {
                RemoveWeak(_themeChangedListeners, "ThemeChanged", handler);
            }
        }

        public static void SubscribeAccentChanged(EventHandler handler)
        {
            EnsureWired();
            lock (_gate)
            {
                AddWeak(_accentChangedListeners, "AccentChanged", handler);
            }
        }

        public static void UnsubscribeAccentChanged(EventHandler handler)
        {
            lock (_gate)
            {
                RemoveWeak(_accentChangedListeners, "AccentChanged", handler);
            }
        }

        public static void SubscribeUiScaleChanged(EventHandler handler)
        {
            EnsureWired();
            lock (_gate)
            {
                AddWeak(_uiScaleChangedListeners, "UiScaleChanged", handler);
            }
        }

        public static void UnsubscribeUiScaleChanged(EventHandler handler)
        {
            lock (_gate)
            {
                RemoveWeak(_uiScaleChangedListeners, "UiScaleChanged", handler);
            }
        }

        // ── Internal forwarding ──────────────────────────────────────────

        private static void OnThemeManagerThemeChanged(object? sender, EventArgs e)
        {
            List<WeakReference<EventHandler>>? listeners;
            lock (_gate)
            {
                if (!_themeChangedListeners.TryGetValue("ThemeChanged", out listeners)) return;
                listeners = new List<WeakReference<EventHandler>>(listeners);
            }

            foreach (var weak in listeners)
            {
                if (weak.TryGetTarget(out var handler))
                {
                    try { handler(sender, e); } catch { }
                }
            }

            PruneDead(_themeChangedListeners, "ThemeChanged");
        }

        private static void OnThemeManagerAccentChanged(object? sender, EventArgs e)
        {
            List<WeakReference<EventHandler>>? listeners;
            lock (_gate)
            {
                if (!_accentChangedListeners.TryGetValue("AccentChanged", out listeners)) return;
                listeners = new List<WeakReference<EventHandler>>(listeners);
            }

            foreach (var weak in listeners)
            {
                if (weak.TryGetTarget(out var handler))
                {
                    try { handler(sender, e); } catch { }
                }
            }

            PruneDead(_accentChangedListeners, "AccentChanged");
        }

        private static void OnThemeManagerUiScaleChanged(object? sender, EventArgs e)
        {
            List<WeakReference<EventHandler>>? listeners;
            lock (_gate)
            {
                if (!_uiScaleChangedListeners.TryGetValue("UiScaleChanged", out listeners)) return;
                listeners = new List<WeakReference<EventHandler>>(listeners);
            }

            foreach (var weak in listeners)
            {
                if (weak.TryGetTarget(out var handler))
                {
                    try { handler(sender, e); } catch { }
                }
            }

            PruneDead(_uiScaleChangedListeners, "UiScaleChanged");
        }

        // ── Weak reference helpers ───────────────────────────────────────

        private static void AddWeak(Dictionary<string, List<WeakReference<EventHandler>>> dict, string key, EventHandler handler)
        {
            if (!dict.TryGetValue(key, out var list))
            {
                list = new List<WeakReference<EventHandler>>();
                dict[key] = list;
            }

            // Avoid duplicate
            foreach (var weak in list)
            {
                if (weak.TryGetTarget(out var existing) && existing == handler)
                    return;
            }

            list.Add(new WeakReference<EventHandler>(handler));
        }

        private static void RemoveWeak(Dictionary<string, List<WeakReference<EventHandler>>> dict, string key, EventHandler handler)
        {
            if (!dict.TryGetValue(key, out var list)) return;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (!list[i].TryGetTarget(out var existing) || existing == handler)
                {
                    list.RemoveAt(i);
                }
            }
        }

        private static void PruneDead(Dictionary<string, List<WeakReference<EventHandler>>> dict, string key)
        {
            lock (_gate)
            {
                if (!dict.TryGetValue(key, out var list)) return;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (!list[i].TryGetTarget(out _))
                        list.RemoveAt(i);
                }
            }
        }

        public static void ClearAll()
        {
            lock (_gate)
            {
                _themeChangedListeners.Clear();
                _accentChangedListeners.Clear();
                _uiScaleChangedListeners.Clear();
            }
        }
    }
}
