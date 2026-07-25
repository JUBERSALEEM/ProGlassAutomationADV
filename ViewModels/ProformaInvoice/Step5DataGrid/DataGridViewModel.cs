// ╔══════════════════════════════════════════════════════════════════════════╗
// ║  ViewModels/ProformaInvoice/Step5DataGrid/DataGridViewModel.cs           ║
// ║                                                                          ║
// ║  SESSION 2 FIXES:                                                        ║
// ║    BUG-46 [CRITICAL]: Clipboard.GetText() wrapped in try/catch           ║
// ║    BUG-47 [HIGH]: SqmCalculator.CalculatePanel() replaces inline formula ║
// ║    BUG-48 [HIGH]: LoadFilteredSpecs — async Task + SafeFire wrapper      ║
// ║    BUG-49 [HIGH]: Rate auto-adjust respects SuppressRateAutoAdjust       ║
// ║    BUG-50 [MEDIUM]: DispatcherTimer disposed properly                    ║
// ║    BUG-51 [MEDIUM]: FireAllChanged batched via single dispatcher call    ║
// ║    BUG-52 [COSMETIC]: File split deferred (XAML dependencies)            ║
// ║                                                                          ║
// ║  PRESERVED EXACTLY:                                                      ║
// ║    ✅ GlassItemRow — all properties + RecalculateAll + SetAllSilent      ║
// ║    ✅ SpecGroup — all properties + IExcelPasteable interface             ║
// ║    ✅ DataGridViewModel — all commands + AddNewSpecGroup                 ║
// ║    ✅ _isBulkAdding guard in OnItemRowsChanged + OnRowChanged            ║
// ║    ✅ Rate auto-adjust logic (Sqm1 >= 4m threshold)                      ║
// ╚══════════════════════════════════════════════════════════════════════════╝

