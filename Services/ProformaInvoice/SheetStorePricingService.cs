// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/ProformaInvoice/SheetStorePricingService.cs
// PROJECT: ProGlassAutomation
// MODULE: Proforma Invoice ↔ Sheet Store pricing bridge (PHASE A)
//
// PURPOSE
//   One read-only lookup surface that lets the Step 4 pricing panels and
//   the Step 9 Profit & Loss engine ask the Sheet Store inventory:
//
//     "which colors do we stock?"                → GetColorsAsync()
//     "which thicknesses for that color?"        → GetThicknessesAsync(color)
//     "which sheet sizes for color + thickness?" → GetSizesAsync(color, thk)
//     "what does that cost / sell for per SQM?"  → TryGetPriceAsync(color, thk)
//
// CONTRACT (deliberate, do not relax)
//   • STATIC        — no DI wiring needed, callable from any VM.
//   • READ-ONLY     — never writes, never mutates stock. Uses the existing
//                     SheetStoreRepository.GetAllAsync() only, so it stays
//                     correct if the Sheet Store schema evolves.
//   • NEVER THROWS  — every public method is fully guarded. A missing DB,
//                     a locked file, an empty table: all degrade to an
//                     empty list / null. Pricing help is a convenience,
//                     it must never break the invoice wizard.
//   • MATCHING      — Norm() (trim + lowercase) on both sides so
//                     " clear ", "Clear" and "CLEAR" are one color.
//                     ParseMm("6mm") → 6 so UI strings map to DB integers.
//
// NOTE ON CACHING: intentionally none. Sheet Store rows change while the
// wizard is open (buy / use stock) and a full GetAllAsync() on a local
// SQLite table is cheap. Correctness beats micro-optimisation here.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.SheetStore;
using ProGlassAutomation.Services.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.ProformaInvoice
{
    /// <summary>
    /// Read-only pricing/size lookups over the Sheet Store inventory,
    /// shaped for the Proforma Invoice wizard. Static, additive, and
    /// guaranteed non-throwing (see file header contract).
    /// </summary>
    public static class SheetStorePricingService
    {
        // ═══════════════════════════════════════════════════════════════
        //  HELPERS — normalisation
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Canonical form used for every comparison: trimmed + lowercase.
        /// Null-safe (null → empty string).
        /// </summary>
        public static string Norm(string? value) =>
            (value ?? string.Empty).Trim().ToLowerInvariant();

        /// <summary>
        /// Parses a UI thickness string into millimetres.
        /// "6mm" → 6 · " 12 MM " → 12 · "10" → 10 · junk → 0.
        /// Never throws.
        /// </summary>
        public static int ParseMm(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;

            var digits = new System.Text.StringBuilder(4);
            foreach (var ch in text)
            {
                if (char.IsDigit(ch)) digits.Append(ch);
                else if (digits.Length > 0) break;   // stop at first break after digits
            }

            return digits.Length > 0 && int.TryParse(digits.ToString(), out int mm)
                ? mm
                : 0;
        }

        // ═══════════════════════════════════════════════════════════════
        //  CORE LOAD — the single point that can fail, fully swallowed
        // ═══════════════════════════════════════════════════════════════

        private static async Task<List<GlassSheet>> LoadAllAsync()
        {
            try
            {
                var repo = new ProGlassAutomation.Services.SheetStore.SheetStoreRepository();
                var all = await repo.GetAllAsync().ConfigureAwait(false);
                return all ?? new List<GlassSheet>();
            }
            catch (Exception ex)
            {
                // Sheet Store unavailable → the wizard simply gets no hints.
                AppLogService.Debug(
                    $"[SheetStorePricingService] Sheet Store read failed: {ex.Message}");
                return new List<GlassSheet>();
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  PUBLIC LOOKUPS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Full store snapshot for linkage consumers (Step 6 auto-use).
        /// Same never-throw path as every other lookup. Empty on failure.
        /// </summary>
        public static Task<List<GlassSheet>> GetAllSheetsAsync() =>
            LoadAllAsync();

        /// <summary>
        /// REPLACES the target combo list with exactly the Sheet Store
        /// colours (2026-09-10: built-in defaults removed by request —
        /// the dropdown must offer only what inventory actually stocks).
        /// Single shared mechanism for SGU / DGU / LAM / DGU+LAM.
        /// Never throws; leaves the list empty when the store is empty.
        /// </summary>
        public static async Task LoadColorsIntoAsync(
            ObservableCollection<string> target)
        {
            if (target == null) return;
            var colors = await GetColorsAsync().ConfigureAwait(true);
            target.Clear();
            foreach (var color in colors) target.Add(color);
        }

        /// <summary>
        /// Distinct glass colors present in the Sheet Store, in the
        /// original casing of the first row that used them, alphabetical.
        /// Empty list when the store is empty or unreadable.
        /// </summary>
        public static async Task<List<string>> GetColorsAsync()
        {
            var sheets = await LoadAllAsync().ConfigureAwait(false);

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<string>();

            foreach (var s in sheets)
            {
                var color = (s?.Color ?? string.Empty).Trim();
                if (color.Length == 0) continue;
                if (!seen.Add(Norm(color))) continue;
                result.Add(color);
            }

            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }

        /// <summary>
        /// Distinct thicknesses (mm) stocked for a given color, ascending.
        /// Blank/unknown color → empty list.
        /// </summary>
        public static async Task<List<int>> GetThicknessesAsync(string? color)
        {
            var key = Norm(color);
            if (key.Length == 0) return new List<int>();

            var sheets = await LoadAllAsync().ConfigureAwait(false);

            return sheets
                .Where(s => s != null && Norm(s.Color) == key && s.ThicknessMm > 0)
                .Select(s => s.ThicknessMm)
                .Distinct()
                .OrderBy(t => t)
                .ToList();
        }

        /// <summary>
        /// Sheet sizes stocked for a color + thickness combination.
        /// Display is the human label ("3210 × 2250"), Sqm the area of one
        /// sheet. Duplicates (same W×H from several suppliers) collapse to
        /// one entry. Largest sheet first — the usual cutting preference.
        /// </summary>
        public static async Task<List<(int W, int H, string Display, decimal Sqm)>>
            GetSizesAsync(string? color, int thicknessMm)
        {
            var result = new List<(int W, int H, string Display, decimal Sqm)>();

            var key = Norm(color);
            if (key.Length == 0 || thicknessMm <= 0) return result;

            var sheets = await LoadAllAsync().ConfigureAwait(false);

            var seen = new HashSet<(int, int)>();

            foreach (var s in sheets
                         .Where(s => s != null
                                     && Norm(s.Color) == key
                                     && s.ThicknessMm == thicknessMm
                                     && s.WidthMm > 0
                                     && s.HeightMm > 0)
                         .OrderByDescending(s => s.WidthMm * (long)s.HeightMm))
            {
                if (!seen.Add((s.WidthMm, s.HeightMm))) continue;

                result.Add((
                    s.WidthMm,
                    s.HeightMm,
                    $"{s.WidthMm} × {s.HeightMm}",
                    Utilities.SqmCalculator.CalculateSheetArea(s.WidthMm, s.HeightMm)));
            }

            return result;
        }

        /// <summary>
        /// Best available price for a color + thickness.
        ///
        /// Selection rule: the cheapest positive purchase price wins (that
        /// is the sheet a buyer would actually use for costing). Rows with
        /// no purchase price are only used as a last resort so a partially
        /// filled inventory still returns a sell price.
        ///
        /// Returns null when nothing matches — callers treat null as
        /// "not in Sheet Store, type it manually".
        /// </summary>
        public static async Task<(decimal PurchasePerSqm, decimal SellPerSqm, string Supplier)?>
            TryGetPriceAsync(string? color, int thicknessMm)
        {
            var key = Norm(color);
            if (key.Length == 0 || thicknessMm <= 0) return null;

            var sheets = await LoadAllAsync().ConfigureAwait(false);

            var candidates = sheets
                .Where(s => s != null
                            && Norm(s.Color) == key
                            && s.ThicknessMm == thicknessMm)
                .ToList();

            if (candidates.Count == 0) return null;

            var priced = candidates
                .Where(s => s.PurchasePricePerSqm > 0m)
                .OrderBy(s => s.PurchasePricePerSqm)
                .FirstOrDefault();

            var pick = priced ?? candidates[0];

            return (pick.PurchasePricePerSqm,
                    pick.SellPricePerSqm,
                    pick.Supplier ?? string.Empty);
        }

        /// <summary>
        /// Strict price for one selected stock sheet: exact colour + exact
        /// thickness + exact width/height (orientation-insensitive). Returns
        /// null when that physical sheet row is not present. This is the
        /// safe lookup for Step 4/Step 9 costing because a selected
        /// 2500 × 1875 pane must never be priced from a 3210 × 2250 row.
        /// </summary>
        public static async Task<(decimal PurchasePerSqm, decimal SellPerSqm, string Supplier)?>
            TryGetExactPriceForSizeAsync(string? color, int thicknessMm, int widthMm, int heightMm)
        {
            var key = Norm(color);
            if (key.Length == 0 || thicknessMm <= 0 || widthMm <= 0 || heightMm <= 0)
                return null;

            var sheets = await LoadAllAsync().ConfigureAwait(false);

            var exact = sheets
                .Where(s => s != null
                            && Norm(s.Color) == key
                            && s.ThicknessMm == thicknessMm
                            && ((s.WidthMm == widthMm && s.HeightMm == heightMm)
                             || (s.WidthMm == heightMm && s.HeightMm == widthMm)))
                .OrderBy(s => s.PurchasePricePerSqm > 0m ? 0 : 1)
                .ThenBy(s => s.PurchasePricePerSqm)
                .FirstOrDefault();

            return exact == null
                ? null
                : (exact.PurchasePricePerSqm,
                   exact.SellPricePerSqm,
                   exact.Supplier ?? string.Empty);
        }

        /// <summary>
        /// Price for an EXACT stock sheet: colour + thickness + width +
        /// height.
        ///
        /// Why this exists: the same colour and thickness is often stocked
        /// in more than one size at different rates (a 3210x2250 jumbo and
        /// a 2440x1830 standard are not the same money). Step 4 lets the
        /// user pick the size, so the base price must follow THAT sheet.
        /// The colour+thickness overload cannot express this — it returns
        /// the cheapest row regardless of size.
        ///
        /// Falls back to the cheapest positive purchase price for the
        /// colour+thickness when the exact size is not stocked, so a typed
        /// custom size still yields a sensible rate. Returns null when
        /// nothing matches at all.
        /// </summary>
        public static async Task<(decimal PurchasePerSqm, decimal SellPerSqm, string Supplier)?>
            TryGetPriceForSizeAsync(string? color, int thicknessMm, int widthMm, int heightMm)
        {
            var key = Norm(color);
            if (key.Length == 0 || thicknessMm <= 0) return null;

            var sheets = await LoadAllAsync().ConfigureAwait(false);

            var sameGlass = sheets
                .Where(s => s != null
                            && Norm(s.Color) == key
                            && s.ThicknessMm == thicknessMm)
                .ToList();

            if (sameGlass.Count == 0) return null;

            if (widthMm > 0 && heightMm > 0)
            {
                // Match the size in either orientation — a 3210x2250 sheet
                // and a 2250x3210 entry are the same physical stock.
                var exact = sameGlass
                    .Where(s => (s.WidthMm == widthMm && s.HeightMm == heightMm)
                             || (s.WidthMm == heightMm && s.HeightMm == widthMm))
                    .OrderBy(s => s.PurchasePricePerSqm > 0m ? 0 : 1)
                    .ThenBy(s => s.PurchasePricePerSqm)
                    .FirstOrDefault();

                if (exact != null)
                {
                    return (exact.PurchasePricePerSqm,
                            exact.SellPricePerSqm,
                            exact.Supplier ?? string.Empty);
                }
            }

            var priced = sameGlass
                .Where(s => s.PurchasePricePerSqm > 0m)
                .OrderBy(s => s.PurchasePricePerSqm)
                .FirstOrDefault();

            var pick = priced ?? sameGlass[0];

            return (pick.PurchasePricePerSqm,
                    pick.SellPricePerSqm,
                    pick.Supplier ?? string.Empty);
        }

        /// <summary>
        /// Convenience overload accepting the UI thickness string
        /// ("6mm"). Delegates to <see cref="TryGetPriceAsync(string, int)"/>.
        /// </summary>
        public static Task<(decimal PurchasePerSqm, decimal SellPerSqm, string Supplier)?>
            TryGetPriceAsync(string? color, string? thicknessText) =>
            TryGetPriceAsync(color, ParseMm(thicknessText));

        /// <summary>
        /// Convenience overload accepting the UI thickness string ("6mm").
        /// </summary>
        public static Task<List<(int W, int H, string Display, decimal Sqm)>>
            GetSizesAsync(string? color, string? thicknessText) =>
            GetSizesAsync(color, ParseMm(thicknessText));
    }
}
