// ═══════════════════════════════════════════════════════════════════════
//  FILE: ViewModels/Specifications/SpecPriceBand.cs
//  PROJECT: ProGlassAutomation
//  CREATED: 2026-09-02
//
//  One distinct rate a specification has been quoted at, with the
//  proformas that used it.
//
//  WHY THIS EXISTS
//  SavedSpecification.PricePerSqm is OVERWRITTEN on every Step 4 apply, so
//  the catalogue row only ever holds the latest rate. The usage history
//  holds what each invoice was actually quoted at. Grouping those rows by
//  price is the only honest way to show that the same specification was
//  sold to different customers at different rates.
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlassAutomation.ViewModels.Specifications
{
    public class SpecPriceBand
    {
        public decimal PricePerSqm { get; set; }

        /// <summary>How many uses were quoted at this rate.</summary>
        public int UseCount { get; set; }

        /// <summary>Comma-separated proforma numbers, or "—".</summary>
        public string ProformaList { get; set; } = "—";

        public string UseCountDisplay =>
            UseCount == 1 ? "1 use" : $"{UseCount} uses";
    }
}
