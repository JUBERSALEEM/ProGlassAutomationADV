using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.ProformaInvoice.Step7PrintPreview.TermsAndConditions;
using ProGlassAutomation.ViewModels;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step7PrintPreview.TermsAndConditions
{
    /// <summary>
    /// Manages the Terms &amp; Conditions section shown on the printable invoice.
    /// Ships with 6 default UAE glass industry clauses.
    /// User can:
    ///   - Toggle each clause on/off
    ///   - Edit clause title and body
    ///   - Add sub-lines to Payment Terms
    ///   - Pick validity days (2..7)
    ///   - Add custom clauses
    ///   - Remove any clause
    ///   - Reset to defaults
    /// </summary>
    public class TermsAndConditionsViewModel : ViewModelBase
    {
        public string Title { get; } = "TERMS & CONDITIONS";
        public string Subtitle { get; } = "Editable clauses printed on the proforma invoice.";

        public ObservableCollection<TermClause> Clauses { get; } = new ObservableCollection<TermClause>();

        /// <summary>Validity dropdown values (2-7 days).</summary>
        public List<int> ValidityDayOptions { get; } = new List<int> { 2, 3, 4, 5, 6, 7 };

        // ── Commands ────────────────────────────────────────────────────────

        public ICommand AddCustomClauseCommand { get; }
        public ICommand RemoveClauseCommand { get; }
        public ICommand ResetToDefaultsCommand { get; }
        public ICommand MoveUpCommand { get; }
        public ICommand MoveDownCommand { get; }

        // Payment Terms sub-line management
        public ICommand AddPaymentSubLineCommand { get; }
        public ICommand RemovePaymentSubLineCommand { get; }

        public TermsAndConditionsViewModel()
        {
            LoadDefaults();

            AddCustomClauseCommand = new RelayCommand(_ => AddCustomClause());
            RemoveClauseCommand = new RelayCommand(p => { if (p is TermClause c) RemoveClause(c); });
            ResetToDefaultsCommand = new RelayCommand(_ => LoadDefaults());
            MoveUpCommand = new RelayCommand(p => { if (p is TermClause c) MoveClause(c, -1); });
            MoveDownCommand = new RelayCommand(p => { if (p is TermClause c) MoveClause(c, +1); });

            AddPaymentSubLineCommand = new RelayCommand(p =>
            {
                if (p is TermClause c) c.SubLines.Add("New payment condition...");
            });

            // Simplified: only takes the string to remove; finds it in whichever clause has it
            RemovePaymentSubLineCommand = new RelayCommand(p =>
            {
                if (p is not string line) return;
                foreach (var clause in Clauses)
                {
                    if (clause.SubLines.Contains(line))
                    {
                        clause.SubLines.Remove(line);
                        break;
                    }
                }
            });
        }

        // ── Public API used by PrintableInvoice preview ────────────────────

        /// <summary>Returns only active clauses in display order.</summary>
        public IEnumerable<TermClause> GetActivePrintableClauses()
            => Clauses.Where(c => c.IsActive).OrderBy(c => c.Order);

        // ── Internal helpers ───────────────────────────────────────────────

        private void LoadDefaults()
        {
            Clauses.Clear();
            foreach (var c in TermsAndConditionsDefaults.BuildDefaults())
                Clauses.Add(c);
            Renumber();
        }

        private void AddCustomClause()
        {
            var next = Clauses.Count + 1;
            Clauses.Add(new TermClause
            {
                Type = TermClauseType.Custom,
                Order = next,
                Title = $"Custom Clause {next}",
                Body = "Enter your custom term text here..."
            });
            Renumber();
        }

        private void RemoveClause(TermClause clause)
        {
            Clauses.Remove(clause);
            Renumber();
        }

        private void MoveClause(TermClause clause, int direction)
        {
            int oldIndex = Clauses.IndexOf(clause);
            if (oldIndex < 0) return;
            int newIndex = oldIndex + direction;
            if (newIndex < 0 || newIndex >= Clauses.Count) return;

            Clauses.Move(oldIndex, newIndex);
            Renumber();
        }

        private void Renumber()
        {
            for (int i = 0; i < Clauses.Count; i++)
                Clauses[i].Order = i + 1;
        }
    }
}