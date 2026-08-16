// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Dashboard/Core/DashboardFilterService.cs
// PROJECT: ProGlassAutomation
// NAMESPACE: ProGlassAutomation.Services.Dashboard.Core
//
// PURPOSE:
//   Holds the dashboard's currently selected date range and notifies
//   listeners when it changes.
//
// WHY AN INSTANCE, NOT A STATIC
//   ThemeManager is static and its ThemeChanged event has no matching
//   unsubscribe anywhere in the dashboard code. That is survivable there
//   only because DashboardViewModel is constructed exactly once for the
//   application lifetime.
//
//   Repeating that pattern would be a real leak the moment a page is
//   created per navigation, so this is an injectable instance and
//   subscribers detach through the returned IDisposable rather than
//   remembering to write "-=".
// ═══════════════════════════════════════════════════════════════════════

using System;

namespace ProGlassAutomation.Services.Dashboard.Core
{
    public interface IDashboardFilterService
    {
        DashboardDateRange CurrentRange { get; }

        /// <summary>Change the range and notify listeners.</summary>
        void SetRange(DashboardRangeKind kind);

        /// <summary>Set an explicit custom window and notify listeners.</summary>
        void SetCustomRange(DateTime from, DateTime to);

        /// <summary>
        /// Subscribe to range changes. Dispose the result to unsubscribe;
        /// this is why the method returns IDisposable rather than
        /// exposing a bare event.
        /// </summary>
        IDisposable Subscribe(Action<DashboardDateRange> onChanged);
    }

    public sealed class DashboardFilterService : IDashboardFilterService
    {
        private readonly object _gate = new();
        private event Action<DashboardDateRange>? Changed;

        private DashboardDateRange _current;

        public DashboardFilterService()
        {
            // Default to all time so existing dashboards keep showing the
            // same lifetime totals they show today. Switching the default
            // to "this month" would quietly change every headline figure.
            _current = DashboardDateRange.AllTime();
        }

        public DashboardDateRange CurrentRange
        {
            get { lock (_gate) return _current; }
        }

        public void SetRange(DashboardRangeKind kind)
        {
            var next = DashboardDateRange.FromKind(kind, DateTime.Now);
            Apply(next);
        }

        public void SetCustomRange(DateTime from, DateTime to) =>
            Apply(DashboardDateRange.Custom(from, to));

        private void Apply(DashboardDateRange next)
        {
            lock (_gate)
            {
                // Same window selected again — nothing to broadcast, and
                // suppressing it avoids a pointless reload of every widget.
                if (_current.Kind == next.Kind &&
                    _current.Start == next.Start &&
                    _current.End == next.End)
                    return;

                _current = next;
            }

            Changed?.Invoke(next);
        }

        public IDisposable Subscribe(Action<DashboardDateRange> onChanged)
        {
            if (onChanged == null) throw new ArgumentNullException(nameof(onChanged));

            Changed += onChanged;
            return new Subscription(() => Changed -= onChanged);
        }

        private sealed class Subscription : IDisposable
        {
            private Action? _dispose;
            public Subscription(Action dispose) => _dispose = dispose;

            public void Dispose()
            {
                // Idempotent: disposing twice must not detach a handler
                // that a later subscriber has since registered.
                var d = _dispose;
                _dispose = null;
                d?.Invoke();
            }
        }
    }
}
