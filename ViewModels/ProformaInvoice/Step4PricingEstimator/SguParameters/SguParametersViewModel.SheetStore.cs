// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/ProformaInvoice/Step4PricingEstimator/SguParameters/
//         SguParametersViewModel.SheetStore.cs
// PROJECT: ProGlassAutomation
// MODULE: Step 4 — SGU parameters ↔ Sheet Store adapter (PHASE A)
//
// ADDITIVE PARTIAL — mirror of the DGU adapter for the single-pane panel.
// The base SguParametersViewModel.cs is untouched apart from two one-line
// setter hooks (OnGlassChanged) and the ObservableCollection option lists.
//
// SGU has one pane, so it exposes one set of prices. The property names
// stay parallel to the DGU adapter (Purchase/Sell + SuggestedRate +
// SheetStoreHint) so the Step 9 engine and the two views read alike.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.ProformaInvoice;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step4PricingEstimator.SguParameters
{
    public partial class SguParametersViewModel
    {
        /// <summary>Shown when the color/thickness pair is not stocked.</summary>
        public const string NotInStoreHint =
            "This glass is not in Sheet Store — check Sheet Store or enter manually (skippable).";

        // ═══════════════════════════════════════════════════════════════
        //  SHEET SIZE OPTIONS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Sheet sizes stocked for the selected color + thickness.</summary>
        public ObservableCollection<string> SheetSizeOptions { get; } = new();

        private string _selectedSheetSize = "";
        /// <summary>
        /// Chosen sheet size ("3210 × 2250"). Editable — a typed value that
        /// is not in the store is kept verbatim.
        /// </summary>
        public string SelectedSheetSize
        {
            get => _selectedSheetSize;
            set
            {
                if (_selectedSheetSize == value) return;
                _selectedSheetSize = value ?? "";
                OnPropertyChanged();
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  READ-ONLY PRICES FROM THE STORE
        // ═══════════════════════════════════════════════════════════════

        private decimal _purchasePerSqm;
        /// <summary>Store purchase price per SQM (read-only).</summary>
        public decimal PurchasePerSqm
        {
            get => _purchasePerSqm;
            private set
            {
                if (_purchasePerSqm == value) return;
                _purchasePerSqm = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SuggestedRate));
            }
        }

        private decimal _sellPerSqm;
        /// <summary>Store selling price per SQM (read-only).</summary>
        public decimal SellPerSqm
        {
            get => _sellPerSqm;
            private set
            {
                if (_sellPerSqm == value) return;
                _sellPerSqm = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SuggestedRate));
            }
        }

        /// <summary>
        /// Suggested SGU rate per SQM: store sell price when known,
        /// otherwise the purchase price plus the panel's own cutting and
        /// tempering costs — a floor the estimator can sanity-check against.
        /// </summary>
        public decimal SuggestedRate
        {
            get
            {
                if (SellPerSqm > 0m) return Math.Round(SellPerSqm, 2);
                if (PurchasePerSqm > 0m)
                    return Math.Round(
                        PurchasePerSqm + CuttingCostPerSqm + TemperingCostPerSqm, 2);
                return 0m;
            }
        }

        private string _sheetStoreHint = "";
        /// <summary>Skippable guidance line; empty when the glass resolved.</summary>
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
        /// Merges Sheet Store colors into <c>ColorOptions</c> and refreshes
        /// sizes + prices. Idempotent; safe to call repeatedly.
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

                await RefreshFromStoreAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SguParametersViewModel] LoadSheetStoreAsync: {ex.Message}");
            }
        }

        /// <summary>Setter hook — thickness/color changed.</summary>
        private void OnGlassChanged() => FireAndForget(RefreshFromStoreAsync);

        private static async void FireAndForget(Func<Task> work)
        {
            try
            {
                await work().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SguParametersViewModel] refresh failed: {ex.Message}");
            }
        }

        private async Task RefreshFromStoreAsync()
        {
            int mm = SheetStorePricingService.ParseMm(SelectedThickness);

            var sizes = await SheetStorePricingService
                .GetSizesAsync(GlassColor, mm).ConfigureAwait(true);

            SheetSizeOptions.Clear();
            foreach (var s in sizes) SheetSizeOptions.Add(s.Display);

            if (!SheetSizeOptions.Contains(SelectedSheetSize))
                SelectedSheetSize = SheetSizeOptions.FirstOrDefault() ?? "";

            var price = await SheetStorePricingService
                .TryGetPriceAsync(GlassColor, mm).ConfigureAwait(true);

            PurchasePerSqm = price?.PurchasePerSqm ?? 0m;
            SellPerSqm = price?.SellPerSqm ?? 0m;

            SheetStoreHint = (PurchasePerSqm <= 0m && SellPerSqm <= 0m)
                ? NotInStoreHint
                : "";
        }
    }
}
