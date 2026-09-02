// ═══════════════════════════════════════════════════════════════════════
//  FILE: Models/Specifications/SpecificationUsage.cs
//  PROJECT: ProGlassAutomation
//  CREATED: 2026-09-02 — Specifications Center usage history
//
//  WHY THIS EXISTS
//  ---------------------------------------------------------------------
//  SavedSpecification dedupes on GlassType + SpecName, so ONE row is
//  reused by many proformas. That is the right shape for a catalogue --
//  "6mm Clear FT Glass" is one specification, not fifty -- but it means a
//  single ProformaId column on the spec can only ever name one invoice,
//  and would be either the first or the last depending on write order.
//
//  This table records EVERY proforma that used a spec, so the Center can
//  answer "where is this specification used?" completely instead of
//  showing one arbitrary PI number.
//
//  It also captures the price AT THE TIME OF USE. A spec's PricePerSqm is
//  overwritten on each Step 4 APPLY, so without this the history of what
//  a given invoice was actually quoted at would be lost.
//
//  Append-only by design: rows are never edited, so this doubles as an
//  audit trail of pricing decisions.
// ═══════════════════════════════════════════════════════════════════════

using System;

namespace ProGlassAutomation.Models.ProformaInvoice.Specifications
{
    /// <summary>
    /// One occurrence of a saved specification being applied to a proforma.
    /// Append-only; there is one row per use, not per specification.
    /// </summary>
    public class SpecificationUsage
    {
        public int Id { get; set; }

        /// <summary>FK to <see cref="SavedSpecification.Id"/>.</summary>
        public int SpecificationId { get; set; }

        /// <summary>
        /// Denormalised spec name. Kept so the history stays readable even
        /// if the parent specification is later deleted.
        /// </summary>
        public string SpecName { get; set; } = string.Empty;

        /// <summary>Glass type at the time of use (SGU/DGU/LAM/DGU+LAM).</summary>
        public string GlassType { get; set; } = string.Empty;

        /// <summary>
        /// The proforma this spec was used on. Blank when the invoice has
        /// not been assigned a number yet — the usage is still recorded so
        /// the count stays honest.
        /// </summary>
        public string ProformaId { get; set; } = string.Empty;

        /// <summary>Customer/project labels, for a readable history list.</summary>
        public string CustomerName { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;

        /// <summary>
        /// Rate per SQM as quoted on THIS use. The parent spec's
        /// PricePerSqm is overwritten on every re-apply, so this is the
        /// only record of what a given invoice was priced at.
        /// </summary>
        public decimal PricePerSqm { get; set; }

        /// <summary>Sheet matched for this use, 0 when none.</summary>
        public int SheetWidthMm { get; set; }
        public int SheetHeightMm { get; set; }

        public string UserName { get; set; } = Environment.UserName;

        public DateTime UsedAt { get; set; } = DateTime.Now;

        // ── Display helpers (list view) ──────────────────────────────────

        public string UsedAtDisplay => UsedAt.ToString("dd-MMM-yyyy HH:mm");
        public string DateDisplay => UsedAt.ToString("dd-MMM-yyyy");

        public string ProformaDisplay =>
            string.IsNullOrWhiteSpace(ProformaId) ? "(unsaved draft)" : ProformaId;

        public string SheetSizeDisplay =>
            SheetWidthMm > 0 && SheetHeightMm > 0
                ? $"{SheetWidthMm} × {SheetHeightMm}"
                : "—";

        /// <summary>"Customer · Project", collapsing blanks cleanly.</summary>
        public string ContextDisplay
        {
            get
            {
                bool hasCustomer = !string.IsNullOrWhiteSpace(CustomerName);
                bool hasProject = !string.IsNullOrWhiteSpace(ProjectName);

                if (hasCustomer && hasProject) return $"{CustomerName} · {ProjectName}";
                if (hasCustomer) return CustomerName;
                if (hasProject) return ProjectName;
                return "—";
            }
        }
    }
}
