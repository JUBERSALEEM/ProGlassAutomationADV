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
using ProGlassAutomation.ViewModels.Validation;
using System;
using System.Globalization;
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
            set { if (_category == value) return; _category = value; Raise(); Revalidate(); }
        }

        private string _thicknessText = "6";
        public string ThicknessText
        {
            get => _thicknessText;
            set { if (_thicknessText == value) return; _thicknessText = value; Raise(); Revalidate(); }
        }

        private string _color = "Clear";
        public string Color
        {
            get => _color;
            set { if (_color == value) return; _color = value; Raise(); Revalidate(); }
        }

        private string _widthText = "3210";
        public string WidthText
        {
            get => _widthText;
            set { if (_widthText == value) return; _widthText = value; Raise(); Revalidate(); }
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
