// ═══════════════════════════════════════════════════════════════
// FILE: OptimizationViewModel.Stocks.cs
// MODULE: Optimization — STOCKS tab — stock pool, Step 4 import and run usage tracking (partial of OptimizationViewModel)
// ═══════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.JobOrder.Common.JobOrderStatus;
using ProGlassAutomation.Models.Production;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.JobOrder.Mapper;
using ProGlassAutomation.Services.JobOrder.Repository;
using ProGlassAutomation.Services.Optimization;
using ProGlassAutomation.Services.Production;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step9ProfitLoss;

namespace ProGlassAutomation.ViewModels.Optimization
{
    public partial class OptimizationViewModel : ViewModels.ViewModelBase
    {

        // ── STOCKS tab ─────────────────────────────────────────────────

        public sealed class StockEntryVm : ViewModels.ViewModelBase
        {
            private string _widthText;
            private string _heightText;
            private string _availableText = "99999999"; // effectively unlimited — usage is tracked, not capped
            private string _usedLabel = string.Empty;
            private string _sourceLabel = string.Empty;
            private string _composition = string.Empty;

            public StockEntryVm(string w, string h) { _widthText = w; _heightText = h; }
            public StockEntryVm(string w, string h, string source) { _widthText = w; _heightText = h; _sourceLabel = source; }
            public StockEntryVm(string w, string h, string composition, string source)
            {
                _widthText = w; _heightText = h; _composition = composition ?? string.Empty; _sourceLabel = source;
            }

            public string WidthText { get => _widthText; set { _widthText = value; OnPropertyChanged(); } }
            public string HeightText { get => _heightText; set { _heightText = value; OnPropertyChanged(); } }
            public string AvailableText { get => _availableText; set { _availableText = value; OnPropertyChanged(); } }

            /// <summary>Sheets consumed by the last run, e.g. "17 sheets used".</summary>
            public string UsedLabel
            {
                get => _usedLabel;
                set { if (_usedLabel == value) return; _usedLabel = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasUsed)); }
            }

            public bool HasUsed => !string.IsNullOrWhiteSpace(_usedLabel);

            /// <summary>Provenance, e.g. "Step 4 · Spec 1 outer".</summary>
            public string SourceLabel { get => _sourceLabel; set { if (_sourceLabel == value) return; _sourceLabel = value; OnPropertyChanged(); } }