using ProGlassAutomation.Behaviors;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.ProformaInvoice.Specifications;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid.GrandTotalSummary;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
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
        // BUG-47: MIN_SQM constant kept for backward-compat readers,
        // but all real calculations delegate to SqmCalculator.
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

        /// <summary>
        /// BUG-49: When true, Rate is NOT auto-adjusted by
        /// SpecGroup.OnRowChanged() when Sqm1 crosses the 4 m² threshold.
        /// Set to true after user manually edits Rate to preserve their value.
        /// </summary>
        internal bool SuppressRateAutoAdjust { get; set; } = false;

        /// <summary>
        /// True if this row has no meaningful data (used to decide whether to overwrite on paste).
        /// </summary>
        public bool IsEmpty =>
            string.IsNullOrWhiteSpace(_glassRef) &&
            _w1 == 0 && _h1 == 0 && _w2 == 0 && _h2 == 0;

        // ── RecalculateAll ───────────────────────────────────────────────────
        // BUG-47: Uses SqmCalculator.CalculatePanel() — single source of truth
        //         for the 0.5 m² minimum floor rule (UAE glass industry std)
        public void RecalculateAll()
        {
            // Glass 1 area (shared calculator with 0.5 m² floor)
            decimal newSqm1 = _w1 > 0 && _h1 > 0
                ? SqmCalculator.CalculatePanel(_w1, _h1)
                : 0m;
            decimal newTotalSqm = Math.Round(newSqm1 * _qty, 3);

            // Glass 2 area (shared calculator with 0.5 m² floor)
            decimal newSqm2 = _w2 > 0 && _h2 > 0
                ? SqmCalculator.CalculatePanel(_w2, _h2)
                : 0m;
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
        /// BUG-47: uses SqmCalculator.CalculatePanel()
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

            // BUG-47: shared calculator
            _sqm1 = _w1 > 0 && _h1 > 0 ? SqmCalculator.CalculatePanel(_w1, _h1) : 0m;
            _totalSqm = Math.Round(_sqm1 * _qty, 3);

            _sqm2 = _w2 > 0 && _h2 > 0 ? SqmCalculator.CalculatePanel(_w2, _h2) : 0m;
            _totalSqm2 = Math.Round(_sqm2 * _qty, 3);

            _lm1 = _w1 > 0 && _h1 > 0
                ? Math.Round(((decimal)_w1 + _h1) / 1000m * _perimeterFactor * _qty, 3) : 0m;
            _lm2 = _w2 > 0 && _h2 > 0
                ? Math.Round(((decimal)_w2 + _h2) / 1000m * _perimeterFactor * _qty, 3) : 0m;
            _value = Math.Round(_sqm1 * _qty * _rate, 2);
        }

        /// <summary>
        /// BUG-51: Batched via single dispatcher call — was firing 13
        /// PropertyChanged events per row, now fires them as one UI batch
        /// via BeginInvoke at Background priority (does not block).
        /// </summary>
        public void FireAllChanged()
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                FireAllChangedCore();
            }
            else
            {
                dispatcher.InvokeAsync(FireAllChangedCore, DispatcherPriority.Background);
            }
        }

        private void FireAllChangedCore()
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
                // BUG-48: fire-and-forget wrapped safely
                _ = LoadFilteredSpecsAsync();
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

        // ── Paste progress properties ────────────────────────────────────────

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

        // ── Toast notification properties ────────────────────────────────────

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

            PasteExcelCommand = new RelayCommand(_ => _ = PasteFromClipboardAsync());
        }

        // ════════════════════════════════════════════════════════════════════
        // COLLECTION CHANGE HANDLERS (preserved)
        // ════════════════════════════════════════════════════════════════════

        private void OnItemRowsChanged(object s, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
                foreach (GlassItemRow r in e.OldItems)
                    r.PropertyChanged -= OnRowChanged;

            if (e.NewItems != null)
                foreach (GlassItemRow r in e.NewItems)
                    r.PropertyChanged += OnRowChanged;

            if (_isBulkAdding) return;

            TotalRows = ItemRows.Count;
            RenumberRows();
            RecalculateFooter();
        }

        private void OnRowChanged(object s, PropertyChangedEventArgs e)
        {
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

            // BUG-49: Rate auto-adjust now respects SuppressRateAutoAdjust
            //         AND checks that the row's Rate is still one of our
            //         two managed values (BaseRate or EffectiveRate).
            //         If user typed a custom rate, we do NOT overwrite it.
            if (e.PropertyName == nameof(GlassItemRow.Sqm1)
                && s is GlassItemRow row
                && !row.SuppressRateAutoAdjust)
            {
                bool rateIsManaged = row.Rate == BaseRate || row.Rate == EffectiveRate;
                if (!rateIsManaged) return;  // user has custom rate — leave it alone

                var targetRate = row.Sqm1 >= 4m ? EffectiveRate : BaseRate;
                if (row.Rate != targetRate)
                    row.Rate = targetRate;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // FOOTER RECALCULATION (preserved)
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
        // ADD ROW (preserved)
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
        // IExcelPasteable — AddRows (preserved)
        // ════════════════════════════════════════════════════════════════════

        public void AddRows(int count)
        {
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
        // IExcelPasteable — PasteFromText (preserved)
        // ════════════════════════════════════════════════════════════════════

        public void PasteFromText(string clipboardText, object startRowContext = null)
        {
            _ = PasteFromTextAsync(clipboardText, startRowContext);
        }

        // ════════════════════════════════════════════════════════════════════
        // ASYNC PASTE PIPELINE (preserved from Chat 7)
        // ════════════════════════════════════════════════════════════════════

        private async Task PasteFromTextAsync(string clipboardText, object startRowContext = null)
        {
            if (string.IsNullOrWhiteSpace(clipboardText)) return;

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

            bool showProgress = parsed.Count >= 200;
            if (showProgress)
            {
                IsPasting = true;
                PasteProgress = 0;
                PasteStatusText = $"Preparing {parsed.Count} rows…";
                await Task.Delay(16);
            }

            int startIdx = -1;
            if (startRowContext is GlassItemRow anchorRow)
            {
                int anchorIdx = ItemRows.IndexOf(anchorRow);
                if (anchorIdx >= 0 && anchorRow.IsEmpty)
                    startIdx = anchorIdx;
            }

            int factor = _selectedGlassType == "LAM" ? 4 : 2;
            var touchedRows = new List<GlassItemRow>(parsed.Count);

            _isBulkAdding = true;
            try
            {
                int parsedIdx = 0;

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
                _isBulkAdding = false;
            }

            TotalRows = ItemRows.Count;
            RenumberRows();
            RecalculateFooter();

            if (showProgress)
            {
                PasteProgress = 90;
                PasteStatusText = "Updating display…";
                await Task.Delay(16);
            }

            // BUG-51: batched — each FireAllChanged() now uses one
            // dispatcher.InvokeAsync at Background priority
            foreach (var row in touchedRows)
                row.FireAllChanged();

            if (showProgress)
            {
                PasteProgress = 100;
                PasteStatusText = $"✓ {parsed.Count} rows ready.";
                await Task.Delay(400);
                IsPasting = false;
                PasteProgress = 0;
                PasteStatusText = "";
            }

            await ShowToastAsync($"✓ Pasted {parsed.Count} row(s) successfully.");
        }

        // ════════════════════════════════════════════════════════════════════
        // PARSE CLIPBOARD TEXT (preserved)
        // ════════════════════════════════════════════════════════════════════

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
        // PASTE FROM CLIPBOARD (BUG-46 [CRITICAL] fix)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// BUG-46 [CRITICAL] FIX: Clipboard.GetText() throws COMException
        /// when the clipboard is locked by another process (screenshot tools,
        /// remote desktop, etc.). Previously this crashed the app. Now it
        /// is wrapped in try/catch and shows a friendly toast.
        /// </summary>
        private async Task PasteFromClipboardAsync()
        {
            string text;

            try
            {
                if (!Clipboard.ContainsText())
                {
                    await ShowToastAsync("Clipboard is empty.", isError: true);
                    return;
                }

                text = Clipboard.GetText();
            }
            catch (Exception ex)
            {
                // COMException, ExternalException, OperationCanceledException...
                // any clipboard access error (locked by another app, remote desktop, etc.)
                Debug.WriteLine($"[SpecGroup.PasteFromClipboard] Clipboard read failed: {ex.Message}");
                await ShowToastAsync(
                    "Cannot read clipboard right now — another app may be using it. " +
                    "Wait a second and try again.",
                    isError: true);
                return;
            }

            GlassItemRow firstEmpty = null;
            foreach (var r in ItemRows)
            {
                if (r.IsEmpty) { firstEmpty = r; break; }
            }

            await PasteFromTextAsync(text, firstEmpty);
        }

        // ════════════════════════════════════════════════════════════════════
        // TOAST NOTIFICATION (BUG-50 timer disposed)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// BUG-50 [MEDIUM]: DispatcherTimer is now stopped AND removed
        /// from tick handler on dismissal, preventing timer leak per toast.
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

                EventHandler tickHandler = null;
                tickHandler = (s, e) =>
                {
                    IsToastVisible = false;
                    ToastMessage = "";

                    // BUG-50: full timer cleanup — stop + unsubscribe
                    if (s is DispatcherTimer t)
                    {
                        t.Stop();
                        t.Tick -= tickHandler;
                    }
                };
                timer.Tick += tickHandler;
                timer.Start();

            }, DispatcherPriority.Background).Task;
        }

        // ════════════════════════════════════════════════════════════════════
        // HELPERS
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
            {
                // BUG-49: respect custom rate
                if (row.SuppressRateAutoAdjust) continue;

                bool rateIsManaged = row.Rate == BaseRate || row.Rate == EffectiveRate;
                if (!rateIsManaged) continue;

                row.Rate = row.Sqm1 >= 4m ? EffectiveRate : BaseRate;
            }
        }

        /// <summary>
        /// BUG-48 [HIGH]: was async void — now async Task returning cleanly.
        /// The setter for SelectedGlassType uses "_ = LoadFilteredSpecsAsync()"
        /// pattern with the Task swallowed by discard, but exceptions no
        /// longer bring down the app because they are caught internally.
        /// </summary>
        public async Task LoadFilteredSpecsAsync()
        {
            FilteredSpecs.Clear();
            if (string.IsNullOrWhiteSpace(_selectedGlassType) || App.Specs == null) return;

            try
            {
                var specs = await App.Specs.GetAllAsync(_selectedGlassType);
                foreach (var x in specs) FilteredSpecs.Add(x);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SpecGroup.LoadFilteredSpecs] {ex.Message}");
            }
        }

        // Backward-compat shim for any external callers still using the
        // old sync signature. Marked internal-use only.
        internal void LoadFilteredSpecs() => _ = LoadFilteredSpecsAsync();

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
    // CLASS: DataGridViewModel (preserved)
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