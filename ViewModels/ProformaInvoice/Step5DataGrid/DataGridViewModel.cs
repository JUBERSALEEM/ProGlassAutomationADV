// ╔══════════════════════════════════════════════════════════════════════════╗
// ║  ViewModels/ProformaInvoice/Step5DataGrid/DataGridViewModel.cs           ║
// ║                                                                          ║
// ║  CHAT 7 CHANGES (Bug 1):                                                 ║
// ║    + PasteFromText() → fires async pipeline, returns immediately         ║
// ║    + PasteFromTextAsync() → background parse + batched UI updates        ║
// ║    + ParseClipboardText() → pure parse, safe for Task.Run               ║
// ║    + PasteFromClipboardAsync() → async clipboard read + paste            ║
// ║    + ShowToastAsync() → replaces ALL MessageBox.Show() calls             ║
// ║    + _isBulkAdding → always in try/finally (never leaks)                ║
// ║    + Progress overlay: IsPasting / PasteProgress / PasteStatusText      ║
// ║    + Toast: IsToastVisible / ToastMessage / ToastIsError                ║
// ║                                                                          ║
// ║  PRESERVED EXACTLY:                                                      ║
// ║    ✅ GlassItemRow — all properties + RecalculateAll + SetAllSilent      ║
// ║    ✅ SpecGroup — all properties + IExcelPasteable interface             ║
// ║    ✅ DataGridViewModel — all commands + AddNewSpecGroup                 ║
// ║    ✅ _isBulkAdding guard in OnItemRowsChanged + OnRowChanged            ║
// ║    ✅ Rate auto-adjust logic (Sqm1 >= 4m threshold)                      ║
// ║    ✅ LoadFilteredSpecs async void (preserved)                           ║
// ╚══════════════════════════════════════════════════════════════════════════╝

