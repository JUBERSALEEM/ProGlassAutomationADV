using ProGlassAutomation.ViewModels;
using System;
using System.Collections.ObjectModel;

namespace ProGlassAutomation.Models.ProformaInvoice.Step7PrintPreview.TermsAndConditions
{
    /// <summary>
    /// Predefined categories of proforma invoice terms.
    /// Custom = user-added free-text clause.
    /// </summary>
    public enum TermClauseType
    {
        PaymentTerms,
        Delivery,
        Warranty,
        Validity,
        Taxes,
        Installation,
        Custom
    }

    /// <summary>
    /// A single clause on the proforma invoice's Terms & Conditions section.
    /// Supports:
    ///   - Single-line body text (most clauses)
    ///   - Multiple sub-lines (Payment Terms with multiple installment lines)
    ///   - Validity days integer picker (2..7)
    /// </summary>
    public class TermClause : ViewModelBase
    {
        public Guid Id { get; } = Guid.NewGuid();

        public TermClauseType Type { get; set; } = TermClauseType.Custom;

        private int _order;
        public int Order
        {
            get => _order;
            set { if (_order == value) return; _order = value; OnPropertyChanged(); OnPropertyChanged(nameof(FormattedNumberedTitle)); }
        }

        private bool _isActive = true;
        public bool IsActive
        {
            get => _isActive;
            set { if (_isActive == value) return; _isActive = value; OnPropertyChanged(); }
        }

        private string _title = "";
        public string Title
        {
            get => _title;
            set { if (_title == value) return; _title = value; OnPropertyChanged(); OnPropertyChanged(nameof(FormattedNumberedTitle)); }
        }

        /// <summary>Main body text of the clause (single line for most types).</summary>
        private string _body = "";
        public string Body
        {
            get => _body;
            set { if (_body == value) return; _body = value; OnPropertyChanged(); }
        }

        // ── Multi-line sub-items (used by Payment Terms) ────────────────────

        /// <summary>
        /// Extra bullet lines shown beneath the main body.
        /// Populated for PaymentTerms (advance %, dispatch %, etc.).
        /// </summary>
        public ObservableCollection<string> SubLines { get; set; } = new ObservableCollection<string>();

        public bool HasSubLines => SubLines != null && SubLines.Count > 0;

        // ── Validity days picker (used by Validity only) ────────────────────

        private int _validityDays = 7;
        public int ValidityDays
        {
            get => _validityDays;
            set
            {
                if (_validityDays == value) return;
                _validityDays = value;
                OnPropertyChanged();

                // Auto-update Body when validity clause days change
                if (Type == TermClauseType.Validity)
                    Body = $"This quotation is valid for {value} day{(value == 1 ? "" : "s")} from issue date.";
            }
        }

        // ── Display helpers ────────────────────────────────────────────────

        /// <summary>"1. PAYMENT TERMS" — used in preview rendering.</summary>
        public string FormattedNumberedTitle => $"{Order}. {Title?.ToUpper() ?? ""}";
    }
}