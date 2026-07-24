using System;

namespace ProGlassAutomation.Utilities
{
    /// <summary>
    /// Single source of truth for SQM (square meter) calculations.
    /// 
    /// Business rule (UAE glass industry standard):
    ///   Panel SQM = (Width mm × Height mm) / 1,000,000
    ///   Minimum billable SQM per panel = 0.5 m²
    ///   If actual SQM < 0.5, bill as 0.5.
    /// 
    /// Used by:
    ///   - DN Detail:  DnItemRowViewModel.Sqm / TotalSqm
    ///   - DN List:    DeliveryNoteRepository.GetSqmSummariesByJobOrderAsync
    ///   - PI Wizard:  (future — swap inline calc to use this)
    ///   - JO Detail:  (future — swap inline calc to use this)
    ///   - Tax Invoice: (Phase 19 — will use this from day 1)
    /// 
    /// Ship 8c-2 (Chat 11).
    /// </summary>
    public static class SqmCalculator
    {
        /// <summary>Minimum billable SQM per panel (UAE industry standard).</summary>
        public const decimal MinimumSqmPerPanel = 0.5m;

        /// <summary>
        /// Calculate SQM for a single panel.
        /// Returns 0 if dimensions are invalid.
        /// Applies 0.5 minimum floor.
        /// Rounded to 3 decimal places.
        /// </summary>
        public static decimal CalculatePanel(int widthMm, int heightMm)
        {
            if (widthMm <= 0 || heightMm <= 0) return 0m;
            var actual = Math.Round((decimal)widthMm * heightMm / 1_000_000m, 3);
            return actual < MinimumSqmPerPanel ? MinimumSqmPerPanel : actual;
        }

        /// <summary>
        /// Calculate total SQM for a quantity of identical panels.
        /// SQM per panel × quantity, rounded to 3 dp.
        /// </summary>
        public static decimal CalculateTotal(int widthMm, int heightMm, int quantity)
        {
            if (quantity <= 0) return 0m;
            return Math.Round(CalculatePanel(widthMm, heightMm) * quantity, 3);
        }
    }
}