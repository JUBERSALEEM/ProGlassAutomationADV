// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/ProformaInvoice/Step4PricingEstimator/DguParameters/
//         DguParametersViewModel.SheetStore.cs
// PROJECT: ProGlassAutomation
// MODULE: Step 4 — DGU parameters ↔ Sheet Store adapter (PHASE A)
//
// ADDITIVE PARTIAL. The original DguParametersViewModel.cs is untouched
// except for four one-line setter hooks (OnOuterGlassChanged /
// OnInnerGlassChanged) and the ObservableCollection option lists.
//
// WHAT THIS ADDS
//   • Per-side sheet-size pickers   (Outer/InnerSheetSizeOptions +
//     SelectedOuterSheetSize / SelectedInnerSheetSize)
//   • Per-side read-only prices     (…PurchasePerSqm / …SellPerSqm)
//   • SuggestedRate                 — glass cost hint for the whole unit
//   • SheetStoreHint                — one skippable warning line
//   • LoadSheetStoreAsync()         — merges store colors into ColorOptions
//
// DESIGN RULES
//   • Everything here is a HINT. Nothing overwrites the user's own
//     OuterBasePrice / InnerBasePrice — the estimator stays manual-first.
//   • Refresh is fire-and-forget (setters cannot await) and each pass is
//     wrapped so a Sheet Store failure can never surface as a crash.
//   • Read-only outputs use private setters; the View binds them one-way.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.ProformaInvoice;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step4PricingEstimator.DguParameters
{
    public partial class DguParametersViewModel
    {
        /// <summary>Shown when a color/thickness pair is not stocked.</summary>
        public const string NotInStoreHint =
            "This glass is not in Sheet Store — check Sheet Store or enter manually (skippable).";

        // ═══════════════════════════════════════════════════════════════
        //  SHEET SIZE OPTIONS (editable — user may type a custom size)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Sheet sizes stocked for the OUTER color + thickness.</summary>
        public ObservableCollection<string> OuterSheetSizeOptions { get; } = new();

        /// <summary>Sheet sizes stocked for the INNER color + thickness.</summary>
        public ObservableCollection<string> InnerSheetSizeOptions { get; } = new();

        private string _selectedOuterSheetSize = "";
        /// <summary>
        /// Chosen outer sheet size ("3210 × 2250"). Editable — a typed
        /// value that is not in the store is kept verbatim.
        /// </summary>
        public string SelectedOuterSheetSize
        {
            get => _selectedOuterSheetSize;
            set
            {
                if (_selectedOuterSheetSize == value) return;
                _selectedOuterSheetSize = value ?? "";
                OnPropertyChanged();
            }
        }

        private string _selectedInnerSheetSize = "";
        /// <summary>Chosen inner sheet size. See SelectedOuterSheetSize.</summary>
        public string SelectedInnerSheetSize
        {
            get => _selectedInnerSheetSize;
            set
            {
                if (_selectedInnerSheetSize == value) return;
                _selectedInnerSheetSize = value ?? "";
                OnPropertyChanged();
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  READ-ONLY PRICES FROM THE STORE
        // ═══════════════════════════════════════════════════════════════

        private decimal _outerPurchasePerSqm;
        /// <summary>Store purchase price per SQM for the outer pane (read-only).</summary>
        public decimal OuterPurchasePerSqm
        {
            get => _outerPurchasePerSqm;
            private set
            {
                if (_outerPurchasePerSqm == value) return;
                _outerPurchasePerSqm = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SuggestedRate));
            }
        }

        private decimal _outerSellPerSqm;
        /// <summary>Store selling price per SQM for the outer pane (read-only).</summary>
        public decimal OuterSellPerSqm
        {
            get => _outerSellPerSqm;
            private set
            {
                if (_outerSellPerSqm == value) return;
                _outerSellPerSqm = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SuggestedRate));
            }
        }

        private decimal _innerPurchasePerSqm;
        /// <summary>Store purchase price per SQM for the inner pane (read-only).</summary>
        public decimal InnerPurchasePerSqm
        {
            get => _innerPurchasePerSqm;
            private set
            {
                if (_innerPurchasePerSqm == value) return;
                _innerPurchasePerSqm = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SuggestedRate));
            }
        }

        private decimal _innerSellPerSqm;
        /// <summary>Store selling price per SQM for the inner pane (read-only).</summary>
        public decimal InnerSellPerSqm
        {
            get => _innerSellPerSqm;
            private set
            {
                if (_innerSellPerSqm == value) return;
                _innerSellPerSqm = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SuggestedRate));
            }
        }

        /// <summary>
        /// Suggested DGU rate per SQM = outer sell + inner sell.
        /// Falls back to purchase price for a side with no sell price set.
        /// Zero when the store knows neither pane — the UI then hides it.
        /// </summary>
        public decimal SuggestedRate
        {
            get
            {
                decimal outer = OuterSellPerSqm > 0m ? OuterSellPerSqm : OuterPurchasePerSqm;
                decimal inner = InnerSellPerSqm > 0m ? InnerSellPerSqm : InnerPurchasePerSqm;
                return Math.Round(outer + inner, 2);
            }
        }

        private string _sheetStoreHint = "";
        /// <summary>
        /// Skippable guidance line. Empty when both panes resolved.
        /// Bound to a WarningBrush TextBlock in the view.
        /// </summary>
        public string SheetStoreHint
        {
            get => _sheetStoreHint;
            private set
            {
                if (_sheetStoreHint == value) return;
                _sheetStoreHint = value ?? "";
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSheetStoreHint));
            }
        }

        /// <summary>True when <see cref="SheetStoreHint"/> has content.</summary>
        public bool HasSheetStoreHint => !string.IsNullOrWhiteSpace(SheetStoreHint);

        // ═══════════════════════════════════════════════════════════════
        //  LOAD / REFRESH
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Merges Sheet Store colors into <c>ColorOptions</c> (never removes
        /// the built-in list) and refreshes both sides. Safe to call more
        /// than once — merging is idempotent.
        /// </summary>
        public async Task LoadSheetStoreAsync()
        {
            try
            {
                var colors = await SheetStorePricingService.GetColorsAsync()
                    .ConfigureAwait(true);

                foreach (var color in colors)
                {
                    bool exists = ColorOptions.Any(c =>
                        SheetStorePricingService.Norm(c) ==
                        SheetStorePricingService.Norm(color));

                    if (!exists) ColorOptions.Add(color);
                }

                await RefreshOuterFromStoreAsync().ConfigureAwait(true);
                await RefreshInnerFromStoreAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DguParametersViewModel] LoadSheetStoreAsync: {ex.Message}");
            }
        }

        /// <summary>Setter hook — outer thickness/color changed.</summary>
        private void OnOuterGlassChanged() => FireAndForget(RefreshOuterFromStoreAsync);

        /// <summary>Setter hook — inner thickness/color changed.</summary>
        private void OnInnerGlassChanged() => FireAndForget(RefreshInnerFromStoreAsync);

        private static async void FireAndForget(Func<Task> work)
        {
            try
            {
                await work().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                // Pricing hints must never surface as a crash dialog.
                Debug.WriteLine($"[DguParametersViewModel] refresh failed: {ex.Message}");
            }
        }

        private async Task RefreshOuterFromStoreAsync()
        {
            int mm = SheetStorePricingService.ParseMm(OuterThickness);

            var sizes = await SheetStorePricingService
                .GetSizesAsync(OuterGlassColor, mm).ConfigureAwait(true);

            ReplaceSizes(OuterSheetSizeOptions, sizes.Select(s => s.Display));

            if (!OuterSheetSizeOptions.Contains(SelectedOuterSheetSize))
                SelectedOuterSheetSize = OuterSheetSizeOptions.FirstOrDefault() ?? "";

            var price = await SheetStorePricingService
                .TryGetPriceAsync(OuterGlassColor, mm).ConfigureAwait(true);

            OuterPurchasePerSqm = price?.PurchasePerSqm ?? 0m;
            OuterSellPerSqm = price?.SellPerSqm ?? 0m;

            UpdateHint();
        }

        private async Task RefreshInnerFromStoreAsync()
        {
            int mm = SheetStorePricingService.ParseMm(InnerThickness);

            var sizes = await SheetStorePricingService
                .GetSizesAsync(InnerGlassColor, mm).ConfigureAwait(true);

            ReplaceSizes(InnerSheetSizeOptions, sizes.Select(s => s.Display));

            if (!InnerSheetSizeOptions.Contains(SelectedInnerSheetSize))
                SelectedInnerSheetSize = InnerSheetSizeOptions.FirstOrDefault() ?? "";

            var price = await SheetStorePricingService
                .TryGetPriceAsync(InnerGlassColor, mm).ConfigureAwait(true);

            InnerPurchasePerSqm = price?.PurchasePerSqm ?? 0m;
            InnerSellPerSqm = price?.SellPerSqm ?? 0m;

            UpdateHint();
        }

        private static void ReplaceSizes(
            ObservableCollection<string> target,
            System.Collections.Generic.IEnumerable<string> values)
        {
            target.Clear();
            foreach (var v in values) target.Add(v);
        }

        private void UpdateHint()
        {
            bool outerMissing = OuterPurchasePerSqm <= 0m && OuterSellPerSqm <= 0m;
            bool innerMissing = InnerPurchasePerSqm <= 0m && InnerSellPerSqm <= 0m;

            SheetStoreHint = (outerMissing || innerMissing)
                ? NotInStoreHint
                : "";
        }
    }
}
