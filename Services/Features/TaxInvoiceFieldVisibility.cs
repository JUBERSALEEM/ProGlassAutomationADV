// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Features/TaxInvoiceFieldVisibility.cs
// PROJECT: ProGlassAutomation
// MODULE: Feature flags — Tax Invoice detail field visibility
//
// Bindable singleton mirror of AppConfig.TaxInvoiceFields. Same shape as
// ProformaFieldVisibility (see that file for the full contract). The
// TiOverviewSection editable blocks consume this so a Settings save
// flips fields on/off without an app restart.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.AI.AIConfig;
using System;
using System.ComponentModel;
using System.Windows;

namespace ProGlassAutomation.Services.Features
{
    /// <summary>
    /// Bindable, singleton mirror of the Tax Invoice per-field visibility
    /// settings. Bind via
    /// <c>{Binding Path=ShowXxx, Source={x:Static features:TaxInvoiceFieldVisibility.Instance},
    /// Converter={StaticResource BoolToVisibilityConverter}}</c>.
    /// </summary>
    public sealed class TaxInvoiceFieldVisibility : INotifyPropertyChanged
    {
        private static readonly Lazy<TaxInvoiceFieldVisibility> _instance =
            new(() => new TaxInvoiceFieldVisibility());

        /// <summary>Process-wide singleton.</summary>
        public static TaxInvoiceFieldVisibility Instance => _instance.Value;

        public event PropertyChangedEventHandler? PropertyChanged;

        private TaxInvoiceFieldVisibility() => CopyFromConfig();

        public bool ShowDueDate         { get; private set; } = true;
        public bool ShowPaymentTerms    { get; private set; } = true;
        public bool ShowDiscountPercent { get; private set; } = true;
        public bool ShowIban            { get; private set; } = true;
        public bool ShowSwiftCode       { get; private set; } = true;

        /// <summary>True when at least one Tax Invoice field is hidden.</summary>
        public bool HasAnyHiddenField =>
            !(ShowDueDate && ShowPaymentTerms && ShowDiscountPercent
              && ShowIban && ShowSwiftCode);

        /// <summary>
        /// Re-reads <see cref="AppConfig.TaxInvoiceFields"/> and raises
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
            var src = App.Config?.TaxInvoiceFields ?? new TaxInvoiceFeatureFlags();

            ShowDueDate         = src.ShowDueDate;
            ShowPaymentTerms    = src.ShowPaymentTerms;
            ShowDiscountPercent = src.ShowDiscountPercent;
            ShowIban            = src.ShowIban;
            ShowSwiftCode       = src.ShowSwiftCode;
        }

        private void RaiseAllChanged()
        {
            var h = PropertyChanged;
            if (h == null) return;

            h(this, new PropertyChangedEventArgs(nameof(ShowDueDate)));
            h(this, new PropertyChangedEventArgs(nameof(ShowPaymentTerms)));
            h(this, new PropertyChangedEventArgs(nameof(ShowDiscountPercent)));
            h(this, new PropertyChangedEventArgs(nameof(ShowIban)));
            h(this, new PropertyChangedEventArgs(nameof(ShowSwiftCode)));
            h(this, new PropertyChangedEventArgs(nameof(HasAnyHiddenField)));
        }
    }
}
