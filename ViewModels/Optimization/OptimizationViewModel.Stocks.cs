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

            public StockEntryVm(string w, string h) { _widthText = w; _heightText = h; }
            public StockEntryVm(string w, string h, string source) { _widthText = w; _heightText = h; _sourceLabel = source; }

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
        }

        public ObservableCollection<StockEntryVm> Stocks { get; } = new();
        public ICommand AddStockCommand { get; }
        public ICommand RemoveStockCommand { get; }
        public ICommand ImportStockFromStep4Command { get; }

        private StockEntryVm _newStock = new("3210", "2250", "manual");
        public StockEntryVm NewStock { get => _newStock; private set { _newStock = value; OnPropertyChanged(); } }

        private static IEnumerable<StockEntryVm> DefaultStockPool()
        {
            yield return new StockEntryVm("3210", "2250", "default rack");
            yield return new StockEntryVm("2500", "1875", "default rack");
            yield return new StockEntryVm("2440", "1830", "default rack");
            yield return new StockEntryVm("3660", "2440", "default rack");
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
                "manual")
            { AvailableText = NewStock.AvailableText };
            Stocks.Add(row);
            RunMessage = string.Empty;
        }

        private void RemoveStock(StockEntryVm entry)
        {
            if (entry != null && Stocks.Count > 1) Stocks.Remove(entry);
        }

        /// <summary>
        /// Pulls the Step 4 chosen stock sheets of every ticked Job Order
        /// into the pool (deduped by size). The job's Step 4 sheet = the
        /// exact rack the factory quoted on, so the optimizer packs against
        /// it instead of a generic pool.
        /// </summary>
        private void ImportStockFromStep4()
        {
            var sheets = SelectedJobs.SelectMany(j => j.Step4Sheets).ToList();
            if (sheets.Count == 0)
            {
                RunMessage = "Tick a Job Order first — its Step 4 sheets import into the stock pool.";
                return;
            }

            int added = 0;
            foreach (var (w, h, source) in sheets)
            {
                bool exists = Stocks.Any(s =>
                    int.TryParse(s.WidthText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int ew) &&
                    int.TryParse(s.HeightText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int eh) &&
                    ew == w && eh == h);
                if (exists) continue;

                Stocks.Add(new StockEntryVm(
                    w.ToString(CultureInfo.InvariantCulture),
                    h.ToString(CultureInfo.InvariantCulture),
                    "Step 4 · " + source));
                added++;
            }

            RunMessage = added == 0
                ? "Step 4 sheets are already in the stock pool."
                : string.Format(CultureInfo.InvariantCulture,
                    "Imported {0} Step 4 stock sheet size(s) — available = unlimited (usage-tracked).", added);
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
