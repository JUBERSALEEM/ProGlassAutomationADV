// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Features/JobOrderFieldVisibility.cs
// PROJECT: ProGlassAutomation
// MODULE: Feature flags — Job Order production panel field visibility
//
// Bindable singleton mirror of AppConfig.JobOrderFields. Same shape as
// ProformaFieldVisibility (see that file for the full contract). The
// JoProductionSection consumes this so a Settings save flips fields
// on/off without an app restart.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.AI.AIConfig;
using System;
using System.ComponentModel;
using System.Windows;

namespace ProGlassAutomation.Services.Features
{
    /// <summary>
    /// Bindable, singleton mirror of the Job Order per-field visibility
    /// settings. Bind via
    /// <c>{Binding Path=ShowXxx, Source={x:Static features:JobOrderFieldVisibility.Instance},
    /// Converter={StaticResource BoolToVisibilityConverter}}</c>.
    /// </summary>
    public sealed class JobOrderFieldVisibility : INotifyPropertyChanged
    {
        private static readonly Lazy<JobOrderFieldVisibility> _instance =
            new(() => new JobOrderFieldVisibility());

        /// <summary>Process-wide singleton.</summary>
        public static JobOrderFieldVisibility Instance => _instance.Value;

        public event PropertyChangedEventHandler? PropertyChanged;

        private JobOrderFieldVisibility() => CopyFromConfig();

        public bool ShowAssignedTo          { get; private set; } = true;
        public bool ShowSiteDeliveryContact { get; private set; } = true;
        public bool ShowSiteDeliveryPhone   { get; private set; } = true;
        public bool ShowSiteDeliveryAddress { get; private set; } = true;
        public bool ShowDrawingReference    { get; private set; } = true;
        public bool ShowCuttingInstructions { get; private set; } = true;
        public bool ShowInternalNotes       { get; private set; } = true;

        /// <summary>True when at least one Job Order field is hidden.</summary>
        public bool HasAnyHiddenField =>
            !(ShowAssignedTo && ShowSiteDeliveryContact && ShowSiteDeliveryPhone
              && ShowSiteDeliveryAddress && ShowDrawingReference
              && ShowCuttingInstructions && ShowInternalNotes);

        /// <summary>
        /// Re-reads <see cref="AppConfig.JobOrderFields"/> and raises
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
            var src = App.Config?.JobOrderFields ?? new JobOrderFeatureFlags();

            ShowAssignedTo          = src.ShowAssignedTo;
            ShowSiteDeliveryContact = src.ShowSiteDeliveryContact;
            ShowSiteDeliveryPhone   = src.ShowSiteDeliveryPhone;
            ShowSiteDeliveryAddress = src.ShowSiteDeliveryAddress;
            ShowDrawingReference    = src.ShowDrawingReference;
            ShowCuttingInstructions = src.ShowCuttingInstructions;
            ShowInternalNotes       = src.ShowInternalNotes;
        }

        private void RaiseAllChanged()
        {
            var h = PropertyChanged;
            if (h == null) return;

            h(this, new PropertyChangedEventArgs(nameof(ShowAssignedTo)));
            h(this, new PropertyChangedEventArgs(nameof(ShowSiteDeliveryContact)));
            h(this, new PropertyChangedEventArgs(nameof(ShowSiteDeliveryPhone)));
            h(this, new PropertyChangedEventArgs(nameof(ShowSiteDeliveryAddress)));
            h(this, new PropertyChangedEventArgs(nameof(ShowDrawingReference)));
            h(this, new PropertyChangedEventArgs(nameof(ShowCuttingInstructions)));
            h(this, new PropertyChangedEventArgs(nameof(ShowInternalNotes)));
            h(this, new PropertyChangedEventArgs(nameof(HasAnyHiddenField)));
        }
    }
}
