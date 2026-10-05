// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Features/DeliveryNoteFieldVisibility.cs
// PROJECT: ProGlassAutomation
// MODULE: Feature flags — Delivery Note detail field visibility
//
// Bindable singleton mirror of AppConfig.DeliveryNoteFields. Same shape
// as ProformaFieldVisibility (see that file for the full contract). The
// DnOverviewSection DELIVERY DETAILS block consumes this so a Settings
// save flips fields on/off without an app restart.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.AI.AIConfig;
using System;
using System.ComponentModel;
using System.Windows;

namespace ProGlassAutomation.Services.Features
{
    /// <summary>
    /// Bindable, singleton mirror of the Delivery Note per-field
    /// visibility settings. Bind via
    /// <c>{Binding Path=ShowXxx, Source={x:Static features:DeliveryNoteFieldVisibility.Instance},
    /// Converter={StaticResource BoolToVisibilityConverter}}</c>.
    /// </summary>
    public sealed class DeliveryNoteFieldVisibility : INotifyPropertyChanged
    {
        private static readonly Lazy<DeliveryNoteFieldVisibility> _instance =
            new(() => new DeliveryNoteFieldVisibility());

        /// <summary>Process-wide singleton.</summary>
        public static DeliveryNoteFieldVisibility Instance => _instance.Value;

        public event PropertyChangedEventHandler? PropertyChanged;

        private DeliveryNoteFieldVisibility() => CopyFromConfig();

        public bool ShowDriverName   { get; private set; } = true;
        public bool ShowVehicleNumber { get; private set; } = true;
        public bool ShowIsFoc        { get; private set; } = true;
        public bool ShowReceiverName { get; private set; } = true;
        public bool ShowNotes        { get; private set; } = true;

        /// <summary>True when at least one Delivery Note field is hidden.</summary>
        public bool HasAnyHiddenField =>
            !(ShowDriverName && ShowVehicleNumber && ShowIsFoc
              && ShowReceiverName && ShowNotes);

        /// <summary>
        /// Re-reads <see cref="AppConfig.DeliveryNoteFields"/> and raises
        /// PropertyChanged for every bindable property. Safe from any
        /// thread — marshals onto the WPF Dispatcher when necessary.
        /// </summary>
        public void Refresh()
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(new Action(RefreshCore));
                return;
            }

            RefreshCore();
        }

        private void RefreshCore()
        {
            CopyFromConfig();
            RaiseAllChanged();
        }

        private void CopyFromConfig()
        {
            var src = App.Config?.DeliveryNoteFields ?? new DeliveryNoteFeatureFlags();

            ShowDriverName   = src.ShowDriverName;
            ShowVehicleNumber = src.ShowVehicleNumber;
            ShowIsFoc        = src.ShowIsFoc;
            ShowReceiverName = src.ShowReceiverName;
            ShowNotes        = src.ShowNotes;
        }

        private void RaiseAllChanged()
        {
            var h = PropertyChanged;
            if (h == null) return;

            h(this, new PropertyChangedEventArgs(nameof(ShowDriverName)));
            h(this, new PropertyChangedEventArgs(nameof(ShowVehicleNumber)));
            h(this, new PropertyChangedEventArgs(nameof(ShowIsFoc)));
            h(this, new PropertyChangedEventArgs(nameof(ShowReceiverName)));
            h(this, new PropertyChangedEventArgs(nameof(ShowNotes)));
            h(this, new PropertyChangedEventArgs(nameof(HasAnyHiddenField)));
        }
    }
}
