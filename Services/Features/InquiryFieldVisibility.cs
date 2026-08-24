// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Features/InquiryFieldVisibility.cs
// PROJECT: ProGlassAutomation
// MODULE: Feature flags — Inquiry editor field visibility
//
// Bindable singleton mirror of AppConfig.InquiryFields. Same shape as
// ProformaFieldVisibility (see that file for the full contract).
// Bindings in InquiryEditorWindow.xaml consume this so a Settings
// save flips fields on/off without an app restart.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.AI.AIConfig;
using System;
using System.ComponentModel;
using System.Windows;

namespace ProGlassAutomation.Services.Features
{
    /// <summary>
    /// Bindable, singleton mirror of the Inquiry per-field visibility
    /// settings. Bind via
    /// <c>{Binding Path=ShowXxx, Source={x:Static features:InquiryFieldVisibility.Instance},
    /// Converter={StaticResource BoolToVisibilityConverter}}</c>.
    /// </summary>
    public sealed class InquiryFieldVisibility : INotifyPropertyChanged
    {
        private static readonly Lazy<InquiryFieldVisibility> _instance =
            new(() => new InquiryFieldVisibility());

        public static InquiryFieldVisibility Instance => _instance.Value;

        public event PropertyChangedEventHandler? PropertyChanged;

        private InquiryFieldVisibility() => CopyFromConfig();

        public bool ShowInquiryReference   { get; private set; } = true;
        public bool ShowCustomerLocation   { get; private set; } = true;
        public bool ShowProjectDescription { get; private set; } = true;
        public bool ShowSalesman           { get; private set; } = true;
        public bool ShowPreparedBy         { get; private set; } = true;
        public bool ShowPriority           { get; private set; } = true;
        public bool ShowInquiryValue       { get; private set; } = true;
        public bool ShowNotes              { get; private set; } = true;

        /// <summary>True when at least one Inquiry field is hidden.</summary>
        public bool HasAnyHiddenField =>
            !(ShowInquiryReference && ShowCustomerLocation && ShowProjectDescription
              && ShowSalesman && ShowPreparedBy && ShowPriority
              && ShowInquiryValue && ShowNotes);

        /// <summary>
        /// Re-reads <see cref="AppConfig.InquiryFields"/> and raises
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
            var src = App.Config?.InquiryFields ?? new InquiryFeatureFlags();

            ShowInquiryReference   = src.ShowInquiryReference;
            ShowCustomerLocation   = src.ShowCustomerLocation;
            ShowProjectDescription = src.ShowProjectDescription;
            ShowSalesman           = src.ShowSalesman;
            ShowPreparedBy         = src.ShowPreparedBy;
            ShowPriority           = src.ShowPriority;
            ShowInquiryValue       = src.ShowInquiryValue;
            ShowNotes              = src.ShowNotes;
        }

        private void RaiseAllChanged()
        {
            var h = PropertyChanged;
            if (h == null) return;

            h(this, new PropertyChangedEventArgs(nameof(ShowInquiryReference)));
            h(this, new PropertyChangedEventArgs(nameof(ShowCustomerLocation)));
            h(this, new PropertyChangedEventArgs(nameof(ShowProjectDescription)));
            h(this, new PropertyChangedEventArgs(nameof(ShowSalesman)));
            h(this, new PropertyChangedEventArgs(nameof(ShowPreparedBy)));
            h(this, new PropertyChangedEventArgs(nameof(ShowPriority)));
            h(this, new PropertyChangedEventArgs(nameof(ShowInquiryValue)));
            h(this, new PropertyChangedEventArgs(nameof(ShowNotes)));
            h(this, new PropertyChangedEventArgs(nameof(HasAnyHiddenField)));
        }
    }
}
