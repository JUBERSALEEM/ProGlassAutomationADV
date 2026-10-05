// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/SheetStore/AddEditSheetViewModel.cs
//
// 2026-09-06 — BLOCKING, PER-FIELD VALIDATION.
//
// WHAT WAS WRONG
//
// This VM was the module's best-behaved: it had a real Validate() with
// specific messages. But it was ADVISORY, not blocking, in three ways:
//
//   1. SaveCommand = new RelayCommand(_ => Save())  — no CanExecute. The
//      button was always live; validation ran AFTER the click.
//   2. Validation was fail-FAST into a single shared ErrorMessage line.
//      Fix the category, click again, discover the thickness is also
//      wrong. On a nine-field form that is nine round trips to learn
//      everything wrong with the form.
//   3. The int/decimal bindings could not observe an EMPTY box —
//      conversion fails, the setter never runs, the previous value
//      silently persists behind a blank-looking control. Clearing Stock
//      and saving therefore wrote back the OLD stock, with no warning.
//
// Fault 3 is the same defect that caused the reported "0 total buy still
// saves" bug in BuyDialogViewModel, and it is the reason every numeric
// entry point in this module now binds RAW TEXT and parses explicitly.
//
// NOW
//   • Every field validates on keystroke, all at once, into its own
//     inline message under its own box.
//   • SaveCommand gates on IsValid — the button is visibly dead until
//     the form is committable.
//   • Save() re-validates anyway. CanExecute is a UI affordance, never a
//     security boundary; a keyboard accelerator or automation peer can
//     still invoke a command whose button is disabled.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.SheetStore;
using ProGlassAutomation.Services.SheetStore;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.ViewModels.Shared;
using ProGlassAutomation.ViewModels.Validation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.SheetStore
{
    /// <summary>
    /// ViewModel for the Add/Edit sheet form.
    /// Holds field values + per-field validation + result.
    /// </summary>
    public class AddEditSheetViewModel : ValidatableViewModelBase
    {
        private readonly GlassSheet? _existing;

        public bool IsEditMode => _existing != null;
        public string DialogTitle => IsEditMode ? "Edit Glass Sheet" : "Add Glass Sheet";
        public string SaveButtonText => IsEditMode ? "Update" : "Add";

        // ═══════════════════════════════════════════════════════════════
        //  FORM FIELDS
        //
        //  Text-backed. The typed value is an OUTPUT of validation, never
        //  the binding surface — see the header note on fault 3.
        // ═══════════════════════════════════════════════════════════════

        private string _category = string.Empty;
        public string Category
        {
            get => _category;
            set
            {
                if (_category == value) return;
                _category = value;
                Raise();
                _sizeTouched = true;
                RefreshSizeOptions();   // cascade: sizes follow cat/thk/colour
                Revalidate();
            }
        }

        private string _thicknessText = "6";
        public string ThicknessText
        {
            get => _thicknessText;
            set
            {
                if (_thicknessText == value) return;
                _thicknessText = value;
                Raise();
                _sizeTouched = true;
                RefreshSizeOptions();   // cascade: sizes follow cat/thk/colour
                Revalidate();
            }
        }

        private string _color = "Clear";
        public string Color
        {
            get => _color;
            set
            {
                if (_color == value) return;
                _color = value;
                Raise();
                _sizeTouched = true;
                RefreshSizeOptions();   // cascade: sizes follow cat/thk/colour
                Revalidate();
            }
        }

        private string _widthText = "3210";
        public string WidthText
        {
            get => _widthText;
            set
            {
                if (_widthText == value) return;
                _widthText = value;
                Raise();
                _sizeTouched = true;
                RefreshHeightOptions();   // cascade: height follows width
                Revalidate();
            }
        }

        private string _heightText = "2250";
        public string HeightText
        {
            get => _heightText;
            set { if (_heightText == value) return; _heightText = value; Raise(); Revalidate(); }
        }

        private string _supplier = string.Empty;
        public string Supplier
        {
            get => _supplier;
            set { if (_supplier == value) return; _supplier = value; Raise(); Revalidate(); }
        }

        private string _purchasePriceText = "0";
        public string PurchasePriceText
        {
            get => _purchasePriceText;
            set { if (_purchasePriceText == value) return; _purchasePriceText = value; Raise(); Revalidate(); }
        }

        private string _sellPriceText = "0";
        public string SellPriceText
        {
            get => _sellPriceText;
            set { if (_sellPriceText == value) return; _sellPriceText = value; Raise(); Revalidate(); }
        }

        private string _stockText = "0";
        public string StockText
        {
            get => _stockText;
            set { if (_stockText == value) return; _stockText = value; Raise(); Revalidate(); }
        }

        // ── Parsed outputs (only valid when IsValid) ───────────────────
        public int ThicknessMm { get; private set; } = 6;
        public int WidthMm { get; private set; } = 3210;
        public int HeightMm { get; private set; } = 2250;
        public decimal PurchasePricePerSqm { get; private set; }
        public decimal SellPricePerSqm { get; private set; }
        public int Stock { get; private set; }

        // ── Per-field error surfaces for the view ──────────────────────
        public string CategoryError => ErrorFor(nameof(Category));
        public string ThicknessError => ErrorFor(nameof(ThicknessText));
        public string ColorError => ErrorFor(nameof(Color));
        public string WidthError => ErrorFor(nameof(WidthText));
        public string HeightError => ErrorFor(nameof(HeightText));
        public string SupplierError => ErrorFor(nameof(Supplier));
        public string PurchasePriceError => ErrorFor(nameof(PurchasePriceText));
        public string SellPriceError => ErrorFor(nameof(SellPriceText));
        public string StockError => ErrorFor(nameof(StockText));

        public bool HasCategoryError => !string.IsNullOrEmpty(CategoryError);
        public bool HasThicknessError => !string.IsNullOrEmpty(ThicknessError);
        public bool HasColorError => !string.IsNullOrEmpty(ColorError);
        public bool HasWidthError => !string.IsNullOrEmpty(WidthError);
        public bool HasHeightError => !string.IsNullOrEmpty(HeightError);
        public bool HasSupplierError => !string.IsNullOrEmpty(SupplierError);
        public bool HasPurchasePriceError => !string.IsNullOrEmpty(PurchasePriceError);
        public bool HasSellPriceError => !string.IsNullOrEmpty(SellPriceError);
        public bool HasStockError => !string.IsNullOrEmpty(StockError);

        /// <summary>Legacy summary line, kept so existing XAML keeps working.</summary>
        public string ErrorMessage => ValidationSummary;
        public bool HasError => HasValidationSummary;

        // ── Live preview ───────────────────────────────────────────────

        /// <summary>
        /// Area of one sheet. Shown live so the operator can sanity-check
        /// the dimensions before committing — a transposed digit in a
        /// width is otherwise invisible until the stock value looks wrong.
        /// </summary>
        public decimal SqmPerSheet => IsValid
            ? ProGlassAutomation.Utilities.SqmCalculator.CalculateSheetArea(WidthMm, HeightMm)
            : 0m;

        public decimal TotalStockValue => SqmPerSheet * Stock * PurchasePricePerSqm;

        /// <summary>
        /// Selling below cost is legal but almost always a typo, so it is
        /// surfaced as a non-blocking WARNING. Blocking it would be wrong:
        /// clearance and sample stock genuinely sell at a loss.
        /// </summary>
        public bool HasMarginWarning =>
            IsValid && SellPricePerSqm > 0 && SellPricePerSqm < PurchasePricePerSqm;

        public string MarginWarning => HasMarginWarning
            ? "Sell price is below purchase price — this sheet will trade at a loss."
            : string.Empty;

        // ============ RESULT ============
        public GlassSheet? Result { get; private set; }
        public bool? DialogResult { get; private set; }

        // ============ COMMANDS ============
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public Action? CloseAction { get; set; }

        // ============ CONSTRUCTORS ============
        // ═══════════════════════════════════════════════════════════════
        //  PICKER OPTIONS
        //
        //  Every one of these is an EDITABLE combo, never a closed list.
        //  The stock trade adds thicknesses and mill sizes constantly, and
        //  a closed list would make a legitimate new sheet unenterable —
        //  the operator would have no way to record 5.5mm or a bespoke cut.
        //  The dropdown is a shortcut for the 95% case, not a gate.
        //
        //  Seeded with the industry standards, then UNIONED with whatever
        //  the database already holds, so a size someone typed last month
        //  is offered as a click this month.
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Standard float thicknesses, 3mm through 19mm.</summary>
        private static readonly int[] StandardThicknesses =
            { 3, 4, 5, 6, 8, 10, 12, 15, 19 };

        /// <summary>
        /// Standard mill sheet sizes. Width is the key; a width can carry
        /// SEVERAL heights (3210 comes as both 2250 normal and 2440), which
        /// is why HeightOptions is a list rather than a single lookup.
        /// </summary>
        private static readonly (int Width, int Height, string Label)[] StandardSizes =
        {
            (3210, 2250, "Normal"),
            (3210, 2440, "Oversize"),
            (3660, 2440, "Jumbo"),
            (3660, 2550, "Jumbo tall"),
            (2440, 1830, "Half"),
            (2140, 1650, "Small"),
        };

        public ObservableCollection<string> CategoryOptions { get; } = new();
        public ObservableCollection<string> ThicknessOptions { get; } = new();
        public ObservableCollection<string> ColorOptions { get; } = new();
        public ObservableCollection<string> SupplierOptions { get; } = new();
        public ObservableCollection<string> WidthOptions { get; } = new();
        public ObservableCollection<string> HeightOptions { get; } = new();

        /// <summary>
        /// Loads the pickers. Fire-and-forget from the constructor: the form
        /// is fully usable before this returns, because every field is a
        /// free-text combo. A failed load costs the shortcuts, not the form.
        /// </summary>
        private async Task LoadOptionsAsync()
        {
            List<string> cats = new(), colors = new(), suppliers = new();
            List<int> thicknesses = new();
            List<GlassSheet> all = new();

            try
            {
                var repo = new SheetStoreRepository();
                cats = await repo.GetCategoriesAsync().ConfigureAwait(false);
                colors = await repo.GetColorsAsync().ConfigureAwait(false);
                suppliers = await repo.GetSuppliersAsync().ConfigureAwait(false);
                thicknesses = await repo.GetThicknessesAsync().ConfigureAwait(false);
                all = await repo.GetAllAsync().ConfigureAwait(false);
            }
            catch
            {
                ProGlassAutomation.Services.Logging.AppLogService.Debug("LoadOptionsAsync suppressed");
            }

            // Real stocked sheets, kept WHOLE. The size cascade filters on
            // category + thickness + colour, so a flat (width,height) list
            // would throw away exactly the columns it needs.
            _stocked = all.Where(g => g.WidthMm > 0 && g.HeightMm > 0).ToList();

            UiDispatch.Run(() =>
            {
                Fill(CategoryOptions, cats);
                Fill(ColorOptions, MergeText(colors,
                    new[] { "Clear", "HD Grey", "Bronze", "Green", "Blue", "Silver" }));
                Fill(SupplierOptions, suppliers);
                Fill(ThicknessOptions, MergeNumeric(thicknesses, StandardThicknesses));
                RefreshSizeOptions();
            });
        }

        private List<GlassSheet> _stocked = new();

        /// <summary>
        /// True once the operator has changed category, thickness, colour or
        /// width by hand. Until then an EXISTING sheet's stored size is left
        /// exactly as saved — see RefreshSizeOptions.
        /// </summary>
        private bool _sizeTouched;

        /// <summary>
        /// Sizes available for the CURRENT category + thickness + colour.
        ///
        /// Narrowed progressively rather than all-or-nothing: an exact
        /// three-way match is best, but a half-filled form must still offer
        /// something useful, so each criterion is dropped in turn before
        /// falling back to the mill standards. Returning an empty list
        /// whenever the combination is new would make the dropdowns vanish
        /// exactly when the operator is entering unfamiliar stock.
        /// </summary>
        private List<(int Width, int Height)> CandidateSizes()
        {
            bool HasCat = !string.IsNullOrWhiteSpace(Category);
            bool HasCol = !string.IsNullOrWhiteSpace(Color);
            bool HasThk = int.TryParse(ThicknessText?.Trim(), out int thk);
            if (!HasThk) thk = -1;

            IEnumerable<GlassSheet> Match(bool cat, bool col, bool t) =>
                _stocked.Where(g =>
                    (!cat || string.Equals(g.Category, Category, StringComparison.OrdinalIgnoreCase)) &&
                    (!col || string.Equals(g.Color, Color, StringComparison.OrdinalIgnoreCase)) &&
                    (!t   || g.ThicknessMm == thk));

            // Most specific first.
            foreach (var (cat, col, t) in new[]
                     {
                         (HasCat, HasCol, HasThk),   // category + colour + thickness
                         (HasCat, HasCol, false),    // category + colour
                         (false,  HasCol, HasThk),   // colour + thickness
                         (false,  HasCol, false),    // colour alone
                         (HasCat, false,  false),    // category alone
                     })
            {
                if (!cat && !col && !t) continue;
                var hit = Match(cat, col, t)
                    .Select(g => (g.WidthMm, g.HeightMm))
                    .Distinct().ToList();
                if (hit.Count > 0) return Order(hit);
            }

            // Nothing stocked resembles this combination — offer the mill
            // standards so the operator still gets one-click sizes.
            return Order(StandardSizes.Select(x => (x.Width, x.Height)).ToList());
        }

        private static List<(int Width, int Height)> Order(List<(int, int)> src) =>
            src.OrderBy(x => x.Item1).ThenBy(x => x.Item2)
               .Select(x => (Width: x.Item1, Height: x.Item2)).ToList();

        /// <summary>
        /// Rebuilds WidthOptions for the current category/thickness/colour,
        /// then re-runs the width to height cascade.
        /// </summary>
        private void RefreshSizeOptions()
        {
            var sizes = CandidateSizes();

            Fill(WidthOptions, sizes.Select(x => x.Width).Distinct()
                                    .Select(w => w.ToString(CultureInfo.InvariantCulture)));

            // If the current width is not offered for this combination, move
            // to the first one that is — otherwise the height list below
            // would be empty and the operator would be stuck.
            //
            // EXCEPT when editing: an existing sheet's saved size is the
            // truth, even if it is a bespoke cut nobody else stocks.
            // Snapping it to a "standard" would silently rewrite real
            // inventory the moment the operator opened the tab to change a
            // price. The dropdown still lists the candidates; it just does
            // not overrule what is already on the record.
            if (!_sizeTouched && IsEditMode) { RefreshHeightOptions(); return; }

            if (WidthOptions.Count > 0 &&
                (!int.TryParse(WidthText?.Trim(), out int cw) ||
                 !sizes.Any(x => x.Width == cw)))
            {
                WidthText = WidthOptions[0];   // setter re-enters the cascade
                return;
            }

            RefreshHeightOptions();
        }

        /// <summary>
        /// Heights available for the CURRENT width. A width with one known
        /// height auto-fills it; a width with several (3210 → 2250 or 2440)
        /// leaves the choice open rather than guessing.
        /// </summary>
        private void RefreshHeightOptions()
        {
            if (!int.TryParse(WidthText?.Trim(), out int w)) return;

            var heights = CandidateSizes().Where(x => x.Width == w)
                                          .Select(x => x.Height)
                                          .Distinct().OrderBy(h => h).ToList();
            if (heights.Count == 0) return;

            Fill(HeightOptions, heights.Select(h => h.ToString(CultureInfo.InvariantCulture)));

            // Auto-fill ONLY when unambiguous, and never over a height that
            // already matches this width — silently rewriting a deliberate
            // entry is worse than leaving it alone.
            if (heights.Count == 1)
            {
                var only = heights[0].ToString(CultureInfo.InvariantCulture);
                if (HeightText != only) HeightText = only;
            }
            else if (!int.TryParse(HeightText?.Trim(), out int h) || !heights.Contains(h))
            {
                HeightText = heights[0].ToString(CultureInfo.InvariantCulture);
            }
        }

        private static void Fill(ObservableCollection<string> target, IEnumerable<string> values)
        {
            target.Clear();
            foreach (var v in values) target.Add(v);
        }

        private static IEnumerable<string> MergeText(IEnumerable<string> db, IEnumerable<string> standards) =>
            standards.Concat(db.Where(x => !string.IsNullOrWhiteSpace(x)))
                     .Select(x => x.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);

        private static IEnumerable<string> MergeNumeric(IEnumerable<int> db, IEnumerable<int> standards) =>
            standards.Concat(db).Where(x => x > 0).Distinct().OrderBy(x => x)
                     .Select(x => x.ToString(CultureInfo.InvariantCulture));

        public AddEditSheetViewModel() : this(null) { }

        public AddEditSheetViewModel(GlassSheet? existing)
        {
            _existing = existing;

            if (existing != null)
            {
                var ci = CultureInfo.CurrentCulture;
                _category = existing.Category;
                _thicknessText = existing.ThicknessMm.ToString(ci);
                _color = existing.Color;
                _widthText = existing.WidthMm.ToString(ci);
                _heightText = existing.HeightMm.ToString(ci);
                _supplier = existing.Supplier;
                _purchasePriceText = existing.PurchasePricePerSqm.ToString("0.##", ci);
                _sellPriceText = existing.SellPricePerSqm.ToString("0.##", ci);
                _stockText = existing.Stock.ToString(ci);
            }

            AsyncUtility.SafeFireAndForget(LoadOptionsAsync, "AddEditSheet LoadOptions");

            SaveCommand = new RelayCommand(_ => Save(), _ => IsValid);
            CancelCommand = new RelayCommand(_ => Cancel());

            Revalidate();
        }

        /// <summary>
        /// Validates EVERY field on each keystroke — deliberately not
        /// fail-fast. The operator should see every outstanding problem
        /// at once rather than discovering them one click at a time.
        /// </summary>
        private void Revalidate()
        {
            var category = ParseRequiredText(Category, nameof(Category), "Category", maxLength: 100);
            if (category != null) { /* value used at Save */ }

            var thickness = ParseQuantity(ThicknessText, nameof(ThicknessText), "thickness (mm)");
            if (thickness.HasValue) ThicknessMm = thickness.Value;

            ParseRequiredText(Color, nameof(Color), "Colour", maxLength: 50);

            var width = ParseQuantity(WidthText, nameof(WidthText), "width (mm)");
            if (width.HasValue) WidthMm = width.Value;

            var height = ParseQuantity(HeightText, nameof(HeightText), "height (mm)");
            if (height.HasValue) HeightMm = height.Value;

            ParseRequiredText(Supplier, nameof(Supplier), "Supplier", maxLength: 100);

            // Prices MAY be zero — free samples and consignment stock are
            // real. Quantities may not.
            var purchase = ParseDecimal(PurchasePriceText, nameof(PurchasePriceText),
                                        "purchase price", allowZero: true);
            if (purchase.HasValue) PurchasePricePerSqm = purchase.Value;

            var sell = ParseDecimal(SellPriceText, nameof(SellPriceText),
                                    "sell price", allowZero: true);
            if (sell.HasValue) SellPricePerSqm = sell.Value;

            // Opening stock of 0 is legitimate — you routinely create the
            // item before the delivery arrives. So this parses as a plain
            // non-negative integer rather than through ParseQuantity,
            // which rejects 0 by design.
            ParseOpeningStock();

            foreach (var n in new[]
            {
                nameof(CategoryError), nameof(ThicknessError), nameof(ColorError),
                nameof(WidthError), nameof(HeightError), nameof(SupplierError),
                nameof(PurchasePriceError), nameof(SellPriceError), nameof(StockError),
                nameof(HasCategoryError), nameof(HasThicknessError), nameof(HasColorError),
                nameof(HasWidthError), nameof(HasHeightError), nameof(HasSupplierError),
                nameof(HasPurchasePriceError), nameof(HasSellPriceError), nameof(HasStockError),
                nameof(ErrorMessage), nameof(HasError),
                nameof(SqmPerSheet), nameof(TotalStockValue),
                nameof(HasMarginWarning), nameof(MarginWarning),
                nameof(ThicknessMm), nameof(WidthMm), nameof(HeightMm),
                nameof(PurchasePricePerSqm), nameof(SellPricePerSqm), nameof(Stock)
            })
                OnPropertyChanged(n);
        }

        private void ParseOpeningStock()
        {
            var raw = StockText?.Trim();

            if (string.IsNullOrEmpty(raw))
            {
                SetError(nameof(StockText), "Enter an opening stock (0 is allowed).");
                return;
            }

            if (!int.TryParse(raw, out int value))
            {
                SetError(nameof(StockText), "Opening stock must be a whole number.");
                return;
            }

            if (value < 0)
            {
                SetError(nameof(StockText), "Opening stock cannot be negative.");
                return;
            }

            // Editing an existing row must not silently strand consumption:
            // if 20 sheets are already recorded as used, stock cannot drop
            // below 20 or Balance goes negative and the ledger is corrupt.
            if (_existing != null && value < _existing.Used)
            {
                SetError(nameof(StockText),
                    $"Stock cannot be less than {_existing.Used} — that many " +
                    "sheet(s) are already recorded as used.");
                return;
            }

            SetError(nameof(StockText), null);
            Stock = value;
        }

        private void Save()
        {
            // Re-validate even though CanExecute gates the button.
            // CanExecute is an affordance, not a boundary — keyboard
            // accelerators and automation peers can still get here.
            Revalidate();
            if (!IsValid) return;

            Result = _existing ?? new GlassSheet();
            Result.Category = Category.Trim();
            Result.ThicknessMm = ThicknessMm;
            Result.Color = Color.Trim();
            Result.WidthMm = WidthMm;
            Result.HeightMm = HeightMm;
            Result.Supplier = Supplier.Trim();
            Result.PurchasePricePerSqm = PurchasePricePerSqm;
            Result.SellPricePerSqm = SellPricePerSqm;
            Result.Stock = Stock;

            DialogResult = true;
            CloseAction?.Invoke();
        }

        private void Cancel()
        {
            DialogResult = false;
            CloseAction?.Invoke();
        }
    }
}
