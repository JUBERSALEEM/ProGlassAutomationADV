using System;

namespace ProGlassAutomation.Models.ProformaInvoice.Specifications
{
    /// <summary>
    /// Represents a saved glass specification stored in the specifications.db database.
    /// Generated from Step 4 (Pricing Estimator) when user clicks APPLY.
    /// Reused in Step 5 (Data Grid) via the specification dropdown.
    /// </summary>
    public class SavedSpecification
    {
        public int Id { get; set; }

        /// <summary>
        /// Glass type: "SGU", "DGU", "LAM", or "DGU+LAM"
        /// Used to filter the Step 5 specification dropdown.
        /// </summary>
        public string GlassType { get; set; }

        /// <summary>
        /// Auto-generated human-readable spec name.
        /// Examples:
        ///   SGU: "6mm Clear FT Glass"
        ///   DGU: "6mm HD Grey FT Glass + 12mm ASP + 6mm Clear FT Glass"
        ///   LAM: "6mm Clear FT Glass + 1.52mm Clear PVB + 6mm Clear FT Glass"
        /// </summary>
        public string SpecName { get; set; }

        /// <summary>
        /// Live-calculated rate per SQM from Step 4.
        /// User can override in Step 5 grid if needed.
        /// </summary>
        public decimal PricePerSqm { get; set; }

        /// <summary>
        /// When this spec was first created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Last time this spec was picked/used in a proforma.
        /// Used for smart sorting (most recently used first).
        /// </summary>
        public DateTime LastUsedAt { get; set; }

        /// <summary>
        /// How many times this spec has been used across all proformas.
        /// Higher usage = show first in dropdown suggestions.
        /// </summary>
        public int UsageCount { get; set; }

        // ═══════════════════════════════════════════════════════════════
        //  SPECIFICATIONS CENTER (2026-09-02)
        //  ---------------------------------------------------------------
        //  Catalogue columns. Step 4 remains the only place a spec is
        //  BUILT and priced; these fields just record what it was built
        //  from so the Specifications Center can list, group and filter
        //  entries the way Sheet Store does.
        //
        //  The Sheet Store relationship is READ-ONLY and for MATCHING the
        //  glass sheet size only -- opening a row shows which sheet this
        //  spec was quoted against. Nothing here drives pricing; that
        //  stays with PricePerSqm from Step 4.
        //
        //  All columns are NULLABLE / defaulted. Existing rows created
        //  before this change stay valid and simply read as blank, which
        //  is why the migration is purely additive.
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Sheet Store category, e.g. "Clear", "Tinted", "Low-E".</summary>
        public string? Category { get; set; }

        /// <summary>Outer pane colour, as parsed from the spec name.</summary>
        public string? Color { get; set; }

        /// <summary>Outer pane thickness in mm. 0 when unknown.</summary>
        public int ThicknessMm { get; set; }

        /// <summary>
        /// Matched Sheet Store sheet size, in mm. Recorded at save time so
        /// the catalogue can show "which sheet was used" without a live
        /// lookup, and so a later Sheet Store edit cannot silently rewrite
        /// what an existing quote was costed against.
        /// </summary>
        public int SheetWidthMm { get; set; }
        public int SheetHeightMm { get; set; }

        /// <summary>
        /// Id of the GlassSheet row this spec matched, or 0 if none did.
        /// Kept only so the Center can open the originating sheet.
        /// </summary>
        public int SheetId { get; set; }

        /// <summary>
        /// Proforma number this spec was first generated from, for
        /// traceability in the catalogue list.
        /// </summary>
        public string? ProformaId { get; set; }

        /// <summary>True when a Sheet Store size was actually matched.</summary>
        public bool HasSheetMatch => SheetWidthMm > 0 && SheetHeightMm > 0;

        /// <summary>Sheet area in SQM, or 0 when no sheet was matched.</summary>
        public decimal SheetSqm =>
            SheetWidthMm > 0 && SheetHeightMm > 0
                ? System.Math.Round(
                    (SheetWidthMm * (decimal)SheetHeightMm) / 1_000_000m, 4)
                : 0m;

        /// <summary>"3210 × 2250" or "—" for the catalogue list.</summary>
        public string SheetSizeDisplay =>
            HasSheetMatch ? $"{SheetWidthMm} × {SheetHeightMm}" : "—";

        public SavedSpecification()
        {
            CreatedAt = DateTime.Now;
            LastUsedAt = DateTime.Now;
            UsageCount = 0;
        }
    }
}