            /// <summary>
            /// The glass composition (cutting group) this rack belongs to.
            /// Empty = a manual "any glass" rack used by every group without
            /// its own Step 4 sheet. This is what routes each cutting group
            /// to its OWN Step 4 rack instead of a shared pool.
            /// </summary>
            public string Composition
            {
                get => _composition;
                set
                {
                    if (_composition == value) return;
                    _composition = value ?? string.Empty;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CompositionLabel));
                    OnPropertyChanged(nameof(HasComposition));
                }
            }

            /// <summary>Human label for the rack's owner ("All groups" for manual racks).</summary>
            public string CompositionLabel =>
                string.IsNullOrWhiteSpace(_composition) ? "Any group" : _composition;

            public bool HasComposition => !string.IsNullOrWhiteSpace(_composition);
        }

        public ObservableCollection<StockEntryVm> Stocks { get; } = new();
        public ICommand AddStockCommand { get; }
        public ICommand RemoveStockCommand { get; }
        public ICommand ImportStockFromStep4Command { get; }

        private StockEntryVm _newStock = new("3210", "2250", "manual");
        public StockEntryVm NewStock { get => _newStock; private set { _newStock = value; OnPropertyChanged(); } }

        private string _stocksStatusText = "Tick a Job Order — its Step 4 sheet sizes become the stock pool for this run.";
        /// <summary>Feedback line on the STOCKS tab (which job/sheets feed the pool).</summary>
        public string StocksStatusText
        {
            get => _stocksStatusText;
            private set { if (_stocksStatusText == value) return; _stocksStatusText = value; OnPropertyChanged(); }
        }

        // ── stock ops ──────────────────────────────────────────────────

        private void AddStock()
        {
            if (!int.TryParse(NewStock.WidthText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int w) ||
                !int.TryParse(NewStock.HeightText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int h) ||
                w <= 0 || h <= 0)
            {
                RunMessage = "Stock needs positive W×H in mm.";
                return;
            }
            var row = new StockEntryVm(
                w.ToString(CultureInfo.InvariantCulture),
                h.ToString(CultureInfo.InvariantCulture),
                string.Empty, // manual rack — applies to any cutting group
                "manual")
            { AvailableText = NewStock.AvailableText };
            Stocks.Add(row);
            RunMessage = string.Empty;
            StocksStatusText = "Manual rack added — it serves any cutting group without its own Step 4 sheet.";
        }

        private void RemoveStock(StockEntryVm entry)
        {
            if (entry != null) Stocks.Remove(entry);
        }

        /// <summary>
        /// Replaces the stock pool with the Step 4 chosen sheets of every
        /// ticked Job Order (deduped by size × glass). The job's Step 4
        /// sheet = the exact rack the factory quoted on, tagged with the
        /// pane's glass composition so each cutting group packs against its
        /// own rack — never the whole sheet store.
        /// </summary>
        private void ImportStockFromStep4()
        {
            if (!SelectedJobs.Any())
            {
                StocksStatusText = "Tick a Job Order first — its Step 4 sheets import into the stock pool.";
                RunMessage = StocksStatusText;
                return;
            }

            int count = SyncStocksFromSelectedJobs();
            StocksStatusText = count == 0
                ? "No Step 4 sheet sizes recorded on the selected job(s) — add a rack manually."
                : string.Format(CultureInfo.InvariantCulture,
                    "Stock pool = {0} Step 4 sheet size(s) from the selected job(s) — available = unlimited (usage-tracked).",
                    count);
            RunMessage = string.Empty;
        }

        /// <summary>
        /// Rebuilds <see cref="Stocks"/> so it contains ONLY the Step 4 sheet
        /// sizes of the ticked Job Orders (outer/inner per spec), tagged by
        /// their pane's glass composition. Runs automatically whenever the
        /// job selection changes; manual racks the operator adds survive
        /// until the next selection change. Returns the number of rows.
        /// </summary>
        private int SyncStocksFromSelectedJobs()
        {
            var sheets = SelectedJobs.SelectMany(j => j.Step4Sheets).ToList();

            Stocks.Clear();
            var seen = new HashSet<(int W, int H, string C)>();
            foreach (var (w, h, composition, source) in sheets)
            {
                if (w <= 0 || h <= 0) continue;
                string comp = (composition ?? string.Empty).Trim();
                if (!seen.Add((w, h, comp))) continue;

                Stocks.Add(new StockEntryVm(
                    w.ToString(CultureInfo.InvariantCulture),
                    h.ToString(CultureInfo.InvariantCulture),
                    comp,
                    "Step 4 · " + source));
            }

            if (sheets.Count == 0 && Stocks.Count == 0)
                StocksStatusText = "Tick a Job Order — its Step 4 sheet sizes become the stock pool for this run.";

            return Stocks.Count;
        }

        private void RefreshStockUsage(List<GroupRun> runs)
        {
            var used = new Dictionary<(int W, int H), int>();
            foreach (var r in runs)
            {
                foreach (var sheet in r.Result.Sheets)
                {
                    var key = (sheet.Stock.WidthMm, sheet.Stock.HeightMm);
                    used.TryGetValue(key, out int n);
                    used[key] = n + 1;
                }
            }

            foreach (var s in Stocks)
            {
                int n = 0;
                bool known = int.TryParse(s.WidthText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int w) &&
                             int.TryParse(s.HeightText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int h) &&
                             used.TryGetValue((w, h), out n);
                s.UsedLabel = known
                    ? n.ToString(CultureInfo.InvariantCulture) + (n == 1 ? " sheet used" : " sheets used")
                    : string.Empty;
            }
        }
    }
}