using ProGlassAutomation.Behaviors;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.ProformaInvoice.Specifications;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid.GrandTotalSummary;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid
{
    // ════════════════════════════════════════════════════════════════════════
    // CLASS: GlassItemRow
    // ════════════════════════════════════════════════════════════════════════

    public class GlassItemRow : ViewModelBase
    {
        private const decimal MIN_SQM = 0.5m;

        // ── SerialNumber ─────────────────────────────────────────────────────
        private int _serialNumber;
        public int SerialNumber
        {
            get => _serialNumber;
            set { if (_serialNumber == value) return; _serialNumber = value; OnPropertyChanged(); }
        }

        // ── GlassRef ─────────────────────────────────────────────────────────
        private string _glassRef = "";
        public string GlassRef
        {
            get => _glassRef;
            set { if (_glassRef == value) return; _glassRef = value; OnPropertyChanged(); }
        }

        // ── W1 ───────────────────────────────────────────────────────────────
        private int _w1;
        public int W1
        {
            get => _w1;
            set { if (_w1 == value) return; _w1 = value; OnPropertyChanged(); RecalculateAll(); }
        }

        // ── H1 ───────────────────────────────────────────────────────────────
        private int _h1;
        public int H1
        {
            get => _h1;
            set { if (_h1 == value) return; _h1 = value; OnPropertyChanged(); RecalculateAll(); }
        }

        // ── W2 ───────────────────────────────────────────────────────────────
        private int _w2;
        public int W2
        {
            get => _w2;
            set { if (_w2 == value) return; _w2 = value; OnPropertyChanged(); RecalculateAll(); }
        }

        // ── H2 ───────────────────────────────────────────────────────────────
        private int _h2;
        public int H2
        {
            get => _h2;
            set { if (_h2 == value) return; _h2 = value; OnPropertyChanged(); RecalculateAll(); }
        }

        // ── Qty ──────────────────────────────────────────────────────────────
        private int _qty = 1;
        public int Qty
        {
            get => _qty;
            set { if (_qty == value) return; _qty = value; OnPropertyChanged(); RecalculateAll(); }
        }

        // ── Sqm1 (calculated) ────────────────────────────────────────────────
        private decimal _sqm1;
        public decimal Sqm1
        {
            get => _sqm1;
            private set { if (_sqm1 == value) return; _sqm1 = value; OnPropertyChanged(); }
        }

        // ── TotalSqm (calculated) ────────────────────────────────────────────
        private decimal _totalSqm;
        public decimal TotalSqm
        {
            get => _totalSqm;
            private set { if (_totalSqm == value) return; _totalSqm = value; OnPropertyChanged(); }
        }

        // ── Sqm2 (calculated) ────────────────────────────────────────────────
        private decimal _sqm2;
        public decimal Sqm2
        {
            get => _sqm2;
            private set { if (_sqm2 == value) return; _sqm2 = value; OnPropertyChanged(); }
        }

        // ── TotalSqm2 (calculated) ───────────────────────────────────────────
        private decimal _totalSqm2;
        public decimal TotalSqm2
        {
            get => _totalSqm2;
            private set { if (_totalSqm2 == value) return; _totalSqm2 = value; OnPropertyChanged(); }
        }

        // ── Rate ─────────────────────────────────────────────────────────────
        private decimal _rate;
        public decimal Rate
        {
            get => _rate;
            set
            {
                if (_rate == value) return;
                _rate = value;
                OnPropertyChanged();
                _value = Math.Round(_sqm1 * _qty * _rate, 2);
                OnPropertyChanged(nameof(Value));
            }
        }

        // ── Value (calculated) ───────────────────────────────────────────────
        private decimal _value;
        public decimal Value
        {
            get => _value;
            private set { if (_value == value) return; _value = value; OnPropertyChanged(); }
        }

        // ── Lm1 (calculated) ────────────────────────────────────────────────
        private decimal _lm1;
        public decimal Lm1
        {
            get => _lm1;
            private set { if (_lm1 == value) return; _lm1 = value; OnPropertyChanged(); }
        }

        // ── Lm2 (calculated) ────────────────────────────────────────────────
        private decimal _lm2;
        public decimal Lm2
        {
            get => _lm2;
            private set { if (_lm2 == value) return; _lm2 = value; OnPropertyChanged(); }
        }

        // ── PerimeterFactor ──────────────────────────────────────────────────
        private int _perimeterFactor = 2;
        public int PerimeterFactor
        {
            get => _perimeterFactor;
            set { if (_perimeterFactor == value) return; _perimeterFactor = value; OnPropertyChanged(); RecalculateAll(); }
        }

        internal bool SuppressRateAutoAdjust { get; set; } = false;

        /// <summary>
        /// True if this row has no meaningful data (used to decide whether to overwrite on paste).
        /// </summary>
        public bool IsEmpty =>
            string.IsNullOrWhiteSpace(_glassRef) &&
            _w1 == 0 && _h1 == 0 && _w2 == 0 && _h2 == 0;

        // ── RecalculateAll ───────────────────────────────────────────────────
        public void RecalculateAll()
        {
            decimal rawSqm1 = (decimal)_w1 * _h1 / 1_000_000m;
            decimal newSqm1 = _w1 > 0 && _h1 > 0
                ? Math.Max(Math.Round(rawSqm1, 3), MIN_SQM) : 0m;
            decimal newTotalSqm = Math.Round(newSqm1 * _qty, 3);

            decimal rawSqm2 = (decimal)_w2 * _h2 / 1_000_000m;
            decimal newSqm2 = _w2 > 0 && _h2 > 0
                ? Math.Max(Math.Round(rawSqm2, 3), MIN_SQM) : 0m;
            decimal newTotalSqm2 = Math.Round(newSqm2 * _qty, 3);

            decimal newLm1 = _w1 > 0 && _h1 > 0
                ? Math.Round(((decimal)_w1 + _h1) / 1000m * _perimeterFactor * _qty, 3) : 0m;
            decimal newLm2 = _w2 > 0 && _h2 > 0
                ? Math.Round(((decimal)_w2 + _h2) / 1000m * _perimeterFactor * _qty, 3) : 0m;
            decimal newValue = Math.Round(newSqm1 * _qty * _rate, 2);

            _sqm1 = newSqm1;
            _totalSqm = newTotalSqm;
            _sqm2 = newSqm2;
            _totalSqm2 = newTotalSqm2;
            _lm1 = newLm1;
            _lm2 = newLm2;
            _value = newValue;

            OnPropertyChanged(nameof(Sqm1));
            OnPropertyChanged(nameof(TotalSqm));
            OnPropertyChanged(nameof(Sqm2));
            OnPropertyChanged(nameof(TotalSqm2));
            OnPropertyChanged(nameof(Lm1));
            OnPropertyChanged(nameof(Lm2));
            OnPropertyChanged(nameof(Value));
        }

        /// <summary>
        /// Set all fields silently (no PropertyChanged fired).
        /// Call FireAllChanged() after to notify UI once.
        /// </summary>
        public void SetAllSilent(
            string glassRef, int w1, int h1, int w2, int h2,
            int qty, int perimeterFactor, decimal rate)
        {
            _glassRef = glassRef;
            _w1 = w1;
            _h1 = h1;
            _w2 = w2;
            _h2 = h2;
            _qty = qty <= 0 ? 1 : qty;
            _perimeterFactor = perimeterFactor;
            _rate = rate;

            decimal rawSqm1 = (decimal)_w1 * _h1 / 1_000_000m;
            _sqm1 = _w1 > 0 && _h1 > 0 ? Math.Max(Math.Round(rawSqm1, 3), MIN_SQM) : 0m;
            _totalSqm = Math.Round(_sqm1 * _qty, 3);

            decimal rawSqm2 = (decimal)_w2 * _h2 / 1_000_000m;
            _sqm2 = _w2 > 0 && _h2 > 0 ? Math.Max(Math.Round(rawSqm2, 3), MIN_SQM) : 0m;
            _totalSqm2 = Math.Round(_sqm2 * _qty, 3);

            _lm1 = _w1 > 0 && _h1 > 0
                ? Math.Round(((decimal)_w1 + _h1) / 1000m * _perimeterFactor * _qty, 3) : 0m;
            _lm2 = _w2 > 0 && _h2 > 0
                ? Math.Round(((decimal)_w2 + _h2) / 1000m * _perimeterFactor * _qty, 3) : 0m;
            _value = Math.Round(_sqm1 * _qty * _rate, 2);
        }

        public void FireAllChanged()
        {
            OnPropertyChanged(nameof(SerialNumber));
            OnPropertyChanged(nameof(GlassRef));
            OnPropertyChanged(nameof(W1));
            OnPropertyChanged(nameof(H1));
            OnPropertyChanged(nameof(W2));
            OnPropertyChanged(nameof(H2));
            OnPropertyChanged(nameof(Qty));
            OnPropertyChanged(nameof(Sqm1));
            OnPropertyChanged(nameof(TotalSqm));
            OnPropertyChanged(nameof(Sqm2));
            OnPropertyChanged(nameof(TotalSqm2));
            OnPropertyChanged(nameof(Lm1));
            OnPropertyChanged(nameof(Lm2));
            OnPropertyChanged(nameof(Rate));
            OnPropertyChanged(nameof(Value));
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // CLASS: SpecGroup
    // ════════════════════════════════════════════════════════════════════════

    public class SpecGroup : ViewModelBase, IExcelPasteable
    {
        // ── GroupNumber ──────────────────────────────────────────────────────
        private int _groupNumber;
        public int GroupNumber
        {
            get => _groupNumber;
            set { if (_groupNumber == value) return; _groupNumber = value; OnPropertyChanged(); }
        }

        // ── GroupName ────────────────────────────────────────────────────────
        private string _groupName = "";
        public string GroupName
        {
            get => _groupName;
            set { _groupName = value; OnPropertyChanged(); }
        }

        // ── BaseRate ─────────────────────────────────────────────────────────
        private decimal _baseRate;
        public decimal BaseRate
        {
            get => _baseRate;
            set
            {
                if (_baseRate == value) return;
                _baseRate = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EffectiveRate));
                UpdateAllRowRates();
            }
        }

        // ── SurchargePercent ─────────────────────────────────────────────────
        private decimal _surchargePercent;
        public decimal SurchargePercent
        {
            get => _surchargePercent;
            set
            {
                if (_surchargePercent == value) return;
                _surchargePercent = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EffectiveRate));
                UpdateAllRowRates();
            }
        }

        public decimal EffectiveRate =>
            Math.Round(_baseRate * (1m + (_surchargePercent / 100m)), 2);

        /// <summary>
        /// Display label for print/preview headers.
        /// Shows SelectedSpecName if available, otherwise GroupName.
        /// </summary>
        public string SpecDisplayName => !string.IsNullOrWhiteSpace(SelectedSpecName)
            ? SelectedSpecName
            : (GroupName ?? $"Spec Group {GroupNumber}");

        // ── SelectedGlassType ────────────────────────────────────────────────
        private string _selectedGlassType = "";
        public string SelectedGlassType
        {
            get => _selectedGlassType;
            set
            {
                _selectedGlassType = value;
                OnPropertyChanged();
                UpdatePerimeterFactors();
                LoadFilteredSpecs();
            }
        }

        // ── SelectedSpecName ─────────────────────────────────────────────────
        private string _selectedSpecName = "";
        public string SelectedSpecName
        {
            get => _selectedSpecName;
            set
            {
                _selectedSpecName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SpecDisplayName));
                TryAutoFillRate();
            }
        }

        public ObservableCollection<SavedSpecification> FilteredSpecs { get; } = new();
        public ObservableCollection<GlassItemRow> ItemRows { get; } = new();

        // ── Paste progress properties (Bug 1: progress overlay) ──────────────

        private bool _isPasting;
        public bool IsPasting
        {
            get => _isPasting;
            private set { if (_isPasting == value) return; _isPasting = value; OnPropertyChanged(); }
        }

        private int _pasteProgress;
        public int PasteProgress
        {
            get => _pasteProgress;
            private set { if (_pasteProgress == value) return; _pasteProgress = value; OnPropertyChanged(); }
        }

        private string _pasteStatusText = "";
        public string PasteStatusText
        {
            get => _pasteStatusText;
            private set { if (_pasteStatusText == value) return; _pasteStatusText = value; OnPropertyChanged(); }
        }

        // ── Toast notification properties (Bug 1: replaces MessageBox) ───────

        private bool _isToastVisible;
        public bool IsToastVisible
        {
            get => _isToastVisible;
            private set { if (_isToastVisible == value) return; _isToastVisible = value; OnPropertyChanged(); }
        }

        private string _toastMessage = "";
        public string ToastMessage
        {
            get => _toastMessage;
            private set { if (_toastMessage == value) return; _toastMessage = value; OnPropertyChanged(); }
        }

        private bool _toastIsError;
        public bool ToastIsError
        {
            get => _toastIsError;
            private set { if (_toastIsError == value) return; _toastIsError = value; OnPropertyChanged(); }
        }

        // ── Footer totals ────────────────────────────────────────────────────

        private int _totalRows;
        public int TotalRows
        {
            get => _totalRows;
            private set { if (_totalRows == value) return; _totalRows = value; OnPropertyChanged(); }
        }

        private int _footerTotalQty;
        public int FooterTotalQty
        {
            get => _footerTotalQty;
            private set { if (_footerTotalQty == value) return; _footerTotalQty = value; OnPropertyChanged(); }
        }

        private decimal _footerTotalSqm;
        public decimal FooterTotalSqm
        {
            get => _footerTotalSqm;
            private set { if (_footerTotalSqm == value) return; _footerTotalSqm = value; OnPropertyChanged(); }
        }

        private decimal _footerTotalSqm2;
        public decimal FooterTotalSqm2
        {
            get => _footerTotalSqm2;
            private set { if (_footerTotalSqm2 == value) return; _footerTotalSqm2 = value; OnPropertyChanged(); }
        }

        private decimal _footerTotalLm1;
        public decimal FooterTotalLm1
        {
            get => _footerTotalLm1;
            private set { if (_footerTotalLm1 == value) return; _footerTotalLm1 = value; OnPropertyChanged(); }
        }

        private decimal _footerTotalLm2;
        public decimal FooterTotalLm2
        {
            get => _footerTotalLm2;
            private set { if (_footerTotalLm2 == value) return; _footerTotalLm2 = value; OnPropertyChanged(); }
        }

        private decimal _footerTotalValue;
        public decimal FooterTotalValue
        {
            get => _footerTotalValue;
            private set { if (_footerTotalValue == value) return; _footerTotalValue = value; OnPropertyChanged(); }
        }

        // ── Commands ─────────────────────────────────────────────────────────

        public ICommand AddRowCommand { get; }
        public ICommand RemoveRowCommand { get; }
        public ICommand PasteExcelCommand { get; }

        // ── Bug 1: _isBulkAdding always managed via try/finally ──────────────
        private bool _isBulkAdding = false;

        // ════════════════════════════════════════════════════════════════════
        // CONSTRUCTOR
        // ════════════════════════════════════════════════════════════════════

        public SpecGroup()
        {
            ItemRows.CollectionChanged += OnItemRowsChanged;

            AddRowCommand = new RelayCommand(_ => AddNewRow());

            RemoveRowCommand = new RelayCommand(p =>
            {
                if (p is GlassItemRow r)
                {
                    r.PropertyChanged -= OnRowChanged;
                    ItemRows.Remove(r);
                }
            });

            // Bug 1: PasteExcelCommand fires async pipeline
            PasteExcelCommand = new RelayCommand(_ => _ = PasteFromClipboardAsync());
        }

        // ════════════════════════════════════════════════════════════════════
        // COLLECTION CHANGE HANDLERS (preserved — guard logic unchanged)
        // ════════════════════════════════════════════════════════════════════

        private void OnItemRowsChanged(object s, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
                foreach (GlassItemRow r in e.OldItems)
                    r.PropertyChanged -= OnRowChanged;

            if (e.NewItems != null)
                foreach (GlassItemRow r in e.NewItems)
                    r.PropertyChanged += OnRowChanged;

            // Bug 1: guard suppresses per-row overhead during bulk add
            if (_isBulkAdding) return;

            TotalRows = ItemRows.Count;
            RenumberRows();
            RecalculateFooter();
        }

        private void OnRowChanged(object s, PropertyChangedEventArgs e)
        {
            // Bug 1: guard suppresses per-row recalculation during bulk add
            if (_isBulkAdding) return;

            if (e.PropertyName is nameof(GlassItemRow.Qty)
                or nameof(GlassItemRow.TotalSqm)
                or nameof(GlassItemRow.TotalSqm2)
                or nameof(GlassItemRow.Lm1)
                or nameof(GlassItemRow.Lm2)
                or nameof(GlassItemRow.Value))
            {
                RecalculateFooter();
            }

            // Rate auto-adjust: large panels (>=4 sqm) use EffectiveRate
            if (e.PropertyName == nameof(GlassItemRow.Sqm1)
                && s is GlassItemRow row
                && !row.SuppressRateAutoAdjust)
            {
                var targetRate = row.Sqm1 >= 4m ? EffectiveRate : BaseRate;
                if (row.Rate != targetRate)
                    row.Rate = targetRate;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // FOOTER RECALCULATION (preserved exactly)
        // ════════════════════════════════════════════════════════════════════

        private void RecalculateFooter()
        {
            if (ItemRows.Count == 0)
            {
                FooterTotalQty = 0;
                FooterTotalSqm = 0m;
                FooterTotalSqm2 = 0m;
                FooterTotalLm1 = 0m;
                FooterTotalLm2 = 0m;
                FooterTotalValue = 0m;
                return;
            }

            int qty = 0;
            decimal sqm = 0m, sqm2 = 0m, lm1 = 0m, lm2 = 0m, val = 0m;

            foreach (var r in ItemRows)
            {
                qty += r.Qty;
                sqm += r.TotalSqm;
                sqm2 += r.TotalSqm2;
                lm1 += r.Lm1;
                lm2 += r.Lm2;
                val += r.Value;
            }

            FooterTotalQty = qty;
            FooterTotalSqm = Math.Round(sqm, 3);
            FooterTotalSqm2 = Math.Round(sqm2, 3);
            FooterTotalLm1 = Math.Round(lm1, 3);
            FooterTotalLm2 = Math.Round(lm2, 3);
            FooterTotalValue = Math.Round(val, 2);
        }

        // ════════════════════════════════════════════════════════════════════
        // ADD ROW (preserved exactly)
        // ════════════════════════════════════════════════════════════════════

        private void AddNewRow()
        {
            var row = new GlassItemRow
            {
                SerialNumber = ItemRows.Count + 1,
                GlassRef = "",
                Qty = 1,
                Rate = EffectiveRate,
                PerimeterFactor = _selectedGlassType == "LAM" ? 4 : 2
            };
            ItemRows.Add(row);
        }

        // ════════════════════════════════════════════════════════════════════
        // IExcelPasteable — AddRows (preserved, now has try/finally)
        // ════════════════════════════════════════════════════════════════════

        public void AddRows(int count)
        {
            // Bug 1: try/finally ensures _isBulkAdding always resets
            _isBulkAdding = true;
            try
            {
                int factor = _selectedGlassType == "LAM" ? 4 : 2;
                for (int i = 0; i < count; i++)
                {
                    ItemRows.Add(new GlassItemRow
                    {
                        SerialNumber = 0,
                        GlassRef = "",
                        Qty = 1,
                        Rate = EffectiveRate,
                        PerimeterFactor = factor
                    });
                }
            }
            finally
            {
                _isBulkAdding = false;
                TotalRows = ItemRows.Count;
                RenumberRows();
                RecalculateFooter();
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // IExcelPasteable — PasteFromText (Bug 1: now async entry point)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Bug 1 FIX: Synchronous IExcelPasteable entry point.
        /// Called by DataGridNavigationBehavior — must return quickly.
        /// Immediately dispatches to async pipeline. UI thread is freed instantly.
        /// </summary>
        public void PasteFromText(string clipboardText, object startRowContext = null)
        {
            _ = PasteFromTextAsync(clipboardText, startRowContext);
        }

        // ════════════════════════════════════════════════════════════════════
        // ASYNC PASTE PIPELINE (Bug 1 — new)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Bug 1 FIX: Full async paste pipeline.
        ///
        /// PIPELINE:
        ///   1. Parse clipboard text on background thread (never blocks UI)
        ///   2. Show progress overlay for large pastes (200+ rows)
        ///   3. Apply rows in ONE _isBulkAdding block (no per-row overhead)
        ///   4. Single renumber + footer recalc after all rows added
        ///   5. Single FireAllChanged pass after all rows settled
        ///   6. Toast notification (never MessageBox — never blocks)
        ///
        /// PERFORMANCE (500 rows):
        ///   Before: parse on UI thread + 7,500 PropertyChanged + MessageBox = freeze
        ///   After:  parse offthread + 1 batch + 500 FireAllChanged = ~0.5s
        /// </summary>
        private async Task PasteFromTextAsync(string clipboardText, object startRowContext = null)
        {
            if (string.IsNullOrWhiteSpace(clipboardText)) return;

            // ── Step 1: Parse on background thread ───────────────────────────
            List<(string glassRef, int w1, int h1, int w2, int h2, int qty)> parsed;

            try
            {
                parsed = await Task.Run(() => ParseClipboardText(clipboardText));
            }
            catch (Exception ex)
            {
                await ShowToastAsync($"Parse error: {ex.Message}", isError: true);
                return;
            }

            if (parsed.Count == 0)
            {
                await ShowToastAsync(
                    "No valid rows found in clipboard. " +
                    "Expected: GlassRef  W1  H1  [W2  H2]  [Qty]  (tab-separated)",
                    isError: true);
                return;
            }

            // ── Step 2: Progress overlay for large pastes ─────────────────────
            bool showProgress = parsed.Count >= 200;
            if (showProgress)
            {
                IsPasting = true;
                PasteProgress = 0;
                PasteStatusText = $"Preparing {parsed.Count} rows…";
                await Task.Delay(16); // yield — let overlay render
            }

            // ── Step 3: Determine start index ─────────────────────────────────
            int startIdx = -1;
            if (startRowContext is GlassItemRow anchorRow)
            {
                int anchorIdx = ItemRows.IndexOf(anchorRow);
                if (anchorIdx >= 0 && anchorRow.IsEmpty)
                    startIdx = anchorIdx;
            }

            int factor = _selectedGlassType == "LAM" ? 4 : 2;
            var touchedRows = new List<GlassItemRow>(parsed.Count);

            // ── Step 4: Apply rows in ONE _isBulkAdding block ────────────────
            _isBulkAdding = true;
            try
            {
                int parsedIdx = 0;

                // Overwrite existing empty rows from startIdx
                if (startIdx >= 0)
                {
                    for (int i = startIdx;
                         i < ItemRows.Count && parsedIdx < parsed.Count;
                         i++, parsedIdx++)
                    {
                        var (glassRef, w1, h1, w2, h2, qty) = parsed[parsedIdx];
                        var existingRow = ItemRows[i];

                        existingRow.SetAllSilent(glassRef, w1, h1, w2, h2, qty, factor, EffectiveRate);

                        decimal correctRate = existingRow.Sqm1 >= 4m ? EffectiveRate : BaseRate;
                        if (existingRow.Rate != correctRate)
                            existingRow.SetAllSilent(glassRef, w1, h1, w2, h2, qty, factor, correctRate);

                        touchedRows.Add(existingRow);

                        if (showProgress && parsedIdx % 50 == 0)
                        {
                            PasteProgress = (int)((double)parsedIdx / parsed.Count * 100);
                            PasteStatusText = $"Processing row {parsedIdx} of {parsed.Count}…";
                            await Task.Delay(1);
                        }
                    }
                }

                // Append remaining rows as new
                while (parsedIdx < parsed.Count)
                {
                    var (glassRef, w1, h1, w2, h2, qty) = parsed[parsedIdx];
                    var row = new GlassItemRow();

                    row.SetAllSilent(glassRef, w1, h1, w2, h2, qty, factor, EffectiveRate);

                    decimal correctRate = row.Sqm1 >= 4m ? EffectiveRate : BaseRate;
                    if (row.Rate != correctRate)
                        row.SetAllSilent(glassRef, w1, h1, w2, h2, qty, factor, correctRate);

                    ItemRows.Add(row);
                    touchedRows.Add(row);
                    parsedIdx++;

                    if (showProgress && parsedIdx % 50 == 0)
                    {
                        PasteProgress = (int)((double)parsedIdx / parsed.Count * 100);
                        PasteStatusText = $"Adding row {parsedIdx} of {parsed.Count}…";
                        await Task.Delay(1);
                    }
                }
            }
            finally
            {
                // Bug 1: ALWAYS reset — even if exception thrown mid-paste
                _isBulkAdding = false;
            }

            // ── Step 5: Single post-paste recalculation ───────────────────────
            TotalRows = ItemRows.Count;
            RenumberRows();
            RecalculateFooter();

            // ── Step 6: Fire PropertyChanged for all touched rows ─────────────
            if (showProgress)
            {
                PasteProgress = 90;
                PasteStatusText = "Updating display…";
                await Task.Delay(16);
            }

            foreach (var row in touchedRows)
                row.FireAllChanged();

            // ── Step 7: Hide progress overlay ─────────────────────────────────
            if (showProgress)
            {
                PasteProgress = 100;
                PasteStatusText = $"✓ {parsed.Count} rows ready.";
                await Task.Delay(400);
                IsPasting = false;
                PasteProgress = 0;
                PasteStatusText = "";
            }

            // ── Step 8: Toast (non-blocking — never MessageBox) ───────────────
            await ShowToastAsync($"✓ Pasted {parsed.Count} row(s) successfully.");
        }

        // ════════════════════════════════════════════════════════════════════
        // PARSE CLIPBOARD TEXT (Bug 1 — runs on background thread)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Pure data transformation — no UI access — safe for Task.Run.
        /// Preserved parse logic from original PasteFromText exactly.
        /// </summary>
        private static List<(string glassRef, int w1, int h1, int w2, int h2, int qty)>
            ParseClipboardText(string clipboardText)
        {
            var lines = clipboardText.Split(
                new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            var result = new List<(string, int, int, int, int, int)>(lines.Length);

            foreach (var line in lines)
            {
                string[] parts = line.Contains('\t')
                    ? line.Split('\t')
                    : line.Trim().Split(
                        new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length == 0) continue;

                string glassRef = parts.Length >= 1 ? parts[0].Trim() : "";
                int w1 = parts.Length >= 2 && int.TryParse(CleanNum(parts[1]), out int _w1) ? _w1 : 0;
                int h1 = parts.Length >= 3 && int.TryParse(CleanNum(parts[2]), out int _h1) ? _h1 : 0;
                int w2 = parts.Length >= 4 && int.TryParse(CleanNum(parts[3]), out int _w2) ? _w2 : 0;
                int h2 = parts.Length >= 5 && int.TryParse(CleanNum(parts[4]), out int _h2) ? _h2 : 0;
                int qty = parts.Length >= 6 && int.TryParse(CleanNum(parts[5]), out int _qty) ? _qty : 1;
                if (qty <= 0) qty = 1;

                result.Add((glassRef, w1, h1, w2, h2, qty));
            }

            return result;
        }

        // ════════════════════════════════════════════════════════════════════
        // PASTE FROM CLIPBOARD (Bug 1: now async)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Bug 1 FIX: Clipboard.GetText() MUST run on UI thread (WPF requirement).
        /// Read clipboard here synchronously, then hand off to async pipeline.
        /// </summary>
        private async Task PasteFromClipboardAsync()
        {
            if (!Clipboard.ContainsText()) return;

            string text = Clipboard.GetText();

            GlassItemRow firstEmpty = null;
            foreach (var r in ItemRows)
            {
                if (r.IsEmpty) { firstEmpty = r; break; }
            }

            await PasteFromTextAsync(text, firstEmpty);
        }

        // ════════════════════════════════════════════════════════════════════
        // TOAST NOTIFICATION (Bug 1: replaces ALL MessageBox.Show calls)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Bug 1 FIX: Non-blocking toast. Replaces every MessageBox.Show().
        /// Dispatches at Background priority — never interrupts rendering.
        /// Auto-dismisses after 3.5 seconds.
        ///
        /// Bind in View XAML:
        ///   IsToastVisible, ToastMessage, ToastIsError
        /// </summary>
        private Task ShowToastAsync(string message, bool isError = false)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return Task.CompletedTask;

            return dispatcher.InvokeAsync(() =>
            {
                ToastMessage = message;
                ToastIsError = isError;
                IsToastVisible = true;

                var timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(3500)
                };
                timer.Tick += (s, e) =>
                {
                    IsToastVisible = false;
                    ToastMessage = "";
                    ((DispatcherTimer)s).Stop();
                };
                timer.Start();

            }, DispatcherPriority.Background).Task;
        }

        // ════════════════════════════════════════════════════════════════════
        // HELPERS (preserved exactly)
        // ════════════════════════════════════════════════════════════════════

        private static string CleanNum(string s) =>
            s?.Trim()
              .Replace(",", "")
              .Replace(" ", "")
              .Replace("–", "")
              .Replace("-", "")
              ?? "0";

        private void RenumberRows()
        {
            for (int i = 0; i < ItemRows.Count; i++)
                ItemRows[i].SerialNumber = i + 1;
        }

        private void UpdatePerimeterFactors()
        {
            int factor = _selectedGlassType == "LAM" ? 4 : 2;
            foreach (var row in ItemRows)
                row.PerimeterFactor = factor;
        }

        private void UpdateAllRowRates()
        {
            foreach (var row in ItemRows)
                row.Rate = row.Sqm1 >= 4m ? EffectiveRate : BaseRate;
        }

        public async void LoadFilteredSpecs()
        {
            FilteredSpecs.Clear();
            if (string.IsNullOrWhiteSpace(_selectedGlassType) || App.Specs == null) return;
            try
            {
                var specs = await App.Specs.GetAllAsync(_selectedGlassType);
                foreach (var x in specs) FilteredSpecs.Add(x);
            }
            catch { }
        }

        private void TryAutoFillRate()
        {
            if (string.IsNullOrWhiteSpace(_selectedSpecName)) return;
            var match = FilteredSpecs?.FirstOrDefault(s =>
                string.Equals(s.SpecName, _selectedSpecName,
                    StringComparison.OrdinalIgnoreCase));
            if (match != null) BaseRate = match.PricePerSqm;
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // CLASS: DataGridViewModel (preserved exactly)
    // ════════════════════════════════════════════════════════════════════════

    public class DataGridViewModel : ViewModelBase
    {
        public string Title { get; } = "SPECIFICATION SUB-GROUPS";
        public string Subtitle { get; } = "Specify the number of unique glass specifications in this proforma.";

        public List<string> GlassTypeOptions { get; } =
            new() { "SGU", "DGU", "LAM", "DGU+LAM" };

        public ObservableCollection<SpecGroup> SpecGroups { get; }
        public int TotalSpecGroups => SpecGroups?.Count ?? 0;

        public ICommand AddSpecGroupCommand { get; }
        public ICommand RemoveSpecGroupCommand { get; }

        /// <summary>
        /// Phase 7.10d: Read-only per-spec rollup + grand total across all spec groups.
        /// </summary>
        public GrandTotalSummaryViewModel SummaryViewModel { get; }

        public DataGridViewModel()
        {
            SpecGroups = new();
            SpecGroups.CollectionChanged += (s, e) =>
                OnPropertyChanged(nameof(TotalSpecGroups));

            AddNewSpecGroup();

            AddSpecGroupCommand = new RelayCommand(_ => AddNewSpecGroup());

            RemoveSpecGroupCommand = new RelayCommand(p =>
            {
                if (p is SpecGroup g && SpecGroups.Count > 1)
                {
                    SpecGroups.Remove(g);
                    RenumberGroups();
                }
            });

            // Wire the summary AFTER initial spec group exists
            SummaryViewModel = new GrandTotalSummaryViewModel(SpecGroups);
        }

        private void AddNewSpecGroup()
        {
            SpecGroups.Add(new SpecGroup
            {
                GroupNumber = SpecGroups.Count + 1,
                GroupName = $"Spec Group {SpecGroups.Count + 1}",
                BaseRate = 150m,
                SurchargePercent = 0m,
                SelectedGlassType = "SGU"
            });
        }

        private void RenumberGroups()
        {
            for (int i = 0; i < SpecGroups.Count; i++)
            {
                SpecGroups[i].GroupNumber = i + 1;
                if (SpecGroups[i].GroupName?.StartsWith("Spec Group ") == true)
                    SpecGroups[i].GroupName = $"Spec Group {i + 1}";
            }
        }
    }
}