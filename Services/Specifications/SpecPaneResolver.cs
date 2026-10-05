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
//  STORED JSON IS A HINT, NOT TRUTH. ResolveAsync used to return the
//  stored PanesJson verbatim whenever present — so a size that was edited
//  or deleted in Sheet Store afterwards kept showing as if it still
//  existed. Now the stored size is kept only when the CURRENT store still
//  stocks that colour/thickness in exactly that size (it was the
//  operator's actual Step 4 choice, which the live "largest stocked"
//  fallback cannot know); otherwise the live match wins, and when the
//  store has nothing the pane honestly reports no match.
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
        public static Task<List<SpecPane>> MatchSheetsAsync(
            List<SpecPane> panes) =>
            MatchSheetsAsync(panes, storedPreference: null);

        /// <summary>
        /// Live match with a validated stored preference (see
        /// <see cref="ResolveAsync"/>): the stored size wins only when the
        /// current store still stocks it for the same colour/thickness.
        /// </summary>
        public static async Task<List<SpecPane>> MatchSheetsAsync(
            List<SpecPane> panes, List<SpecPane>? storedPreference)
        {
            if (panes == null || panes.Count == 0)
                return panes ?? new List<SpecPane>();

            for (int i = 0; i < panes.Count; i++)
            {
                var pane = panes[i];
                if (string.IsNullOrWhiteSpace(pane.Color) || pane.ThicknessMm <= 0)
                    continue;

                try
                {
                    var sizes = await ProformaInvoice.SheetStorePricingService
                        .GetSizesAsync(pane.Color, pane.ThicknessMm)
                        .ConfigureAwait(false);

                    if (sizes.Count == 0) continue;

                    // The stored size is the operator's actual Step 4 choice
                    // — more truthful than "largest stocked" — but ONLY when
                    // the current store still offers it. Colour/thickness
                    // must match too: stored JSON from a renamed spec must
                    // never donate its size to a different glass.
                    var stored = storedPreference != null && i < storedPreference.Count
                        ? storedPreference[i]
                        : null;

                    if (stored != null
                        && stored.SheetWidthMm > 0 && stored.SheetHeightMm > 0
                        && string.Equals(
                            (stored.Color ?? "").Trim(), pane.Color.Trim(),
                            StringComparison.OrdinalIgnoreCase)
                        && stored.ThicknessMm == pane.ThicknessMm
                        && sizes.Any(s => s.W == stored.SheetWidthMm
                                       && s.H == stored.SheetHeightMm))
                    {
                        pane.SheetWidthMm = stored.SheetWidthMm;
                        pane.SheetHeightMm = stored.SheetHeightMm;
                        continue;
                    }

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
        /// Panes for a saved specification: parsed from the name, matched
        /// against the CURRENT Sheet Store, with the stored JSON kept only
        /// where the store still stocks that exact size. A name that cannot
        /// be parsed at all falls back to the stored record — it is
        /// literally all there is — and anything else honestly reports no
        /// match instead of showing a size that no longer exists.
        /// </summary>
        public static async Task<List<SpecPane>> ResolveAsync(SavedSpecification spec)
        {
            if (spec == null) return new List<SpecPane>();

            var parsed = Parse(spec.SpecName);
            if (parsed.Count == 0)
                return Deserialize(spec.PanesJson);

            var stored = Deserialize(spec.PanesJson);
            return await MatchSheetsAsync(parsed, stored).ConfigureAwait(false);
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
