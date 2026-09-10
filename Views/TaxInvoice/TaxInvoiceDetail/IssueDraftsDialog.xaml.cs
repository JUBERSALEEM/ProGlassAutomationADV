using ProGlassAutomation.ViewModels;
using ProGlassAutomation.ViewModels.TaxInvoice.TaxInvoiceList;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using ProGlassAutomation.Views.Shared.Dialogs;

namespace ProGlassAutomation.Views.TaxInvoice.TaxInvoiceDetail
{
    /// <summary>
    /// One row in the Issue All popup. Pill ON = include in the batch.
    /// </summary>
    public sealed class IssueDraftPick : ViewModelBase
    {
        public IssueDraftPick(TaxInvoiceRowViewModel source)
        {
            Source = source;
        }

        public TaxInvoiceRowViewModel Source { get; }
        public string InvoiceNumber => Source.InvoiceNumber;
        public string TypeLabel => Source.InvoiceTypeLabel;
        public string AmountDisplay => Source.GrandTotalDisplay;
        public bool IsCurrent => Source.IsCurrentInvoice;

        private bool _isOn = true;
        public bool IsOn
        {
            get => _isOn;
            set
            {
                if (_isOn == value) return;
                _isOn = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Select which family drafts to issue. Pills default ON.
    /// </summary>
    public partial class IssueDraftsDialog : ThemedDialogWindow
    {
        private readonly ObservableCollection<IssueDraftPick> _picks = new();
        private bool _confirmed;

        private IssueDraftsDialog(IReadOnlyList<TaxInvoiceRowViewModel> drafts)
        {
            InitializeComponent();

            foreach (var draft in drafts)
            {
                var pick = new IssueDraftPick(draft);
                pick.PropertyChanged += OnPickChanged;
                _picks.Add(pick);
            }

            DraftList.ItemsSource = _picks;

            SelectAllButton.Click += (_, __) => SetAll(true);
            SelectNoneButton.Click += (_, __) => SetAll(false);
            CancelButton.Click += (_, __) => Close();
            IssueButton.Click += (_, __) =>
            {
                if (!_picks.Any(p => p.IsOn)) return;
                _confirmed = true;
                Close();
            };

            RefreshIssueEnabled();
        }

        /// <summary>
        /// Returns the drafts the clerk left ON, or null if they cancelled.
        /// </summary>
        public static IReadOnlyList<TaxInvoiceRowViewModel>? Show(
            Window? owner,
            IReadOnlyList<TaxInvoiceRowViewModel> drafts)
        {
            var dialog = new IssueDraftsDialog(drafts) { Owner = owner };
            dialog.ShowDialog();
            if (!dialog._confirmed) return null;
            return dialog._picks.Where(p => p.IsOn).Select(p => p.Source).ToList();
        }

        private void SetAll(bool on)
        {
            foreach (var pick in _picks)
                pick.IsOn = on;
        }

        private void OnPickChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IssueDraftPick.IsOn))
                RefreshIssueEnabled();
        }

        private void RefreshIssueEnabled() =>
            IssueButton.IsEnabled = _picks.Any(p => p.IsOn);
    }
}
