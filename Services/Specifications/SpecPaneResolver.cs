// ═══════════════════════════════════════════════════════════════════════
//  FILE: Services/Specifications/SpecPaneResolver.cs
//  PROJECT: ProGlassAutomation
//  CREATED: 2026-09-02
//
//  Turns a specification NAME into its individual glass panes, and matches
//  each pane to a Sheet Store size.
//
//  WHY DERIVE FROM THE NAME
//  ---------------------------------------------------------------------
//  Two reasons:
//
//  1. BACKFILL. Every specification already saved has no pane data. The
//     name is the only record of what it was made of, so parsing it is
//     the only way to show sizes for the catalogue that already exists
//     rather than only for specs saved from today onward.
//
//  2. COVERAGE. Step 4's DGU+LAM panel has no Sheet Store size wiring at
//     all (unlike SGU/DGU/LAM, which each have a .SheetStore.cs partial),
//     so it cannot report pane sizes even for new saves. Parsing the name
//     works for every build family uniformly.
//
//  The parse reuses PnLSpecParser.GlassPattern — the same regex Step 9
//  costs against — so a pane recognised here is exactly a pane recognised
//  there. No second, drifting definition of "what is a pane".
//
//  NEVER THROWS. A spec whose name cannot be parsed returns an empty list
//  and the UI says so, which is honest. Guessing a size would put a wrong
//  number in front of someone pricing glass.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.ProformaInvoice.Specifications;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step9ProfitLoss;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Specifications
{
    public static class SpecPaneResolver
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = false
        };

        /// <summary>
        /// Splits a spec name into panes. Sheet sizes are NOT filled in here
        /// — call <see cref="MatchSheetsAsync"/> for that, so the pure parse
        /// stays synchronous and testable.
        /// </summary>
        public static List<SpecPane> Parse(string? specName)
        {
            var panes = new List<SpecPane>();
            if (string.IsNullOrWhiteSpace(specName)) return panes;

            try
            {
                var matches = PnLSpecParser.GlassMatches(specName);
                int count = matches.Count;

                for (int i = 0; i < count; i++)
                {
                    var m = matches[i];
                    string color = (m.Groups["color"].Value ?? "").Trim();
                    int.TryParse((m.Groups["thk"].Value ?? "").Trim(), out int thk);

                    panes.Add(new SpecPane
                    {
                        Position = PositionLabel(i, count),
                        Color = color,
                        ThicknessMm = thk
                    });
                }
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[SpecPaneResolver.Parse] {ex.Message}");
            }

            return panes;
        }

        /// <summary>
        /// Outer is always first and inner always last, matching how Step 4
        /// composes a name. A middle leaf in a triple is numbered rather
        /// than guessed at.
        /// </summary>
        private static string PositionLabel(int index, int total)
        {
            if (total <= 1) return "Glass";
            if (index == 0) return "Outer";
            if (index == total - 1) return "Inner";
            return $"Middle {index}";
        }

        /// <summary>
        /// Fills each pane's sheet size from Sheet Store, matching on
        /// colour + thickness. A pane with no match keeps 0/0 and the UI
        /// reports it explicitly rather than substituting a default.
        /// </summary>
        public static async Task<List<SpecPane>> MatchSheetsAsync(
            List<SpecPane> panes)
        {
            if (panes == null || panes.Count == 0)
                return panes ?? new List<SpecPane>();

            foreach (var pane in panes)
            {
                if (string.IsNullOrWhiteSpace(pane.Color) || pane.ThicknessMm <= 0)
                    continue;

                try
                {
                    var sizes = await ProformaInvoice.SheetStorePricingService
                        .GetSizesAsync(pane.Color, pane.ThicknessMm)
                        .ConfigureAwait(false);

                    if (sizes.Count == 0) continue;

                    // Largest sheet first: when a colour/thickness is stocked
                    // in several sizes the jumbo is the normal cutting stock,
                    // and it is also what Step 4's dropdown defaults to.
                    var best = sizes
                        .OrderByDescending(s => (long)s.W * s.H)
                        .First();

                    pane.SheetWidthMm = best.W;
                    pane.SheetHeightMm = best.H;
                }
                catch (Exception ex)
                {
                    // SheetStorePricingService is documented never-throws,
                    // but a pane without a size must not abort the others.
                    AppLogService.Debug(
                        $"[SpecPaneResolver.Match] {pane.Color}/{pane.ThicknessMm}: {ex.Message}");
                }
            }

            return panes;
        }

        /// <summary>
        /// Panes for a saved specification: uses the stored JSON when
        /// present, otherwise derives them from the name and matches live.
        /// This is what makes the whole existing catalogue display correctly
        /// without a data backfill pass.
        /// </summary>
        public static async Task<List<SpecPane>> ResolveAsync(SavedSpecification spec)
        {
            if (spec == null) return new List<SpecPane>();

            var stored = Deserialize(spec.PanesJson);
            if (stored.Count > 0) return stored;

            var parsed = Parse(spec.SpecName);
            return await MatchSheetsAsync(parsed).ConfigureAwait(false);
        }

        public static string? Serialize(List<SpecPane>? panes)
        {
            if (panes == null || panes.Count == 0) return null;

            try { return JsonSerializer.Serialize(panes, JsonOptions); }
            catch (Exception ex)
            {
                AppLogService.Debug($"[SpecPaneResolver.Serialize] {ex.Message}");
                return null;
            }
        }

        public static List<SpecPane> Deserialize(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<SpecPane>();

            try
            {
                return JsonSerializer.Deserialize<List<SpecPane>>(json)
                       ?? new List<SpecPane>();
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[SpecPaneResolver.Deserialize] {ex.Message}");
                return new List<SpecPane>();
            }
        }
    }
}
