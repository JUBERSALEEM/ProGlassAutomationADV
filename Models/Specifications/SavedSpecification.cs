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

        public SavedSpecification()
        {
            CreatedAt = DateTime.Now;
            LastUsedAt = DateTime.Now;
            UsageCount = 0;
        }
    }
}